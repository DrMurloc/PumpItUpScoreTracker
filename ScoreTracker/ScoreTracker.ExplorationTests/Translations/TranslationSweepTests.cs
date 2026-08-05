using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.ExplorationTests.Translations.Evaluation;
using ScoreTracker.Translations.Contracts;
using ScoreTracker.Translations.Contracts.Commands;
using Xunit.Abstractions;

namespace ScoreTracker.ExplorationTests.Translations;

/// <summary>
///     The workbench itself: real community comments, in whatever language they were written,
///     rendered into four locales by three model tiers so the cheapest one that still does the
///     job can be found.
///     <para>
///         Every test here spends the owner's money. They are gated on a configured key and named
///         so the cheap one can be run alone — <see cref="SmokeOneCommentOnSonnet" /> costs a
///         couple of cents and proves the plumbing, which is worth doing before
///         <see cref="SweepThreeArmsOverTheCorpus" /> spends a few dollars finding out the schema
///         was malformed.
///     </para>
/// </summary>
public sealed class TranslationSweepTests(ITestOutputHelper output)
{
    /// <summary>
    ///     Low enough not to trip rate limits on an account that is not provisioned for a fleet.
    ///     Four was comfortable on the first runs and became a problem after several sweeps in an
    ///     hour: request latency climbed, and the arms that suffered were Opus and Sonnet — the
    ///     two the whole exercise exists to compare — while Haiku sailed through on speed alone.
    ///     A probe that degrades its own subjects under load measures the load, not the models.
    /// </summary>
    private const int Concurrency = 2;

    /// <summary>
    ///     Resolving the handler through a real container is deliberate. A vertical's handlers are
    ///     internal classes behind public contract records, and nothing else in this solution
    ///     proves one can actually be dispatched — a whole vertical once shipped with every
    ///     handler unregistered and every suite green.
    /// </summary>
    private static IMediator BuildMediator()
    {
        var services = new ServiceCollection();
        // MediatR 14's license accessor resolves a logger during Mediator construction, so a
        // container without logging fails before any handler is reached.
        services.AddLogging();
        services.AddMediatR(o =>
            o.RegisterServicesFromAssemblies(typeof(TranslateCommentCommand).Assembly));
        services.AddSingleton<ILanguageModelClient>(AnthropicLanguageModelClient.Create());

        return services.BuildServiceProvider().GetRequiredService<IMediator>();
    }

    [TranslationProbeFact]
    public async Task SmokeOneCommentOnSonnet()
    {
        var comment = TranslationCorpus.All.First(c => c.Id == "spartan7919");
        var mediator = BuildMediator();

        var outcome = await mediator.Send(
            new TranslateCommentCommand(comment.Text, ModelArm.Sonnet.ModelId));

        output.WriteLine($"source: {outcome.Pivot.SourceLanguage}  register: {outcome.Pivot.Register}  " +
                         $"formality marked: {outcome.Pivot.FormalityMarked}");
        output.WriteLine($"tone: {outcome.Pivot.Tone}");
        output.WriteLine($"en-US: {outcome.Pivot.English}");
        foreach (var (locale, text) in outcome.Translations) output.WriteLine($"{locale}: {text}");

        var cost = outcome.Calls.Sum(c => ModelArm.Sonnet.Cost(c.Usage));
        foreach (var call in outcome.Calls)
            output.WriteLine($"{call.Stage}: in {call.Usage.InputTokens} out {call.Usage.OutputTokens}");
        output.WriteLine($"cost: ${cost:F5}");

        Assert.Equal("ko", outcome.Pivot.SourceLanguage);

        // A Korean comment gets three renderings, not four: ko-KR is deliberately absent so the
        // reader sees the author's own words instead of a round trip through English.
        var expected = TranslationTarget.ForSource(outcome.Pivot.SourceLanguage);
        Assert.DoesNotContain("ko-KR", expected);
        Assert.Equal(expected.Count, outcome.Translations.Count);
        Assert.All(expected, locale => Assert.True(
            !string.IsNullOrWhiteSpace(outcome.Translations.GetValueOrDefault(locale)),
            $"{locale} came back empty."));
    }

    [TranslationProbeFact]
    public async Task SweepThreeArmsOverTheCorpus()
    {
        var mediator = BuildMediator();
        var gate = new SemaphoreSlim(Concurrency);

        async Task<SweepResult> Translate(ModelArm arm, CorpusComment comment)
        {
            await gate.WaitAsync();
            try
            {
                var outcome = await mediator.Send(new TranslateCommentCommand(comment.Text, arm.ModelId));

                return new SweepResult(arm, comment, outcome, null);
            }
            catch (Exception exception)
            {
                // Recorded, not thrown: one bad response should not discard the other
                // sixty-eight results, and a model that fails on a particular comment is
                // more useful as a row in the report than as a stack trace.
                return new SweepResult(arm, comment, null, exception.Message);
            }
            finally
            {
                gate.Release();
            }
        }

        var results = (await Task.WhenAll(ModelArm.All
            .SelectMany(arm => TranslationCorpus.All.Select(comment => (arm, comment)))
            .Select(pair => Translate(pair.arm, pair.comment)))).ToArray();

        // One retry pass. A timeout or a 429 says nothing about whether a model can translate a
        // comment, but it lands in the report looking exactly like a model that couldn't — and a
        // run with holes in it is a run that has to be repeated in full.
        var retried = await Task.WhenAll(results
            .Where(r => r.Outcome == null)
            .Select(r => Translate(r.Arm, r.Comment)));

        foreach (var attempt in retried.Where(r => r.Outcome != null))
            results[Array.FindIndex(results,
                r => r.Arm == attempt.Arm && r.Comment.Id == attempt.Comment.Id)] = attempt;

        if (retried.Length > 0)
            output.WriteLine($"retried {retried.Length}, recovered {retried.Count(r => r.Outcome != null)}");

        Directory.CreateDirectory(TranslationProbeConfiguration.ReportDirectory);
        var path = Path.Combine(TranslationProbeConfiguration.ReportDirectory, "sweep.md");
        await File.WriteAllTextAsync(path,
            SweepReport.Write(results, string.Join(", ", ModelArm.All.Select(a => a.Name))));

        foreach (var arm in ModelArm.All)
        {
            var forArm = results.Where(r => r.Arm == arm).ToArray();
            var findings = forArm.SelectMany(r => r.Entities).ToArray();
            output.WriteLine(
                $"{arm.Name}: ${forArm.Sum(r => r.Cost):F4}  " +
                $"failures {forArm.Count(r => r.Outcome == null)}  " +
                $"entities {findings.Count(f => f.Survived)}/{findings.Length}  " +
                $"language {forArm.Count(r => r.DetectedLanguageCorrectly)}/{forArm.Length}");
        }

        output.WriteLine($"report: {path}");
        output.WriteLine($"total spend: ${results.Sum(r => r.Cost):F4}");

        Assert.True(results.Count(r => r.Outcome != null) > results.Length / 2,
            "More than half the sweep failed — the harness is broken, not the models.");
    }
}
