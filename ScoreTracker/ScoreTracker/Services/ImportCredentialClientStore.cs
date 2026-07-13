using System.Text.Json;
using Microsoft.JSInterop;

namespace ScoreTracker.Web.Services;

// The remembered credential as it sits in this device's browser: an opaque key id, the base64
// AES-GCM ciphertext, and the credential generation it was written under (a global bump — an
// admin key-cycle — makes a stale blob self-purge on next load).
public sealed record StoredCredentialBlob(Guid KeyId, string Ciphertext, int Generation);

public interface IImportCredentialClientStore
{
    Task<StoredCredentialBlob?> Read(CancellationToken cancellationToken = default);
    Task Write(StoredCredentialBlob blob, CancellationToken cancellationToken = default);
    Task Clear(CancellationToken cancellationToken = default);
}

public sealed class ImportCredentialClientStore : IImportCredentialClientStore
{
    private const string StorageKey = "piu.importCredential.v1";
    private readonly IJSRuntime _js;

    public ImportCredentialClientStore(IJSRuntime js)
    {
        _js = js;
    }

    public async Task<StoredCredentialBlob?> Read(CancellationToken cancellationToken = default)
    {
        var json = await _js.InvokeAsync<string?>("credentialStorage.get", cancellationToken, StorageKey);
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<StoredCredentialBlob>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task Write(StoredCredentialBlob blob, CancellationToken cancellationToken = default)
    {
        await _js.InvokeVoidAsync("credentialStorage.set", cancellationToken, StorageKey,
            JsonSerializer.Serialize(blob));
    }

    public async Task Clear(CancellationToken cancellationToken = default)
    {
        await _js.InvokeVoidAsync("credentialStorage.remove", cancellationToken, StorageKey);
    }
}
