namespace ScoreTracker.Domain.SecondaryPorts
{
    /// <summary>
    ///     Wraps and unwraps a data-encryption key with a master key that never leaves its store
    ///     (Azure Key Vault in production; a configured local key elsewhere). Wrapped output is
    ///     opaque and unwrappable only by the same master key.
    /// </summary>
    public interface IKeyEnvelope
    {
        Task<byte[]> Wrap(byte[] dataKey, CancellationToken cancellationToken = default);
        Task<byte[]> Unwrap(byte[] wrappedDataKey, CancellationToken cancellationToken = default);
    }
}
