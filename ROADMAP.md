# Supabase for Unity roadmap

Updated November 26, 2026.

This roadmap tracks the work toward a stable 1.0.0 release of
`com.supabaseunity.client`. Each milestone includes implementation tasks,
verification, documentation, and acceptance criteria. Every public release is
built from a specific commit and ships with its verification results.

Completed milestones are summarized here. See
[CHANGELOG.md](Packages/com.supabaseunity.client/CHANGELOG.md) for the full
list of changes in each release.

## Milestone overview

| Milestone | Version | Status |
| --- | --- | --- |
| Runtime baseline and compatibility | 0.2.0 | Completed |
| Editor request lifecycle | 0.3.0 | Completed |
| Setup Assistant and Quickstart interface | 0.4.0 | Completed |
| Model Generator preview and safe writes | 0.5.0 | Completed |
| Platform verification and support boundaries | 0.6.0 | Completed |
| Supabase service contracts and integration | 0.7.0 | Completed |
| Runtime lifecycle and IL2CPP preservation | 0.8.0 | Completed |
| Adoption, upgrades, and interface review | 0.9.0 | Completed; published November 11 |
| Pre-freeze hardening | 0.10.0 | Planned |
| Release candidate validation | 1.0.0-rc.1 | Planned |
| Stable baseline and publication | 1.0.0 | Planned |

The November 26 review added the pre-freeze hardening milestone. Work that can
change code, metadata, or the public contract finishes there, so release
candidates accept only fixes.

## Milestone 1 — 0.2.0 Runtime baseline and compatibility

### Problem and solution

Applications need predictable Auth, Database, Storage, Functions, and Realtime
behavior across Unity environments. Establish a tested runtime baseline and a
snapshot of its public contract before expanding the Editor workflow.

### Tasks and delivered scope

1. Provide the runtime service APIs and explicit client ownership.
2. Validate configuration before starting network or browser operations.
3. Protect concurrent Auth operations against stale session adoption and writes.
4. Keep session persistence optional and document its storage exposure.
5. Protect the published API signatures, defaults, enum values, and interfaces.
6. Supply repeatable Quickstart SQL, installation instructions, and troubleshooting.
7. Establish deterministic archives, local tests, and release verification.

### Acceptance and evidence

The public API snapshot passes, invalid configuration fails predictably, and
the required release checks pass for the distributed artifacts. The changelog,
compatibility tests, and release records document the baseline.

## Milestone 2 — 0.3.0 Editor request lifecycle

### Problem and solution

Editor requests must work outside Play mode and release their resources during
reload, shutdown, and window closure. Give Editor clients an explicit lifetime
and discard connection results when their configuration becomes stale.

### Tasks and delivered scope

1. Add explicit HTTP operations through `SupabaseEditorClient`.
2. Keep Editor Auth and PKCE state in memory with manual refresh.
3. Cancel requests on disposal, assembly reload, Editor exit, and Play entry.
4. Prevent an older Setup connection result from replacing the current result.
5. Cancel model generation when its window closes or Play mode changes.
6. Capture generation options before requests and provide cancellation controls.
7. Document an owning-window example and Editor capability boundaries.

### Acceptance and evidence

Lifecycle tests confirm cancellation and cleanup. Documented Editor examples
compile, and connection status follows the selected configuration.

## Milestone 3 — 0.4.0 Setup Assistant and Quickstart interface

### Problem and solution

First-time users need to understand configuration, network verification, and
sample setup. Organize the native Editor interface around those steps and ship
a runnable scene with clear recovery instructions.

### Tasks and delivered scope

1. Group settings, connection testing, and Quickstart actions into clear steps.
2. Show the selected settings path and wrap status and error messages.
3. Place runtime options in a foldout and retain persistence warnings.
4. Distinguish local settings validation from successful network verification.
5. Discover imported sample scenes and SQL through Setup.
6. Respect Unity's modified-scene save prompt before opening the demo.
7. Supply the Quickstart scene and preserve scene-to-script references in exports.
8. Reset runtime subscriptions and callbacks with Domain Reload disabled.

### Acceptance and evidence

Users can configure the client, test the connection, find SQL, and open the
sample. Scene references survive export and import. Narrow-window, keyboard,
and Play mode checks cover the implemented workflow.

## Milestone 4 — 0.5.0 Model Generator preview and safe writes

### Problem and solution

Schema generation can overwrite custom code or produce unusable models.
Preview the proposed output, validate destinations and types, and preserve
existing asset identities when applying changes.

### Tasks and delivered scope

1. Preview new, changed, unchanged, and blocked files before applying output.
2. Show current and proposed source for changed files.
3. Map supported PostgREST scalar, object, JSON, and array schemas.
4. Reject unsupported schemas, conflicting C# names, and unsafe destinations.
5. Recheck destinations before writing and skip identical files.
6. Preserve `.meta` files and leave removed-table files for manual review.
7. Keep the elevated schema key out of reload state, files, and error bodies.
8. Clear custom-host consent when the project URL changes.
9. Open generation from the selected Setup configuration and review its layout.

### Acceptance and evidence

Generation tests cover schema mapping, cancellation, destination validation,
and file preservation. Users can inspect output before accepting changes.

## Milestone 5 — 0.6.0 Platform verification and support boundaries

### Problem and solution

A successful build establishes different coverage from executing a service
operation on a device. Record the environment and level of verification for
each platform so users can assess the tested scope.

### Tasks and delivered scope

1. Record Editor tests, clean imports, player builds, and Xcode exports separately.
2. Add Linux x64 to the release build record.
3. Check native WebSocket cancellation on Unity's Mono runtime.
4. Ask platform reports for version, backend, device, services, and sanitized logs.
5. Document browser, operating-system, and networking constraints.
6. Keep public platform claims consistent with the available evidence.

### Acceptance and evidence

The release record identifies each required target individually. Platform
documentation explains the remaining runtime and device coverage gaps.

## Milestone 6 — 0.7.0 Supabase service contracts and integration

### Problem and solution

Tests using local transports can miss differences in real Supabase responses,
query syntax, and access policies. Combine focused wire-contract tests with
bounded integration journeys against an isolated stack.

### Tasks and delivered scope

1. Verify Auth and Database request and response contracts.
2. Check full-text search configuration and PostgreSQL array filter encoding.
3. Verify Storage list defaults, uploads, downloads, and access isolation.
4. Exercise Functions requests and sanitized errors.
5. Exercise Postgres Changes, Broadcast, and Presence with multiple clients.
6. Verify two-user Database and Storage policy isolation.
7. Attempt fixture cleanup after failures and report cleanup problems clearly.
8. Document service coverage and provider-dependent authentication flows.

### Acceptance and evidence

Contract tests and the required integration journeys pass. Policy isolation is
demonstrated, fixtures are bounded, and compatibility claims match the evidence.

## Milestone 7 — 0.8.0 Runtime lifecycle and IL2CPP preservation

### Problem and solution

Pending requests, connections, timers, and reflection-based models need to
remain correct through shutdown and managed stripping. Tie asynchronous work
to its owner and preserve the properties used by serialization.

### Tasks and delivered scope

1. Cancel owned runtime HTTP requests on disposal and Play mode exit.
2. Cancel pending connections and preserve manual Realtime disconnection.
3. Release acknowledgement timers when pushes complete.
4. Check repeated Play sessions and runtime host destruction.
5. Preserve generated model properties used by Json.NET.
6. Verify generated and handwritten models in an IL2CPP player with stripping.
7. Explain client ownership, shutdown, and retry considerations for writes.
8. Document regeneration or preservation steps for older models.

### Acceptance and evidence

Lifecycle regressions pass and the IL2CPP runtime probe deserializes models.
Shutdown releases owned work and the migration instructions cover older output.

## Milestone 8 — 0.9.0 Adoption, upgrades, and interface review

### Problem and solution

A working SDK can still fail adoption through unclear setup, fragile upgrades,
missing dependencies, stale documentation, or confusing recovery states.
Rehearse the documented journeys in representative projects and review the
native Editor interfaces after the service and lifecycle changes.

### Tasks

1. Rehearse Git UPM, tarball, and unitypackage installations at the supported
   Unity boundaries, including dependency resolution and duplicate-install recovery.
2. Complete the imported Quickstart against a fresh isolated Supabase project,
   including settings, SQL, RLS, seeded queries, and Realtime updates.
3. Check missing-settings, missing-SQL, connection failure, and retry instructions.
4. Upgrade representative early and intervening releases. Verify saved settings,
   scene references, sample script GUIDs, and generated model compatibility.
5. Document sample refresh and switching distributions without duplicate assemblies.
6. Review sample disposal, cancellation, error handling, and Play mode behavior.
7. Review Setup, Model Generator, the settings inspector, and demo in both Editor
   themes at narrow, default, and wide sizes. Check keyboard focus, scrolling,
   progress, long diagnostics, cancellation, retries, and overwrite recovery.
8. Reconcile English and Turkish documentation, README, changelog, API guide,
   troubleshooting, package metadata, links, and platform claims.
9. Review public feedback and classify defects by user impact.
10. Run release, compatibility, reproducibility, and public-text checks.
11. Draft 1.x API, dependency, deprecation, platform, security, and support policies
    for review at the release candidate freeze.

### Acceptance and recorded result

The November 11 local review recorded 193 passing EditMode tests on each of
Unity 2021.3.45f2, 2022.3.62f2, and 6000.6.7f1. It covered the 0.2.0 Git upgrade,
tarball installation and sample import, and the 0.4.0 Assets upgrade. Saved
settings and tested references survived those upgrades. The imported Quickstart
loaded seeded rows and received a Postgres update with a publishable key.

The review recorded both Editor themes, multiple window sizes, keyboard and
scroll behavior, archive reproducibility, clean import, five build/export
targets, and Chromium startup. A separate high-DPI Windows configuration remains
untested. Local Realtime initialization required a retry before successful
delivery. Record these limits in the candidate acceptance matrix.

Release `v0.9.0` was published on November 11 from tag commit `c65585b` on
`main`. Its source tree is identical to the verified local candidate `21cfd50`. On November 26,
the downloaded archives matched `SHA256SUMS`, and the published verification
record matched [release-verification.json](.github/release-verification.json).

## Milestone 9 — 0.10.0 Pre-freeze hardening

### Problem and solution

The release candidate freeze should accept only fixes, but several open items
could still change code, package metadata, or the public contract. The release
record also covers less than the roadmap claims. Close these gaps in a normal
release so the candidate starts from a settled baseline.

### Tasks

1. **Canonical repository identity.** *Done.* Package metadata, both READMEs, Setup window
   links, issue templates, the release-notes default, and `validate_package.py`
   pointed to the original personal-account repository, which redirects to
   `simpletools-studio/unity-supabase`. That redirect would stop if someone
   recreated the old name, and Git UPM URLs would then resolve to that new
   repository. Every reference now uses the canonical repository. The validator rejects the old
   owner, and the upgrade guide explains the URL change for existing Git installations.
2. **Realtime first-attempt failure.** Find the root cause of the Realtime
   initialization retry recorded for 0.9.0 on a fresh local stack. Fix it, or
   show that it comes from the local stack and document it. Add a regression check.
   An unexplained failure blocks the freeze.
3. **Complete release record.** Extend `release-verification.json` with the
   source commit, the per-version test results for every supported Unity
   version, the version used for clean import and builds, and the IL2CPP probe
   result. The publishing workflow rejects a record whose commit differs from the tag.
4. **Player runtime checks.** EditMode tests and player builds do not exercise
   player networking. Run the five-service journey in a WebGL player, which uses
   the browser WebSocket and request path, and in an Android IL2CPP player on a
   device or emulator. Record backend, browser or OS, and device.
5. **Application lifecycle.** Check background and resume beyond the socket
   lifetime, offline and online transitions, and session expiry while
   backgrounded on desktop, WebGL, and Android. Auth reacts to focus changes;
   Realtime recovers from socket close. Confirm both, and add regression tests
   for any defect found.
6. **Soak test.** Run Auth, Database, and Realtime for at least two hours
   against a local stack with a short JWT expiry so the session refreshes many
   times. Record refreshes, reconnects, missed or duplicate messages, and managed
   memory at the start and end.
7. **Public API review.** Review the full API snapshot for naming,
   nullability, cancellation support on asynchronous members, error kinds,
   defaults, and members that should be internal. Removing or renaming a member
   after 1.0.0 is a breaking change, so make those changes now and record each decision.
8. **Security review.** Review session storage, PKCE and redirect handling,
   elevated-key detection in settings and builds, token and response-body
   logging, WebGL exposure, and dependency notices. Fix findings before the freeze.
9. **Unity support floor.** Decide whether 1.x keeps 2021.3 as its minimum or
   raises it. Unity 2021.3 is outside Unity's standard LTS support window, and
   raising the minimum during 1.x would follow the minimum-version policy.
10. **Platform support levels.** Assign each platform a level, such as runtime
    verified, build verified, or unverified, and align `platforms.md`, the
    README, and package metadata with those levels.
11. **Blocker definition.** Define severity levels before the candidate starts.
    Blockers include data loss, security defects, crashes, incorrect session
    state, failed installation or upgrade, and a documented journey that fails on
    a runtime-verified platform.

### Acceptance and evidence

Each task is complete or explicitly removed from the 1.0.0 scope with a recorded
reason. No references to the old repository owner remain, the extended release
record is produced by the local gate, and no observed failure is unexplained.

## Milestone 10 — 1.0.0-rc.1 Release candidate validation

### Problem and solution

Checks performed on different revisions can hide integration regressions.
Freeze a concrete candidate and validate the combined package against an
explicit acceptance matrix. Use rc.2 and later candidates when fixes require it.

### Tasks

1. Freeze the runtime API snapshot, defaults, serialized settings, dependency
   requirements, supported environments, and deferred feature list.
2. Finalize the proposed 1.x compatibility and deprecation policies. State how
   security exceptions and minimum-version changes will be communicated.
3. Link every required acceptance check to its environment, procedure, result,
   candidate revision, archive hashes, and evidence location.
4. Run deterministic tests and compatibility checks on the chosen Unity versions.
5. Rehearse clean installations and representative upgrades against candidate artifacts.
6. Run the required five-service journey, two-user policy isolation, Realtime
   scenarios, lifecycle checks, IL2CPP model preservation, and the soak test.
7. Repeat the player runtime and application lifecycle checks for every
   runtime-verified platform. Record browser, backend, service versions, and
   test dates. Keep device and build claims precise.
8. Close the remaining interface coverage gaps required by the final support
   scope, including display scaling and recovery workflows.
9. Review vulnerability reporting, dependency notices, support expectations,
   release procedures, and recovery instructions.
10. Define evaluation journeys and the evaluation window before evaluation starts.
    The proposed window is at least 14 days after the last code change to the
    final candidate. Record outcomes and environments. Track external feedback
    when available.
11. Limit candidate changes to fixes and necessary documentation corrections.
    Rerun affected checks after fixes and the complete gate on the final candidate.
12. Resolve blockers or remove the affected capability from the supported scope.
    Document accepted nonblocking limitations and their impact.
13. Prepare candidate notes and have a maintainer approve any public prerelease.

### Acceptance and evidence

The final candidate completes its defined evaluation and all required checks.
Every supported claim has current evidence, every blocker has a resolution,
and the reviewed artifacts match the recorded hashes. Missing or inconclusive
required checks remain outstanding. Elapsed evaluation time alone is insufficient.

The 1.0.0 release bar:

- No open blockers as defined in the hardening milestone.
- Every required check passed on the final candidate commit, not an earlier candidate.
- The public API snapshot is unchanged since the final candidate.
- Every accepted limitation is documented with its user impact.

## Milestone 11 — 1.0.0 Stable baseline and publication

### Problem and solution

A stable release commits users to a maintained public contract. Finalize the
reviewed policies and verify that the final version and distributed artifacts
match the accepted candidate behavior.

### Tasks

1. Publish the reviewed semantic versioning, API, deprecation, platform,
   dependency, security, and support policies with the stable documentation.
2. Finalize migration instructions from 0.2.x and intervening releases, using
   the tested installation routes and preservation guidance.
3. Compare final source with the accepted candidate and explain every difference.
4. Rerun affected acceptance checks and the complete final gate after version changes.
5. Confirm there are no release blockers and document accepted limitations.
6. Review metadata, licenses, notices, sample discovery, screenshots, English and
   Turkish instructions, documentation links, and issue templates from a clean checkout.
7. Prepare the PR, tag, release notes, checksums, and verification record.
8. Document replacement-version recovery. Repair released defects through a new
   version while preserving existing published tags.
9. Have a maintainer approve the final stable artifacts and their evidence.
10. After publication, verify the public tag, workflow, release notes,
    downloaded archives, and checksums against the approved candidate.
11. Record the released version and any follow-up maintenance tasks.

### Acceptance and evidence

The stable contract and support boundaries are documented, the final required
checks pass, and the published artifacts match the verified hashes. The Asset Store
submission has its own requirements, listed below.

## Asset Store distribution checklist

This work starts once the stable release is ready.

1. Review Unity's submission requirements at submission time and resolve the
   external-service SDK licensing terms while preserving the intended open-source
   license. A license statement in listing details alone does not establish approval.
2. Confirm publisher identity, package name, dependency instructions, notices,
   and supported Unity versions against the intended deliverable.
3. Export the reviewed Assets layout with stable metadata and a discoverable demo.
4. Check archive exclusions, folder naming, duplicate files, credentials, temporary
   artifacts, and publishing-tool dependencies.
5. Import the exact submission archive into a clean project and follow the complete
   setup journey, including installing Json.NET and locating the sample SQL.
6. Run the applicable Asset Store validator. Investigate warnings and document
   valid advisories such as correctly reset static state.
7. Check listing copy, screenshots, render-pipeline claims, external-service
   requirements, licensing disclosures, and support links against actual behavior.
8. Have a maintainer approve the final submission materials before the asset is
   uploaded or submitted.

## Principles

- Finish each milestone's required verification before its release.
- Tie readiness claims to a concrete revision and artifacts.
- Keep user-facing text specific, accurate, and consistent across documentation and UI.
- Preserve existing settings, asset references, and supported caller behavior during upgrades.
- Record limitations and unresolved checks so they remain visible at the candidate freeze.
