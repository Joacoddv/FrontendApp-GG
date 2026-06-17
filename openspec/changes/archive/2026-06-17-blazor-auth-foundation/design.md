# Design: Blazor Auth Foundation (Phase 7, Slice A)

## Technical Approach

Revive JWT auth in the Blazor WASM (`net8.0`) frontend against the live .NET 8 backend
(`POST /auth/login` → `{ AccessToken, ExpiresAtUtc, UsuarioId, Rol }`, HS256, 8h, no refresh).
Read `ApiBaseUrl` from `wwwroot/appsettings.json` at startup, bind to a typed options record.
Two HttpClients: an **unauthenticated** one for login, an **authenticated** named client wrapped
by `BearerTokenHandler : DelegatingHandler` that injects `Authorization: Bearer`. Token + expiry
persist in `localStorage` via `Blazored.LocalStorage`. Rewrite `CustomAuthenticationStateProvider`
to manually decode the JWT payload and build a `ClaimsIdentity` whose **RoleClaimType is pinned to
the full `ClaimTypes.Role` URI** so `AuthorizeView Roles` / `IsInRole` resolve for all 4 roles.
Wrap routing in `CascadingAuthenticationState` + `AuthorizeRouteView`. Add `Login.razor` (Spanish).
Strip the leftover fake OIDC stub + `AuthenticationService.js`.

## Architecture Decisions

| # | Decision | Choice | Alternatives rejected | Rationale |
|---|----------|--------|-----------------------|-----------|
| ADR-1 | Token storage backend | **`Blazored.LocalStorage`** | raw `IJSRuntime` localStorage calls; `sessionStorage` | Idiomatic Blazor WASM API, typed `GetItemAsync<T>`/`SetItemAsync`, no hand-rolled JS interop strings. One small, ubiquitous dependency. Raw IJSRuntime saves a package but reimplements the same wrapper with weaker typing. sessionStorage loses the session on tab close (worse UX for an 8h LOB token). |
| ADR-2 | JWT parsing | **Manual base64url payload decode** (split on `.`, pad, `Convert.FromBase64String`, `JsonSerializer` to `Dictionary<string,JsonElement>`) | `System.IdentityModel.Tokens.Jwt` (`JwtSecurityTokenHandler`) | Client only needs to READ claims for UI gating; the server already validated the signature. The Jwt package pulls a heavy dependency graph (Microsoft.IdentityModel.*) that bloats the WASM bundle for zero security value (a WASM client cannot trust its own validation anyway). |
| ADR-3 | Role-claim mapping (THE trap) | Build `ClaimsIdentity` with **`roleType: ClaimTypes.Role`** (the full URI `http://schemas.microsoft.com/ws/2008/06/identity/claims/role`) and emit each role claim under that same URI | Leave default `roleType` ("role"); post-map claim names | Backend emits the role under the full `ClaimTypes.Role` URI (PR #17 RoleClaimType pin lesson). If the identity's `RoleClaimType` does not match the claim key, `AuthorizeView Roles=` and `IsInRole` silently fail. Pin both to the SAME URI. |
| ADR-4 | Authenticated HttpClient | **Named client `"AuthorizedApi"`** + `BearerTokenHandler`; login uses a separate unauthenticated `"AuthApi"` client | Single typed client with skip-logic in the handler; `AddHttpClient<T>()` typed clients | A separate login client cleanly avoids attaching a (nonexistent) bearer on `/auth/login` without per-request branching. Named clients match the project's current "inject `HttpClient` into service" style with minimal churn. |
| ADR-5 | Expiry handling | On `GetAuthenticationStateAsync` and before each protected call, compare stored `ExpiresAtUtc` (or JWT `exp`) to `DateTime.UtcNow`; if past → clear token, return anonymous principal | Reactive 401-only handling | No refresh token, so a 401 path is still needed, but proactively treating an expired token as logged-out avoids firing doomed requests and gives a clean redirect. |
| ADR-6 | OIDC cleanup | Remove `appsettings.json` `Local` block, `index.html` `AuthenticationService.js` `<script>`, and `Pages/Authentication.razor` (`RemoteAuthenticatorView`) + `RedirectToLogin` `NavigateToLogin` | Keep stub dormant | The `Microsoft.AspNetCore.Components.WebAssembly.Authentication` package and its JS shim are unused dead template code; keeping them invites confusion. Package can stay referenced (harmless) but the wiring goes. |

## Data Flow

```
Login.razor ──(email,pwd)──> IAuthService.LoginAsync
        │                          │ POST /auth/login   [AuthApi: no bearer]
        │                          ▼
        │                    LoginResponse {AccessToken,ExpiresAtUtc,UsuarioId,Rol}
        │                          │ store token+expiry
        │                          ▼
        │                    Blazored.LocalStorage
        │                          │
        │                    AuthStateProvider.NotifyUserAuthentication(token)
        ▼                          │ decode JWT -> ClaimsIdentity(RoleClaimType=URI)
   NavigateTo("/")  <──────────────┘ NotifyAuthenticationStateChanged

Protected call: Service ──> HttpClient("AuthorizedApi") ──> BearerTokenHandler
        reads token from localStorage ──> adds Authorization: Bearer ──> backend
        401 OR expiry ──> clear token ──> NotifyUserLogout ──> RedirectToLogin
```

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `wwwroot/appsettings.json` | Modify | Replace `Local` OIDC stub with `{ "ApiBaseUrl": "https://localhost:7126" }` |
| `Options/ApiOptions.cs` | Create | `public sealed record ApiOptions { public string ApiBaseUrl { get; init; } = ""; }` |
| `Services/Auth/IAuthService.cs` | Create | Auth service contract (see Interfaces) |
| `Services/Auth/AuthService.cs` | Create | Impl: POST login via `"AuthApi"`, store token, notify provider, logout |
| `Services/Auth/LoginRequest.cs` / `LoginResponse.cs` | Create | DTOs matching backend contract |
| `Services/Auth/BearerTokenHandler.cs` | Create | `DelegatingHandler` reading token from localStorage, adds header |
| `CustomAuthenticationStateProvider.cs` | Modify | Manual JWT decode, pin `RoleClaimType` URI, expiry check, notify methods |
| `Services/Auth/JwtPayloadParser.cs` | Create | Static base64url payload → claims helper |
| `Pages/Login.razor` | Create | Spanish login form, error display, redirect on success |
| `Program.cs` | Modify | Full DI wiring (see below) |
| `App.razor` | Modify | `CascadingAuthenticationState` + `AuthorizeRouteView` + `RedirectToLogin` |
| `Layout/LoginDisplay.razor` | Modify | Show user/role + logout button (AuthorizeView) |
| `Layout/NavMenu.razor` | Modify | Wire logout button to `IAuthService.LogoutAsync` |
| `Layout/RedirectToLogin.razor` | Modify | `Navigation.NavigateTo("/login")` instead of `NavigateToLogin` |
| `Pages/Authentication.razor` | Delete | Remove `RemoteAuthenticatorView` OIDC page |
| `wwwroot/index.html` | Modify | Remove `AuthenticationService.js` `<script>` |
| `_Imports.razor` | Modify | Add `@using Microsoft.AspNetCore.Components.Authorization`, `@using Blazored.LocalStorage` |

## Interfaces / Contracts (LOCKED)

```csharp
// Options/ApiOptions.cs
public sealed record ApiOptions { public string ApiBaseUrl { get; init; } = ""; }

// Services/Auth/LoginRequest.cs
public sealed record LoginRequest(string Email, string Password);

// Services/Auth/LoginResponse.cs
public sealed record LoginResponse(
    string AccessToken, DateTime ExpiresAtUtc, Guid UsuarioId, string Rol);

// Services/Auth/IAuthService.cs
public interface IAuthService
{
    Task<bool> LoginAsync(string email, string password); // true on success
    Task LogoutAsync();
    Task<string?> GetTokenAsync();        // null if absent/expired
    Task<bool> IsAuthenticatedAsync();
}

// Services/Auth/BearerTokenHandler.cs
public sealed class BearerTokenHandler : DelegatingHandler
{
    // ctor(ILocalStorageService storage)
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct);
}

// CustomAuthenticationStateProvider.cs
// NOTE: Implemented signature deviates from locked design (see W-01 in verify-report):
// Actual: NotifyUserAuthentication(string token, DateTime expiresAtUtc) — improvement, not regression
public class CustomAuthenticationStateProvider : AuthenticationStateProvider
{
    public override Task<AuthenticationState> GetAuthenticationStateAsync();
    public void NotifyUserAuthentication(string token, DateTime expiresAtUtc); // actual impl
    public void NotifyUserLogout();
}

// JwtPayloadParser.cs
public static class JwtPayloadParser
{
    public static IEnumerable<Claim> ParseClaims(string jwt);   // role -> ClaimTypes.Role URI
    public static DateTime? GetExpiryUtc(string jwt);           // exp -> UTC
}
```

Storage keys (constants): `gg_token`, `gg_token_expiry`.

### Program.cs wiring (full picture, order matters)

```csharp
var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// 1. config: appsettings.json is auto-loaded by CreateDefault into Configuration
var apiOptions = builder.Configuration.Get<ApiOptions>() ?? new ApiOptions();
builder.Services.AddSingleton(apiOptions);

// 2. storage + auth services
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<CustomAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(
    sp => sp.GetRequiredService<CustomAuthenticationStateProvider>());
builder.Services.AddScoped<IAuthService, AuthService>();

// 3. handler must be registered as transient before the named client
builder.Services.AddTransient<BearerTokenHandler>();

// 4a. unauthenticated client for login
builder.Services.AddHttpClient("AuthApi",
    c => c.BaseAddress = new Uri(apiOptions.ApiBaseUrl));

// 4b. authenticated client with bearer handler
builder.Services.AddHttpClient("AuthorizedApi",
    c => c.BaseAddress = new Uri(apiOptions.ApiBaseUrl))
    .AddHttpMessageHandler<BearerTokenHandler>();

// 5. existing services consume the AuthorizedApi named client
builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("AuthorizedApi"));
builder.Services.AddScoped<ClienteService>();
builder.Services.AddScoped<IngredienteService>();
builder.Services.AddAutoMapper(typeof(AutoMapperProfiles));

await builder.Build().RunAsync();
```

Note: `AuthService` resolves `IHttpClientFactory.CreateClient("AuthApi")` internally; `BearerTokenHandler`
must NOT decorate `"AuthApi"`. Existing `ClienteService`/`IngredienteService` keep the same `HttpClient`
ctor but now receive the authenticated client (URL rewrites are Slice B — for this slice they still
point at dead URLs and are untouched).

### Role-claim mechanism (ADR-3 concrete)

```csharp
const string RoleUri = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";
var identity = new ClaimsIdentity(
    claims, authenticationType: "jwt",
    nameType: "email", roleType: RoleUri);
// each backend role claim is emitted with claim.Type == RoleUri
```

## Testing Strategy

`strict_tdd: false` → Standard Mode, no test project this slice. Manual smoke-test checklist:

| Check | Expected |
|-------|----------|
| Login valid creds | redirect to `/`, token in localStorage |
| Login invalid creds | Spanish error, no token stored |
| Protected route while anon | redirect to `/login` |
| `AuthorizeView Roles="Cocinero,Administrador"` | renders only for those roles |
| Authenticated request | `Authorization: Bearer` header present (DevTools) |
| Login request | NO bearer header |
| Logout | token cleared, redirect to `/login` |
| Expired/tampered token | treated as logged out |
| Build | `dotnet build` green |

## Migration / Rollout

No data migration. Isolated frontend feature branch; revert PR restores prior state.

## NuGet Packages

- **Add**: `Blazored.LocalStorage` (latest 4.x, net8.0 compatible).
- **Add**: `Microsoft.Extensions.Http 8.0.0` (required explicitly — not in WASM SDK transitive closure after removing WebAssembly.Authentication).
- **Keep**: `Microsoft.AspNetCore.Components.Authorization` (now actually wired).
- **Remove wiring (package may stay)**: `Microsoft.AspNetCore.Components.WebAssembly.Authentication` — JS shim + `RemoteAuthenticatorView` removed; reference is harmless if left but should be dropped if nothing else uses it.

## Open Questions (Resolved)

- Role claim key: backend uses short `"role"` key; `JwtPayloadParser` handles both short and full URI.
- Post-login landing: `/` (Home).
