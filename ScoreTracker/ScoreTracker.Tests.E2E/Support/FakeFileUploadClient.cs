using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Tests.E2E.Support;

/// <summary>
///     Azure blob is the one external boundary WireMock can't stand in for: the avatar
///     conversion during a PIUGAME login copies the avatar from the PIU CDN into blob
///     storage, which would both hit the real CDN and need a live storage account. The
///     fake answers with stable synthetic URIs and stores nothing.
/// </summary>
internal sealed class FakeFileUploadClient : IFileUploadClient
{
    public Task<Uri> UploadFile(string path, Stream fileStream, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ToUri(path));
    }

    public Task<bool> DoesFileExist(string path, out Uri fullPath, CancellationToken cancellationToken = default)
    {
        fullPath = ToUri(path);
        return Task.FromResult(false);
    }

    public Task DeleteFile(string path, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<Uri> CopyFromSource(Uri oldPath, string newPath, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ToUri(newPath));
    }

    public Task<IEnumerable<Uri>> GetFiles(string path, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Enumerable.Empty<Uri>());
    }

    private static Uri ToUri(string path)
    {
        return new Uri($"https://e2e-files.invalid{(path.StartsWith('/') ? path : "/" + path)}");
    }
}
