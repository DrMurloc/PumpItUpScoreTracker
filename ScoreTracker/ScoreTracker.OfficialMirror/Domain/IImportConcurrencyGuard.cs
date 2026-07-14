namespace ScoreTracker.OfficialMirror.Domain;

// Stops one user from kicking off overlapping imports — button spam, refresh-and-retry, or a
// second browser tab. In-memory by design: the import bus is in-memory too, so the process
// restart that drops any in-flight import also clears the guard.
internal interface IImportConcurrencyGuard
{
    // True if this user had no import in flight (and now holds the slot); false if one is running.
    bool TryBegin(Guid userId);

    // Releases the slot. Safe to call for a user that never held one.
    void End(Guid userId);
}
