using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using ScoreTracker.Data.Configuration;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Data.Clients
{
    /// <summary>
    ///     The step-file archive on Azure blob, in its own container beside the photo pipeline —
    ///     same <see cref="AzureBlobConfiguration.ConnectionString" />, zero new secrets
    ///     (docs/design/step-chart-failure-map.md D7). Parked when unconfigured, the
    ///     AnthropicBatchClient pattern: the constructor never touches Azure, so dependency
    ///     validation and every secret-less environment boot clean and simply skip the archive.
    /// </summary>
    public sealed class AzureBlobStepFileStore : IStepFileStore
    {
        private const string ContainerName = "stepfiles";
        private readonly AzureBlobConfiguration _configuration;
        private BlobContainerClient? _container;

        public AzureBlobStepFileStore(IOptions<AzureBlobConfiguration> options)
        {
            _configuration = options.Value;
        }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_configuration?.ConnectionString);

        public async Task Put(string vintage, string path, Stream content,
            CancellationToken cancellationToken = default)
        {
            var container = await Container(cancellationToken);
            await container.GetBlobClient(BlobName(vintage, path))
                .UploadAsync(content, true, cancellationToken);
        }

        public async Task<string?> GetText(string vintage, string path,
            CancellationToken cancellationToken = default)
        {
            var container = await Container(cancellationToken);
            var blob = container.GetBlobClient(BlobName(vintage, path));
            if (!await blob.ExistsAsync(cancellationToken)) return null;
            var content = await blob.DownloadContentAsync(cancellationToken);
            return content.Value.Content.ToString();
        }

        public async Task<IReadOnlyList<string>> List(string vintage,
            CancellationToken cancellationToken = default)
        {
            var container = await Container(cancellationToken);
            var prefix = $"{vintage}/";
            var paths = new List<string>();
            await foreach (var blob in container.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix,
                               cancellationToken))
                paths.Add(blob.Name[prefix.Length..]);
            return paths;
        }

        public async Task<IReadOnlyList<string>> ListVintages(CancellationToken cancellationToken = default)
        {
            var container = await Container(cancellationToken);
            var vintages = new List<string>();
            await foreach (var item in container.GetBlobsByHierarchyAsync(BlobTraits.None, BlobStates.None,
                               "/", null, cancellationToken))
                if (item.IsPrefix)
                    vintages.Add(item.Prefix.TrimEnd('/'));
            return vintages;
        }

        private static string BlobName(string vintage, string path)
        {
            return $"{vintage}/{path.Replace('\\', '/').TrimStart('/')}";
        }

        private async Task<BlobContainerClient> Container(CancellationToken cancellationToken)
        {
            if (!IsConfigured)
                throw new InvalidOperationException(
                    "The step-file store is not configured — check IsConfigured before calling.");
            if (_container == null)
            {
                _container = new BlobContainerClient(_configuration.ConnectionString, ContainerName);
                await _container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            }

            return _container;
        }
    }
}
