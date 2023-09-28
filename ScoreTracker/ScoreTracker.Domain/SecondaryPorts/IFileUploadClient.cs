namespace ScoreTracker.Domain.SecondaryPorts
{
    public interface IFileUploadClient
    {
        Task<Uri> UploadFile(string path, Stream fileStream, CancellationToken cancellationToken = default);
    }
}