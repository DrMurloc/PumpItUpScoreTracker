namespace ScoreTracker.Web.Services;

public sealed class ChartVideoDisplayer
{
    public event Action<Guid>? ChartSelected;

    public void ShowVideo(Guid chartId)
    {
        ChartSelected?.Invoke(chartId);
    }
}