# Proposal: Blazor Auth Foundation (Phase 7, Slice A)

## Intent

The Blazor WASM frontend has **no working auth**: `CustomAuthenticationStateProvider` is orphaned, services hardcode the dead legacy API (`localhost:5001`), there is no JWT handling, no token storage, no `[Authorize]`, and `App.razor` uses a plain `RouteView`. Until login + an authenticated `HttpClient` exist, **every other Phase 7 slice is blocked** (existing-screen rewrites in Slice B, kitchen board in Slice C). This slice builds the auth foundation against the live `.NET 8` backend.

## Verified Backend Contract (confirmed in source)

- **Route**: `POST /auth/login` — `[AllowAnonymous]`, group has NO `RequireAuthorization`.
- **Request**: `{ Email: string, Password: string }`.
- **Response 200**: `{ AccessToken: string, ExpiresAtUtc: DateTime(UTC), UsuarioId: Guid, Rol: string }` — `Rol` is serialized as the enum NAME string.
- **Roles** (`RolUsuario`): `Administrador`, `Cajero`, `Mozo`, `Cocinero` (4, not 3).
- **Token**: HS256, **8h** expiry, **no refresh token**. JWT claims: `sub`=UsuarioId, `email`, and role under **`ClaimTypes.Role`** (URI `http://schemas.microsoft.com/ws/2008/06/identity/claims/role`).
- **Backend API base**: `https://localhost:7126` / `http://localhost:5265`.
- Failure: generic `AuthenticationFailedException` (indistinguishable bad-credential message).

## Scope

### In Scope
- Config-driven `ApiBaseUrl` (`wwwroot/appsettings.json`, read at startup) replacing hardcoded `localhost:5001`.
- JWT auth service: login (`POST /auth/login`), logout, token persistence in `localStorage`.
- Authenticated `HttpClient` via a `DelegatingHandler` attaching `Authorization: Bearer <token>` (skipped for the login call).
- Revive + register `CustomAuthenticationStateProvider`: parse JWT, expose `ClaimsPrincipal` with role; map `ClaimTypes.Role` URI to `RoleClaimType` so `IsInRole`/`AuthorizeView Roles` work.
- `Login.razor` page (Spanish UI).
- `App.razor`: `CascadingAuthenticationState` + `AuthorizeRouteView` with not-authorized + redirect-to-login paths.
- Wire the existing dead logout button.
- Remove fake OIDC config (appsettings) + `AuthenticationService.js` shim from `index.html`.

### Out of Scope (explicit non-goals → later slices)
- Rewriting Cliente/Ingrediente services & DTOs for new contracts → **Slice B**.
- Kitchen board + SignalR client → **Slice C**.
- Adding a test project (`strict_tdd: false`; Standard Mode, manual smoke-test only).

## Decisions (object if needed)
- **Token storage = `localStorage`** — pragmatic for WASM; single 8h token, no refresh, internal LOB app. Tradeoff: readable by XSS (accepted; mitigated by output encoding + no refresh token to steal).
- **401 handling** — on any `401` from the API, clear token + redirect to login (no refresh flow exists).
- **Post-login landing** — redirect to home/dashboard; kitchen-board nav entry role-gated to `Cocinero`/`Administrador` (board itself = Slice C).
- **No "remember me" / no refresh token** in this slice.

## Capabilities

### New Capabilities
- `blazor-auth`: client-side login, JWT persistence, authenticated HttpClient, auth-state provider, route protection.

### Modified Capabilities
- None.

## Approach
Add a typed config record bound from `wwwroot/appsettings.json` at `Program.cs` startup. Implement `IAuthService` (login/logout/getToken) over an unauthenticated `HttpClient`; store token + expiry in `localStorage` via JS interop. Register a named/typed authenticated `HttpClient` with a `BearerTokenHandler : DelegatingHandler` (no header on the login client). Rewrite `CustomAuthenticationStateProvider` to read the stored token, decode JWT claims, and emit a `ClaimsPrincipal` (configuring `RoleClaimType` to the `ClaimTypes.Role` URI). Wrap routing in `CascadingAuthenticationState` + `AuthorizeRouteView`. Add `Login.razor` (Spanish). Strip OIDC stub + JS shim.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `Program.cs` | Modified | Bind config, register auth service, authenticated HttpClient + handler, auth-state provider, authorization |
| `wwwroot/appsettings.json` | Modified | Add `ApiBaseUrl`; remove OIDC stub |
| `wwwroot/index.html` | Modified | Remove `AuthenticationService.js` shim |
| `Auth/*` (service, handler, state provider) | New/Modified | JWT auth service, BearerTokenHandler, revived state provider |
| `Pages/Login.razor` | New | Spanish login page |
| `App.razor` | Modified | CascadingAuthenticationState + AuthorizeRouteView |
| `Shared/*` (nav/logout) | Modified | Wire logout, role-gate kitchen entry |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Role claim URI not mapped → `AuthorizeView Roles` fails | Med | Explicitly set `RoleClaimType` to the `ClaimTypes.Role` URI when building the identity |
| No tests | High | Manual smoke-test checklist; defer test project |
| `localStorage` XSS | Low | Output encoding; no refresh token; internal app |
| Contract drift (other endpoints) | Med | Out of scope here; handled in Slice B |

## Rollback Plan
Single-slice change in an isolated frontend repo on a feature branch. Revert the branch/PR; the app returns to its prior (already-broken) auth state. No backend or data migration involved.

## Dependencies
- Backend running on `https://localhost:7126` with the verified `/auth/login` contract (confirmed in source).

## Success Criteria
- [ ] User can log in via `Login.razor`; valid credentials return a token, invalid show a Spanish error.
- [ ] Token attached as `Authorization: Bearer` on API calls (not on login).
- [ ] Protected routes gated; unauthenticated users redirect to login.
- [ ] Role claim resolves (`AuthorizeView Roles="Cocinero,Administrador"` works).
- [ ] Logout clears token + returns to login.
- [ ] OIDC stub + JS shim removed; build is green.
