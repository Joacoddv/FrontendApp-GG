# Delta Spec: catalog-crud-ui

**Change**: catalog-crud-ui  
**Capability**: blazor-screens (Screens)  
**Delivery**: Chained PRs — PR-A (Cliente), PR-B (Ingrediente)  
**Tags**: `[Cliente]` = PR-A scope · `[Ingrediente]` = PR-B scope · `[Shared]` = both PRs

---

## MODIFIED Requirements

### Requirement: BSB-C06 — Buscar/Editar/Eliminar controls enabled for Clientes `[Cliente]`

The Clientes page MUST enable and wire the Buscar, Editar, and Eliminar controls. The disabled state and "no disponible aún" note MUST be removed. Edit and Delete actions MUST be wrapped in `<AuthorizeView Roles="Administrador">` so they are hidden for non-admin authenticated users. Search (Buscar) MUST be available to all authenticated roles.

(Previously: BSB-C06 required all three controls to be disabled with "no disponible aún" tooltip; no HTTP call was permitted.)

#### Scenario: Admin sees enabled edit and delete buttons

- GIVEN an authenticated user with role Administrador on the Clientes page
- WHEN the list renders
- THEN Edit and Delete buttons are visible and enabled for each row
- AND Buscar input and button are enabled

#### Scenario: Non-admin cannot see edit or delete buttons

- GIVEN an authenticated user without role Administrador on the Clientes page
- WHEN the list renders
- THEN Edit and Delete buttons are NOT rendered (hidden by AuthorizeView)
- AND Buscar input and button are visible and enabled

#### Scenario: No HTTP call from disabled state (regression guard)

- GIVEN the controls are now enabled
- WHEN a non-admin user inspects the page
- THEN no Edit or Delete button is reachable via the rendered DOM

---

### Requirement: BSB-I06 — Buscar/Editar/Eliminar controls enabled for Ingredientes `[Ingrediente]`

The Ingredientes page MUST enable and wire the Buscar, Editar, and Eliminar controls under the same rules as BSB-C06: Edit/Delete wrapped in `<AuthorizeView Roles="Administrador">`, Buscar open to all authenticated roles.

(Previously: BSB-I06 required all three controls to be disabled; no HTTP call was permitted.)

#### Scenario: Admin sees enabled edit and delete buttons

- GIVEN an authenticated user with role Administrador on the Ingredientes page
- WHEN the list renders
- THEN Edit and Delete buttons are visible and enabled for each row

#### Scenario: Non-admin cannot see edit or delete buttons

- GIVEN an authenticated user without role Administrador
- WHEN the Ingredientes page renders
- THEN Edit and Delete buttons are NOT rendered

---

## ADDED Requirements

### Requirement: CUI-C01 — EditarClienteRequest contract `[Cliente]`

A new client-side record `EditarClienteRequest` MUST be added under `Contracts/Clientes/`. It MUST contain exactly: `Nombre (string)`, `CondicionIVA (CondicionIVA)`, `Cuit (string?)`, `Email (string?)`. `NumeroCliente` MUST NOT appear in this record.

#### Scenario: Contract mirrors backend PUT /clientes/{id} body

- GIVEN the EditarClienteRequest record
- WHEN serialized as JSON
- THEN the body contains Nombre, CondicionIVA (string), Cuit, Email
- AND NumeroCliente is absent from the serialized body

---

### Requirement: CUI-I01 — EditarIngredienteRequest contract `[Ingrediente]`

A new client-side record `EditarIngredienteRequest` MUST be added under `Contracts/Ingredientes/`. It MUST contain exactly: `Nombre (string)`. `UnidadBase` MUST NOT appear in this record.

#### Scenario: Contract mirrors backend PUT /ingredientes/{id} body

- GIVEN the EditarIngredienteRequest record
- WHEN serialized as JSON
- THEN the body contains only Nombre
- AND UnidadBase is absent from the serialized body

---

### Requirement: CUI-C02 — ClienteService CRUD methods `[Cliente]`

ClienteService MUST expose three new methods: `SearchAsync(string? nombre, bool incluirInactivos)`, `UpdateAsync(Guid id, EditarClienteRequest)`, `DeleteAsync(Guid id)`. Each MUST use the `ThrowApiExceptionAsync` error pattern to surface `ProblemDetails.Detail ?? Title ?? fallback` as an `ApiException`. `incluirInactivos` MUST always be passed as received (caller hardcodes `false`).

#### Scenario: SearchAsync calls correct endpoint

- GIVEN a call to SearchAsync(nombre: "Acme", incluirInactivos: false)
- WHEN the HTTP request is made
- THEN the request is GET /clientes?nombre=Acme&incluirInactivos=false via AuthorizedApi

#### Scenario: UpdateAsync calls PUT and returns response

- GIVEN a call to UpdateAsync(id, editarRequest)
- WHEN the HTTP request is made
- THEN the request is PUT /clientes/{id} with EditarClienteRequest body
- AND on 200 the method returns the deserialized ClienteResponse

#### Scenario: DeleteAsync calls DELETE and expects 204

- GIVEN a call to DeleteAsync(id)
- WHEN the HTTP request is made
- THEN the request is DELETE /clientes/{id}
- AND on 204 the method returns without error

#### Scenario: Non-2xx throws ApiException

- GIVEN the backend returns 409 with ProblemDetails
- WHEN UpdateAsync or DeleteAsync is called
- THEN ThrowApiExceptionAsync extracts ProblemDetails.Detail and throws ApiException

---

### Requirement: CUI-I02 — IngredienteService CRUD methods `[Ingrediente]`

IngredienteService MUST expose `SearchAsync(string? nombre, bool incluirInactivos)`, `UpdateAsync(Guid id, EditarIngredienteRequest)`, and `DeleteAsync(Guid id)` following the same contract as CUI-C02.

#### Scenario: SearchAsync calls correct endpoint

- GIVEN a call to SearchAsync(nombre: null, incluirInactivos: false)
- WHEN the HTTP request is made
- THEN the request is GET /ingredientes?nombre=&incluirInactivos=false

#### Scenario: UpdateAsync sends Nombre only

- GIVEN a call to UpdateAsync(id, EditarIngredienteRequest("Sal fina"))
- WHEN the request body is inspected
- THEN it contains only Nombre; UnidadBase is absent

---

### Requirement: CUI-C03 — Clientes page: search behavior `[Cliente]`

The Clientes page MUST load initial data via `SearchAsync(null, false)` instead of `GetAllAsync()`. The Buscar button MUST trigger `SearchAsync(searchNombre, false)` on click (no debounce, no on-keyup). `incluirInactivos` MUST be hardcoded `false`; no UI toggle for it MUST exist.

#### Scenario: Initial load returns all active clientes

- GIVEN the user navigates to the Clientes page
- WHEN the page initializes
- THEN SearchAsync(null, false) is called
- AND the returned active clientes are displayed in the list

#### Scenario: Buscar filters by name

- GIVEN the user types "Acme" in the search input and clicks Buscar
- WHEN SearchAsync("Acme", false) returns results
- THEN only matching active clientes are shown in the list

#### Scenario: Empty search term returns all active

- GIVEN the user clears the search input and clicks Buscar
- WHEN SearchAsync(null or "", false) is called
- THEN all active clientes are returned and displayed

---

### Requirement: CUI-I03 — Ingredientes page: search behavior `[Ingrediente]`

The Ingredientes page MUST follow the same search rules as CUI-C03: initial load via `SearchAsync(null, false)`, on-submit search, `incluirInactivos` hardcoded `false`.

#### Scenario: Initial load returns all active ingredientes

- GIVEN the user navigates to the Ingredientes page
- WHEN the page initializes
- THEN SearchAsync(null, false) is called and active ingredientes are displayed

#### Scenario: Buscar filters by name on click

- GIVEN the user types "Sal" and clicks Buscar
- WHEN SearchAsync("Sal", false) is called
- THEN only matching active ingredientes are listed

---

### Requirement: CUI-C04 — Clientes page: inline edit form `[Cliente]`

The Clientes page MUST display an inline edit form (inside the existing `detail-container`) when the user clicks Edit on a row. The edit form MUST be mutually exclusive with the detail view and create form (opening one MUST close the others). The edit form MUST expose fields: Nombre (text input), CondicionIVA (select, same options as create), Cuit (text input), Email (text input). NumeroCliente MUST be shown as read-only text and MUST NOT appear in the PUT request body. On successful PUT the list MUST refresh and the edit form MUST close.

#### Scenario: Opening edit closes other panels

- GIVEN the detail view is open for a cliente
- WHEN the user clicks Edit on any row
- THEN the detail view closes and the edit form opens for that row
- AND the create form is also closed if it was open

#### Scenario: Edit form pre-populates from row data

- GIVEN the user clicks Edit on a cliente row
- WHEN the edit form renders
- THEN Nombre, CondicionIVA, Cuit, Email are pre-populated from that cliente's current values

#### Scenario: Submit PUT refreshes list and closes form

- GIVEN the user modifies Nombre and submits the edit form
- WHEN the backend returns 200
- THEN the list refreshes via SearchAsync with the current search term
- AND the edit form closes

#### Scenario: NumeroCliente not sent to backend

- GIVEN the user submits the edit form
- WHEN the PUT /clientes/{id} request body is inspected
- THEN NumeroCliente is absent

---

### Requirement: CUI-I04 — Ingredientes page: inline edit form `[Ingrediente]`

The Ingredientes page MUST display an inline edit form when the user clicks Edit. The form MUST expose only: Nombre (text input). UnidadBase MUST be shown as a read-only disabled input for reference and MUST NOT be included in the PUT request body. Panel mutual-exclusion and list-refresh rules apply identically to CUI-C04.

#### Scenario: Edit form shows Nombre only as editable

- GIVEN the user clicks Edit on an ingrediente row
- WHEN the edit form renders
- THEN only Nombre is an editable field
- AND UnidadBase is displayed as read-only (disabled)

#### Scenario: PUT body contains Nombre only

- GIVEN the user submits the edit form
- WHEN the PUT /ingredientes/{id} request body is inspected
- THEN only Nombre is present; UnidadBase is absent

#### Scenario: Successful PUT refreshes list

- GIVEN the user changes Nombre to "Sal marina" and submits
- WHEN the backend returns 200
- THEN the ingrediente list refreshes and the edit form closes

---

### Requirement: CUI-C05 — Clientes page: soft-delete with confirmation `[Cliente]`

When the user clicks Delete on a row, the page MUST call `window.confirm` via `IJSRuntime.InvokeAsync<bool>("confirm", message)` BEFORE making any HTTP call. If the user cancels the confirmation, NO HTTP call MUST be made. If confirmed, `ClienteService.DeleteAsync(id)` MUST be called. On 204 the row MUST be removed from the displayed list (refresh via SearchAsync). The operation MUST be idempotent — a 204 on an already-inactive record MUST be treated as success.

#### Scenario: User cancels confirmation — no HTTP call

- GIVEN the user clicks Delete on a cliente row
- WHEN the window.confirm dialog returns false
- THEN DeleteAsync is NOT called and the list is unchanged

#### Scenario: User confirms — DELETE fires and list refreshes

- GIVEN the user clicks Delete and confirms
- WHEN DeleteAsync returns 204
- THEN the list refreshes and the deleted row is no longer visible

#### Scenario: Idempotent delete — already inactive treated as success

- GIVEN the backend returns 204 for an already-inactive cliente
- WHEN DeleteAsync completes
- THEN the list refreshes without error

---

### Requirement: CUI-I05 — Ingredientes page: soft-delete with confirmation `[Ingrediente]`

The Ingredientes page MUST apply the same delete behavior as CUI-C05: window.confirm via IJSRuntime before any HTTP call; 204 triggers list refresh; idempotent.

#### Scenario: User cancels confirmation — no HTTP call

- GIVEN the user clicks Delete on an ingrediente row
- WHEN window.confirm returns false
- THEN DeleteAsync is NOT called

#### Scenario: Confirmed delete refreshes list

- GIVEN the user confirms deletion
- WHEN DeleteAsync returns 204
- THEN the list refreshes and the row is absent

---

### Requirement: CUI-X01 — Edit error surfacing (both entities) `[Shared]`

When an edit or delete operation returns a non-2xx response, the page MUST surface the `ApiException.Message` (which carries `ProblemDetails.Detail ?? Title ?? fallback`) in an inline edit error area. The following HTTP statuses MUST be handled: 400 (validation failure), 403 (non-admin access), 404 (not found), 409 (Cuit/Nombre conflict), 422 (domain rule violation). Error messages MUST be displayed verbatim from the server (no client-side re-mapping in this slice). The 403 and 404 error messages are in English as returned by the backend; Spanish re-mapping is deferred.

#### Scenario: 409 conflict displayed in edit area

- GIVEN the user submits an edit with a Cuit already used by another cliente
- WHEN the backend returns 409 with ProblemDetails.Detail
- THEN the detail message is displayed in the edit error area
- AND the edit form remains open

#### Scenario: 422 domain rule displayed in edit area

- GIVEN the user submits an edit that violates a domain rule (e.g. RI without Cuit)
- WHEN the backend returns 422
- THEN the ProblemDetails.Detail message is displayed in the edit error area

#### Scenario: 403 displayed in edit area

- GIVEN a non-admin user reaches the edit path (e.g. via direct API call)
- WHEN the backend returns 403
- THEN the ApiException message is displayed in the edit error area

---

## REMOVED Requirements

### Requirement: BSB-C06 and BSB-I06 — disabled-controls constraint

(Reason: The backend CRUD endpoints (PUT/DELETE/search GET) are now merged and stable. The controls are being enabled and wired in this change. BSB-C06 and BSB-I06 are replaced by their MODIFIED versions above, which define the enabled behavior.)
(Migration: The MODIFIED BSB-C06 and BSB-I06 entries above supersede the disabled state. Archive step will replace the old requirement blocks with the modified versions.)

---

## Out of Scope (Non-Goals)

- Backend changes of any kind
- Automated tests (`strict_tdd: false` for this project)
- `incluirInactivos` UI toggle (hardcoded `false` only)
- Modal dialog framework
- NumeroCliente editing (read-only display, never editable)
- UnidadBase editing (read-only display in Ingrediente edit form, absent from PUT body)
- asignar-cocinero (already shipped separately)
- Spanish re-mapping of 403/404 error messages (deferred)

---

## PR Mapping

| Requirement IDs | PR |
|---|---|
| CUI-C01, CUI-C02, CUI-C03, CUI-C04, CUI-C05, BSB-C06 (modified) | PR-A — Clientes |
| CUI-I01, CUI-I02, CUI-I03, CUI-I04, CUI-I05, BSB-I06 (modified) | PR-B — Ingredientes |
| CUI-X01 | Both PRs (each page has its own edit error area) |
