using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;
using ScoreTracker.Data.Configuration;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Data.Clients
{
    public sealed class AzureBlobFileUploadClient : IFileUploadClient
    {
        private readonly BlobContainerClient _blob;

        public AzureBlobFileUploadClient(IOptions<AzureBlobConfiguration> options)
        {
            _blob = new BlobContainerClient(options.Value.ConnectionString, "$web");
        }

        public async Task<Uri> UploadFile(string path, Stream fileStream, CancellationToken cancellationToken = default)
        {
            path = path.TrimStart('/');

            var blobClient = _blob.GetBlobClient(path);
            await blobClient.UploadAsync(fileStream, cancellationToken);
            return new Uri($"https://piuimages.arroweclip.se/{path}");
        }
    }
}