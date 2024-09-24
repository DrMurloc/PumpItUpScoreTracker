﻿using System.Web;
using Azure.Storage.Blobs;
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
        path = HttpUtility.UrlEncode(path.TrimStart('/'));

        var blobClient = _blob.GetBlobClient(path);
        await blobClient.UploadAsync(fileStream, cancellationToken);
        return new Uri($"https://piuimages.arroweclip.se/{path}");
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


    public async Task<Uri> CopyFromSource(Uri oldPath, string newPath,
        CancellationToken cancellationToken = default)
    {
        newPath = newPath.TrimStart('/');

        var blobClient = _blob.GetBlobClient(newPath);

        var stream = await _client.GetStreamAsync(oldPath, cancellationToken);
        await blobClient.UploadAsync(stream, true, cancellationToken);
        return new Uri($"https://piuimages.arroweclip.se/{newPath}");
    }
}