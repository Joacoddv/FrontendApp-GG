# Verify Report: Blazor Auth Foundation (Phase 7, Slice A)

## Verdict: PASS WITH WARNINGS

**Build**: 0 errors, 238 warnings (all pre-existing CS8618/CS8602/CS0169 — none introduced by this change).  
**Branch**: `feat/blazor-auth-foundation`  
**Commits**: 1fd1b86, e39c065, 001f290  
**Tasks**: 15/15 complete (BAF-T01 through BAF-T15)  
**PR readiness**: READY

---

## Role-Claim Trace Result (BAF-06 — The Critical Security Path)

**Concrete trace: JWT with role claim "Cocinero" → `<AuthorizeView Roles="Cocinero">`**

1. Backend emits JWT with claim key `"role"` or the full URI key.
2. `JwtPayloadParser.ParseClaims` (line 31): `kvp.Key is "role" or RoleUri` → emits `new Claim("http://schemas.microsoft.com/ws/2008/06/identity/claims/role", "Cocinero")`.
3. `CustomAuthenticationStateProvider.GetAuthenticationStateAsync` (lines 56-60): `new ClaimsIdentity(claims, "jwt", nameType: ClaimTypes.Email, roleType: RoleUri)`.
4. `ClaimsIdentity.IsInRole("Cocinero")` finds the claim via the pinned URI.
5. `AuthorizeView Roles="Cocinero"` → **resolves TRUE**. No silent failure.

Both short `"role"` key and full URI key are handled. The trap (ADR-3) is correctly avoided.

---

## CRITICAL Issues

**None.**

---

## WARNING Issues

### W-01 — `NotifyUserAuthentication` signature diverges from locked design

- **File**: `GastroGestionBlazor/CustomAuthenticationStateProvider.cs:68`
- **Locked signature**: `NotifyUserAuthentication(string token)` (single param)
- **Actual signature**: `NotifyUserAuthentication(string token, DateTime expiresAtUtc)` (two params)
- **Impact**: The deviation is a behavioral improvement — without `expiresAtUtc` the `gg_token_expiry` storage key would never be written, making the fast-path expiry check (lines 35-39) unreachable. The code is correct. The design contract is stale.
- **Action**: Update the design doc to match the implemented signature.

### W-02 — `ClienteService` and `IngredienteService` have hardcoded `localhost:5001` URLs

- **Files**: `GastroGestionBlazor/Services/ClienteService.cs` (lines 22, 42, 67, 85, 103) and `GastroGestionBlazor/Services/IngredienteService.cs` (lines 22, 42, 67, 85, 103)
- **Spec ref**: BAF-01 states hardcoded `localhost:5001` MUST NOT exist in any service.
- **Context**: Explicitly deferred to Slice B by both design and tasks artifacts. The `HttpClient` injected IS the authenticated `AuthorizedApi` client (bearer token IS attached), but all calls use absolute URLs that override the `BaseAddress`. Functionally broken for production but within scope deferral.
- **Action**: Must be resolved in Slice B before any route beyond auth goes live.

---

## SUGGESTION Issues

### S-01 — `BearerTokenHandler` 401 path skips `NotifyAuthenticationStateChanged`

- **File**: `GastroGestionBlazor/Services/Auth/BearerTokenHandler.cs:46-49`
- On 401, the handler removes storage keys directly without calling `NotifyUserLogout()`. Auth state cascade updates only on next `GetAuthenticationStateAsync` call (triggered by navigation). Invisible to users in practice; correct DI-cycle-avoidance tradeoff.

### S-02 — `Login.razor` has no redirect guard for already-authenticated users

- **File**: `GastroGestionBlazor/Pages/Login.razor`
- Authenticated users navigating to `/login` see the form again. Not a security issue; UX-only.

### S-03 — `@using GastroGestionBlazor.Options` missing from `_Imports.razor`

- No practical impact — `ApiOptions` is only used in `Program.cs` (C#, not Razor).

---

## Spec Compliance Matrix

| Req | Status | Evidence |
|-----|--------|----------|
| BAF-01 Config-driven URL | PASS | `Program.cs:17` — Get\<ApiOptions\>() with null throw; `appsettings.json` — only `ApiBaseUrl` |
| BAF-01 Missing key fails startup | PASS | `Program.cs:17` — `?? throw new InvalidOperationException(...)` |
| BAF-02 Login uses AuthApi (no bearer) | PASS | `AuthService.cs:31` — `CreateClient("AuthApi")`; `Program.cs:32-35` — no handler attached |
| BAF-03 Token stored on 200 | PASS | `AuthService.cs:45` → `NotifyUserAuthentication` → SetItemAsStringAsync |
| BAF-03 No token stored on failure | PASS | `AuthService.cs:37` — non-success returns false before storing |
| BAF-04 Spanish error on 401 | PASS | `Login.razor:78` — "Credenciales inválidas. Verificá tu email y contraseña." |
| BAF-05 Bearer on authenticated calls | PASS | `BearerTokenHandler.cs:33-35` — sets Authorization header |
| BAF-05 No header when no token | PASS | `BearerTokenHandler.cs:33` — conditional on non-empty token |
| BAF-06 roleType pinned to full URI | PASS | `CustomAuthenticationStateProvider.cs:60` — `roleType: RoleUri` |
| BAF-06 Role-claim URI mapped | PASS | `JwtPayloadParser.cs:31` — maps `"role"` or full URI → RoleUri |
| BAF-06 Expiry: stored expiry check | PASS | `CustomAuthenticationStateProvider.cs:35-40` |
| BAF-06 Expiry: JWT exp check | PASS | `CustomAuthenticationStateProvider.cs:43-47` |
| BAF-06 Anonymous when no token | PASS | `CustomAuthenticationStateProvider.cs:31` |
| BAF-07 AuthorizeRouteView + CascadingAuthState | PASS | `App.razor:1,4` |
| BAF-07 Unauthenticated → /login | PASS | `App.razor:6` + `RedirectToLogin.razor:5` |
| BAF-08 Kitchen nav gated Cocinero/Administrador | PASS | `NavMenu.razor:29` — `<AuthorizeView Roles="Cocinero,Administrador">` |
| BAF-09 Logout clears storage | PASS | `AuthService.cs:52-54` → `NotifyUserLogout()` → `ClearStorageAsync()` |
| BAF-09 Logout → /login | PASS | `NavMenu.razor:43` + `LoginDisplay.razor:28` |
| BAF-10 401 on API call → clear + redirect | PASS | `BearerTokenHandler.cs:39-49` |
| BAF-10 Login 401 no session clear | PASS | `BearerTokenHandler.cs:44` — path check skips `/auth/login` |
| BAF-11 OIDC stub removed | PASS | `appsettings.json` — only `{ "ApiBaseUrl": "..." }` |
| BAF-11 AuthenticationService.js removed | PASS | Not present in `index.html` |
| BAF-11 Authentication.razor deleted | PASS | File not found by glob |
| BAF-11 No dangling OIDC refs | PASS | Grep for NavigateToLogin/RemoteAuthenticatorView/WebAssembly.Authentication = 0 matches |

---

## DI Registration Order

Matches design doc order exactly. No cycle: `BearerTokenHandler` injects only `ILocalStorageService` + `NavigationManager` (verified, `BearerTokenHandler.cs:18-20`).

---

## Git State

Working tree clean. Only `?? .atl/` and `?? openspec/` untracked (both expected and correct).

---

## Build Evidence

```
0 Errors
238 Warnings (all pre-existing CS8618/CS8602/NU1903/CS0169 — zero new warnings from this change)
Tiempo transcurrido 00:00:02.35
```
