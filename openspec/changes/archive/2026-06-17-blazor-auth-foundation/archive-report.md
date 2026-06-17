# Archive Report: Blazor Auth Foundation (Phase 7, Slice A)

**Archived**: 2026-06-17  
**Change name**: blazor-auth-foundation  
**Archive path**: `openspec/changes/archive/2026-06-17-blazor-auth-foundation/`  
**Main spec created**: `openspec/specs/Auth/spec.md`

---

## Executive Summary

Change `blazor-auth-foundation` is fully planned, implemented, verified, and archived. PR #1 was merged to `main` as squash commit `c05bd91` with build green (0 errors, 238 pre-existing warnings) and verify verdict PASS WITH WARNINGS (0 CRITICAL). All 15 tasks are complete. This is Phase 7, Slice A of 3 for the GastroGestion Blazor frontend.

---

## Final State

| Field | Value |
|-------|-------|
| PR | #1 (feat/blazor-auth-foundation → main) |
| Merge commit | c05bd91 (squash) |
| Branch commits (pre-merge) | 1fd1b86, e39c065, 001f290 |
| Build | 0 errors, 238 warnings (all pre-existing) |
| Verify verdict | PASS WITH WARNINGS — 0 CRITICAL, 2 WARNINGs, 3 SUGGESTIONs |
| Tasks complete | 15/15 |

---

## Tasks Completed (BAF-T01 through BAF-T15)

| Task | Description | Satisfies |
|------|-------------|-----------|
| BAF-T01 | `csproj`: add Blazored.LocalStorage 4.5.0 + Microsoft.Extensions.Http 8.0.0; remove WebAssembly.Authentication | ADR-1, BAF-11 |
| BAF-T02 | `appsettings.json`: replace OIDC stub with `{ "ApiBaseUrl": "https://localhost:7126" }` | BAF-01, BAF-11 |
| BAF-T03 | `Options/ApiOptions.cs`: sealed record with `ApiBaseUrl` | BAF-01 |
| BAF-T04 | `Services/Auth/LoginRequest.cs` + `LoginResponse.cs` (locked signatures) | BAF-02, BAF-03 |
| BAF-T05 | `Services/Auth/JwtPayloadParser.cs`: manual base64url decode, role URI mapping | BAF-06 (ADR-2, ADR-3) |
| BAF-T06 | `CustomAuthenticationStateProvider.cs`: full rewrite, ClaimsIdentity roleType pinned | BAF-06, BAF-09 (ADR-3, ADR-5) |
| BAF-T07 | `Services/Auth/IAuthService.cs` + `AuthService.cs`: login, logout, token access | BAF-02..BAF-04, BAF-09 |
| BAF-T08 | `Services/Auth/BearerTokenHandler.cs`: DelegatingHandler, Bearer injection, 401 clear+redirect | BAF-05, BAF-10 |
| BAF-T09 | `Program.cs`: full DI wiring in exact design order | BAF-01, BAF-02, BAF-05 |
| BAF-T10 | `_Imports.razor`: added Authorization + Blazored.LocalStorage + Auth usings | BAF-06, BAF-07 |
| BAF-T11 | `Pages/Login.razor`: Spanish form, inline error, redirect on success | BAF-02, BAF-03, BAF-04 |
| BAF-T12 | `App.razor`: CascadingAuthenticationState + AuthorizeRouteView + RedirectToLogin | BAF-07 |
| BAF-T13 | `Layout/RedirectToLogin.razor`, `LoginDisplay.razor`, `NavMenu.razor`: logout wired, kitchen nav gated | BAF-08, BAF-09 |
| BAF-T14 | `Pages/Authentication.razor` deleted; `AuthenticationService.js` removed from `index.html` | BAF-11 |
| BAF-T15 | Build + manual smoke-test: 0 errors, all BAF scenarios verified | BAF-01..BAF-11 |

---

## Verify Verdict

**PASS WITH WARNINGS — 0 CRITICAL**

### Deferred Warnings (carry to Slice B)

**W-01 — `NotifyUserAuthentication` signature deviation**  
Actual: `NotifyUserAuthentication(string token, DateTime expiresAtUtc)` — two parameters.  
Design locked: single parameter. The deviation is a behavioral improvement (without `expiresAtUtc`, the fast-path expiry check `gg_token_expiry` would never be populated). The two-parameter form is the correct implementation. Design doc updated in archived copy.  
Action for Slice B: callers (currently only `AuthService`) already use the correct two-param signature; no change required in Slice B unless the interface contract is revisited.

**W-02 — `ClienteService` / `IngredienteService` hardcoded `localhost:5001` URLs**  
Both services still have absolute hardcoded URLs (lines 22, 42, 67, 85, 103 in each file). The `HttpClient` they receive IS the authenticated `AuthorizedApi` client (bearer token is attached), but the absolute URL overrides the `BaseAddress`. Explicitly deferred to Slice B by design and tasks.  
Action: MUST be resolved in Slice B before any route beyond auth goes to production.

---

## Stale-Checkbox Reconciliation

The local `openspec/changes/blazor-auth-foundation/tasks.md` file contained stale unchecked boxes (the apply agent wrote to engram but did not update the local file). The archived `tasks.md` has all boxes reconciled to checked, backed by:
- Engram apply-progress #120: "COMPLETE (15/15 tasks done)"
- Engram verify-report #122: "15/15 complete (BAF-T01 through BAF-T15)"
- PR #1 merged to main, squash commit c05bd91, build green

This reconciliation was performed at archive time per orchestrator instruction (stale-checkbox exceptional repair path from `sdd-archive` skill).

---

## Specs Synced

| Domain | File | Action | Details |
|--------|------|--------|---------|
| Auth | `openspec/specs/Auth/spec.md` | Created (first-ever spec in this repo) | 11 requirements (BAF-01 through BAF-11) with all scenarios, storage keys, ADRs, deferred items |

`openspec/specs/` was empty (first archived change). The delta spec became the full capability spec directly.

---

## Engram Observation IDs (Traceability)

| Artifact | Engram ID | Topic Key |
|----------|-----------|-----------|
| Proposal | #115 | `sdd/blazor-auth-foundation/proposal` |
| Spec | #116 | `sdd/blazor-auth-foundation/spec` |
| Design | #117 | `sdd/blazor-auth-foundation/design` |
| Tasks | #118 | `sdd/blazor-auth-foundation/tasks` |
| Apply Progress | #120 | `sdd/blazor-auth-foundation/apply-progress` |
| Verify Report | #122 | `sdd/blazor-auth-foundation/verify-report` |
| Archive Report | (this document) | `sdd/blazor-auth-foundation/archive-report` |

---

## Phase 7 Progress

| Slice | Scope | Status |
|-------|-------|--------|
| **Slice A** (this change) | Auth foundation: config, login, JWT, Bearer handler, auth-state, route protection, OIDC cleanup | **DONE — merged c05bd91** |
| **Slice B** | Existing-screen contract rewrites: ClienteService + IngredienteService URL rewrites, DTO alignment with new backend contracts | Pending |
| **Slice C** | Kitchen board + SignalR client | Pending |

---

## Archive Contents

- `proposal.md` — original proposal
- `spec.md` — delta spec (BAF-01..BAF-11)
- `design.md` — technical design with ADRs (updated to reflect actual `NotifyUserAuthentication` signature)
- `tasks.md` — 15 tasks, all reconciled to checked
- `verify-report.md` — PASS WITH WARNINGS, 0 CRITICAL
- `archive-report.md` — this file
