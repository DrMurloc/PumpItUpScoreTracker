using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Controllers.Api.V2;

namespace ScoreTracker.Tests.Api;

/// <summary>
///     Wire shapes for the api/v2 catalog. These are the contract community tools build against.
/// </summary>
public sealed class V2CatalogApiShapeTests
{
    private readonly Mock<IMediator> _mediator = new();

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
                    Name.From("Doin"), 170m, 190m)
            });

        var result = await WithContext(new SongsController(_mediator.Object)).Get("Phoenix");

        JsonApproval.AssertWireShape("""
            {
              "data": [
                {
                  "name": "Conflict",
                  "type": "Arcade",
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
                  "songName": "Conflict",
                  "imageUrl": "https://piuimages.example.com/conflict.png",
                  "type": "Single",
                  "level": 20,
                  "difficulty": "S20",
                  "noteCount": 731,
                  "playerCount": 1,
                  "stepArtist": "ANDAMIRO",
                  "legacySlot": null
                }
              ],
              "limit": 100,
              "total": 1,
              "next": null
            }
            """, result);
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

        var result = await WithContext(new ChartAnalysisController(_mediator.Object)).GetSkills();

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
}
