namespace ScoreTracker.Domain.SecondaryPorts
{
    public interface IFileUploadClient
    {
        Task<Uri> UploadFile(string path, Stream fileStream, CancellationToken cancellationToken = default);
        Task<bool> DoesFileExist(string path, out Uri fullPath, CancellationToken cancellationToken = default);
        Task DeleteFile(string path, CancellationToken cancellationToken = default);
        Task<Uri> CopyFromSource(Uri oldPath, string newPath, CancellationToken cancellationToken = default);
        Task<IEnumerable<Uri>> GetFiles(string path, CancellationToken cancellationToken = default);
    }
}