# blazor-auth Specification

## Purpose

Client-side authentication foundation for the Blazor WASM frontend. Covers config-driven API base URL, login/logout, JWT storage, authenticated HTTP requests, auth-state exposure (with role-claim URI resolution), route protection, and removal of the legacy OIDC stub.

## Requirements

---

### Requirement: BAF-01 — Config-Driven API Base URL

The system MUST read `ApiBaseUrl` from `wwwroot/appsettings.json` at startup and use it for all HTTP requests. Hardcoded `localhost:5001` references MUST NOT exist in any service or handler.

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
