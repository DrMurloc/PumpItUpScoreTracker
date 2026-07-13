using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using ScoreTracker.Data.Clients;
using ScoreTracker.Data.Configuration;
using ScoreTracker.Identity.Domain;
using ScoreTracker.Identity.Infrastructure;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class ImportCredentialProtectorTests
{
    private static ImportCredentialProtector Build()
    {
        var envelope = new KeyEnvelope(Options.Create(new KeyVaultConfiguration
        {
            LocalKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        }));
        return new ImportCredentialProtector(envelope, new InMemoryKeyStore(),
            FakeDateTime.At(DateTimeOffset.UnixEpoch).Object);
    }

    [Fact]
    public async Task ProtectThenUnprotectRoundTripsUsernameAndPassword()
    {
        var protector = Build();
        var userId = Guid.NewGuid();

        var (keyId, ciphertext) = await protector.Protect(userId, "player1", "hunter2");
        var (username, password) = await protector.Unprotect(userId, keyId, ciphertext);

        Assert.Equal("player1", username);
        Assert.Equal("hunter2", password);
    }

    [Fact]
    public async Task UnprotectWithNoStoredKeyThrows()
    {
        var protector = Build();

        await Assert.ThrowsAsync<CredentialUnlockException>(
            () => protector.Unprotect(Guid.NewGuid(), Guid.NewGuid(), Convert.ToBase64String(new byte[40])));
    }

    [Fact]
    public async Task AnotherUserCannotUnprotectYourCredential()
    {
        var protector = Build();
        var owner = Guid.NewGuid();
        var attacker = Guid.NewGuid();
        var (keyId, ciphertext) = await protector.Protect(owner, "player1", "hunter2");

        await Assert.ThrowsAsync<CredentialUnlockException>(() => protector.Unprotect(attacker, keyId, ciphertext));
    }

    [Fact]
    public async Task TamperedCiphertextFailsToUnprotect()
    {
        var protector = Build();
        var userId = Guid.NewGuid();
        var (keyId, ciphertext) = await protector.Protect(userId, "player1", "hunter2");

        var raw = Convert.FromBase64String(ciphertext);
        raw[^1] ^= 0xFF;

        await Assert.ThrowsAsync<CredentialUnlockException>(
            () => protector.Unprotect(userId, keyId, Convert.ToBase64String(raw)));
    }

    private sealed class InMemoryKeyStore : IImportCredentialKeyStore
    {
        private readonly Dictionary<Guid, (Guid UserId, byte[] Wrapped)> _rows = new();

        public Task Save(Guid keyId, Guid userId, byte[] wrappedDataKey, DateTimeOffset createdAt,
            CancellationToken cancellationToken = default)
        {
            _rows[keyId] = (userId, wrappedDataKey);
            return Task.CompletedTask;
        }

        public Task<byte[]?> GetWrappedKey(Guid keyId, Guid userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_rows.TryGetValue(keyId, out var row) && row.UserId == userId ? row.Wrapped : null);
        }

        public Task Delete(Guid keyId, Guid userId, CancellationToken cancellationToken = default)
        {
            if (_rows.TryGetValue(keyId, out var row) && row.UserId == userId) _rows.Remove(keyId);
            return Task.CompletedTask;
        }

        public Task DeleteAllForUser(Guid userId, CancellationToken cancellationToken = default)
        {
            foreach (var key in _rows.Where(r => r.Value.UserId == userId).Select(r => r.Key).ToArray())
                _rows.Remove(key);
            return Task.CompletedTask;
        }

        public Task DeleteAll(CancellationToken cancellationToken = default)
        {
            _rows.Clear();
            return Task.CompletedTask;
        }
    }
}
