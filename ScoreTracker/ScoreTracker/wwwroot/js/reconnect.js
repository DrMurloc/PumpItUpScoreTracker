// Boots Blazor by hand so the reconnect ladder can be shortened. blazor.web.js is loaded
// with autostart="false" directly above this file; nothing else may sit between them,
// because until start() runs the page has no circuit at all.
//
// The framework's default ladder is 30 attempts spaced 0ms (1-9), 5s (10-19) and 30s
// (20-29): six minutes of spinner before the reader is offered a button to press. A circuit
// is either back within a few seconds or it needs a person, and the six minutes bought
// nothing but a stuck-looking page.
//
// Twelve attempts: four immediate, for a websocket dropped by a sleeping laptop or a wifi
// handover, then eight at three seconds, which covers roughly the window an app restart
// needs. That reaches "Couldn't reconnect." in about 25 seconds. Both numbers are judgment,
// not measurement — the ladder cannot be tuned without knowing why circuits drop here, and
// a wrong-but-bounded guess beats an unbounded default.
//
// maxRetries also reaches the overlay: Blazor writes it into
// #components-reconnect-max-retries, which is where the card's "Attempt 5 of 12" gets its
// second number.
Blazor.start({
    circuit: {
        reconnectionOptions: {
            maxRetries: 12,
            retryIntervalMilliseconds: function (attempt) {
                return attempt < 4 ? 0 : 3000;
            }
        }
    }
});
