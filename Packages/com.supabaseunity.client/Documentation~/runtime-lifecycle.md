# Runtime lifecycle

Construct and use the default Unity runtime client on the main thread. Give each client an owner, such as a persistent game subsystem or a scene component, and dispose it when that owner is destroyed. Create a new client for each Play session. If a client must survive a scene change, keep its owner alive across scenes.

The client owns its default HTTP transport, Realtime socket, and default Auth callback provider. If you supply a transport or callback provider, manage its lifetime. Client disposal cancels default HTTP requests, the shared Auth refresh, and any pending Realtime connection. Unity polls HTTP requests on the main thread, so an active request observes disposal on its next poll. Runtime host shutdown cancels pending requests and timers.

Callers own their cancellation tokens. A canceled operation throws `OperationCanceledException`. Canceling a write can stop the client from receiving a result even after the server applies the write. Read the affected record or object before retrying. For writes that must be safe to repeat, design a server-side idempotency key or use a stable unique constraint and an appropriate upsert.

Retry decisions for HTTP writes belong to the caller. Inspect `SupabaseResult.Error` and its status before deciding whether to retry a failed request. For rate limits, respect the service's retry guidance. Realtime has its own connection and channel recovery. Calling `DisconnectAsync` stops an automatic reconnect waiting in backoff. Dispose the client to end its lifetime.

With domain reload disabled, clear your own static client references and event handlers at Play entry or dispose them at Play exit. The SDK clears the runtime host's own callbacks and pending work across Play sessions. Its callback and focus paths depend on the Unity player or browser lifecycle. See [platform notes](platforms.md) for the checks run on each target.

Auth shares an in-flight refresh among callers. Canceling one caller's wait leaves the shared refresh available to others. Disposing Auth cancels the shared refresh. A custom store should honor cancellation before I/O, serialize access to shared keys, and avoid exposing session or PKCE data in diagnostics. See [session storage](session-storage.md) for persistence behavior and [security](security.md) for handling credentials and logs.
