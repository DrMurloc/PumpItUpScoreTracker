namespace ScoreTracker.Domain.Models.Titles.Interface
{
    public interface ISpecificChartTitle
    {
        bool AppliesToChart(Chart chart);
    }
}
