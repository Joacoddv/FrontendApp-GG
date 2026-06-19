# Tasks: catalog-crud-ui

**Change**: catalog-crud-ui
**Delivery**: Chained PRs — stacked-to-main
**Chain Strategy**: stacked-to-main (PR-A → main, PR-B → main independently)
**Artifact store**: hybrid (engram + openspec)
**Build**: `dotnet build GastroGestionBlazor.sln` from frontend repo root
**Tests**: none (strict_tdd: false) — verification = build + manual smoke

---

## Review Workload Forecast

| PR | Files touched | Est. additions | Est. deletions | Total lines | Budget risk | Decision |
|----|--------------|---------------|----------------|-------------|-------------|---------|
| PR-A (Clientes) | 3 | ~140 | ~25 | ~165 | Low | Single PR ✓ |
| PR-B (Ingredientes) | 3 | ~120 | ~20 | ~140 | Low | Single PR ✓ |
| Combined | 6 | ~260 | ~45 | ~305 | Low | —  |

**Chained PRs recommended**: Yes (by delivery strategy choice — user selected stacked-to-main)
**400-line budget risk**: Low for each individual PR (~165 and ~140 lines)
**Decision status**: Resolved — user chose chained/stacked-to-main. Each PR is well under budget; chaining is for entity isolation and focused review, not budget overflow.

---

## PR-A — Clientes

Branch: `feat/catalog-crud-ui-clientes`
Depends on: `main` only
Commits: work-unit commits (one per task group below)

### Dependency diagram

```
main
 └── feat/catalog-crud-ui-clientes (PR-A) 📍
 └── feat/catalog-crud-ui-ingredientes (PR-B)  [independent]
```

---

### [x] CUI-A-T01 — Create EditarClienteRequest contract

**Satisfies**: CUI-C01
**Type**: Create file
**File**: `GastroGestionBlazor/Contracts/Clientes/EditarClienteRequest.cs`
**Parallel**: No (foundation for T02, T03)

Steps:
1. Create `Contracts/Clientes/EditarClienteRequest.cs`
2. Define as `record` with exactly four properties: `string Nombre`, `CondicionIVA CondicionIVA`, `string? Cuit`, `string? Email`
3. Confirm `NumeroCliente` is absent from the record
4. Build: `dotnet build GastroGestionBlazor.sln`

Acceptance: build succeeds; record exists under `Contracts/Clientes/`; no `NumeroCliente` property.

Commit: `feat(clientes): add EditarClienteRequest contract`

---

### [x] CUI-A-T02 — Extend ClienteService with Search/Update/Delete

**Satisfies**: CUI-C02, CUI-C03 (SearchAsync)
**Type**: Modify file
**File**: `GastroGestionBlazor/Services/ClienteService.cs`
**Parallel**: After T01 (needs EditarClienteRequest)

Steps:
1. Add `private static async Task ThrowApiExceptionAsync(HttpResponseMessage response, string fallback, CancellationToken ct)` — verbatim copy from KitchenBoardService, same JsonOptions (JsonStringEnumConverter)
2. Add `SearchAsync(string? nombre, bool incluirInactivos = false, CancellationToken ct = default)` → `GET /clientes?nombre={Uri.EscapeDataString(nombre)}&incluirInactivos={incluirInactivos}` (omit `nombre` query param when null or empty); returns `List<ClienteResponse>`
3. Add `UpdateAsync(Guid id, EditarClienteRequest req, CancellationToken ct = default)` → `PUT /clientes/{id}` with JSON body; returns `ClienteResponse?`; calls `ThrowApiExceptionAsync` on non-2xx
4. Add `DeleteAsync(Guid id, CancellationToken ct = default)` → `DELETE /clientes/{id}`; expects 204; calls `ThrowApiExceptionAsync` on non-2xx
5. Rewire `GetAllAsync()` body to delegate to `SearchAsync(null, false, ct)` — keep public signature intact
6. Build: `dotnet build GastroGestionBlazor.sln`

Acceptance: build succeeds; four methods present; `GetAllAsync` delegates; `ThrowApiExceptionAsync` private static; no shared helper file.

Commit: `feat(clientes): add SearchAsync, UpdateAsync, DeleteAsync to ClienteService`

---

### [x] CUI-A-T03 — Update Clientes.razor: search, inline edit, delete

**Satisfies**: BSB-C06 (modified), CUI-C03, CUI-C04, CUI-C05, CUI-X01
**Type**: Modify file
**File**: `GastroGestionBlazor/Pages/Clientes.razor`
**Parallel**: After T02 (needs ClienteService methods)

Sub-steps (all in one commit — they form one coherent user-visible behavior unit):

#### 3a — Inject IJSRuntime and switch initial load to SearchAsync
- Add `@inject IJSRuntime JS` directive
- Change `OnInitializedAsync` to call `ClienteService.SearchAsync(null, false)` instead of `GetAllAsync`
- Wire Buscar button `onclick` to `BuscarClientes()` handler: calls `SearchAsync(searchNombre, false)`
- Declare `private string? searchNombre` bound to the search input
- Remove "no disponible aún" note and disabled state from Buscar input/button (satisfies BSB-C06 search part)

#### 3b — Enable Edit/Delete buttons under AuthorizeView
- Wrap Edit and Delete table buttons in `<AuthorizeView Roles="Administrador">` block
- Remove disabled state and "no disponible aún" tooltip from Edit and Delete (satisfies BSB-C06 auth part)

#### 3c — Three-panel mutual exclusion
- Declare: `private bool showEditForm = false`; `private ClienteResponse? clienteEnEdicion = null`; `private string? editErrorMessage = null`
- Add method `MostrarEditar(ClienteResponse c)`: sets `clienteEnEdicion = c`, `showEditForm = true`, `clienteSeleccionado = null` (closes detail), `showCreateForm = false` (closes create)
- Add method `CerrarEdicion()`: resets edit flags/state
- Wire Edit button `onclick` to `MostrarEditar(cliente)`

#### 3d — Inline edit form
- Add edit form block (inside `detail-container` or equivalent) gated on `showEditForm && clienteEnEdicion != null`
- Fields:
  - Nombre: text input, two-way bound to edit VM
  - CondicionIVA: select with 4 options (ResponsableInscripto, Monotributista, ConsumidorFinal, ExentoIVA)
  - Cuit: text input, nullable
  - Email: text input, nullable
  - NumeroCliente: displayed as read-only text — NOT a form field, NOT in PUT body
- Guardar button: calls `GuardarEdicionCliente()` → builds `EditarClienteRequest(Nombre, CondicionIVA, Cuit, Email)` → calls `ClienteService.UpdateAsync(id, req)` → on success: `BuscarClientes()` + `CerrarEdicion()`; on `ApiException`: set `editErrorMessage = ex.Message`
- Cancelar button: calls `CerrarEdicion()`
- Error display: `<p class="text-danger">@editErrorMessage</p>` visible when non-null (satisfies CUI-X01)

#### 3e — Delete with confirmation
- Delete button handler `EliminarCliente(Guid id)`:
  - `var ok = await JS.InvokeAsync<bool>("confirm", "¿Eliminar este cliente?")`
  - If `!ok` → return (no HTTP call)
  - Else → `await ClienteService.DeleteAsync(id)` → on 204: `BuscarClientes()`; on `ApiException`: set `errorMessage = ex.Message`

4. Build: `dotnet build GastroGestionBlazor.sln`

Acceptance: build succeeds; Buscar/Edit/Delete all wired; AuthorizeView present; edit form pre-populates; NumeroCliente not in PUT; window.confirm before delete; error surfacing visible.

Commit: `feat(clientes): enable search, inline edit form, soft-delete on Clientes page`

---

### [x] CUI-A-T04 — PR-A build verification

**Satisfies**: delivery gate
**Type**: Verification
**Parallel**: After T03

Steps:
1. Run `dotnet build GastroGestionBlazor.sln` from `C:\Users\Joaquin\OneDrive\Desktop\Desktop\GastroGestion\GastroGestionBlazor`
2. Confirm exit code 0 and 0 errors
3. Confirm 3 files changed: `EditarClienteRequest.cs` (new), `ClienteService.cs` (modified), `Clientes.razor` (modified)

Commit: none (verification only)

---

## PR-B — Ingredientes

Branch: `feat/catalog-crud-ui-ingredientes`
Depends on: `main` only (independent of PR-A)
Commits: work-unit commits

### Dependency diagram

```
main
 └── feat/catalog-crud-ui-clientes (PR-A)  [independent]
 └── feat/catalog-crud-ui-ingredientes (PR-B) 📍
```

---

### [x] CUI-B-T01 — Create EditarIngredienteRequest contract

**Satisfies**: CUI-I01
**Type**: Create file
**File**: `GastroGestionBlazor/Contracts/Ingredientes/EditarIngredienteRequest.cs`
**Parallel**: No (foundation for T02, T03)

Steps:
1. Create `Contracts/Ingredientes/EditarIngredienteRequest.cs`
2. Define as `record` with exactly one property: `string Nombre`
3. Confirm `UnidadBase` is absent from the record
4. Build: `dotnet build GastroGestionBlazor.sln`

Acceptance: build succeeds; record exists under `Contracts/Ingredientes/`; `Nombre` only; no `UnidadBase`.

Commit: `feat(ingredientes): add EditarIngredienteRequest contract`

---

### [x] CUI-B-T02 — Extend IngredienteService with Search/Update/Delete

**Satisfies**: CUI-I02, CUI-I03 (SearchAsync)
**Type**: Modify file
**File**: `GastroGestionBlazor/Services/IngredienteService.cs`
**Parallel**: After T01 (needs EditarIngredienteRequest)

Steps:
1. Add `private static async Task ThrowApiExceptionAsync(HttpResponseMessage response, string fallback, CancellationToken ct)` — verbatim copy from KitchenBoardService (per-service duplication is the codebase convention; no shared helper)
2. Add `SearchAsync(string? nombre, bool incluirInactivos = false, CancellationToken ct = default)` → `GET /ingredientes?nombre={Uri.EscapeDataString(nombre)}&incluirInactivos={incluirInactivos}` (omit `nombre` when null or empty); returns `List<IngredienteResponse>`
3. Add `UpdateAsync(Guid id, EditarIngredienteRequest req, CancellationToken ct = default)` → `PUT /ingredientes/{id}` with JSON body (Nombre only); returns `IngredienteResponse?`; calls `ThrowApiExceptionAsync` on non-2xx
4. Add `DeleteAsync(Guid id, CancellationToken ct = default)` → `DELETE /ingredientes/{id}`; expects 204; calls `ThrowApiExceptionAsync` on non-2xx
5. Rewire `GetAllAsync()` body to delegate to `SearchAsync(null, false, ct)` — keep public signature intact
6. Build: `dotnet build GastroGestionBlazor.sln`

Acceptance: build succeeds; four methods present; `GetAllAsync` delegates; `ThrowApiExceptionAsync` private static; no shared helper file.

Commit: `feat(ingredientes): add SearchAsync, UpdateAsync, DeleteAsync to IngredienteService`

---

### [x] CUI-B-T03 — Update Ingredientes.razor: search, inline edit, delete

**Satisfies**: BSB-I06 (modified), CUI-I03, CUI-I04, CUI-I05, CUI-X01
**Type**: Modify file
**File**: `GastroGestionBlazor/Pages/Ingredientes.razor`
**Parallel**: After T02 (needs IngredienteService methods)

Sub-steps (one commit — one coherent user-visible behavior unit):

#### 3a — Inject IJSRuntime and switch initial load to SearchAsync
- Add `@inject IJSRuntime JS` directive
- Change `OnInitializedAsync` to call `IngredienteService.SearchAsync(null, false)`
- Wire Buscar button `onclick` to `BuscarIngredientes()` handler: calls `SearchAsync(searchNombre, false)`
- Declare `private string? searchNombre` bound to search input
- Remove "no disponible aún" note and disabled state from Buscar input/button (satisfies BSB-I06 search part)

#### 3b — Enable Edit/Delete buttons under AuthorizeView
- Wrap Edit and Delete table buttons in `<AuthorizeView Roles="Administrador">` block
- Remove disabled state and "no disponible aún" tooltip from Edit and Delete (satisfies BSB-I06 auth part)

#### 3c — Three-panel mutual exclusion
- Declare: `private bool showEditForm = false`; `private IngredienteResponse? ingredienteEnEdicion = null`; `private string? editErrorMessage = null`
- Add method `MostrarEditar(IngredienteResponse i)`: sets `ingredienteEnEdicion = i`, `showEditForm = true`, closes detail and create panels
- Add method `CerrarEdicion()`: resets edit flags/state
- Wire Edit button `onclick` to `MostrarEditar(ingrediente)`

#### 3d — Inline edit form (Nombre only)
- Add edit form block gated on `showEditForm && ingredienteEnEdicion != null`
- Fields:
  - Nombre: text input, two-way bound to edit VM
  - UnidadBase: displayed as read-only disabled input for reference — NOT a form field, NOT in PUT body
- Guardar button: calls `GuardarEdicionIngrediente()` → builds `EditarIngredienteRequest(Nombre)` → calls `IngredienteService.UpdateAsync(id, req)` → on success: `BuscarIngredientes()` + `CerrarEdicion()`; on `ApiException`: set `editErrorMessage = ex.Message`
- Cancelar button: calls `CerrarEdicion()`
- Error display: `<p class="text-danger">@editErrorMessage</p>` visible when non-null (satisfies CUI-X01)

#### 3e — Delete with confirmation
- Delete button handler `EliminarIngrediente(Guid id)`:
  - `var ok = await JS.InvokeAsync<bool>("confirm", "¿Eliminar este ingrediente?")`
  - If `!ok` → return (no HTTP call)
  - Else → `await IngredienteService.DeleteAsync(id)` → on 204: `BuscarIngredientes()`; on `ApiException`: set `errorMessage = ex.Message`

4. Build: `dotnet build GastroGestionBlazor.sln`

Acceptance: build succeeds; Buscar/Edit/Delete wired; AuthorizeView present; edit form shows Nombre editable + UnidadBase read-only; UnidadBase absent from PUT body; window.confirm before delete; error surfacing visible.

Commit: `feat(ingredientes): enable search, inline edit form, soft-delete on Ingredientes page`

---

### [x] CUI-B-T04 — PR-B build verification

**Satisfies**: delivery gate
**Type**: Verification
**Parallel**: After T03

Steps:
1. Run `dotnet build GastroGestionBlazor.sln` from `C:\Users\Joaquin\OneDrive\Desktop\Desktop\GastroGestion\GastroGestionBlazor`
2. Confirm exit code 0 and 0 errors
3. Confirm 3 files changed: `EditarIngredienteRequest.cs` (new), `IngredienteService.cs` (modified), `Ingredientes.razor` (modified)

Commit: none (verification only)

---

## Task Execution Order

### PR-A (Clientes)

```
CUI-A-T01 (EditarClienteRequest contract)
    └── CUI-A-T02 (ClienteService methods)
            └── CUI-A-T03 (Clientes.razor wiring)
                    └── CUI-A-T04 (build verify)
```

### PR-B (Ingredientes)

```
CUI-B-T01 (EditarIngredienteRequest contract)
    └── CUI-B-T02 (IngredienteService methods)
            └── CUI-B-T03 (Ingredientes.razor wiring)
                    └── CUI-B-T04 (build verify)
```

Both chains are fully sequential within each PR. Both PR chains are fully parallel with each other (no shared files, no shared helper).

---

## Manual Smoke Checklist (post-apply, both PRs)

- [ ] Admin: search by name filters list
- [ ] Admin: click Edit — edit form opens, other panels close
- [ ] Admin: edit form pre-populates all fields
- [ ] Admin: submit edit — PUT fires, list refreshes, form closes
- [ ] Admin: 409 Cuit conflict displayed in edit error area (no form close)
- [ ] Admin: 422 domain violation displayed in edit error area
- [ ] Admin: click Delete — window.confirm appears
- [ ] Admin: cancel confirm — no HTTP call, list unchanged
- [ ] Admin: confirm delete — 204, row disappears
- [ ] Non-admin: Edit and Delete buttons NOT rendered (AuthorizeView)
- [ ] Non-admin: Buscar input and button visible and functional
- [ ] Clientes: NumeroCliente absent from PUT body (check DevTools Network)
- [ ] Ingredientes: UnidadBase absent from PUT body
- [ ] Ingredientes: UnidadBase visible as read-only in edit form

---

## File Index

| File | PR | Change type |
|------|----|-------------|
| `Contracts/Clientes/EditarClienteRequest.cs` | PR-A | Create |
| `Services/ClienteService.cs` | PR-A | Modify |
| `Pages/Clientes.razor` | PR-A | Modify |
| `Contracts/Ingredientes/EditarIngredienteRequest.cs` | PR-B | Create |
| `Services/IngredienteService.cs` | PR-B | Modify |
| `Pages/Ingredientes.razor` | PR-B | Modify |
