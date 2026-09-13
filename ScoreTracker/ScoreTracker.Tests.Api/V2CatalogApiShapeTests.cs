using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Domain.Records;
using ScoreTracker.Application.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Controllers.Api.V2;

namespace ScoreTracker.Tests.Api;

/// <summary>
///     Wire shapes for the api/v2 catalog. These are the contract community tools build against.
/// </summary>
public sealed class V2CatalogApiShapeTests
{
    private readonly Mock<IMediator> _mediator = new();

    public V2CatalogApiShapeTests()
    {
        // Scoring levels ride on every chart now, so every chart read asks for them. Empty by
        // default; the tests that care about the value set their own.
        _mediator.Setup(m => m.Send(It.IsAny<GetChartScoringLevelsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, double>());
    }

    /// <summary>
    ///     One field out of a finished body, for the assertions that check a value rather than the
    ///     whole shape. Parsed rather than string-matched — the catalog reads self-serialize compact,
    ///     so a substring assertion would be testing whitespace.
    /// </summary>
    private static System.Text.Json.JsonElement FirstRow(IActionResult result)
    {
        var json = result is ContentResult c
            ? c.Content ?? string.Empty
            : System.Text.Json.JsonSerializer.Serialize(((ObjectResult)result).Value);
        return System.Text.Json.JsonDocument.Parse(json).RootElement.GetProperty("data")[0];
    }

    private static T WithContext<T>(T controller) where T : ApiV2ControllerBase
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { Request = { Scheme = "https", Host = new HostString("piu") } }
        };
        return controller;
    }

    [Fact]
    public async Task MixListCarriesTheScoringModelDiscriminator()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetMixesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new MixRecord(MixEnum.FiestaEx, "FiestaEx", "Fiesta EX", 220, false, true),
                new MixRecord(MixEnum.Phoenix2, "Phoenix2", "Phoenix 2", 280, true, false)
            });

        var result = await WithContext(new MixesController(_mediator.Object)).Get();

        JsonApproval.AssertWireShape("""
            {
              "data": [
                {
                  "name": "FiestaEx",
                  "displayName": "Fiesta EX",
                  "sortOrder": 220,
                  "isPrimary": false,
                  "scoringModel": "legacy"
                },
                {
                  "name": "Phoenix2",
                  "displayName": "Phoenix 2",
                  "sortOrder": 280,
                  "isPrimary": true,
                  "scoringModel": "phoenix"
                }
              ],
              "limit": 2,
              "total": 2,
              "next": null
            }
            """, result);
    }

    [Fact]
    public async Task SongsExposeArtistDurationAndBpm()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetSongsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new SongRecord(Name.From("Conflict"), SongType.Arcade,
                    new Uri("https://piuimages.example.com/conflict.png"), TimeSpan.FromSeconds(95),
                    Name.From("Doin"), 170m, 190m, Channel.Original)
            });

        var result = await WithContext(new SongsController(_mediator.Object)).Get("Phoenix");

        JsonApproval.AssertWireShape("""
            {
              "data": [
                {
                  "name": "Conflict",
                  "type": "Arcade",
                  "channel": "Original",
                  "artist": "Doin",
                  "durationSeconds": 95,
                  "imageUrl": "https://piuimages.example.com/conflict.png",
                  "bpm": {
                    "min": 170,
                    "max": 190
                  }
                }
              ],
              "limit": 100,
              "total": 1,
              "next": null
            }
            """, result);
    }

    [Fact]
    public async Task ChartCarriesPerMixLevelAndTheSlotAwareDifficulty()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ApiTestData.Chart1 });

        var result = await WithContext(new ChartsController(_mediator.Object)).Get("Phoenix");

        JsonApproval.AssertWireShape("""
            {
              "data": [
                {
                  "id": "11111111-1111-1111-1111-111111111111",
                  "mix": "Phoenix",
                  "originalMix": "Phoenix",
                  "addedInVersion": null,
                  "addedOn": null,
                  "debutVersion": null,
                  "debutedOn": null,
                  "debut": true,
                  "channel": null,
                  "songName": "Conflict",
                  "imageUrl": "https://piuimages.example.com/conflict.png",
                  "type": "Single",
                  "level": 20,
                  "difficulty": "S20",
                  "noteCount": 731,
                  "playerCount": 1,
                  "stepArtist": "ANDAMIRO",
                  "legacySlot": null,
                  "scoringLevel": null
                }
              ],
              "limit": 100,
              "total": 1,
              "next": null
            }
            """, result);
    }

    // Scoring difficulty is chart metadata, not a separate resource — it keys on (chart, mix),
    // exactly what this DTO is. A tool asking "how hard is this to score on" gets it in the same
    // call rather than joining two.
    [Fact]
    public async Task ChartCarriesItsScoringLevelWhenWeHaveOne()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ApiTestData.Chart1 });
        _mediator.Setup(m => m.Send(It.IsAny<GetChartScoringLevelsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, double> { [ApiTestData.ChartId1] = 20.7 });

        var result = await WithContext(new ChartsController(_mediator.Object)).Get("Phoenix");

        Assert.Equal(20.7, FirstRow(result).GetProperty("scoringLevel").GetDouble());
    }

    // Null, not zero and not the listed level. Every mix but Phoenix and XX has no measurement at
    // all, and a tool reading a missing value as 0 would rank them as the easiest charts in the game.
    [Fact]
    public async Task AChartWithNoMeasurementReportsNullRatherThanZero()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ApiTestData.Chart1 });

        var result = await WithContext(new ChartsController(_mediator.Object)).Get("Phoenix2");

        Assert.Equal(System.Text.Json.JsonValueKind.Null,
            FirstRow(result).GetProperty("scoringLevel").ValueKind);
    }

    [Fact]
    public async Task SimilarChartsPublishTheFloorAndTheComparedCountRatherThanFiltering()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetFilteredSimilarChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FilteredSimilarChartsRecord(new[]
            {
                new ChartSimilarityRecord(ApiTestData.ChartId2, 0.5859, 0.6132, 0.5041, new[]
                {
                    new ChartSharedBadgeRecord("twist_over90", 0.5)
                }),
                // Below the floor and deliberately still present — a near-miss is not an absence.
                new ChartSimilarityRecord(ApiTestData.ChartId1, 0.5496, 0.5100, 0.6002,
                    Array.Empty<ChartSharedBadgeRecord>())
            }, 30));

        var result = await WithContext(new ChartsController(_mediator.Object))
            .GetSimilar(ApiTestData.ChartId1, "Phoenix");

        JsonApproval.AssertWireShape("""
            {
              "chartsCompared": 30,
              "matchFloor": 0.55,
              "data": [
                {
                  "chartId": "22222222-2222-2222-2222-222222222222",
                  "score": 0.5859,
                  "skillScore": 0.6132,
                  "intensityScore": 0.5041,
                  "sharedBadges": [
                    {
                      "badge": "twist_over90",
                      "coverage": 0.5
                    }
                  ]
                },
                {
                  "chartId": "11111111-1111-1111-1111-111111111111",
                  "score": 0.5496,
                  "skillScore": 0.51,
                  "intensityScore": 0.6002,
                  "sharedBadges": []
                }
              ]
            }
            """, result);
    }

    [Fact]
    public async Task ChartSkillsCarryNpsAndTheJoinedSkillFamilies()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetChartSkillProfilesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ChartSkillProfile(ApiTestData.ChartId1, 50726, 8.4, 18.4, 122, 140, true,
                    new[] { new ChartSkillCoverage("twist_over90", 0.625, 1, 4, true) },
                    new[] { new ChartRarePattern("bracket-5", 3) })
            });

        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ApiTestData.Chart1 });

        var result = await WithContext(new ChartsController(_mediator.Object)).GetSkills("Phoenix");

        JsonApproval.AssertWireShape("""
            {
              "data": [
                {
                  "chartId": "11111111-1111-1111-1111-111111111111",
                  "dataVersion": 50726,
                  "nps": 8.4,
                  "difficultyPrediction": 18.4,
                  "sustainTimeSeconds": 122,
                  "timeUnderTensionSeconds": 140,
                  "lastSegmentIsPeak": true,
                  "skills": [
                    {
                      "name": "twist_over90",
                      "fraction": 0.625,
                      "top3Rank": 1,
                      "practiceRank": 4,
                      "inLastSegment": true
                    }
                  ],
                  "rarePatterns": [
                    {
                      "name": "bracket-5",
                      "count": 3
                    }
                  ]
                }
              ],
              "limit": 100,
              "total": 1,
              "next": null
            }
            """, result);
    }

    [Fact]
    public async Task TierListReturnsChartIdsAndNeverAnotherMixesFallback()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetTierListWithFallbackQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TierListResult(new[]
            {
                new SongTierListEntry(Name.From("Scores"), ApiTestData.ChartId1, TierListCategory.Easy, 1)
            }, false));

        var result = await WithContext(new TierListsController(_mediator.Object))
            .Get("score-difficulty", "Phoenix");

        JsonApproval.AssertWireShape("""
            {
              "data": [
                {
                  "chartId": "11111111-1111-1111-1111-111111111111",
                  "category": "Easy",
                  "order": 1
                }
              ],
              "limit": 1,
              "total": 1,
              "next": null
            }
            """, result);
    }

    [Fact]
    public async Task ProvisionalFallbackIsDiscardedRatherThanServedAsThisMixesData()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetTierListWithFallbackQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TierListResult(new[]
            {
                new SongTierListEntry(Name.From("Scores"), ApiTestData.ChartId1, TierListCategory.Easy, 1)
            }, true));

        var result = await WithContext(new TierListsController(_mediator.Object))
            .Get("score-difficulty", "Phoenix2");

        JsonApproval.AssertWireShape("""
            {
              "data": [],
              "limit": 0,
              "total": 0,
              "next": null
            }
            """, result);
    }

    [Fact]
    public async Task MissingMixIsAProblemDocumentListingTheValidValues()
    {
        var result = await WithContext(new ChartsController(_mediator.Object)).Get();

        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(result).Value);
        Assert.Equal("https://piuscores.arroweclip.se/errors/mix-required", problem.Type);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Contains("Phoenix2", problem.Detail);
        Assert.Contains("FiestaEx", problem.Detail);
    }

    [Fact]
    public async Task UnknownTierListIs404NotAnEmptyList()
    {
        var result = await WithContext(new TierListsController(_mediator.Object)).Get("nope", "Phoenix");

        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(result).Value);
        Assert.Equal(StatusCodes.Status404NotFound, problem.Status);
    }

    // Three lists, named for the question each answers. Publishing "Popularity" alongside them
    // would invite an integrator to read a play count as a difficulty judgement.
    [Theory]
    [InlineData("score-difficulty", "Scores")]
    [InlineData("pass-difficulty", "Pass Count")]
    [InlineData("pg-difficulty", "PG")]
    public async Task EachPhoenixRouteReadsItsOwnStoredList(string route, string storedName)
    {
        Name? asked = null;
        _mediator.Setup(m => m.Send(It.IsAny<GetTierListWithFallbackQuery>(), It.IsAny<CancellationToken>()))
            .Callback((object q, CancellationToken _) => asked = ((GetTierListWithFallbackQuery)q).TierListName)
            .ReturnsAsync(new TierListResult(Array.Empty<SongTierListEntry>(), false));

        await WithContext(new TierListsController(_mediator.Object)).Get(route, "Phoenix");

        Assert.Equal(storedName, asked?.ToString());
    }

    // Before Phoenix the lists were built as one "Difficulty" list rather than split by question,
    // so the route stays stable and the mapping moves. A caller gets the same shape and meaning.
    [Theory]
    [InlineData("XX")]
    [InlineData("Prime2")]
    public async Task PassDifficultyReadsTheLegacyDifficultyListBeforePhoenix(string mix)
    {
        Name? asked = null;
        _mediator.Setup(m => m.Send(It.IsAny<GetTierListWithFallbackQuery>(), It.IsAny<CancellationToken>()))
            .Callback((object q, CancellationToken _) => asked = ((GetTierListWithFallbackQuery)q).TierListName)
            .ReturnsAsync(new TierListResult(Array.Empty<SongTierListEntry>(), false));

        await WithContext(new TierListsController(_mediator.Object)).Get("pass-difficulty", mix);

        Assert.Equal("Difficulty", asked?.ToString());
    }

    // 404, not an empty 200: "this mix never had a scoring model" and "nobody has voted yet" are
    // different answers, and a caller that cannot tell them apart waits for data that is not coming.
    [Theory]
    [InlineData("score-difficulty")]
    [InlineData("pg-difficulty")]
    public async Task ScoreAndPgDifficultyAre404BeforePhoenix(string route)
    {
        var result = await WithContext(new TierListsController(_mediator.Object)).Get(route, "XX");

        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(result).Value);
        Assert.Equal(StatusCodes.Status404NotFound, problem.Status);
        // The message names what this mix does have, so the next call is the right one.
        Assert.Contains("pass-difficulty", problem.Detail);
    }

    // Deliberately unpublished (owner, 2026-08-02): they are visible on /TierLists but they are not
    // difficulty judgements, so they are not part of the difficulty vocabulary.
    [Theory]
    [InlineData("official-scores")]
    [InlineData("popularity")]
    [InlineData("chabala")]
    [InlineData("difficulty")]
    public async Task TheUnpublishedListsAreNotRoutes(string route)
    {
        var result = await WithContext(new TierListsController(_mediator.Object)).Get(route, "Phoenix");

        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(result).Value);
        Assert.Equal(StatusCodes.Status404NotFound, problem.Status);
    }

    [Fact]
    public async Task StaleCursorIsRejectedRatherThanSilentlyRepaged()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ApiTestData.Chart1 });
        var controller = WithContext(new ChartsController(_mediator.Object));

        var mintedForLevel20 = ContinuationToken
            .FromOffset(1, ContinuationToken.FingerprintOf(MixEnum.Phoenix, 20, null, 100)).Encode();
        var result = await controller.Get("Phoenix", 21, cursor: mintedForLevel20);

        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(result).Value);
        Assert.Equal("https://piuscores.arroweclip.se/errors/invalid-cursor", problem.Type);
    }

    // The catalog is re-pulled in full by every tool on every run; the ETag is what makes that free.
    [Fact]
    public async Task RepeatedCatalogReadWithTheSameETagIs304()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ApiTestData.Chart1 });
        var controller = WithContext(new ChartsController(_mediator.Object));

        await controller.Get("Phoenix");
        var etag = controller.Response.Headers.ETag.ToString();
        Assert.NotEmpty(etag);

        controller.Request.Headers.IfNoneMatch = etag;
        var second = await controller.Get("Phoenix");

        Assert.Equal(StatusCodes.Status304NotModified, Assert.IsType<StatusCodeResult>(second).StatusCode);
    }

    // A sub-resource, not a query string: skills belong to a chart. And an unanalysed chart is a
    // different answer from a chart that does not exist — most of the catalog is unanalysed, so a
    // reader who cannot tell them apart chases a perfectly valid id.
    [Fact]
    public async Task OneChartsSkillsAreASubResource()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetChartSkillProfilesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ChartSkillProfile(ApiTestData.ChartId1, 50726, 8.4, 18.4, 122, 140, true,
                    Array.Empty<ChartSkillCoverage>(), Array.Empty<ChartRarePattern>())
            });

        var result = await WithContext(new ChartsController(_mediator.Object))
            .GetChartSkills(ApiTestData.ChartId1);

        Assert.IsType<ContentResult>(result);
    }

    [Fact]
    public async Task AnUnanalysedChartIs404WithAnExplanation()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetChartSkillProfilesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ChartSkillProfile>());

        var result = await WithContext(new ChartsController(_mediator.Object))
            .GetChartSkills(ApiTestData.ChartId1);

        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(result).Value);
        Assert.Equal(StatusCodes.Status404NotFound, problem.Status);
        Assert.Contains("does not", problem.Detail);
    }

    // ── The patch a chart arrived in, and the four version filters (docs/design/chart-versions.md §3) ──

    private static readonly Guid LaunchChart = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid PatchChart = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    private static readonly Guid RelaunchChart = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003");
    private static readonly Guid UnknownChart = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000004");
    private static readonly Guid CarriedChart = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000005");

    /// <summary>A chart that debuted in Phoenix at that patch: the same stamp on both timelines.</summary>
    private static Chart Stamped(Chart chart, string version, DateOnly date, int order)
    {
        var stamp = new VersionStamp(MixEnum.Phoenix, version, date, order);
        return chart with { AddedIn = stamp, Debut = stamp };
    }

    private void SeedPhoenixVersions()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetMixVersionsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new MixVersionRecord(MixEnum.Phoenix, "1.00.0", new DateOnly(2023, 7, 4), 10, 1),
                new MixVersionRecord(MixEnum.Phoenix, "1.01.0", new DateOnly(2023, 7, 27), 20, 1),
                new MixVersionRecord(MixEnum.Phoenix, "2.00.0", new DateOnly(2024, 5, 27), 30, 1)
            });
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Stamped(ApiTestData.Chart1 with { Id = LaunchChart }, "1.00.0", new DateOnly(2023, 7, 4), 10),
                Stamped(ApiTestData.Chart1 with { Id = PatchChart }, "1.01.0", new DateOnly(2023, 7, 27), 20),
                Stamped(ApiTestData.Chart1 with { Id = RelaunchChart }, "2.00.0", new DateOnly(2024, 5, 27), 30),
                ApiTestData.Chart1 with { Id = UnknownChart },
                // Carried into Phoenix at launch from XX, where it debuted at 2.05.0.
                ApiTestData.Chart1 with
                {
                    Id = CarriedChart, OriginalMix = MixEnum.XX,
                    AddedIn = new VersionStamp(MixEnum.Phoenix, "1.00.0", new DateOnly(2023, 7, 4), 10),
                    Debut = new VersionStamp(MixEnum.XX, "2.05.0", new DateOnly(2021, 3, 1), 50)
                }
            });
    }

    private static Guid[] Ids(IActionResult result)
    {
        var json = result switch
        {
            ContentResult c => c.Content ?? string.Empty,
            JsonResult j => System.Text.Json.JsonSerializer.Serialize(j.Value, ApiV2ControllerBase.WireOptions),
            _ => System.Text.Json.JsonSerializer.Serialize(((ObjectResult)result).Value)
        };
        return System.Text.Json.JsonDocument.Parse(json).RootElement.GetProperty("data")
            .EnumerateArray().Select(row => row.GetProperty("id").GetGuid()).ToArray();
    }

    [Fact]
    public async Task ChartCarriesBothTimelinesAndTheirDates()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Stamped(ApiTestData.Chart1, "1.01.0", new DateOnly(2026, 9, 3), 20) });

        var result = await WithContext(new ChartsController(_mediator.Object)).Get("Phoenix2");

        var row = FirstRow(result);
        Assert.Equal("1.01.0", row.GetProperty("addedInVersion").GetString());
        Assert.Equal("2026-09-03", row.GetProperty("addedOn").GetString());
        Assert.Equal("1.01.0", row.GetProperty("debutVersion").GetString());
        Assert.Equal("2026-09-03", row.GetProperty("debutedOn").GetString());
        Assert.True(row.GetProperty("debut").GetBoolean());
    }

    [Fact]
    public async Task VersionsListCarriesTheDateTheOrderAndWhatEachPatchAdded()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetMixVersionsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new MixVersionRecord(MixEnum.Phoenix2, "1.00.0", new DateOnly(2026, 7, 9), 10, 4675, 249),
                new MixVersionRecord(MixEnum.Phoenix2, "1.01.0", new DateOnly(2026, 9, 3), 20, 59, 59),
                new MixVersionRecord(MixEnum.Phoenix2, "JE", null, 30, 0, 0)
            });

        var result = await WithContext(new VersionsController(_mediator.Object)).Get("Phoenix2");

        JsonApproval.AssertWireShape("""
            {
              "data": [
                {
                  "name": "1.00.0",
                  "releaseDate": "2026-07-09",
                  "sortOrder": 10,
                  "chartCount": 4675,
                  "debutCount": 249
                },
                {
                  "name": "1.01.0",
                  "releaseDate": "2026-09-03",
                  "sortOrder": 20,
                  "chartCount": 59,
                  "debutCount": 59
                },
                {
                  "name": "JE",
                  "releaseDate": null,
                  "sortOrder": 30,
                  "chartCount": 0,
                  "debutCount": 0
                }
              ],
              "limit": 3,
              "total": 3,
              "next": null
            }
            """, result);
    }

    [Fact]
    public async Task VersionsRequireAMixLikeEveryOtherCatalogRead()
    {
        var result = await WithContext(new VersionsController(_mediator.Object)).Get();

        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(result).Value);
        Assert.Equal("https://piuscores.arroweclip.se/errors/mix-required", problem.Type);
    }

    [Fact]
    public async Task AddedByVersionKeepsEverythingUpToAndIncludingItAndNeverAnUnknownPatch()
    {
        SeedPhoenixVersions();

        var result = await WithContext(new ChartsController(_mediator.Object)).Get("Phoenix", addedBy: "1.01.0");

        Assert.Equal(new[] { LaunchChart, PatchChart, CarriedChart }, Ids(result));
    }

    [Fact]
    public async Task AddedAfterVersionIsStrictlyAfter()
    {
        SeedPhoenixVersions();

        var result = await WithContext(new ChartsController(_mediator.Object)).Get("Phoenix", addedAfterVersion: "1.01.0");

        Assert.Equal(new[] { RelaunchChart }, Ids(result));
    }

    [Fact]
    public async Task AddedInVersionTakesACommaListOrARepeatedParameter()
    {
        SeedPhoenixVersions();
        var controller = WithContext(new ChartsController(_mediator.Object));

        var commaList = await controller.Get("Phoenix", addedIn: new[] { "1.00.0,2.00.0" });
        var repeated = await controller.Get("Phoenix", addedIn: new[] { "1.00.0", "2.00.0" });

        Assert.Equal(new[] { LaunchChart, RelaunchChart, CarriedChart }, Ids(commaList));
        Assert.Equal(new[] { LaunchChart, RelaunchChart, CarriedChart }, Ids(repeated));
    }

    [Fact]
    public async Task AddedAfterADateIsExclusiveOfTheDay()
    {
        SeedPhoenixVersions();

        var result = await WithContext(new ChartsController(_mediator.Object))
            .Get("Phoenix", addedAfter: new DateOnly(2023, 7, 27));

        Assert.Equal(new[] { RelaunchChart }, Ids(result));
    }

    [Fact]
    public async Task DebutedInVersionKeepsOnlyChartsThatFirstAppearedInThatPatchOfThisMix()
    {
        SeedPhoenixVersions();

        var result = await WithContext(new ChartsController(_mediator.Object)).Get("Phoenix", debutedIn: new[] { "1.00.0" });

        Assert.Equal(new[] { LaunchChart }, Ids(result));
    }

    [Fact]
    public async Task DebutFalseKeepsTheCarryOversWhoseRowsNameTheOlderMixesPatch()
    {
        SeedPhoenixVersions();

        var result = await WithContext(new ChartsController(_mediator.Object)).Get("Phoenix", debut: false);

        Assert.Equal(new[] { CarriedChart }, Ids(result));
        var row = FirstRow(result);
        Assert.Equal("1.00.0", row.GetProperty("addedInVersion").GetString());
        Assert.Equal("2.05.0", row.GetProperty("debutVersion").GetString());
        Assert.Equal("2021-03-01", row.GetProperty("debutedOn").GetString());
        Assert.False(row.GetProperty("debut").GetBoolean());
    }

    [Fact]
    public async Task ARandomDrawWithDebutedInNarrowsThePicksAndSetsTheFlag()
    {
        SeedPhoenixVersions();
        _mediator.Setup(m => m.Send(It.IsAny<GetRandomChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Chart>());

        await WithContext(new ChartsController(_mediator.Object))
            .GetRandom("Phoenix", addedBy: "1.01.0", debutedIn: new[] { "1.01.0,2.00.0" });

        _mediator.Verify(m => m.Send(It.Is<GetRandomChartsQuery>(q =>
            q.Settings.Versions.SetEquals(new[] { "1.01.0" }) && q.Settings.Debut == true), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnUnknownVersionIsAProblemPointingAtTheVersionsList()
    {
        SeedPhoenixVersions();

        var result = await WithContext(new ChartsController(_mediator.Object)).Get("Phoenix", addedBy: "9.99.9");

        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(result).Value);
        Assert.Equal("https://piuscores.arroweclip.se/errors/invalid-version", problem.Type);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Contains("9.99.9", problem.Title);
        Assert.Contains("/api/v2/versions?mix=Phoenix", problem.Detail);
    }

    [Fact]
    public async Task ARandomDrawCarriesThePickedPatchesIntoItsSettings()
    {
        SeedPhoenixVersions();
        _mediator.Setup(m => m.Send(It.IsAny<GetRandomChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Chart>());

        await WithContext(new ChartsController(_mediator.Object)).GetRandom("Phoenix", addedBy: "1.01.0");

        _mediator.Verify(m => m.Send(It.Is<GetRandomChartsQuery>(q =>
            q.Settings.Versions.SetEquals(new[] { "1.00.0", "1.01.0" })), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ARandomDrawWhoseFilterReachesNoPatchIsEmptyRatherThanUnfiltered()
    {
        SeedPhoenixVersions();

        var result = await WithContext(new ChartsController(_mediator.Object))
            .GetRandom("Phoenix", addedAfter: new DateOnly(2030, 1, 1));

        Assert.Empty(Ids(result));
        _mediator.Verify(m => m.Send(It.IsAny<GetRandomChartsQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Channels (docs/design/song-channels.md §4) ──────────────────────────────────────────

    private static readonly Guid KPopChart = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    private static readonly Guid XrossChart = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly Guid OriginalChart = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000003");
    private static readonly Guid UnchannelledChart = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000004");

    private static Chart InChannel(Chart chart, Channel channel)
    {
        return chart with { Song = chart.Song with { Channel = channel } };
    }

    private static int Total(IActionResult result)
    {
        var json = result is ContentResult c
            ? c.Content ?? string.Empty
            : System.Text.Json.JsonSerializer.Serialize(((ObjectResult)result).Value);
        return System.Text.Json.JsonDocument.Parse(json).RootElement.GetProperty("total").GetInt32();
    }

    /// <summary>Phoenix 2 offers four channels — no J-Music — and its charts sit in three of them, one song with none.</summary>
    private void SeedPhoenix2Channels()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetMixChannelsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new MixChannelRecord(MixEnum.Phoenix2, Channel.Original, 441, 2997),
                new MixChannelRecord(MixEnum.Phoenix2, Channel.KPop, 24, 219),
                new MixChannelRecord(MixEnum.Phoenix2, Channel.WorldMusic, 142, 1088),
                new MixChannelRecord(MixEnum.Phoenix2, Channel.Xross, 47, 371)
            });
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                InChannel(ApiTestData.Chart1 with { Id = KPopChart, Mix = MixEnum.Phoenix2 }, Channel.KPop),
                InChannel(ApiTestData.Chart1 with { Id = XrossChart, Mix = MixEnum.Phoenix2 }, Channel.Xross),
                InChannel(ApiTestData.Chart1 with { Id = OriginalChart, Mix = MixEnum.Phoenix2 }, Channel.Original),
                ApiTestData.Chart1 with { Id = UnchannelledChart, Mix = MixEnum.Phoenix2 }
            });
    }

    [Fact]
    public async Task ChannelsListCarriesTheTokenTheDisplayNameTheOrderAndTheCounts()
    {
        SeedPhoenix2Channels();

        var result = await WithContext(new ChannelsController(_mediator.Object)).Get("Phoenix2");

        JsonApproval.AssertWireShape("""
            {
              "data": [
                {
                  "name": "Original",
                  "displayName": "Original",
                  "sortOrder": 0,
                  "songCount": 441,
                  "chartCount": 2997
                },
                {
                  "name": "KPop",
                  "displayName": "K-Pop",
                  "sortOrder": 10,
                  "songCount": 24,
                  "chartCount": 219
                },
                {
                  "name": "WorldMusic",
                  "displayName": "World Music",
                  "sortOrder": 20,
                  "songCount": 142,
                  "chartCount": 1088
                },
                {
                  "name": "Xross",
                  "displayName": "Xross",
                  "sortOrder": 40,
                  "songCount": 47,
                  "chartCount": 371
                }
              ],
              "limit": 4,
              "total": 4,
              "next": null
            }
            """, result);
    }

    [Fact]
    public async Task ChannelsRequireAMixLikeEveryOtherCatalogRead()
    {
        var result = await WithContext(new ChannelsController(_mediator.Object)).Get();

        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(result).Value);
        Assert.Equal("https://piuscores.arroweclip.se/errors/mix-required", problem.Type);
    }

    [Fact]
    public async Task ChartCarriesTheSongsChannelOnTheMix()
    {
        SeedPhoenix2Channels();

        var result = await WithContext(new ChartsController(_mediator.Object)).Get("Phoenix2");

        Assert.Equal("KPop", FirstRow(result).GetProperty("channel").GetString());
    }

    [Fact]
    public async Task ChannelFilterKeepsThePickedChannelsAndTakesACommaListOrARepeatedParameter()
    {
        SeedPhoenix2Channels();
        var controller = WithContext(new ChartsController(_mediator.Object));

        var commaList = await controller.Get("Phoenix2", channels: new[] { "KPop,Xross" });
        var repeated = await controller.Get("Phoenix2", channels: new[] { "kpop", "XROSS" });

        Assert.Equal(new[] { KPopChart, XrossChart }, Ids(commaList));
        Assert.Equal(new[] { KPopChart, XrossChart }, Ids(repeated));
    }

    [Fact]
    public async Task AChannelTheMixDoesNotOfferIsAProblemPointingAtTheChannelsList()
    {
        SeedPhoenix2Channels();

        var result = await WithContext(new ChartsController(_mediator.Object)).Get("Phoenix2", channels: new[] { "JMusic" });

        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(result).Value);
        Assert.Equal("https://piuscores.arroweclip.se/errors/invalid-channel", problem.Type);
        Assert.Contains("/api/v2/channels?mix=Phoenix2", problem.Detail);
    }

    [Fact]
    public async Task ADisplayNameIsNotAChannelToken()
    {
        SeedPhoenix2Channels();

        var result = await WithContext(new ChartsController(_mediator.Object)).Get("Phoenix2", channels: new[] { "K-Pop" });

        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(result).Value);
        Assert.Equal("https://piuscores.arroweclip.se/errors/invalid-channel", problem.Type);
        Assert.Contains("KPop", problem.Detail);
    }

    [Fact]
    public async Task SongsTakeTheChannelFilterAndNeverMatchASongWithNone()
    {
        SeedPhoenix2Channels();
        _mediator.Setup(m => m.Send(It.IsAny<GetSongsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new SongRecord(Name.From("Nostalgia"), SongType.Arcade, new Uri("https://piuimages.example.com/n.png"),
                    TimeSpan.FromSeconds(125), Name.From("Woody"), 100m, 100m, Channel.KPop),
                new SongRecord(Name.From("Ghroth"), SongType.Arcade, new Uri("https://piuimages.example.com/g.png"),
                    TimeSpan.FromSeconds(110), Name.From("nato"), 200m, 200m, Channel.Original),
                new SongRecord(Name.From("Mystery"), SongType.Arcade, new Uri("https://piuimages.example.com/m.png"),
                    TimeSpan.FromSeconds(110), Name.From("Unknown"), 200m, 200m)
            });

        var result = await WithContext(new SongsController(_mediator.Object)).Get("Phoenix2", channels: new[] { "KPop" });

        Assert.Equal(1, Total(result));
        var row = FirstRow(result);
        Assert.Equal("Nostalgia", row.GetProperty("name").GetString());
        Assert.Equal("KPop", row.GetProperty("channel").GetString());
    }

    [Fact]
    public async Task ARandomDrawPassesThePickedChannelsToTheSettings()
    {
        SeedPhoenix2Channels();
        _mediator.Setup(m => m.Send(It.IsAny<GetRandomChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Chart>());

        await WithContext(new ChartsController(_mediator.Object)).GetRandom("Phoenix2", channels: new[] { "WorldMusic,Xross" });

        _mediator.Verify(m => m.Send(It.Is<GetRandomChartsQuery>(q =>
            q.Settings.Channels.SetEquals(new[] { Channel.WorldMusic, Channel.Xross })), It.IsAny<CancellationToken>()), Times.Once);
    }
}
