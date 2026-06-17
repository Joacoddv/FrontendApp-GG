# Tasks: Blazor Auth Foundation (Phase 7, Slice A)

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 550–700 (15 files; 6 new, 8 modified, 1 deleted) |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1: foundation + core services (BAF-T01–BAF-T08) → PR 2: UI, wiring, cleanup (BAF-T09–BAF-T15) |
| Delivery strategy | ask-on-risk |
| Chain strategy | stacked-to-main |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Packages + config + auth core services (no UI) | PR 1 | Buildable in isolation; no UI changes |
| 2 | UI, App.razor wiring, nav, 401 handler, OIDC cleanup, smoke test | PR 2 | Targets main; depends on PR 1 |

---

## Phase 1: Foundation & Packages

- [ ] **BAF-T01** — `GastroGestionBlazor.csproj`: add `Blazored.LocalStorage` 4.x package reference; remove `Microsoft.AspNetCore.Components.WebAssembly.Authentication` reference if no other code uses it. Satisfies: ADR-1, BAF-11.

- [ ] **BAF-T02** — `wwwroot/appsettings.json`: replace the `Local` OIDC stub block with `{ "ApiBaseUrl": "https://localhost:7126" }`. Satisfies: BAF-01, BAF-11.

- [ ] **BAF-T03** — Create `GastroGestionBlazor/Options/ApiOptions.cs`: `public sealed record ApiOptions { public string ApiBaseUrl { get; init; } = ""; }`. Satisfies: BAF-01.

- [ ] **BAF-T04** — Create `GastroGestionBlazor/Services/Auth/LoginRequest.cs` and `LoginResponse.cs` with locked signatures (`LoginRequest(string Email, string Password)`, `LoginResponse(string AccessToken, DateTime ExpiresAtUtc, Guid UsuarioId, string Rol)`). Satisfies: BAF-02, BAF-03.

- [ ] **BAF-T05** — Create `GastroGestionBlazor/Services/Auth/JwtPayloadParser.cs`: static class, manual base64url decode, `ParseClaims(string jwt)` emitting each claim under `ClaimTypes.Role` URI, `GetExpiryUtc(string jwt)`. Satisfies: BAF-06 (ADR-2, ADR-3).

- [ ] **BAF-T06** — Modify `GastroGestionBlazor/CustomAuthenticationStateProvider.cs` (currently orphaned stub): full rewrite — ctor takes `ILocalStorageService`; `GetAuthenticationStateAsync` reads `gg_token` + `gg_token_expiry`, validates expiry, calls `JwtPayloadParser.ParseClaims`, builds `ClaimsIdentity` with `roleType: ClaimTypes.Role` URI; `NotifyUserAuthentication(string token)`; `NotifyUserLogout()`; expired token path returns anonymous principal and clears storage. Satisfies: BAF-06, BAF-09 (ADR-3, ADR-5).

- [ ] **BAF-T07** — Create `GastroGestionBlazor/Services/Auth/IAuthService.cs` (locked interface) and `GastroGestionBlazor/Services/Auth/AuthService.cs`: `LoginAsync` POSTs to `"AuthApi"` client, stores `gg_token`+`gg_token_expiry` on 200, calls `NotifyUserAuthentication`, returns true; `LogoutAsync` removes keys, calls `NotifyUserLogout`; `GetTokenAsync` returns null if absent/expired; `IsAuthenticatedAsync`. Satisfies: BAF-02, BAF-03, BAF-04, BAF-09.

- [ ] **BAF-T08** — Create `GastroGestionBlazor/Services/Auth/BearerTokenHandler.cs`: `DelegatingHandler` ctor taking `ILocalStorageService`; `SendAsync` reads `gg_token`, attaches `Authorization: Bearer` only when token is non-null/non-empty; forwards request; if response is 401 clears `gg_token`/`gg_token_expiry`, calls `NotifyUserLogout` on provider, and triggers redirect to `/login` (inject `NavigationManager`). Satisfies: BAF-05, BAF-10 (ADR-4; BAF-10 redirect-loop guard: checks `request.RequestUri` path != `/auth/login` before clearing session). Satisfies: BAF-10.

---

## Phase 2: DI Wiring

- [ ] **BAF-T09** — Modify `GastroGestionBlazor/Program.cs`: implement the full DI wiring in exact order from design — (1) `ApiOptions` bind + `AddSingleton`, (2) `AddBlazoredLocalStorage` + `AddAuthorizationCore` + `CustomAuthenticationStateProvider` scoped + `AuthenticationStateProvider` factory + `IAuthService`/`AuthService`, (3) `AddTransient<BearerTokenHandler>`, (4a) `AddHttpClient("AuthApi")`, (4b) `AddHttpClient("AuthorizedApi").AddHttpMessageHandler<BearerTokenHandler>()`, (5) `AddScoped` factory for default `HttpClient` resolved from `"AuthorizedApi"`, existing services; remove old single-client registration. Satisfies: BAF-01, BAF-02, BAF-05 (ADR-4; order is critical — handler transient before named client).

- [ ] **BAF-T10** — Modify `GastroGestionBlazor/_Imports.razor`: add `@using Microsoft.AspNetCore.Components.Authorization` and `@using Blazored.LocalStorage`. Satisfies: BAF-06, BAF-07.

---

## Phase 3: UI & Routing

- [ ] **BAF-T11** — Create `GastroGestionBlazor/Pages/Login.razor`: Spanish form (email + password fields, submit button), bound to `IAuthService.LoginAsync`, inline error display on failure (e.g., "Credenciales inválidas. Verificá tu email y contraseña."), `NavigateTo("/")` on success, `@page "/login"`, no `[Authorize]` attribute. Satisfies: BAF-02, BAF-03, BAF-04.

- [ ] **BAF-T12** — Modify `GastroGestionBlazor/App.razor`: wrap `Router` in `<CascadingAuthenticationState>`; replace `<RouteView>` with `<AuthorizeRouteView>`; set `<NotAuthorized>` to render `<RedirectToLogin />`; add `<Authorizing>` template (simple spinner or blank). Satisfies: BAF-07.

- [ ] **BAF-T13** — Modify `GastroGestionBlazor/Layout/RedirectToLogin.razor`: replace `NavigateToLogin` call with `NavigationManager.NavigateTo("/login", forceLoad: false)`; modify `GastroGestionBlazor/Layout/LoginDisplay.razor` to show authenticated user info and a logout button calling `IAuthService.LogoutAsync()`; modify `GastroGestionBlazor/Layout/NavMenu.razor` to wire the logout button to `IAuthService.LogoutAsync()` and gate the kitchen-board nav entry inside `<AuthorizeView Roles="Cocinero,Administrador">`. Satisfies: BAF-08, BAF-09.

---

## Phase 4: Cleanup

- [ ] **BAF-T14** — Delete `GastroGestionBlazor/Pages/Authentication.razor` (RemoteAuthenticatorView OIDC page); remove `AuthenticationService.js` `<script>` tag from `GastroGestionBlazor/wwwroot/index.html`. Satisfies: BAF-11 (ADR-6).

---

## Phase 5: Verification

- [ ] **BAF-T15** — Build verification + manual smoke-test:
  - Run `dotnet build GastroGestionBlazor.sln` — must exit 0, zero errors.
  - Login valid credentials → redirected to `/`, `gg_token` present in DevTools → Application → LocalStorage.
  - Login invalid credentials → Spanish error inline, no token stored, stays on `/login`.
  - Navigate to protected route while unauthenticated → redirected to `/login`.
  - Login as `Cocinero` → kitchen-board nav entry visible; login as `Cajero` → entry hidden.
  - Login as `Administrador` → kitchen-board nav entry visible.
  - Inspect any authenticated API call in Network tab → `Authorization: Bearer` header present.
  - Inspect `POST /auth/login` in Network tab → NO `Authorization` header.
  - Logout → token removed from localStorage, redirected to `/login`.
  - Manually set `gg_token_expiry` to a past date in DevTools → refresh → treated as logged out, redirect to `/login`.
  - Browser console → zero errors referencing `AuthenticationService.js` or OIDC config.
  Satisfies: BAF-01 through BAF-11.
