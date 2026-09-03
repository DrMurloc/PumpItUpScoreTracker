using System.Text.RegularExpressions;
using ScoreTracker.SharedKernel.Enums;
using Xunit.Abstractions;

namespace ScoreTracker.ExplorationTests.LiveSite;

/// <summary>
///     The reproducible path back to the avatar catalog that <c>scores.Avatar</c> is seeded from
///     (docs/design/avatar-selection.md). Re-run it when a new mix lands, or when a name or
///     picture on the live site is suspected of having changed.
///     <para>
///         Read-only: one authenticated GET of each mix's avatar page. The pages are named
///         differently per mix and both were found by probing, so the candidate list is kept
///         rather than narrowed — a 404 costs nothing and a rename would otherwise look like an
///         empty catalog. Phoenix serves <c>my_page/avatar_shop.php</c>, Phoenix 2 serves
///         <c>my_page/avatar.php</c>, and both render the same
///         <c>ul.data_titleList2 &gt; li[data-name]</c> markup as <c>my_page/title.php</c>.
///     </para>
///     <para>
///         XX is deliberately fetched anonymously. <c>xx.piugame.com</c> is alive and its shop
///         page at <c>/piu.xx/itemshop/xx_avatarshop.php</c> answers "Use after log in" without a
///         session, but the art itself is public, so the catalog listing comes from a saved copy
///         while the images need no credentials at all.
///     </para>
///     <para>
///         Report-only, and deliberately not a parser: it saves each page to the temp directory
///         and prints what it found, because turning 412 listed entries into 170 avatars needs a
///         pixel comparison (Phoenix's decorative frame is the only difference for 63 of them)
///         and that is offline work, not an assertion. It asserts only that the fetch produced a
///         catalog-sized page. Run on demand:
///         <c>dotnet test ScoreTracker/ScoreTracker.ExplorationTests/... --filter
///         "FullyQualifiedName~AvatarCatalogRecon" --logger "console;verbosity=detailed"</c>
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class AvatarCatalogReconTests : IClassFixture<PiuGameSessionFixture>
{
    private readonly PiuGameSessionFixture _fixture;
    private readonly ITestOutputHelper _output;

    public AvatarCatalogReconTests(PiuGameSessionFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    /// <summary>
    ///     Matches the avatar file out of the page's inline background-image style. The optional
    ///     trailing "2" is load-bearing: Phoenix serves /data/avatar_img/ and Phoenix 2 serves
    ///     /data/avatar_img2/, and the two directories reuse ids for completely unrelated art —
    ///     so the file alone never identifies an avatar, and neither does a match across mixes.
    /// </summary>
    private static readonly Regex AvatarFileRegex =
        new(@"avatar_img2?\/(?<file>[A-Za-z0-9_\-]+\.[A-Za-z]{3,4})", RegexOptions.Compiled);

    [LiveSiteFact]
    public async Task Phoenix_avatar_page_shape()
    {
        await Probe(MixEnum.Phoenix, await _fixture.GetAuthenticatedClient(CancellationToken.None));
    }

    [LiveSiteFact]
    public async Task Phoenix2_avatar_page_shape()
    {
        await Probe(MixEnum.Phoenix2, await _fixture.GetAuthenticatedPhoenix2Client(CancellationToken.None));
    }

    private async Task Probe(MixEnum mix, HttpClient client)
    {
        var host = mix == MixEnum.Phoenix ? "https://phoenix.piugame.com" : "https://piugame.com";
        var found = false;

        foreach (var path in new[] { "/my_page/avatar.php", "/my_page/avatar_shop.php" })
        {
            string html;
            try
            {
                var response = await client.GetAsync(host + path, CancellationToken.None);
                html = await response.Content.ReadAsStringAsync(CancellationToken.None);
                _output.WriteLine($"=== {mix} {path} -> {(int)response.StatusCode} ({html.Length} chars)");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"=== {mix} {path} -> threw {ex.GetType().Name}: {ex.Message}");
                continue;
            }

            // The real page is hundreds of KB. Length is the honest signal that the session
            // held: the site's shared header carries "login_wrap" on every page including this
            // one, so the obvious string check reports a false login bounce.
            if (html.Length < 20000)
            {
                _output.WriteLine("    (too small to be the catalog — login bounce or 404)");
                continue;
            }

            found = true;
            var files = AvatarFileRegex.Matches(html).Select(m => m.Groups["file"].Value).Distinct().ToArray();
            var names = Regex.Matches(html, @"<li class=""[^""]*"" data-name=""(?<name>[^""]*)""")
                .Select(m => m.Groups["name"].Value).ToArray();
            _output.WriteLine($"    distinct avatar files: {files.Length}");
            _output.WriteLine($"    data-name entries:     {names.Length}");
            _output.WriteLine($"    names listed twice:    " +
                              string.Join(", ", names.GroupBy(n => n).Where(g => g.Count() > 1).Select(g => g.Key)));
            _output.WriteLine($"    case-only collisions:  " +
                              string.Join(", ", names.GroupBy(n => n.ToLowerInvariant())
                                  .Where(g => g.Select(x => x).Distinct().Count() > 1)
                                  .SelectMany(g => g.Distinct())));
            foreach (var n in names.Take(8)) _output.WriteLine($"      {n}");

            var dump = Path.Combine(Path.GetTempPath(), $"avatarpage_{mix}.html");
            await File.WriteAllTextAsync(dump, html, CancellationToken.None);
            _output.WriteLine($"    saved: {dump}");
        }

        Assert.True(found,
            $"No {mix} avatar page answered with a catalog-sized body. Either the session did not " +
            "hold, or the page moved again — probe for its new name before assuming the catalog shrank.");
    }

    [LiveSiteFact]
    public async Task Xx_avatar_shop_shape()
    {
        using var client = new HttpClient();
        var response = await client.GetAsync("https://xx.piugame.com/piu.xx/itemshop/xx_avatarshop.php",
            CancellationToken.None);
        var html = await response.Content.ReadAsStringAsync(CancellationToken.None);
        _output.WriteLine($"XX shop -> {(int)response.StatusCode} ({html.Length} chars)");
        _output.WriteLine(html.Contains("Use after log in")
            ? "Login-gated listing, as expected — the catalog comes from a saved signed-in copy."
            : "Anonymous read worked; the listing can be parsed straight from here.");

        // The art is public even when the listing is not, which is why seeding XX needed no
        // credentials — only the names did.
        var probe = await client.GetAsync("https://xx.piugame.com/piu.xx/piu.avatar/070.png",
            CancellationToken.None);
        _output.WriteLine($"XX avatar 070.png -> {(int)probe.StatusCode} " +
                          $"({probe.Content.Headers.ContentLength} bytes, {probe.Content.Headers.ContentType})");
        Assert.Equal(System.Net.HttpStatusCode.OK, probe.StatusCode);
    }
}
