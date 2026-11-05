# Service compatibility

## Scope of 0.7.0

The SDK implements a subset of Supabase's client interfaces. Matching another SDK's complete API is not a compatibility promise. The public runtime API remains unchanged from 0.2.0; this release corrects request encoding and expands verification.

Acceptance runs against an isolated local stack. These results establish the listed operations in that environment. Hosted projects, authentication providers, browser CORS, and device callbacks need their own acceptance checks.

| Area | Local integration scenarios | Additional boundaries |
| --- | --- | --- |
| Auth | Email/password fixture sign-in or signup, token refresh, user lookup | Email confirmation, OTP/SMS delivery, external ID tokens, interactive OAuth/SSO, identity linking, MFA, and CAPTCHA require provider/configuration-specific acceptance. Deterministic fixtures do not establish provider integration. |
| Database | Insert, typed single/maybe-single, exact count and inclusive range, configured full-text search, array containment, CSV, scalar RPC, minimal update, delete | Other filters, relationships, conflict policies, custom schemas, and service errors have differing fixture coverage. Validate application-specific combinations. |
| Storage | Upload, duplicate rejection, update, download bytes, info, list with partial options, single/batch signing, signed upload, copy, move, remove | Bucket administration depends on policies; elevated client keys are rejected. Image transforms depend on server availability. Signed URL construction is checked separately from downloading through the URL. |
| Functions | Typed JSON, raw request/response, HTTP method error | The fixture checks POST success and GET rejection. Verify application-specific MIME types, authorization, CORS, and timeouts. Responses are buffered; this API does not expose a streaming reader. |
| Realtime | Postgres insert delivery, two-client Broadcast acknowledgement/delivery, Presence join/leave | Fixtures cover binding isolation and reconnect/token recovery. Local delivery does not establish mobile resume, prolonged outage recovery, private-channel policies, or hosted capacity. |
| Authorization | A second user cannot see/update the first user's row or read/overwrite their private object | These policies are fixtures, not an audit of an application's schema. Hidden rows can produce a successful empty result. |

The stack used PostgreSQL 17.6.1.159, Auth v2.195.0, PostgREST v16.1, Storage v1.69.11, Realtime v2.129.0, and Edge Runtime v1.74.3. The runner pins CLI 2.115.0. These identify the environment, not minimum server-version guarantees.

## Requests and results

Requests carry the configured API key and, when signed in, the user token. New publishable keys are not sent as Bearer tokens. Cancellation throws `OperationCanceledException`; HTTP/service failures use `SupabaseResult`. Inspect status, service code, and metadata rather than matching message text. Shared fixtures include 401, 403, 429, 500, redaction, timeouts, and transport failures.

`Range(from, to)` is inclusive. `MaybeSingleAsync` requests a list and checks cardinality locally: zero rows succeeds with null for a reference model, one returns the model, and multiple rows fail with `multiple_rows`. `SingleAsync` uses the singular response media type instead.

For PostgreSQL array columns, pass a C# collection to `Contains`, `ContainedBy`, or `Overlaps`:

```csharp
var rows = await client.From<Item>().Contains("tags", new[] { "unity", "sdk" }).GetAsync();
```

For a JSONB array, pass `new Newtonsoft.Json.Linq.JArray("unity", "sdk")`. This release changes C# collection encoding for these three operators; applications previously using C# arrays against JSONB columns should switch to `JArray`.

Storage listing omits unset options so the server applies its defaults. Explicit sort/pagination values are sent as supplied. Uploads are buffered byte arrays, not resumable/TUS transfers. S3, vector/analytics buckets, and other APIs absent from this package are outside its current interface.

## Verification

See [live acceptance](live-acceptance.md) for setup, accounts, commands, cleanup, and readiness. Credential-free tests remain distinct from service checks. The release gate records compilation, import, build, and archive evidence; it does not imply every operation ran on every target. See [Editor scripting](editor-scripting.md) and [platform notes](platforms.md) for their narrower boundaries.

References reviewed on 2026-11-05:

- [Auth specification](https://github.com/supabase/auth/blob/v2.195.0/openapi.yaml)
- [PostgREST filters](https://docs.postgrest.org/en/stable/references/api/tables_views.html)
- [Pagination/count](https://docs.postgrest.org/en/stable/references/api/pagination_count.html)
- [RPC](https://docs.postgrest.org/en/stable/references/api/functions.html)
- [Storage listing](https://supabase.com/docs/reference/javascript/storage-from-list)
- [Functions](https://supabase.com/docs/reference/javascript/functions-invoke)
- [Realtime protocol](https://supabase.com/docs/guides/realtime/protocol)
