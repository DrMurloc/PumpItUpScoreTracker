namespace ScoreTracker.Domain.SecondaryPorts
{
    public interface IFileUploadClient
    {
        Task<Uri> UploadFile(string path, Stream fileStream, CancellationToken cancellationToken = default);
        Task<bool> DoesFileExist(string path, out Uri fullPath, CancellationToken cancellationToken = default);
        Task<Uri> CopyFromSource(Uri oldPath, string newPath, CancellationToken cancellationToken = default);
    }
}