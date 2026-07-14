namespace ScoreTracker.Data.Configuration
{
    public sealed class KeyVaultConfiguration
    {
        // When both are set, wrap/unwrap run in Key Vault and the master key never leaves it.
        public string? VaultUri { get; set; }
        public string? KeyName { get; set; }

        // Local development / tests: a base64 32-byte AES key that wraps locally when no vault
        // is configured. Left unset in production.
        public string? LocalKey { get; set; }
    }
}
