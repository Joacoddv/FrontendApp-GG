# Auth — Frontend Capability Spec

**Capability**: blazor-auth  
**First delivered by**: blazor-auth-foundation (Phase 7, Slice A — PR #1, merge commit c05bd91)  
**Status**: Active  
**Last updated**: 2026-06-17

---

## Purpose

Client-side authentication foundation for the Blazor WASM frontend. Covers config-driven API base URL, login/logout, JWT storage, authenticated HTTP requests, auth-state exposure (with role-claim URI resolution), route protection, and removal of the legacy OIDC stub.

---

## Backend Contract (Locked)

- **Endpoint**: `POST /auth/login` — `[AllowAnonymous]`, no `RequireAuthorization` on the group.
- **Request**: `{ Email: string, Password: string }`
- **Response 200**: `{ AccessToken: string, ExpiresAtUtc: DateTime(UTC), UsuarioId: Guid, Rol: string }` — `Rol` serialized as enum name string.
- **Roles** (`RolUsuario`): `Administrador`, `Cajero`, `Mozo`, `Cocinero`.
- **Token**: HS256, 8-hour expiry, no refresh. JWT claim key for role: `http://schemas.microsoft.com/ws/2008/06/identity/claims/role` (full URI), and/or the short `"role"` key — both handled.
- **Failure**: generic `AuthenticationFailedException` (no distinguishable credential message).

---

## Requirements

---

### Requirement: BAF-01 — Config-Driven API Base URL

The system MUST read `ApiBaseUrl` from `wwwroot/appsettings.json` at startup and use it for all HTTP requests. Hardcoded `localhost:5001` references MUST NOT exist in any auth service or handler.

#### Scenario: Valid config loaded

- GIVEN `wwwroot/appsettings.json` contains `"ApiBaseUrl": "https://localhost:7126"`
- WHEN the application starts
- THEN all HttpClient instances resolve their base address from that config value

#### Scenario: Missing ApiBaseUrl key

- GIVEN `wwwroot/appsettings.json` does not contain `ApiBaseUrl`
- WHEN the application starts
- THEN startup fails with a clear configuration error (no silent fallback to localhost:5001)

---

### Requirement: BAF-02 — Login Submits Credentials to Backend

The system MUST send `POST /auth/login` with `{ Email, Password }` when the user submits the login form. The login HTTP call MUST NOT include an `Authorization` header.

#### Scenario: Successful login

- GIVEN the user is on the login page and enters valid credentials
- WHEN the user submits the form
- THEN the app sends `POST /auth/login` with `{ Email, Password }` and no `Authorization` header
- AND the response HTTP status is 200

#### Scenario: Login request has no Authorization header

- GIVEN a valid session token exists in localStorage from a prior login
- WHEN the user submits the login form
- THEN the outgoing `POST /auth/login` request does NOT carry an `Authorization: Bearer` header

---

### Requirement: BAF-03 — JWT Stored on Successful Login

On a 200 response from `POST /auth/login`, the system MUST store the `AccessToken` in `localStorage` and redirect the user to the home/dashboard page. No token MUST be stored on any non-200 response.

#### Scenario: Token stored and redirect on 200

- GIVEN the login request returns HTTP 200 with `{ AccessToken, ExpiresAtUtc, UsuarioId, Rol }`
- WHEN the response is processed
- THEN `AccessToken` is persisted in `localStorage`
- AND the user is redirected to the home/dashboard route

#### Scenario: No token stored on failure

- GIVEN the login request returns HTTP 401 (or any non-200)
- WHEN the response is processed
- THEN `localStorage` contains no token
- AND the user remains on the login page

---

### Requirement: BAF-04 — Spanish Error Shown on Invalid Credentials

The system MUST display a Spanish error message on the login page when credentials are rejected (401 or network/auth failure). The error MUST be visible inline; no token is stored; no redirect occurs.

#### Scenario: Invalid credentials show inline Spanish error

- GIVEN the user submits the login form with wrong credentials
- WHEN the backend returns HTTP 401
- THEN an inline Spanish error message is displayed on the login page (e.g., "Credenciales inválidas. Verificá tu email y contraseña.")
- AND `localStorage` contains no token
- AND the user is NOT redirected

---

### Requirement: BAF-05 — Authenticated Requests Carry Bearer Token

After login, the system MUST attach `Authorization: Bearer <token>` to every API request except the login call itself. A missing or empty token MUST result in no Authorization header being sent (not an empty-value header).

#### Scenario: Authenticated call carries Bearer header

- GIVEN a valid token is stored in `localStorage`
- WHEN the application makes any API call other than `POST /auth/login`
- THEN the request includes `Authorization: Bearer <token>`

#### Scenario: No token — no Authorization header

- GIVEN no token is stored in `localStorage`
- WHEN the application makes an API call
- THEN the request does NOT include an `Authorization` header

---

### Requirement: BAF-06 — Auth State Exposes ClaimsPrincipal with Role via Full URI

**MUST** — This is a critical correctness requirement.

`CustomAuthenticationStateProvider` MUST parse the stored JWT and return an authenticated `ClaimsPrincipal` with the `RoleClaimType` explicitly set to the full URI `http://schemas.microsoft.com/ws/2008/06/identity/claims/role`. Without this mapping, `AuthorizeView Roles="..."` and `IsInRole(...)` silently return false for all four roles (`Administrador`, `Cajero`, `Mozo`, `Cocinero`).

`JwtPayloadParser` MUST handle both the short `"role"` key and the full URI key, emitting all role claims under `RoleUri` regardless of which key the backend uses.

#### Scenario: Authenticated state after login

- GIVEN a valid JWT is stored in `localStorage`
- WHEN `AuthenticationStateProvider.GetAuthenticationStateAsync()` is called
- THEN it returns an authenticated `ClaimsPrincipal` with `Identity.IsAuthenticated == true`
- AND the principal contains the role claim from the JWT

#### Scenario: Role-claim URI mapped — AuthorizeView works

- GIVEN a user has logged in with role `Cocinero`
- WHEN `<AuthorizeView Roles="Cocinero">` is evaluated
- THEN the content IS rendered (role resolves correctly)

#### Scenario: Wrong-role-claim-mapping — AuthorizeView fails (regression guard)

- GIVEN `RoleClaimType` is NOT set to the full `ClaimTypes.Role` URI (e.g., left as default "role")
- WHEN `<AuthorizeView Roles="Cocinero">` is evaluated
- THEN the content is NOT rendered even though the user holds the Cocinero role
- AND this scenario MUST NOT pass in the final implementation (it documents the exact failure mode)

#### Scenario: Unauthenticated state when no token

- GIVEN no token is stored in `localStorage`
- WHEN `GetAuthenticationStateAsync()` is called
- THEN it returns an unauthenticated `ClaimsPrincipal` with `Identity.IsAuthenticated == false`

#### Scenario: Expired stored expiry triggers logout

- GIVEN `gg_token_expiry` in localStorage is a past UTC datetime
- WHEN `GetAuthenticationStateAsync()` is called
- THEN the token is cleared from localStorage
- AND an unauthenticated principal is returned

#### Scenario: Expired JWT exp triggers logout

- GIVEN the JWT `exp` claim is in the past
- WHEN `GetAuthenticationStateAsync()` is called
- THEN an unauthenticated principal is returned

---

### Requirement: BAF-07 — Protected Routes Require Authentication

The application MUST use `AuthorizeRouteView` (not plain `RouteView`) wrapped in `CascadingAuthenticationState`. An unauthenticated user navigating to any `[Authorize]`-decorated route MUST be redirected to the login page.

#### Scenario: Unauthenticated access to protected route

- GIVEN the user is not logged in
- WHEN the user navigates to a route decorated with `[Authorize]`
- THEN the user is redirected to `/login` (not shown a blank page or error)

#### Scenario: Authenticated access to protected route

- GIVEN the user is logged in with a valid token
- WHEN the user navigates to a route decorated with `[Authorize]`
- THEN the page renders normally

---

### Requirement: BAF-08 — Kitchen-Board Nav Entry Visible Only to Cocinero/Administrador

The navigation entry for the kitchen board MUST be visible only when the authenticated user holds the `Cocinero` or `Administrador` role. Users with `Cajero` or `Mozo` roles MUST NOT see the entry. The board page itself is implemented in Slice C.

#### Scenario: Cocinero sees kitchen entry

- GIVEN a user is logged in with role `Cocinero`
- WHEN the navigation is rendered
- THEN the kitchen-board nav entry IS visible

#### Scenario: Cajero does not see kitchen entry

- GIVEN a user is logged in with role `Cajero`
- WHEN the navigation is rendered
- THEN the kitchen-board nav entry is NOT visible

#### Scenario: Administrador sees kitchen entry

- GIVEN a user is logged in with role `Administrador`
- WHEN the navigation is rendered
- THEN the kitchen-board nav entry IS visible

---

### Requirement: BAF-09 — Logout Clears Token and Returns to Login

The system MUST, on logout, remove the stored token from `localStorage`, notify `CustomAuthenticationStateProvider` (returning an unauthenticated principal), and redirect the user to the login page.

#### Scenario: Successful logout

- GIVEN a user is logged in and triggers logout
- WHEN the logout action completes
- THEN `localStorage` contains no token
- AND `AuthenticationState` is unauthenticated
- AND the user is on the login page

---

### Requirement: BAF-10 — 401 from Any API Call Clears Session

When any API call (other than the login call) receives HTTP 401, the system MUST clear the stored token, update auth state to unauthenticated, and redirect to the login page. No refresh flow exists.

#### Scenario: 401 on authenticated API call triggers logout

- GIVEN a user is logged in
- WHEN any API call returns HTTP 401
- THEN the token is removed from `localStorage`
- AND the user is redirected to the login page

#### Scenario: Login 401 does not trigger session clear

- GIVEN no user session exists
- WHEN `POST /auth/login` returns HTTP 401
- THEN no session-clear redirect occurs; only the inline login error is shown

---

### Requirement: BAF-11 — Legacy OIDC Stub and JS Shim Removed

The fake OIDC configuration in `wwwroot/appsettings.json` MUST be removed. The `AuthenticationService.js` shim reference in `wwwroot/index.html` MUST be removed. After removal, the application build MUST succeed with no console errors related to missing auth scripts.

#### Scenario: Build succeeds without OIDC stub

- GIVEN the legacy OIDC keys are absent from `appsettings.json` and the JS shim `<script>` tag is removed from `index.html`
- WHEN the application is built and launched
- THEN the browser console shows no errors referencing `AuthenticationService.js` or OIDC configuration
- AND the app loads normally on the login page

---

## Storage Keys

| Key | Purpose |
|-----|---------|
| `gg_token` | JWT `AccessToken` string |
| `gg_token_expiry` | ISO-8601 UTC string of `ExpiresAtUtc` for fast expiry check without JWT decode |

---

## Key Design Decisions (ADRs)

| ADR | Decision | Rationale |
|-----|----------|-----------|
| ADR-1 | Token storage = `Blazored.LocalStorage` | Idiomatic typed wrapper; single 8h token, no refresh, internal LOB; XSS tradeoff accepted. |
| ADR-2 | JWT parsing = manual base64url decode | Avoids `System.IdentityModel.Tokens.Jwt` (heavy WASM bundle); server already validates. |
| ADR-3 | `RoleClaimType` pinned to full `ClaimTypes.Role` URI | Without this pin, `AuthorizeView Roles=` and `IsInRole()` silently return false for all roles. |
| ADR-4 | Two separate named clients: `"AuthApi"` (no handler) and `"AuthorizedApi"` (with `BearerTokenHandler`) | Eliminates per-request skip branching; clean separation of login vs authenticated calls. |
| ADR-5 | Expiry: stored `ExpiresAtUtc` checked first, JWT `exp` as fallback | Fast path avoids base64 decode on every state check. |
| ADR-6 | Remove `WebAssembly.Authentication` wiring | OIDC cleanup; JS shim, `Authentication.razor`, `NavigateToLogin` all removed. |

---

## Known Deferred Items (Slice B)

- `ClienteService.cs` and `IngredienteService.cs` still have hardcoded `localhost:5001` URLs. They receive the authenticated `HttpClient` (bearer is attached), but the absolute URLs override the base address. Must be fixed in Slice B before production use.
- `NotifyUserAuthentication` signature is `(string token, DateTime expiresAtUtc)` — two parameters — rather than the one-parameter design lock. This is an acceptable improvement (allows the expiry fast-path to be populated) but deviates from the original locked contract; document if Slice B needs to call this method.

---

## Non-Goals (Out of Scope for this Capability)

- Refresh tokens (no refresh flow in this backend)
- Remember-me / persistent sessions beyond 8h
- Test project / automated test coverage (strict_tdd: false)
- `Cliente`/`Ingrediente` URL rewrites (Slice B)
- Kitchen board page implementation (Slice C)
- Already-authenticated redirect guard on `/login` page (cosmetic UX, not a security issue)
