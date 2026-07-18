using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using ScoreTracker.Data.Configuration;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Data.Clients;

public sealed class AzureBlobFileUploadClient : IFileUploadClient
{
    private static readonly ISet<string> _existingPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly BlobContainerClient _blob;
    private readonly HttpClient _client;

    public AzureBlobFileUploadClient(IOptions<AzureBlobConfiguration> options,
        HttpClient client)
    {
        _client = client;
        _blob = new BlobContainerClient(options.Value.ConnectionString, "$web");
    }

    public async Task<Uri> UploadFile(string path, Stream fileStream, CancellationToken cancellationToken = default)
    {
        path = path.TrimStart('/');
        var blobClient = _blob.GetBlobClient(path);

        // Overwrites: every caller treats upload as save-semantics. The plain Stream
        // overload this replaced threw BlobAlreadyExists instead — which silently killed
        // the daily share-card refresh on every run after its first, and a UCS photo
        // re-submission the same way. The headers carry the content type, which uploads
        // otherwise serve as application/octet-stream.
        await blobClient.UploadAsync(fileStream, new BlobUploadOptions
        {
            HttpHeaders = HeadersFor(path)
        }, cancellationToken);
        return new Uri($"https://piuimages.arroweclip.se/{path}");
    }

    // Blobs serve as application/octet-stream unless the type rides the upload — and some
    // unfurlers and crawlers refuse to render an og:image served that way.
    private static BlobHttpHeaders HeadersFor(string path)
    {
        return new BlobHttpHeaders
        {
            ContentType = Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".svg" => "image/svg+xml",
                ".ico" => "image/x-icon",
                _ => "application/octet-stream"
            }
        };
    }

    public Task<bool> DoesFileExist(string path, out Uri fullPath,
        CancellationToken cancellationToken = default)
    {
        path = path.TrimStart('/');
        fullPath = new Uri($"https://piuimages.arroweclip.se/{path}");
        if (_existingPaths.Contains(path)) return Task.FromResult(true);
        var blobClient = _blob.GetBlobClient(path);
        var result = blobClient.ExistsAsync(cancellationToken).Result.Value;
        if (result) _existingPaths.Add(path);
        return Task.FromResult(result);
    }

    public async Task DeleteFile(string path, CancellationToken cancellationToken = default)
    {
        path = path.TrimStart('/');
        var blobClient = _blob.GetBlobClient(path);
        await blobClient.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, null, cancellationToken);
        _existingPaths.Remove(path);
    }


    public async Task<Uri> CopyFromSource(Uri oldPath, string newPath,
        CancellationToken cancellationToken = default)
    {
        newPath = newPath.TrimStart('/');

        var blobClient = _blob.GetBlobClient(newPath);

        var stream = await _client.GetStreamAsync(oldPath, cancellationToken);
        // No conditions: this path always overwrote (the bool overload it replaced).
        await blobClient.UploadAsync(stream, new BlobUploadOptions { HttpHeaders = HeadersFor(newPath) },
            cancellationToken);
        return new Uri($"https://piuimages.arroweclip.se/{newPath}");
    }

    public async Task<IEnumerable<Uri>> GetFiles(string path, CancellationToken cancellationToken = default)
    {
        var list = new List<Uri>();

        await foreach (var item in _blob.GetBlobsByHierarchyAsync(new GetBlobsByHierarchyOptions { Prefix = path })
                           .WithCancellation(cancellationToken))
            if (item.IsBlob)
                list.Add(new Uri($"https://piuimages.arroweclip.se/{item.Blob.Name}"));

        return list;
    }
}