namespace ScoreTracker.Application.Dtos;

public sealed record SearchResultDto<T>(IEnumerable<T> Results, int Total)
{
}