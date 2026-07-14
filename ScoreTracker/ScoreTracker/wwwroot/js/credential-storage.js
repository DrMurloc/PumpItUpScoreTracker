// Raw localStorage for the remembered-import credential blob. The value stored here is
// AES-GCM ciphertext that is useless without the server-side key, so it is kept in plain
// localStorage (not ProtectedLocalStorage, whose data-protection ring rotates and is shared
// with auth cookies). Every call is wrapped so a storage exception never breaks a render.
window.credentialStorage = {
    get: function (key) {
        try {
            return localStorage.getItem(key);
        } catch {
            return null;
        }
    },
    set: function (key, value) {
        try {
            localStorage.setItem(key, value);
        } catch {
            /* storage full or blocked — the credential just won't be remembered */
        }
    },
    remove: function (key) {
        try {
            localStorage.removeItem(key);
        } catch {
            /* nothing to do */
        }
    }
};
