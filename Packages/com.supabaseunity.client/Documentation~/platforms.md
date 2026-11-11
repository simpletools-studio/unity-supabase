# Platform notes

## WebGL

- HTTP uses `UnityWebRequest`. Browser CORS rules apply; Edge Functions called from WebGL must return the appropriate CORS and preflight headers for the deployed origin.
- Realtime uses the browser `WebSocket` API through `SupabaseUnity.jslib`.
- When persistence is enabled, sessions use browser `localStorage`. Persistence is off by default.
- PKCE verifiers always use `localStorage`, independently of the persistence setting. See [session storage](session-storage.md).
- Auth callback query/fragment credentials are removed from the visible URL after processing.
- Browser security rules control popups, redirects, cookies and mixed content. Serve production builds over HTTPS/WSS.
- Threads are not required by the SDK.

## Desktop and mobile

- Realtime uses `System.Net.WebSockets.ClientWebSocket`.
- Persistence is off by default. If enabled without a custom store, `UnencryptedFileSessionStore` writes the refresh token as plain text under `Application.persistentDataPath/Supabase`. That directory is app-private on Android and iOS, but readable by any process running as the same user on Windows, macOS, and Linux. See [session storage](session-storage.md) for the exposure table and an `ISessionStore` skeleton.
- OAuth opens the system browser through `Application.OpenURL`; configure `Application.deepLinkActivated` support in the player and add that callback URI to Supabase Auth.
- The OS may terminate a backgrounded app during that browser hop. PKCE verifiers are therefore stored durably regardless of the persistence setting, so the deep link can still complete sign-in after a cold start.

## Verification levels

| Check | What it establishes |
| --- | --- |
| Editor tests | The listed Unity Editor ran the SDK test suite. Loopback tests use local fixtures. |
| Clean import | The released `.unitypackage` imported and compiled in a new Unity project. |
| Player build | Unity produced a smoke player for the listed target. |
| Xcode export | Unity generated an iOS project for Xcode. |
| Runtime check | A named behavior ran in a browser, player, or on a device. The result applies to that environment and behavior. |
| Live service check | A named operation ran against a real Supabase project. |

## Results for 0.8.0

| Target | Recorded check | Further coverage |
| --- | --- | --- |
| Unity Editor on Windows | EditMode tests on 2021.3.45f2, 2022.3.62f2, and 6000.6.7f1. Clean archive import on 6000.6.7f1. | Editor tests on Linux and macOS. |
| WebGL | Smoke player build and startup in Chromium 151.0.7922.34. | Browser service operations, authentication callbacks, and persistence. |
| Windows x64 | Smoke player build. An IL2CPP player on Unity 6000.6.7f1 deserialized generated and handwritten models with Medium managed stripping. | Long-running native network and hosted-project checks. |
| Linux x64 | Mono smoke player cross-built from the clean archive on Windows. | Linux Editor, desktop player, and live service coverage. |
| macOS | No recorded player check. | Player build and execution. |
| Android | Smoke APK build. | Device testing, including background and resume. |
| iOS | Xcode project export. | Xcode compilation, signing, and device testing. |

The release verification record contains the exact Editor version, test count, build results,
and archive hashes. GitHub Actions validates the artifacts and the record. Unity checks run
locally on an activated Editor. Console targets are outside the SDK's support scope.
