using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Tests.Api;

internal static class ApiTestData
{
    public static readonly Guid ChartId1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid ChartId2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid PublicUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid PrivateUserId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    public static readonly Guid TournamentId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    public static readonly DateTimeOffset Date1 = new(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset Date2 = new(2026, 2, 20, 0, 0, 0, TimeSpan.Zero);

    public static Chart Chart1 { get; } = new(
        ChartId1,
        MixEnum.Phoenix,
        new Song(Name.From("Conflict"), SongType.Arcade,
            new Uri("https://piuimages.example.com/conflict.png"),
            TimeSpan.FromSeconds(95), Name.From("Doin"), null),
        ChartType.Single,
        DifficultyLevel.From(20),
        MixEnum.Phoenix,
        Name.From("ANDAMIRO"),
        731,
        new HashSet<Skill>());

    public static Chart Chart2 { get; } = new(
        ChartId2,
        MixEnum.Phoenix,
        new Song(Name.From("District 1"), SongType.Arcade,
            new Uri("https://piuimages.example.com/district1.png"),
            TimeSpan.FromSeconds(100), Name.From("Max"), null),
        ChartType.Double,
        DifficultyLevel.From(22),
        MixEnum.Phoenix,
        null,
        845,
        new HashSet<Skill>());

    public static User PublicUser { get; } = new(
        PublicUserId, Name.From("VisiblePlayer"), true, Name.From("VISIBL"),
        new Uri("https://piuimages.example.com/avatar1.png"), Name.From("Canada"));

    public static User PrivateUser { get; } = new(
        PrivateUserId, Name.From("HiddenPlayer"), false, Name.From("HIDDEN"),
        new Uri("https://piuimages.example.com/avatar2.png"), null);
}
