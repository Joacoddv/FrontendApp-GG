# Design: Blazor Slice C — Realtime Kitchen Board (Cocina)

Phase 7, Slice C. WASM net8.0 frontend. Scope LOCKED: **"A + marcar lista"** = read-only
realtime board (initial GET + live SignalR updates) **plus the single mutation `marcar-lista`**.
OUT of scope: asignar-cocinero, OT generation, any cocinero/legajo picker, backend changes,
automated tests (none in repo; manual smoke).

Repo is nested: project files live under `GastroGestionBlazor/GastroGestionBlazor/`.

---

## VERIFIED backend contract (read from source — authoritative)

- **Board GET**: `GET /ordenes-trabajo?estado={EstadoOT?}` → `200 OrdenTrabajoBoardResponse[]`.
  In-handler role gate Cocinero/Administrador (else **403** ProblemDetails). Bad `estado` → **400**.
  Source: `src/GastroGestion.Api/Endpoints/OrdenTrabajoEndpoints.cs` lines 90–127.
- **marcar-lista**: `POST /pedidos/{pedidoId}/ordenes-trabajo/{otId}/marcar-lista` — **NO request body**.
  Returns `200 OrdenTrabajoResponse` (single-OT shape, NOT the board shape — no `PedidoTipo`).
  Role gate Cocinero/Administrador. Source: same file, lines 67–88.
  - **CRITICAL**: route is **nested under `pedidoId`** → the client MUST have `PedidoId` to build the
    URL. The board item carries `PedidoId`, so this is available from the board row.
  - **Error codes** (via `GastroGestionExceptionHandler.cs`):
    - OT not in `Preparandose` → domain `DomainException` → **422 UnprocessableEntity** (`title: "Domain rule violation"`).
    - Pedido/OT not found → `NotFoundException` → **404** (`title: "Resource not found"`).
    - Wrong role → `ForbiddenException` → **403** (`title: "Forbidden"`).
    - **There is NO 409 path** for marcar-lista. (ConflictException maps to 409 but the mark-ready
      flow throws DomainException, not ConflictException.)
- **Hub**: `/hubs/kitchen` (`MapHub<KitchenHub>`), `[Authorize(Roles="Cocinero,Administrador")]`,
  joins group `"kitchen"` on connect. Source: `src/GastroGestion.Api/Hubs/KitchenHub.cs`.
- **Event**: server broadcasts **`"OtChanged"`** with payload **`OrdenTrabajoBoardResponse`**
  (notifier does `item.ToResponse()` → same shape as a board row). Source:
  `src/GastroGestion.Api/Realtime/SignalRKitchenNotifier.cs` lines 15–17.
  → **The board GET row and the `OtChanged` payload are the SAME record. One client type covers both.**
- **WS auth**: browsers cannot set an `Authorization` header on the WS upgrade; backend reads
  `?access_token=` for `/hubs/kitchen`. Client supplies the JWT via `AccessTokenProvider`.
- **Enums serialize as STRINGS globally** (`JsonStringEnumConverter`). `EstadoOT`: `Creada(0)`,
  `Preparandose(1)`, `Lista(2)`, `Cancelada(3)`. `TipoPedido`: `Salon(0)`, `TakeAway(1)`, `Delivery(2)`.
- **API base**: `https://localhost:7126` (from `wwwroot/appsettings.json`, exposed as `ApiOptions.ApiBaseUrl`).

### Backend payload shape (verified, `OrdenTrabajoResponses.cs`)
```
OrdenTrabajoBoardResponse(Guid OtId, Guid PedidoId, TipoPedido PedidoTipo, Guid PlatoId,
                          Guid LineaPedidoId, EstadoOT Estado, Guid? CocineroAsignadoLegajoId)
```

---

## Verified frontend facts (read from source)

- `Program.cs`: factory registers `AuthorizedApi` (BaseAddress = ApiBaseUrl, BearerTokenHandler) as the
  **default `HttpClient`** for scoped services (lines 47–51). `ApiOptions` is a registered singleton.
- `IAuthService.GetTokenAsync(): Task<string?>` exists (Slice A). The SignalR `AccessTokenProvider`
  reuses this.
- Slice B `Contracts/` layout: `Contracts.Enums`, `Contracts.Clientes`, `Contracts.Ingredientes`,
  `Contracts.Common` (`ProblemDetailsResponse(string? Title, string? Detail, int? Status)`,
  `ApiException(string message)`). Services use a static
  `JsonOptions = new(JsonSerializerDefaults.Web){ Converters={ new JsonStringEnumConverter() } }`
  and **relative** URIs against `AuthorizedApi`. This is the pattern Slice C reuses verbatim.
- **NavMenu** (`Layout/NavMenu.razor` lines 27–33): the kitchen entry is ALREADY role-gated
  (`<AuthorizeView Roles="Cocinero,Administrador">`) and points to **`href="ordenes-trabajo"`** —
  NOT `/cocina`. → **Design decision: the page route is `/ordenes-trabajo` to match existing nav;
  NavMenu needs ZERO changes.** (Proposal said `/cocina`; aligning to the already-shipped gated nav
  is the lower-risk choice and avoids touching Slice A artifacts.)
- csproj: `net8.0`, Nullable + ImplicitUsings enabled. Existing pkgs pinned at `8.0.x`
  (Components.Authorization 8.0.6, WebAssembly 8.0.4, Extensions.Http 8.0.0), Blazored.LocalStorage 4.5.0.

---

## Technical approach

Reuse Slice A infra (authenticated `HttpClient`, `IAuthService.GetTokenAsync`) and Slice B patterns
(thin client records, `JsonStringEnumConverter`, `ProblemDetailsResponse`/`ApiException`,
relative-URI services) unchanged.

Add ONE new package (`Microsoft.AspNetCore.SignalR.Client`), ONE board service
(`KitchenBoardService`: initial GET + marcar-lista POST), ONE realtime client owned by the page,
and ONE page (`Pages/Cocina.razor` routed at `/ordenes-trabajo`, `IAsyncDisposable`).

Data flow:
```
Cocina.razor (OnInitializedAsync)
  ├─ KitchenBoardService.GetBoardAsync()  → GET /ordenes-trabajo        (initial hydrate)
  └─ KitchenRealtimeConnection.StartAsync()
         HubConnectionBuilder.WithUrl({ApiBaseUrl}/hubs/kitchen,
                 o => o.AccessTokenProvider = () => IAuthService.GetTokenAsync())
             .WithAutomaticReconnect()
         .On<OrdenTrabajoBoardItem>("OtChanged", item => OnOtChanged(item))   // off UI thread
         .Reconnected(_ => re-hydrate via GetBoardAsync)                       // close missed-update gap

OnOtChanged(item)  → upsert by OtId into in-memory list, Cancelada drops
                   → InvokeAsync(StateHasChanged)   (callback runs off the render thread)

"Marcar lista" button (Preparandose card)
  → KitchenBoardService.MarcarListaAsync(PedidoId, OtId)
       POST /pedidos/{pedidoId}/ordenes-trabajo/{otId}/marcar-lista
  → on success: rely on SignalR echo to move the card (server broadcasts OtChanged after commit)
  → on 422/404/403: catch ApiException → Spanish error banner; card unchanged

Cocina.razor (DisposeAsync) → connection.DisposeAsync()
```

---

## Key decisions (ADRs)

- **ADR-1 — SignalR package**: add `Microsoft.AspNetCore.SignalR.Client` **8.0.x** (pin `8.0.6`
  to match Components.Authorization; any `8.0.*` is framework-compatible). This is the CORRECT
  package for Blazor WASM — it is the generic .NET client and works in-browser (negotiates
  WebSockets, falls back to SSE/LongPolling automatically). Do NOT use server-side `SignalR` packages.
- **ADR-2 — board route = `/ordenes-trabajo`** (not `/cocina`). Matches the already-shipped gated
  NavMenu entry; NavMenu untouched. Lower risk than retargeting Slice A nav.
- **ADR-3 — single client record for board + event**. Board GET and `OtChanged` share the backend
  shape, so the client defines ONE record `OrdenTrabajoBoardItem` used for both `GetBoardAsync`
  deserialization and `On<OrdenTrabajoBoardItem>("OtChanged", …)`. Name `OtId` (not `Id`) — must match
  backend JSON property casing for STJ round-trip.
- **ADR-4 — string-vs-enum = mirror enums + `JsonStringEnumConverter`** (Slice B ADR-4). Client
  `EstadoOT`/`TipoPedido` enums, member names matching backend EXACTLY. Type-safe grouping/labels,
  compile-time column keys. Reject string properties (loses safety) and ints (couples to numeric order).
- **ADR-5 — contracts location = new `Contracts.OrdenesTrabajo` + reuse `Contracts.Enums`**. Put the
  record in `Contracts/OrdenesTrabajo/OrdenTrabajoBoardItem.cs`; put `EstadoOT`+`TipoPedido` in the
  existing `Contracts/Enums/` folder alongside `CondicionIVA`/`UnidadDeMedida`. Reuse the existing
  `Contracts.Common.{ProblemDetailsResponse, ApiException}` (no new common types).
- **ADR-6 — marcar-lista: echo-driven update (NOT optimistic)**. The recommended path: POST, and let
  the server's `OtChanged` broadcast move the card Preparandose→Lista. Rationale: the server is the
  single source of truth, the user already gets sub-second realtime feedback, and an optimistic flip
  would have to be rolled back on 422. **Light local fallback**: disable the button while the POST is
  in-flight (`isMarking` per-OT guard) to prevent double-submit; on success keep the button disabled
  until the echo arrives; on error re-enable and show the banner. No manual local state mutation on
  success — the echo owns it. (If the echo never arrives within the connection's reconnect window,
  the `Reconnected` re-GET will reconcile.)
- **ADR-7 — marcar-lista errors via Slice B ProblemDetails pattern**. Service deserializes
  `ProblemDetailsResponse`, throws `ApiException(Detail ?? Title ?? Spanish-fallback)`. Page catches →
  red banner. Handled cases: **422** (OT not Preparandose — e.g. already Lista/Cancelada by another
  cocinero), **404** (pedido/OT gone), **403** (role lost). No 409 path exists.
- **ADR-8 — load-all + group client-side** (resolves proposal open Q2). Call `GET /ordenes-trabajo`
  with NO `estado` filter; group in memory by `EstadoOT`. Simpler than per-column fetches and the
  `OtChanged` upsert naturally re-buckets a row across columns.
- **ADR-9 — `Cancelada` hidden** (resolves proposal open Q3). Not rendered as a column; an incoming
  `OtChanged` with `Estado == Cancelada` REMOVES the row from the in-memory list.
- **ADR-10 — connection owned by the page, not DI**. `KitchenRealtimeConnection` is instantiated and
  disposed by `Cocina.razor` (lifecycle = page lifetime). It is NOT a DI singleton/scoped service —
  a HubConnection has per-page lifecycle and must `DisposeAsync` on navigate-away. The page implements
  `IAsyncDisposable`. (`KitchenBoardService` for the HTTP calls IS DI-scoped, like the Slice B services.)

---

## wss vs https scheme derivation

`HubConnectionBuilder.WithUrl(...)` accepts the **`https`/`http`** absolute URL; the SignalR client
upgrades to `wss`/`ws` internally during transport negotiation. **Do NOT hand-rewrite the scheme.**
Build the hub URL as `$"{ApiOptions.ApiBaseUrl.TrimEnd('/')}/hubs/kitchen"` → e.g.
`https://localhost:7126/hubs/kitchen`. The client derives `wss://localhost:7126/hubs/kitchen` for the
WebSocket transport automatically.

---

## Token-on-reconnect handling

`options.AccessTokenProvider` is a **callback re-invoked on every (re)connect and transport
negotiation**, so a fresh JWT is pulled each time:
```
o.AccessTokenProvider = async () => await authService.GetTokenAsync();
```
If `GetTokenAsync()` returns null/expired, the hub `[Authorize]` rejects the connection; the page
surfaces "Desconectado" and the user must re-login (no refresh tokens in this system — Slice A ADR-5).
On a transient drop, `WithAutomaticReconnect()` re-invokes the provider with whatever token is then in
storage. The `Reconnected` handler additionally re-runs `GetBoardAsync()` to close the missed-update gap.

---

## LOCKED public shapes (for tasks/apply — do not drift)

### Enums — `Contracts/Enums/`
```csharp
namespace GastroGestionBlazor.Contracts.Enums;
public enum EstadoOT   { Creada = 0, Preparandose = 1, Lista = 2, Cancelada = 3 }
public enum TipoPedido { Salon = 0, TakeAway = 1, Delivery = 2 }
```

### Board item record — `Contracts/OrdenesTrabajo/OrdenTrabajoBoardItem.cs`
```csharp
namespace GastroGestionBlazor.Contracts.OrdenesTrabajo;

// Matches backend OrdenTrabajoBoardResponse 1:1 (board GET AND OtChanged payload).
public sealed record OrdenTrabajoBoardItem(
    Guid       OtId,
    Guid       PedidoId,
    TipoPedido PedidoTipo,
    Guid       PlatoId,
    Guid       LineaPedidoId,
    EstadoOT   Estado,
    Guid?      CocineroAsignadoLegajoId);
```

### Board service — `Services/KitchenBoardService.cs` (DI-scoped, ctor `HttpClient`)
```csharp
public sealed class KitchenBoardService
{
    public KitchenBoardService(HttpClient httpClient);

    // GET /ordenes-trabajo  (no estado filter — load all, group client-side)
    Task<List<OrdenTrabajoBoardItem>> GetBoardAsync(CancellationToken ct = default);

    // POST /pedidos/{pedidoId}/ordenes-trabajo/{otId}/marcar-lista  (no body)
    // success → returns (echo moves the card); failure → throws ApiException (422/404/403)
    Task MarcarListaAsync(Guid pedidoId, Guid otId, CancellationToken ct = default);
}
```
- Static `JsonOptions = new(JsonSerializerDefaults.Web){ Converters = { new JsonStringEnumConverter() } }`
  (Slice B pattern). Relative URIs only.
- `MarcarListaAsync`: `PostAsync($"pedidos/{pedidoId}/ordenes-trabajo/{otId}/marcar-lista", content: null)`;
  on `!IsSuccessStatusCode` deserialize `ProblemDetailsResponse` → throw
  `ApiException(problem?.Detail ?? problem?.Title ?? "No se pudo marcar la orden como lista.")`.
  Return body (`OrdenTrabajoResponse`) is ignored.

### Realtime connection — `Services/KitchenRealtimeConnection.cs` (page-owned, `IAsyncDisposable`)
```csharp
public sealed class KitchenRealtimeConnection : IAsyncDisposable
{
    public KitchenRealtimeConnection(ApiOptions apiOptions, IAuthService authService);

    HubConnectionState State { get; }              // for the status indicator

    // wires .On<OrdenTrabajoBoardItem>("OtChanged", onOtChanged); Reconnected → onReconnected;
    // Reconnecting/Closed → onStateChanged; then StartAsync()
    Task StartAsync(
        Action<OrdenTrabajoBoardItem> onOtChanged,
        Func<Task> onReconnected,
        Action onStateChanged,
        CancellationToken ct = default);

    ValueTask DisposeAsync();
}
```

---

## Cocina.razor (`Pages/Cocina.razor`)

- `@page "/ordenes-trabajo"`
- `@attribute [Authorize(Roles = "Cocinero,Administrador")]`
- `@implements IAsyncDisposable`
- `@inject KitchenBoardService BoardService`, `@inject ApiOptions ApiOptions`,
  `@inject IAuthService AuthService`
- **State**: `List<OrdenTrabajoBoardItem> _items`, `bool _isLoading`, `string? _errorMessage`,
  `HashSet<Guid> _marking` (per-OT in-flight guard), `KitchenRealtimeConnection? _conn`,
  `HubConnectionState _connState`.
- **Lifecycle**: `OnInitializedAsync` → `_items = await BoardService.GetBoardAsync()`; build `_conn`
  and `StartAsync(OnOtChanged, ReHydrateAsync, OnConnStateChanged)`. `DisposeAsync` → `_conn.DisposeAsync()`.
- **Grouping & columns** (Spanish labels, `Cancelada` hidden — ADR-9):
  - `Creada` → **"Pendientes"**
  - `Preparandose` → **"En preparación"**
  - `Lista` → **"Listas"**
- **`OnOtChanged(item)`** (off the UI thread → must marshal via `InvokeAsync`).
- **`ReHydrateAsync()`** (Reconnected): re-runs GetBoardAsync to close missed-update gap.
- Connection status badge: Conectado / Reconectando… / Desconectado.

---

## NavMenu

No change. Existing gated entry (`Layout/NavMenu.razor`) already points to `ordenes-trabajo` and is
wrapped in `<AuthorizeView Roles="Cocinero,Administrador">`. The new page route `/ordenes-trabajo`
matches it exactly (ADR-2).

---

## File changes

New (under `GastroGestionBlazor/GastroGestionBlazor/`):
- `Contracts/Enums/EstadoOT.cs`
- `Contracts/Enums/TipoPedido.cs`
- `Contracts/OrdenesTrabajo/OrdenTrabajoBoardItem.cs`
- `Services/KitchenBoardService.cs`
- `Services/KitchenRealtimeConnection.cs`
- `Pages/Cocina.razor`

Modify:
- `GastroGestionBlazor.csproj` — add `Microsoft.AspNetCore.SignalR.Client` 8.0.x.
- `Program.cs` — `builder.Services.AddScoped<KitchenBoardService>();`
- `_Imports.razor` — add `@using Microsoft.AspNetCore.SignalR.Client` and
  `@using GastroGestionBlazor.Contracts.OrdenesTrabajo`.

Unchanged: `Layout/NavMenu.razor`, all Slice A/B files, `Contracts/Common/*`.
