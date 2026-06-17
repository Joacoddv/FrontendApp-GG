# Kitchen — Realtime Kitchen Board Capability Spec

**Capability**: kitchen-board-realtime
**First delivered by**: blazor-slice-c (Phase 7, Slice C — PR #3, merge commit 6d93967)
**Status**: Active
**Last updated**: 2026-06-17

---

## Purpose

Role-gated live kitchen board for the Blazor WASM frontend. Cocinero and Administrador users access
`/ordenes-trabajo` to see all active OTs grouped by estado (Spanish labels), hydrated on load via
`GET /ordenes-trabajo` and kept current in real time by the SignalR `OtChanged` event. Includes the
`Marcar lista` mutation for OTs in `Preparandose` state, with echo-driven state transitions and
Spanish 422 error surfacing.

---

## Backend Contract (Locked)

### Board endpoint

- **GET** `/ordenes-trabajo?estado={EstadoOT?}` → `200 OrdenTrabajoBoardResponse[]`
- In-handler role gate: `Cocinero` or `Administrador` (else `403 ProblemDetails`).
- Invalid `estado` value → `400`.
- Client always calls with no filter (load-all, group client-side — ADR-8).

### Marcar lista endpoint

- **POST** `/pedidos/{pedidoId}/ordenes-trabajo/{otId}/marcar-lista` — **no request body**
- Returns `200 OrdenTrabajoResponse` (single-OT shape; response body is ignored by the client).
- Role gate: Cocinero or Administrador.
- Error mappings (via `GastroGestionExceptionHandler`):
  - OT not in `Preparandose` → `DomainException` → **422 UnprocessableEntity** (`title: "Domain rule violation"`).
  - Pedido/OT not found → `NotFoundException` → **404**.
  - Wrong role → `ForbiddenException` → **403**.
  - No 409 path exists for this action.
- Route is nested under `pedidoId` — the client MUST supply `PedidoId` (present on every board row).

### SignalR hub

- **URL**: `/hubs/kitchen` (`MapHub<KitchenHub>`)
- **Authorization**: `[Authorize(Roles="Cocinero,Administrador")]` — hub gate is independent backstop.
- **Group**: `"kitchen"` (clients join on connect).
- **Event**: `"OtChanged"` with payload `OrdenTrabajoBoardResponse` (same shape as a board row).
- **WS auth**: browsers cannot set the `Authorization` header on the WebSocket upgrade; backend reads
  `?access_token=` query parameter. Client supplies JWT via `AccessTokenProvider`.
- Enums serialize as **strings** globally (`JsonStringEnumConverter`).

### Payload shape (board GET and OtChanged — same record)

```
OrdenTrabajoBoardResponse(
    Guid       OtId,
    Guid       PedidoId,
    TipoPedido PedidoTipo,
    Guid       PlatoId,
    Guid       LineaPedidoId,
    EstadoOT   Estado,
    Guid?      CocineroAsignadoLegajoId
)
```

### Enum values

| Enum | Values |
|------|--------|
| `EstadoOT` | `Creada(0)`, `Preparandose(1)`, `Lista(2)`, `Cancelada(3)` |
| `TipoPedido` | `Salon(0)`, `TakeAway(1)`, `Delivery(2)` |

---

## Client Contracts (Locked)

```csharp
// Contracts/Enums/EstadoOT.cs
namespace GastroGestionBlazor.Contracts.Enums;
public enum EstadoOT { Creada = 0, Preparandose = 1, Lista = 2, Cancelada = 3 }

// Contracts/Enums/TipoPedido.cs
namespace GastroGestionBlazor.Contracts.Enums;
public enum TipoPedido { Salon = 0, TakeAway = 1, Delivery = 2 }

// Contracts/OrdenesTrabajo/OrdenTrabajoBoardItem.cs
namespace GastroGestionBlazor.Contracts.OrdenesTrabajo;
// Board GET response and OtChanged event payload — same shape.
public sealed record OrdenTrabajoBoardItem(
    Guid       OtId,
    Guid       PedidoId,
    TipoPedido PedidoTipo,
    Guid       PlatoId,
    Guid       LineaPedidoId,
    EstadoOT   Estado,
    Guid?      CocineroAsignadoLegajoId);
```

Reuses `Contracts.Common.ProblemDetailsResponse` and `ApiException` from Slice B.

---

## Requirements

---

### Requirement: BC-01 — Page Route Authorization

The `Cocina` page (`/ordenes-trabajo`) MUST be accessible only to users with role `Cocinero` or
`Administrador`. Any other authenticated user or unauthenticated request MUST be denied at page level
via `[Authorize(Roles)]`. The NavMenu entry (added in Slice A) MUST remain role-gated consistently.

#### Scenario: Authorized user accesses /ordenes-trabajo

- GIVEN a user authenticated with role `Cocinero` or `Administrador`
- WHEN they navigate to `/ordenes-trabajo`
- THEN the Cocina page loads and begins board hydration

#### Scenario: Unauthorized user attempts /ordenes-trabajo

- GIVEN a user authenticated with a role other than `Cocinero`/`Administrador`
- WHEN they navigate to `/ordenes-trabajo`
- THEN they are redirected to the access-denied page or `/` (Blazor auth redirect)
- AND the kitchen board is never rendered

#### Scenario: Unauthenticated access

- GIVEN an unauthenticated session
- WHEN `/ordenes-trabajo` is requested
- THEN the user is redirected to the login page

---

### Requirement: BC-02 — Initial Board Hydration

On page initialization the system MUST call `GET /ordenes-trabajo` (no estado filter) via the
authenticated HTTP client. The response MUST be deserialized as `OrdenTrabajoBoardItem[]`. OTs MUST
be grouped into columns by `Estado` using Spanish column labels. `Cancelada` OTs MUST be excluded
from display. A loading indicator MUST be visible while the request is in flight.

Column label mapping:
- `Creada` → **"Pendientes"**
- `Preparandose` → **"En preparación"**
- `Lista` → **"Listas"**
- `Cancelada` → hidden (never shown)

#### Scenario: Successful board load

- GIVEN the user lands on `/ordenes-trabajo`
- WHEN the initial GET completes with 200 and a non-empty array
- THEN OTs are displayed grouped under "Pendientes", "En preparación", "Listas"
- AND each card shows at minimum: PedidoTipo label, OtId reference, and current Estado

#### Scenario: Board load returns empty

- GIVEN the GET returns 200 with an empty array
- WHEN the page renders
- THEN an empty-state message is shown ("No hay órdenes de trabajo activas.")
- AND no column is shown as broken or missing

#### Scenario: Board load returns 403

- GIVEN the JWT is present but the role is insufficient (edge case)
- WHEN the GET returns 403
- THEN an error state is shown; the board does not crash

---

### Requirement: BC-03 — SignalR Connection Lifecycle

The system MUST establish a `HubConnection` to `/hubs/kitchen` on page initialization. The JWT MUST
be supplied via `AccessTokenProvider` using `GetTokenAsync()` (query-string delivery per backend
contract). `WithAutomaticReconnect()` MUST be enabled. The connection MUST be stopped and disposed
when the user navigates away (`IAsyncDisposable`). On reconnect the board MUST re-hydrate via
`GET /ordenes-trabajo` to recover any missed updates during the gap.

Connection status must be shown as a visible badge:
- `Connected` → "Conectado" (green)
- `Reconnecting` → "Reconectando…" (amber)
- `Disconnected`/`Closed` → "Desconectado" (red)

#### Scenario: Successful hub connect on init

- GIVEN the user opens `/ordenes-trabajo`
- WHEN the HubConnection starts successfully
- THEN no connection-error indicator is shown
- AND the board is ready to receive live events

#### Scenario: Transient disconnect with reconnect

- GIVEN the hub drops momentarily
- WHEN `WithAutomaticReconnect()` triggers and the hub reconnects
- THEN the board re-runs `GET /ordenes-trabajo` to resync
- AND the "Reconectando…" badge becomes "Conectado" after successful reconnect

#### Scenario: Hub connection fails / stays disconnected

- GIVEN the backend hub is unreachable
- WHEN reconnect exhausts retries or connection never establishes
- THEN a "Desconectado" indicator is visible to the user
- AND the last known board state is preserved (no blank wipe)

#### Scenario: Navigate away disposes connection

- GIVEN the user has an active hub connection on `/ordenes-trabajo`
- WHEN they navigate to another page
- THEN `IAsyncDisposable.DisposeAsync()` is called
- AND the HubConnection is stopped; no background listener remains

---

### Requirement: BC-04 — Live Board Updates via OtChanged

The system MUST register an `"OtChanged"` handler on the HubConnection that receives
`OrdenTrabajoBoardItem`. On each event: if the OT already exists in the local list it MUST be
replaced (upsert by `OtId`); if it is new it MUST be added to the correct column; if `Estado` is
`Cancelada` it MUST be removed from the board immediately. The UI MUST re-render without a manual
page refresh. All mutations and `StateHasChanged()` MUST be marshaled via `InvokeAsync`.

#### Scenario: Existing OT moves estado

- GIVEN an OT with `Estado = Creada` is displayed under "Pendientes"
- WHEN an `OtChanged` event arrives with the same `OtId` and `Estado = Preparandose`
- THEN the OT card moves to "En preparación" without a page reload

#### Scenario: New OT arrives via event

- GIVEN the board is displaying some OTs
- WHEN an `OtChanged` event arrives with an `OtId` not yet in the list
- THEN a new card appears in the column matching the new `Estado`

#### Scenario: OT cancelled via event

- GIVEN an OT is displayed on the board
- WHEN an `OtChanged` event arrives with `Estado = Cancelada`
- THEN the OT card is removed from the board immediately
- AND no "Cancelada" column is created

---

### Requirement: BC-05 — Marcar Lista Action

Each OT card in the `Preparandose` column MUST display a "Marcar lista" button. Clicking it MUST
POST to `POST /pedidos/{pedidoId}/ordenes-trabajo/{otId}/marcar-lista` (no body). The card
transition to `Lista` is **echo-driven**: the client waits for the server to broadcast `OtChanged`
after the commit rather than applying an optimistic local update. The button MUST be disabled during
the in-flight request to prevent double-submit (controlled via a `HashSet<Guid> _marking` guard).
On a 422 response the Spanish error message from `ProblemDetails.Detail` MUST be surfaced. The
action is only available within the page already gated to `Cocinero`/`Administrador`.

#### Scenario: Marcar lista — success

- GIVEN an OT card is in the "En preparación" column
- WHEN the user clicks "Marcar lista"
- THEN the button is disabled while the POST is in flight
- AND on 200 the button remains disabled until the `OtChanged` echo from the server moves the card to "Listas"

#### Scenario: Marcar lista — 422 validation error

- GIVEN an OT is in a state the server rejects (e.g. already Lista)
- WHEN the POST returns 422
- THEN the Spanish error message from `ProblemDetails.Detail` is shown
- AND the button is re-enabled so the user can retry or dismiss

#### Scenario: Marcar lista — network error

- GIVEN the request fails with a network/timeout error
- WHEN the exception is caught
- THEN a generic Spanish error message is shown
- AND the OT state is NOT changed locally until a server confirmation arrives

---

### Requirement: BC-06 — Auth Token Refresh on Hub

The `AccessTokenProvider` delegate MUST call `GetTokenAsync()` on every (re)connect attempt to
supply a fresh JWT. The system MUST handle a 401 from the hub endpoint consistently with the Slice A
global auth-expiry pattern (redirect to login — no refresh tokens in this system).

#### Scenario: Token fresh on initial connect

- GIVEN the hub is connecting for the first time
- WHEN `AccessTokenProvider` is invoked
- THEN the current JWT from `GetTokenAsync()` is returned as the `?access_token=` query parameter

#### Scenario: Token refreshed on reconnect

- GIVEN a reconnect is triggered after token expiry
- WHEN `AccessTokenProvider` is invoked again during reconnect
- THEN a fresh token is fetched via `GetTokenAsync()`
- AND the reconnect proceeds with the new token

---

### Requirement: BC-07 — Board Contracts (DTOs + Enums)

The frontend MUST define `OrdenTrabajoBoardItem` as a C# sealed record matching the verified backend
payload exactly. `EstadoOT` and `TipoPedido` MUST be string-backed enums consistent with the Slice B
DTO pattern (`JsonStringEnumConverter`). Contracts MUST live in `Contracts/OrdenesTrabajo/` (record)
and `Contracts/Enums/` (enums), reusing the Slice B namespace layout.

#### Scenario: Contract round-trip

- GIVEN the backend returns a JSON array with enum values as strings (e.g. `"Preparandose"`)
- WHEN it is deserialized into `List<OrdenTrabajoBoardItem>`
- THEN all fields are populated without deserialization errors
- AND enum members match the correct ordinal values

---

## Service and Page Architecture

### KitchenBoardService (DI-scoped)

- Ctor: `(HttpClient httpClient)` — receives default `AuthorizedApi` (Slice A factory).
- `GetBoardAsync(CancellationToken ct)` → `GET ordenes-trabajo` (relative), returns `List<OrdenTrabajoBoardItem>`.
- `MarcarListaAsync(Guid pedidoId, Guid otId, CancellationToken ct)` → `POST pedidos/{pedidoId}/ordenes-trabajo/{otId}/marcar-lista` with `null` content; on failure deserializes `ProblemDetailsResponse`, throws `ApiException(Detail ?? Title ?? Spanish-fallback)`.
- Static `JsonOptions = new(JsonSerializerDefaults.Web){ Converters = { new JsonStringEnumConverter() } }`.

### KitchenRealtimeConnection (page-owned, `IAsyncDisposable`)

- Ctor: `(ApiOptions apiOptions, IAuthService authService)`.
- `StartAsync(Action<OrdenTrabajoBoardItem> onOtChanged, Func<Task> onReconnected, Action onStateChanged, CancellationToken ct)`.
- Hub URL: `$"{ApiBaseUrl.TrimEnd('/')}/hubs/kitchen"` — SignalR client upgrades to `wss://` internally; do NOT hand-rewrite the scheme.
- `WithAutomaticReconnect()` enabled.
- `AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))` for enum string deserialization on the hub payload.
- `DisposeAsync()` → `await _connection.DisposeAsync()`.

### Cocina.razor (`Pages/Cocina.razor`)

- Route: `@page "/ordenes-trabajo"` (matches existing gated NavMenu href — ADR-2).
- `@attribute [Authorize(Roles = "Cocinero,Administrador")]`.
- `@implements IAsyncDisposable`.
- Off-thread callbacks (`OnOtChanged`, `ReHydrateAsync`, `OnConnStateChanged`) MUST marshal all state mutations AND `StateHasChanged()` via `InvokeAsync`.

---

## Key Design Decisions (ADRs)

| ADR | Decision | Rationale |
|-----|----------|-----------|
| ADR-1 | Package: `Microsoft.AspNetCore.SignalR.Client` 8.0.6 | Correct in-browser WASM client (generic .NET client, negotiates WS/SSE/LongPolling). Do NOT use server-side SignalR packages. |
| ADR-2 | Route = `/ordenes-trabajo` (not `/cocina`) | Matches the already-shipped gated NavMenu entry from Slice A; NavMenu zero changes. |
| ADR-3 | Single client record `OrdenTrabajoBoardItem` for board GET + OtChanged | Board row and event payload are the same backend shape — one type covers both. |
| ADR-4 | Mirror enums + `JsonStringEnumConverter` (Slice B ADR-4) | Compile-time safety; wire format is string; avoids integer coupling to enum ordinals. |
| ADR-5 | Contracts in `Contracts/OrdenesTrabajo/` + existing `Contracts/Enums/` | Consistent with Slice B namespace layout; reuses `Contracts.Common.{ProblemDetailsResponse,ApiException}`. |
| ADR-6 | Marcar lista: echo-driven (NOT optimistic) | Server is source of truth; sub-second echo delivers feedback; no rollback-on-422 needed. `_marking` HashSet guard prevents double-submit; on success button stays disabled until echo arrives. |
| ADR-7 | Marcar lista errors via Slice B ProblemDetails pattern | `ProblemDetailsResponse` → `ApiException(Detail ?? Title ?? Spanish-fallback)`. Handled: 422 (not Preparandose), 404, 403. No 409 path. |
| ADR-8 | Load-all + group client-side (no `?estado=` filter) | Simpler than per-column fetches; `OtChanged` upsert naturally re-buckets rows across columns. |
| ADR-9 | `Cancelada` hidden | Not rendered as a column; incoming `OtChanged` with `Estado == Cancelada` removes the row. |
| ADR-10 | Connection page-owned (not DI) | HubConnection has page lifetime; must `DisposeAsync` on navigate-away. `KitchenBoardService` (HTTP-only) is DI-scoped. |

---

## Known Deferred Items / Carried Follow-ups

| Item | Detail | Target |
|------|--------|--------|
| Asignar cocinero | No cook-list endpoint exists to populate a cocinero picker; feature deferred. | Slice C2 or future backend task |
| `Counter.razor` orphan | `Pages/Counter.razor` at `/counter` duplicates Clientes.razor; not in nav; recommend delete. | Follow-up PR (noted in Slice B deferred) |
| AutoMapper NU1903 | Pre-existing vulnerability warning on AutoMapper package; does not originate from this change. | Separate dependency-update task |
| WARNING-02: ReHydrateAsync thread safety | `_items =` assignment in `ReHydrateAsync` is outside `InvokeAsync`; WASM-safe today (single-threaded JS), architecturally imperfect. Fixed in commit 0fe2c57. | Already addressed |

---

## Non-Goals (Out of Scope for this Capability)

- Asignar cocinero action (no cook-list endpoint; deferred to future slice).
- OT generation (mozo/Pedido flow — different role/workflow).
- Automated tests (no test project in repo; acceptance is manual smoke test).
- Any backend change.
- Refresh tokens (no refresh flow — Slice A ADR-5).
- Friendly display labels beyond the three-column Spanish mapping.
