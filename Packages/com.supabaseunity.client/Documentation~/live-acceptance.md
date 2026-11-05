# Live acceptance testing

The normal EditMode suite is credential-free and remains part of the release gate. The PlayMode suite checks Auth, PostgREST, Realtime, Storage, and an Edge Function against an isolated stack. It is required for the 0.7.0 compatibility review and is separate from the credential-free release gate.

## Local stack

Install a Docker-compatible container runtime, close the Unity Editor, then run:

```powershell
pwsh .github/scripts/run_live_acceptance.ps1 -StartLocalStack
```

The script uses the pinned Supabase CLI through `npx`, reads the local URL and anon key from
`supabase status`, and passes them to Unity without printing credentials. The committed migration,
seed, Storage policies, and `unity-acceptance` function create an isolated test surface.

When the local migration changes and the local data is disposable, rebuild only the local database:

```powershell
pwsh .github/scripts/run_live_acceptance.ps1 -ResetLocalDatabase
```

`-ResetLocalDatabase` destroys the local development database. It never targets a linked project.

## Isolated hosted project

Use a throwaway development project, never production. Apply the files under `supabase/`, then set:

```powershell
$env:SUPABASE_TEST_URL = "https://your-test-project.supabase.co"
$env:SUPABASE_TEST_PUBLISHABLE_KEY = "your publishable or legacy anon key"
$env:SUPABASE_TEST_EMAIL = "unity-acceptance@example.test"
$env:SUPABASE_TEST_PASSWORD = "a test-only password"
pwsh .github/scripts/run_live_acceptance.ps1
```

Do not provide a secret or `service_role` key. The client rejects elevated credentials, and the
acceptance schema is deliberately protected by Row Level Security and user-scoped Storage policies.

## Scenarios and cleanup

The three scenarios cover a five-service journey with Database/Storage variants, two-user policy isolation, and two-client Broadcast/Presence. Apply every migration and the seed under `supabase/`, including the array-column/scalar-RPC fixtures. These fixtures belong in isolated acceptance projects; applications do not need them in production.

The second account uses `SUPABASE_TEST_SECOND_EMAIL` and `SUPABASE_TEST_SECOND_PASSWORD`, defaulting to `unity-acceptance-second@example.test` and the same local test password. Use distinct accounts. For hosted testing, pre-create and confirm both accounts when required. Fixture accounts remain for reuse. Rows/objects use unique identifiers; cleanup runs after failures and reports problems separately without hiding the scenario failure.

Scenarios have a 90-second cancellation budget, event waits have 20-second deadlines, and cleanup has a separate 20-second budget. A newly started local Realtime tenant can still be initializing its database connection. The harness permits at most three transient subscription attempts and logs readiness retries; permanent errors fail immediately. This is test-environment handling, not a change to SDK retry guarantees.

A successful result is three passed tests without skips or inconclusive cases. Missing credentials skip the suite and do not satisfy the compatibility gate. Earlier service success does not establish later stages after a failure.
