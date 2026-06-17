# Kitchen Board Realtime — Specification

## Purpose

New `Cocina` page for WASM frontend. Provides a role-gated, live-updating kitchen board hydrated from the backend OT board endpoint and kept current by the SignalR `OtChanged` event. Includes the "Marcar lista" mutation for OTs in `Preparandose` state.

---

## Requirements

### Requirement: BC-01 — Page Route Authorization

The `Cocina` page MUST be accessible only to users with role `Cocinero` or `Administrador`. Any other authenticated user or unauthenticated request MUST be denied at page level (framework-level `[Authorize(Roles)]` directive). The NavMenu entry (added in Slice A) MUST remain role-gated consistently.

#### Scenario: Authorized user accesses /cocina

- GIVEN a user authenticated with role `Cocinero` or `Administrador`
- WHEN they navigate to `/cocina`
- THEN the Cocina page loads and begins board hydration

#### Scenario: Unauthorized user attempts /cocina

- GIVEN a user authenticated with a role other than `Cocinero`/`Administrador`
- WHEN they navigate to `/cocina`
- THEN they are redirected to the access-denied page or `/` (Blazor auth redirect)
- AND the kitchen board is never rendered

#### Scenario: Unauthenticated access

- GIVEN an unauthenticated session
- WHEN `/cocina` is requested
- THEN the user is redirected to the login page

---

### Requirement: BC-02 — Initial Board Hydration

On page initialization the system MUST call `GET /ordenes-trabajo` (no estado filter) via the authenticated HTTP client. The response MUST be deserialized as `OrdenTrabajoBoardResponse[]`. OTs MUST be grouped into columns by `Estado` using Spanish column labels. `Cancelada` OTs MUST be excluded from display. A loading indicator MUST be visible while the request is in flight.

#### Scenario: Successful board load

- GIVEN the user lands on `/cocina`
- WHEN the initial GET completes with 200 and a non-empty array
- THEN OTs are displayed grouped into columns: "Creada", "Preparándose", "Lista"
- AND each card shows at minimum: PedidoTipo label, OtId reference, and current Estado

#### Scenario: Board load returns empty

- GIVEN the GET returns 200 with an empty array
- WHEN the page renders
- THEN an empty-state message is shown (e.g. "Sin órdenes de trabajo")
- AND no column is shown as broken or missing

#### Scenario: Board load returns 403

- GIVEN the JWT is present but the role is insufficient (edge case)
- WHEN the GET returns 403
- THEN an error state is shown; the board does not crash

---

### Requirement: BC-03 — SignalR Connection Lifecycle

The system MUST establish a `HubConnection` to `/hubs/kitchen` on page initialization. The JWT MUST be supplied via `AccessTokenProvider` using `GetTokenAsync()` (query-string delivery per backend contract). `WithAutomaticReconnect()` MUST be enabled. The connection MUST be stopped and disposed when the user navigates away from the page (`IAsyncDisposable`). On reconnect the board MUST re-hydrate via GET to recover any missed updates during the gap.

#### Scenario: Successful hub connect on init

- GIVEN the user opens `/cocina`
- WHEN the HubConnection starts successfully
- THEN no connection-error indicator is shown
- AND the board is ready to receive live events

#### Scenario: Transient disconnect with reconnect

- GIVEN the hub drops momentarily
- WHEN `WithAutomaticReconnect()` triggers and the hub reconnects
- THEN the board re-runs GET /ordenes-trabajo to resync
- AND the reconnecting indicator disappears after successful reconnect

#### Scenario: Hub connection fails / stays disconnected

- GIVEN the backend hub is unreachable
- WHEN reconnect exhausts retries or connection never establishes
- THEN a "Desconectado" or "Reconectando…" indicator is visible to the user
- AND the last known board state is preserved (no blank wipe)

#### Scenario: Navigate away disposes connection

- GIVEN the user has an active hub connection on `/cocina`
- WHEN they navigate to another page
- THEN `IAsyncDisposable.DisposeAsync()` is called
- AND the HubConnection is stopped; no background listener remains

---

### Requirement: BC-04 — Live Board Updates via OtChanged

The system MUST register an `"OtChanged"` handler on the HubConnection that receives `OrdenTrabajoBoardResponse`. On each event: if the OT already exists in the local list it MUST be replaced (upsert by `OtId`); if it is new it MUST be added to the correct column; if `Estado` is `Cancelada` it MUST be removed from the board immediately. The UI MUST re-render without a manual page refresh.

#### Scenario: Existing OT moves estado

- GIVEN an OT with `Estado = Creada` is displayed in the "Creada" column
- WHEN an `OtChanged` event arrives with the same `OtId` and `Estado = Preparandose`
- THEN the OT card moves to the "Preparándose" column without a page reload

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

Each OT card in the `Preparandose` column MUST display a "Marcar lista" button. Clicking it MUST POST to the marcar-lista endpoint (no body). On a 200 response the OT MUST reflect the `Lista` state (via the SignalR echo or local update). On a 409 or 422 response the Spanish error message from the server MUST be surfaced inline on the card. The button MUST be disabled during the in-flight request to prevent double-submit. This action is only available within the page already gated to `Cocinero`/`Administrador` (no additional role check needed client-side).

#### Scenario: Marcar lista — success

- GIVEN an OT card is in the "Preparándose" column
- WHEN the user clicks "Marcar lista"
- THEN the button is disabled while the POST is in flight
- AND on 200 the OT moves to "Lista" (either via SignalR echo or local state update)

#### Scenario: Marcar lista — conflict or validation error

- GIVEN an OT in "Preparándose" is in a state the server rejects
- WHEN the POST returns 409 or 422
- THEN the Spanish error message from the response body is shown on the card
- AND the button is re-enabled so the user can retry or dismiss

#### Scenario: Marcar lista — network error

- GIVEN the request to marcar-lista fails with a network/timeout error
- WHEN the exception is caught
- THEN a generic Spanish error message is shown on the card
- AND the OT state is NOT changed locally until a server confirmation arrives

---

### Requirement: BC-06 — Auth Token Refresh on Hub

The `AccessTokenProvider` delegate MUST call `GetTokenAsync()` on every (re)connect attempt to supply a fresh JWT. The system MUST handle a 401 from the hub endpoint consistently with the Slice A global auth-expiry pattern (redirect to login or trigger refresh).

#### Scenario: Token fresh on initial connect

- GIVEN the hub is connecting for the first time
- WHEN `AccessTokenProvider` is invoked
- THEN the current JWT from `GetTokenAsync()` is returned as the query-string token

#### Scenario: Token refreshed on reconnect

- GIVEN a reconnect is triggered after token expiry
- WHEN `AccessTokenProvider` is invoked again during reconnect
- THEN a fresh token is fetched via `GetTokenAsync()`
- AND the reconnect proceeds with the new token

---

### Requirement: BC-07 — Board Contracts (DTOs + Enums)

The frontend MUST define `OrdenTrabajoBoardResponse` as a C# record/class matching the verified backend payload exactly. `EstadoOT` and `TipoPedido` MUST be represented as string-backed enums or constants consistent with the Slice B DTO pattern. These contracts MUST live in the `Contracts/` layer (Slice B convention).

#### Scenario: Contract round-trip

- GIVEN the backend returns a JSON array with enum values as strings (e.g. `"Preparandose"`)
- WHEN it is deserialized into `OrdenTrabajoBoardResponse[]`
- THEN all fields are populated without deserialization errors

---

## Out of Scope

- Asignar cocinero (no cook-list endpoint; deferred to Slice C2).
- OT generation (mozo/Pedido flow, different role/workflow).
- Automated tests (none exist in repo; acceptance is manual smoke test).
- Any backend change.
