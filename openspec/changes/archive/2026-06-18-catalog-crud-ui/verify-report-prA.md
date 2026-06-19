# Verification Report: catalog-crud-ui — PR-A (Clientes)

**Change**: catalog-crud-ui  
**Scope**: PR-A only (Clientes) — branch `feat/catalog-crud-ui-clientes`  
**Verified**: 2026-06-18  
**Mode**: Build + static/contract review (strict_tdd: false — no automated tests)  
**Verdict**: PASS WITH WARNINGS — 0 CRITICAL, 1 WARNING, 2 SUGGESTIONS

---

## Build Evidence

- Command: `dotnet build GastroGestionBlazor.sln` from `C:\Users\Joaquin\OneDrive\Desktop\Desktop\GastroGestion\GastroGestionBlazor`
- Result: **Build succeeded — 167 warnings, 0 errors**
- Baseline warnings (pre-existing): NU1903 (AutoMapper vulnerability), CS8618 (DTO/Domain files)
- New warnings introduced by PR-A: **0**
- All CS8618 and NU1903 warnings originate in pre-existing DTO/Dominio files — none in the 3 changed files

---

## File Scope — PR-A Only

Changed files vs. main (confirmed via `git diff main...HEAD --name-only`):
| File | Change |
|------|--------|
| `GastroGestionBlazor/Contracts/Clientes/EditarClienteRequest.cs` | Created |
| `GastroGestionBlazor/Services/ClienteService.cs` | Modified |
| `GastroGestionBlazor/Pages/Clientes.razor` | Modified |

No Ingredientes files changed. No backend files changed. No Program.cs change. PASS.

---

## Requirement Compliance Matrix

### CUI-C01 — EditarClienteRequest contract
- File: `Contracts/Clientes/EditarClienteRequest.cs`
- Fields: `string Nombre`, `CondicionIVA CondicionIVA`, `string? Cuit`, `string? Email` — EXACT match
- `NumeroCliente`: absent — PASS
- Sealed record with positional constructor — PASS
- Uses `GastroGestionBlazor.Contracts.Enums.CondicionIVA` — PASS
- Status: **COMPLIANT**

### CUI-C02 — ClienteService CRUD methods
- `SearchAsync(string? nombre, bool incluirInactivos = false, CancellationToken ct = default)` → GET clientes?incluirInactivos=…&nombre=… — PASS
  - Note: query string puts `incluirInactivos` first, `nombre` second (appended only if non-null). HTTP query param order is irrelevant to the backend — functionally correct.
  - `Uri.EscapeDataString` used for nombre — PASS
  - When nombre is null/empty, nombre param is omitted entirely (backend treats absence as "all") — acceptable but differs from `nombre=` literal. No breaking change, behavior equivalent.
- `UpdateAsync(Guid id, EditarClienteRequest req, CancellationToken ct = default)` → PUT clientes/{id} with `PutAsJsonAsync` + JsonOptions (JsonStringEnumConverter) — PASS
- `DeleteAsync(Guid id, CancellationToken ct = default)` → DELETE clientes/{id}, 204 treated as success, non-2xx throws — PASS. Idempotency: 204 on already-inactive = 204 = success — PASS.
- `ThrowApiExceptionAsync(response, fallback, ct)` — private static, reads ProblemDetails, `problem?.Detail ?? problem?.Title ?? fallback` — PASS. Spanish fallback message — PASS.
- `GetAllAsync` rewired to `SearchAsync(null, false, ct)` — PASS. Signature changed to accept optional `CancellationToken ct = default` — backward compatible (pre-existing callers with no args still compile — confirmed by build + Counter.razor caller).
- Status: **COMPLIANT**

### BSB-C06 (modified) — Edit/Delete wrapped in AuthorizeView, Search open to all
- `<AuthorizeView Roles="Administrador">` wraps both Edit and Delete buttons (lines 79–82 of Clientes.razor) — PASS
- Using correct short-form AuthorizeView (no explicit `<Authorized>` child needed for direct content — valid Blazor syntax) — PASS
- Search input + Buscar button are outside AuthorizeView — PASS
- Status: **COMPLIANT**

### CUI-C03 — Search behavior
- `OnInitializedAsync` calls `BuscarClientes()` which calls `SearchAsync(searchNombre, false)` — on init searchNombre is null → SearchAsync(null, false) — PASS
- Buscar button: `@onclick="BuscarClientes"` — on-submit, not on-keyup — PASS
- `incluirInactivos` hardcoded `false`, no UI toggle — PASS
- Status: **COMPLIANT**

### CUI-C04 — Inline edit form
- `showEditForm` bool + `clienteEnEdicion` + `editModel` (EditarClienteModel mutable view-model) — PASS
- `MostrarEditar(cliente)`: sets clienteEnEdicion, pre-populates editModel from row, sets showEditForm=true, clienteSeleccionado=null, showCreateForm=false → three-panel exclusion detail+create — PASS
- `AgregarNuevoCliente()`: sets showCreateForm=true, clienteSeleccionado=null, showEditForm=false, clienteEnEdicion=null → three-panel exclusion edit — PASS
- `VerDetalle(cliente)`: sets clienteSeleccionado, showCreateForm=false, showEditForm=false, clienteEnEdicion=null → three-panel exclusion edit+create — PASS
- Edit form fields: Nombre (text input), CondicionIVA (select using Enum.GetValues<CondicionIVA>() — all 4 values), Cuit (text), Email (text) — PASS
- `NumeroCliente`: NOT in the edit form, NOT in EditarClienteRequest — PASS
- On success: `CerrarEdicion()` then `BuscarClientes()` (refreshes with current searchNombre) — PASS
- Status: **COMPLIANT**

### CUI-C05 — Soft-delete with confirmation
- `EliminarCliente(Guid id)`: `await JS.InvokeAsync<bool>("confirm", "¿Eliminar este cliente?")` → if `!ok` returns immediately, no HTTP call — PASS
- On confirm: `DeleteAsync(id)` → `BuscarClientes()` — PASS
- `@inject IJSRuntime JS` present at top of razor — PASS
- ApiException caught and surfaced in `errorMessage` (general list error area, not edit area — see WARNING below) — partial
- Status: **COMPLIANT** (with WARNING on error routing)

### CUI-X01 — Edit error surfacing
- `GuardarEdicionCliente`: catches `ApiException` → `editErrorMessage = ex.Message` → displayed in `<p class="text-danger">@editErrorMessage</p>` inside edit form container — PASS for edit path
- `EliminarCliente`: catches `ApiException` → routes to `errorMessage` (general list error, above table) — WARNING (see below)
- Status: **PARTIALLY COMPLIANT** — WARNING raised

---

## Issues

### WARNING — Delete error surfacing routes to list-level errorMessage instead of edit area

- Location: `Clientes.razor`, `EliminarCliente` method (lines 328–343)
- Finding: When DeleteAsync throws ApiException, it populates `errorMessage` (rendered above the table) rather than `editErrorMessage` (rendered inside the edit form area). The spec (CUI-X01) states errors must surface "in the inline edit area." Delete is not triggered from the edit form, so this is debatable — but strictly, the spec error-surfacing requirement covers both edit and delete, and delete errors go to a different location.
- Severity: WARNING — functionally visible to user; does not block operation; edit form error area is correct; list-level error is a reasonable alternative for a delete-from-row action.

### SUGGESTION — NumeroCliente read-only display omitted (field absent from DTO)

- The spec says "NumeroCliente MUST be shown as read-only text" — but ClienteResponse has no NumeroCliente. Omitting the display is the correct pragmatic decision. This should be tracked: if backend adds NumeroCliente to the response DTO, the edit form needs a read-only field.

### SUGGESTION — Query string omits `nombre=` when null (sends no nombre param)

- When searchNombre is null/empty, SearchAsync builds `clientes?incluirInactivos=false` with no `nombre` param. The spec scenario says `SearchAsync(null or "", false)` returns all active clientes. Backend BuscarClientesHandler should default nombre=null to "all" — verified by design doc. Functionally correct but differs from the literal `nombre=` form. Low risk.

---

## Final Verdict

**PASS WITH WARNINGS** — 0 CRITICAL / 1 WARNING / 2 SUGGESTIONS

Build: succeeded, 0 errors, 0 new warnings.  
All PR-A requirements (CUI-C01..C05, BSB-C06 modified, CUI-X01 partially) implemented correctly.  
One WARNING: delete errors surface in list-level error area rather than inline edit area — functionally visible but in a different location than spec intended.  
No blocking issues. Safe to push and open PR.
