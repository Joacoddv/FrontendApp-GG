# Proposal: catalog-crud-ui

## Intent

### Problem
`Pages/Clientes.razor` and `Pages/Ingredientes.razor` ship with Edit, Delete, and Search controls
that are visually present but `disabled` (tagged `title="No disponible aún"`). The backend CRUD
endpoints they should drive (`PUT`, `DELETE`, search-enabled `GET`) are already merged and live.
The result is a half-finished catalog UI: users can list and create records, but cannot find a record
by name, correct a mistake, or retire an obsolete entry. Operators currently work around this by
scrolling the full list and have no path at all to amend bad data.

### Why now
The backend contracts are merged and stable; the frontend is the only thing blocking the catalog
from being fully usable. Closing this gap is pure wiring against an already-proven server contract —
no backend risk, no new infrastructure.

### Success
- An Administrador can search clientes/ingredientes by name, edit an existing record's allowed fields,
  and soft-delete a record, all from the existing pages.
- Any authenticated user can still list and search; mutation controls are simply not rendered for them.
- Backend errors (conflict, domain-rule, forbidden, validation, not-found) surface as readable Spanish
  messages in the existing inline error area.
- No regressions to the create flow or the read-only detail panel.

## Scope

### In scope
- **ClienteService / IngredienteService**: add `SearchAsync`, `UpdateAsync`, `DeleteAsync`, using the
  `ThrowApiExceptionAsync` ProblemDetails pattern for error surfacing.
- **New contracts**:
  - `EditarClienteRequest(Nombre, CondicionIVA, Cuit, Email)`
  - `EditarIngredienteRequest(Nombre)`
- **Clientes.razor**: enable the search input + Buscar button; wire Edit/Delete row actions behind an
  `<AuthorizeView Roles="Administrador">` gate; add an inline edit form (`detail-container` pattern);
  add edit state and `BuscarClientes` / `EditarCliente` / `EliminarCliente` handlers.
- **Ingredientes.razor**: same wiring, with edit limited to `Nombre`.
- **Delete confirmation**: a single `window.confirm` call via `IJSRuntime` before issuing the DELETE.
- Switch initial list load from `GetAllAsync()` to `SearchAsync(null, false)` (functionally identical
  backend route; cosmetic consolidation).

### Out of scope (non-goals)
- No backend changes (contracts are merged and frozen).
- No automated tests (the repo has none; not introducing a harness here).
- No `incluirInactivos` UI — hardcoded `false`; search returns active records only, no checkbox.
- No modal/dialog framework — inline panels only.
- `NumeroCliente` editing (never editable).
- `UnidadBase` editing for ingredientes (immutable; absent from the edit form and PUT body).
- `asignar-cocinero` (separate, already shipped).

## Approach

Pure frontend wiring against confirmed backend contracts.

### Services
Each service gains three methods mirroring the verified routes:
- `SearchAsync(string? nombre, bool incluirInactivos)` → `GET /{entity}?nombre=&incluirInactivos=`
- `UpdateAsync(Guid id, EditarXRequest)` → `PUT /{entity}/{id}` → returns the `XResponse`
- `DeleteAsync(Guid id)` → `DELETE /{entity}/{id}` → 204 (idempotent soft-delete)

All three use the `ThrowApiExceptionAsync(response, fallback, ct)` helper (from KitchenBoardService)
so ProblemDetails `Detail`/`Title` becomes the user-facing message. `incluirInactivos` is always
passed as `false` from the UI.

### Pages
- **Search**: keep the existing on-submit (button-click) model — no debounce, no live search. The
  Buscar button calls `SearchAsync(nombre, false)` and rebinds the table.
- **Edit**: a third inline panel rendered in the `detail-container` region, using a mutable edit
  view-model seeded from the selected row. Detail, create, and edit panels are **mutually exclusive** —
  opening any one closes the other two via explicit state resets.
- **Delete**: `await JS.InvokeAsync<bool>("confirm", "...")`; only on `true` issue the DELETE, then
  reload the list.
- **Role gating**: Edit and Delete controls wrapped in `<AuthorizeView Roles="Administrador">`. The
  page, list, and search stay available to every authenticated role. Backend 403 remains the second
  enforcement layer.

### Field rules (locked)
- Cliente edit form: `Nombre`, `Email`, `Cuit`, `CondicionIVA`. `NumeroCliente` never shown/editable.
- Ingrediente edit form: `Nombre` only. `UnidadBase` may be shown read-only but is NOT in the PUT body.

## PR plan — chained, stacked-to-main

Two independent PRs, one per entity, each ~175 changed lines (well under the 400 budget). They share
only a repeated pattern, not shared code, so they are genuinely independent and both merge to `main`.

- **PR-A — Clientes**
  - `Contracts/Clientes/EditarClienteRequest.cs` (new)
  - `Services/ClienteService.cs` (+ `SearchAsync`, `UpdateAsync`, `DeleteAsync`)
  - `Pages/Clientes.razor` (search enable, edit form, delete wiring, AuthorizeView gate)
  - Includes the `ThrowApiExceptionAsync` helper if not already shared.

- **PR-B — Ingredientes** (independent of PR-A; also stacked to `main`)
  - `Contracts/Ingredientes/EditarIngredienteRequest.cs` (new)
  - `Services/IngredienteService.cs` (+ same three methods)
  - `Pages/Ingredientes.razor` (same wiring; edit = `Nombre` only)

Boundary rationale: each entity is self-contained; splitting keeps each diff narrow and reviewable,
and lets Clientes ship even if Ingredientes review lags.

## Edge cases

- **409 conflict**: Cuit already assigned to another cliente / Nombre already assigned to another
  ingrediente → surface ProblemDetails `Detail` in the edit error area.
- **422 domain rule**: e.g. ResponsableInscripto requires Cuit → surface domain-rule message.
- **403 forbidden**: non-admin reaching a mutation via race/direct call → caught as `ApiException`
  ("Access denied. Required role: Administrador."), shown in the edit/delete error area. The
  AuthorizeView gate prevents this in normal flow.
- **400 validation**: empty `Nombre` → FluentValidation message surfaced.
- **404 not found**: record deleted/edited concurrently → "Resource not found" surfaced.
- **Idempotent delete**: DELETE on an already-inactive record returns 204; treat as success.
- **Three-panel collision**: detail / create / edit must be mutually exclusive; opening one resets
  the others.
- **Non-admin user**: Edit/Delete hidden; search and list still fully functional.
- **GetAllAsync → SearchAsync(null,false)**: same backend handler (BuscarClientesHandler); behavior
  unchanged. Purely a naming consolidation.

## Risks

1. **Three-panel state management** — adding `showEditForm` to existing `showCreateForm` +
   `seleccionado` requires disciplined mutual-exclusion resets. Low risk, needs care in spec/tasks.
2. **Error-message language consistency** — backend ProblemDetails messages are partly English
   (e.g. "Access denied...", "Resource not found"). UI copy is Spanish; the surfaced server message
   may be English. Decision for spec: surface server `Detail` verbatim, or map known codes to Spanish.
   Leaning verbatim for first slice to avoid a translation layer.
3. **ThrowApiExceptionAsync location** — extract to a shared helper vs. duplicate inline. Recommend
   the shared helper to keep both services DRY; resolve in design/tasks.
4. **Open question (non-blocking)**: should 403/404 use the same inline error area as 409/422, or a
   distinct treatment? Default: one shared inline error area per panel.

## Proposal question round (deferred — assumptions for user review)

The user has already locked all material product decisions (delete confirmation = window.confirm,
no incluirInactivos UI, chained stacked-to-main PRs, field rules, role gating). No blocking product
questions remain. The only open product-shaped item is whether English backend messages should be
translated to Spanish before display — assumption: surface verbatim in the first slice. Flag if the
user wants a Spanish mapping layer instead.
