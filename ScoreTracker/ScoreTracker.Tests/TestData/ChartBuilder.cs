using System;
using System.Collections.Generic;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Tests.TestData;

internal sealed class ChartBuilder
{
    private Guid _id = Guid.NewGuid();
    private MixEnum _originalMix = MixEnum.Phoenix;
    private MixEnum _mix = MixEnum.Phoenix;
    private Song _song = new(
        Name.From("Test Song"),
        SongType.Arcade,
        new Uri("https://example.invalid/song.png"),
        TimeSpan.FromMinutes(2),
        Name.From("Test Artist"),
        Bpm: null);
    private ChartType _type = ChartType.Single;
    private DifficultyLevel _level = DifficultyLevel.From(15);
    private Name? _stepArtist;
    private double? _scoringLevel;
    private int? _noteCount;
    private IReadOnlySet<Skill> _skills = new HashSet<Skill>();

    public ChartBuilder WithId(Guid id) { _id = id; return this; }
    public ChartBuilder WithSong(Song song) { _song = song; return this; }
    public ChartBuilder WithSongName(string name) { _song = _song with { Name = Name.From(name) }; return this; }
    public ChartBuilder WithType(ChartType type) { _type = type; return this; }
    public ChartBuilder WithLevel(int level) { _level = DifficultyLevel.From(level); return this; }
    public ChartBuilder WithLevel(DifficultyLevel level) { _level = level; return this; }
    public ChartBuilder WithMix(MixEnum mix) { _mix = mix; return this; }
    public ChartBuilder WithOriginalMix(MixEnum mix) { _originalMix = mix; return this; }
    public ChartBuilder WithStepArtist(string stepArtist) { _stepArtist = Name.From(stepArtist); return this; }
    public ChartBuilder WithScoringLevel(double scoringLevel) { _scoringLevel = scoringLevel; return this; }
    public ChartBuilder WithNoteCount(int noteCount) { _noteCount = noteCount; return this; }
    public ChartBuilder WithSkills(IReadOnlySet<Skill> skills) { _skills = skills; return this; }

    public Chart Build() => new(_id, _originalMix, _song, _type, _level, _mix,
        _stepArtist, _scoringLevel, _noteCount, _skills);

    public static implicit operator Chart(ChartBuilder b) => b.Build();
}
