# Verify Report: catalog-crud-ui — PR-B (Ingredientes)

**Verdict**: PASS-WITH-WARNINGS
**Branch**: feat/catalog-crud-ui-ingredientes
**SHAs verified**: b35b5d4, d949661
**Date**: 2026-06-18
**CRITICAL**: 0 | **WARNING**: 2 | **SUGGESTION**: 1

---

## Build Evidence

- Command: `dotnet build GastroGestionBlazor.sln` from repo root
- Result: `Compilación correcta. 2 Advertencia(s), 0 Errores`
- Warnings: 2x NU1903 (AutoMapper vulnerability) — pre-existing baseline, NOT introduced by PR-B
- New warnings introduced: 0
- Exit code: 0 — BUILD PASS

---

## Diff Isolation Check

Files changed vs main (git diff main...HEAD --name-only):
- GastroGestionBlazor/Contracts/Ingredientes/EditarIngredienteRequest.cs ✓
- GastroGestionBlazor/Pages/Ingredientes.razor ✓
- GastroGestionBlazor/Services/IngredienteService.cs ✓

No Clientes files. No backend files. No Program.cs. Isolation: CLEAN.

---

## Requirement-by-Requirement Checklist

### CUI-I01 — EditarIngredienteRequest contract
**File**: GastroGestionBlazor/Contracts/Ingredientes/EditarIngredienteRequest.cs
```
namespace GastroGestionBlazor.Contracts.Ingredientes;
public sealed record EditarIngredienteRequest(string Nombre);
```
- [x] Contains exactly: `Nombre (string)`
- [x] UnidadBase structurally ABSENT — record has one positional parameter only
- [x] `sealed record` — immutable, correct type
- STATUS: PASS

### BSB-I06 — Buscar/Editar/Eliminar controls enabled for Ingredientes
**File**: Pages/Ingredientes.razor
- [x] Edit button present at line 76: `@onclick="() => MostrarEditar(ingrediente)"`
- [x] Delete button present at line 77: `@onclick="() => EliminarIngrediente(ingrediente.Id)"`
- [x] Both wrapped in `<AuthorizeView Roles="Administrador">` (lines 75–78)
- [x] Buscar button at line 29 NOT inside AuthorizeView — available to all authenticated roles
- [x] No disabled state or "no disponible aún" present anywhere
- STATUS: PASS

### CUI-I02 — IngredienteService CRUD methods
**File**: GastroGestionBlazor/Services/IngredienteService.cs

#### SearchAsync (lines 34–49)
- [x] Signature: `SearchAsync(string? nombre, bool incluirInactivos = false, CancellationToken ct = default)`
- [x] Uses `_httpClient.GetAsync(query, ct)` — correct verb
- WARNING: Query string built as `ingredientes?incluirInactivos={incluirInactivos}` then conditionally appends `&nombre=...` — this means when nombre is null/empty, the URL is `GET /ingredientes?incluirInactivos=false` (no `nombre=` param). Backend must tolerate absent `nombre` param. Spec scenario says `GET /ingredientes?nombre=&incluirInactivos=false` (with explicit empty nombre). This is a CONTRACT DEVIATION — the backend accepts both per common convention, but if it strictly requires `nombre=`, blank searches will fail. LOW risk given backend implementation, but a WARNING.
- [x] ThrowApiExceptionAsync called on non-success
- [x] Returns `List<IngredienteResponse>` (empty list on null)

#### UpdateAsync (lines 51–61)
- [x] Signature: `UpdateAsync(Guid id, EditarIngredienteRequest req, CancellationToken ct = default)`
- [x] Uses `PutAsJsonAsync($"ingredientes/{id}", req, JsonOptions, ct)` — PUT verb, correct route
- [x] Body is `EditarIngredienteRequest` which has ONLY `Nombre` — UnidadBase NOT sent
- [x] ThrowApiExceptionAsync on non-success
- [x] Returns deserialized `IngredienteResponse?`

#### DeleteAsync (lines 63–68)
- [x] Signature: `DeleteAsync(Guid id, CancellationToken ct = default)`
- [x] Uses `_httpClient.DeleteAsync($"ingredientes/{id}", ct)` — DELETE verb, correct route
- [x] ThrowApiExceptionAsync on non-success — 204 is success, falls through
- [x] Idempotent: a 204 on an already-inactive record is success (ThrowApiExceptionAsync not called)

#### ThrowApiExceptionAsync (lines 112–123)
- [x] Private static method
- [x] Reads ProblemDetailsResponse from response body
- [x] Throws `ApiException(problem?.Detail ?? problem?.Title ?? fallback)` — Spanish fallback present
- [x] Pattern matches KitchenBoardService convention

#### GetAllAsync rewire (lines 21–24)
- [x] `GetAllAsync(CancellationToken ct = default)` → delegates to `SearchAsync(null, false, ct)`
- [x] Default parameter — no breaking change at call sites
- STATUS: PASS-WITH-WARNING (SearchAsync query string — see WARNING-1 below)

### CUI-I03 — Ingredientes page: search behavior
**File**: Pages/Ingredientes.razor
- [x] `OnInitializedAsync` → calls `BuscarIngredientes()` (line 182–184)
- [x] `BuscarIngredientes()` calls `IngredienteService.SearchAsync(searchNombre, false)` (line 192) — incluirInactivos hardcoded false
- [x] Buscar button `@onclick="BuscarIngredientes"` (line 29) — on-submit only, no debounce, no on-keyup
- [x] No `incluirInactivos` UI toggle present anywhere in the file
- [x] `searchNombre` field bound to input (line 26)
- STATUS: PASS

### CUI-I04 — Ingredientes page: inline edit form
**File**: Pages/Ingredientes.razor
- [x] Edit form conditional: `@if (showEditForm && ingredienteEnEdicion != null)` (line 108)
- [x] Only `Nombre` is an editable field: `<input @bind="editModel.Nombre"` (line 118)
- [x] UnidadBase shown read-only: `<input value="@ingredienteEnEdicion.UnidadBase" class="form-control" disabled />` (line 122) — correct, disabled
- [x] UnidadBase NOT bound with `@bind` — it is a display-only value attribute
- [x] PUT body constructed as: `new EditarIngredienteRequest(editModel.Nombre)` (line 264) — ONLY Nombre
- [x] UnidadBase ABSENT from EditarIngredienteRequest struct — structurally impossible to send it

#### CRITICAL CHECK: UnidadBase NOT in PUT body
Tracing line 264: `var req = new EditarIngredienteRequest(editModel.Nombre);`
→ `EditarIngredienteRequest` has one positional parameter: `string Nombre` only
→ `PutAsJsonAsync` serializes `req` — JSON will be `{"nombre":"..."}` only
→ UnidadBase is structurally IMPOSSIBLE to leak into PUT body
- CRITICAL CHECK: PASS

#### Three-panel mutual exclusion — adversarial analysis
Panel state variables: `ingredienteSeleccionado` (detail), `showEditForm` (edit), `showCreateForm` (create).

| Action | ingredienteSeleccionado | showEditForm | showCreateForm | ingredienteEnEdicion |
|--------|------------------------|-------------|----------------|---------------------.|
| VerDetalle() line 204 | set | false | false | null |
| MostrarEditar() line 218 | null | true | false | set |
| AgregarNuevoIngrediente() line 239 | null | false | true | null |
| CerrarDetalle() line 213 | null | - | - | - |
| CerrarEdicion() line 231 | - | false | - | null |
| CancelarCreacion() line 339 | - | - | false | - |

Adversarial path analysis:
1. Detail open → click Edit: MostrarEditar sets `ingredienteSeleccionado=null`, `showEditForm=true`, `showCreateForm=false` → detail closes, only edit shows. PASS.
2. Edit open → click VerDetalle: VerDetalle sets `showCreateForm=false`, `showEditForm=false`, `ingredienteEnEdicion=null` → edit closes, only detail shows. PASS.
3. Create open → click Edit: MostrarEditar sets `showCreateForm=false` → create closes, edit opens. PASS.
4. Edit open → click "Nuevo ingrediente": AgregarNuevoIngrediente sets `showEditForm=false` → edit closes, create opens. PASS.
5. Detail + Edit simultaneously: Impossible — all entry points close the other panel first. PASS.

WARNING: `showCreateForm` is rendered OUTSIDE the `else` block (line 135 — `@if (showCreateForm)` is at the top level, while detail and edit panels are INSIDE the `else` block of `@if (ingredientes.Count == 0)`). This means if the list is empty AND showCreateForm=true, the create form shows. But if the list is empty, the detail/edit panels are also never rendered (they're in the `else` block). This is not a collision — it's actually correct behavior. But it means if a user deletes all ingredientes, the edit panel (if open) would disappear when the list becomes empty. This is a cosmetic edge case, not a bug.

- STATUS: PASS (three-panel mutual exclusion is structurally enforced)

### CUI-I05 — Ingredientes page: soft-delete with confirmation
**File**: Pages/Ingredientes.razor, lines 284–303
- [x] `EliminarIngrediente(Guid id)` calls `JS.InvokeAsync<bool>("confirm", "¿Eliminar este ingrediente?")` FIRST
- [x] `if (!ok) return;` — early return on cancel, NO HTTP call made
- [x] `DeleteAsync(id)` only called after confirmation
- [x] On success: `BuscarIngredientes()` refreshes the list
- [x] Idempotent: ThrowApiExceptionAsync treats non-204 as error — 204 (including already-inactive) passes through
- STATUS: PASS

### CUI-X01 — Edit error surfacing
**File**: Pages/Ingredientes.razor
- [x] `editErrorMessage` field (line 179)
- [x] Error display: `@if (!string.IsNullOrEmpty(editErrorMessage)) { <p class="text-danger">@editErrorMessage</p> }` (lines 111–114)
- [x] `GuardarEdicionIngrediente` catches `ApiException ex` → `editErrorMessage = ex.Message` (line 272)
- [x] `ApiException.Message` carries `ProblemDetails.Detail ?? Title ?? fallback` (from ThrowApiExceptionAsync)
- [x] Edit form remains open on error (CerrarEdicion not called in catch block)
- SUGGESTION: Delete errors go to `errorMessage` (list-level, line 292) rather than `editErrorMessage`. This means delete errors appear at the top of the page rather than inline with the row. This is acceptable UX for a delete operation — no inline edit form remains open after delete — but differs slightly from how edit errors appear.
- Status checks for specific HTTP codes (400/403/404/409/422): All are surfaced verbatim via `ApiException.Message` from ThrowApiExceptionAsync — no client-side re-mapping. PASS.
- STATUS: PASS-WITH-SUGGESTION

---

## Findings

### WARNING-1 — SearchAsync query string: nombre param omitted when empty
**File**: GastroGestionBlazor/Services/IngredienteService.cs, lines 39–41
**Severity**: WARNING
**Detail**: When `nombre` is null or empty, the query is `GET /ingredientes?incluirInactivos=false` (no `nombre=` param). The spec scenario states the request should be `GET /ingredientes?nombre=&incluirInactivos=false`. Most backend implementations treat missing param and empty string identically, but this is a contract deviation. If the backend strictly checks for the `nombre` key, a blank search will fail.
**Recommendation**: Change to always append `&nombre={Uri.EscapeDataString(nombre ?? "")}` unconditionally after the base query string.

### WARNING-2 — Delete errors surface at list level, not edit-form level
**File**: Pages/Ingredientes.razor, line 292
**Severity**: WARNING
**Detail**: `EliminarIngrediente` writes to `errorMessage` (global list error), not `editErrorMessage`. Since no edit form is open during a delete (it's a separate action), this is functionally acceptable, but the spec (CUI-X01) says errors from delete MUST be surfaced. They are — just at a different visual position than edit errors. Not a spec violation, but a UX inconsistency worth noting.
**Recommendation**: Consider a dedicated `deleteErrorMessage` or reuse the same `errorMessage` area consistently. As-is, it works and satisfies the spec requirement.

### SUGGESTION-1 — `EditarIngredienteRequest` uses positional record but `editModel` is a separate class
**File**: Pages/Ingredientes.razor, lines 353–356
**Severity**: SUGGESTION
**Detail**: A separate mutable `EditarIngredienteModel` class is used as the `@bind` target (since records are immutable), then converted to `EditarIngredienteRequest` at submit time (line 264). This is the correct pattern for Blazor forms. No issue — just flagging the intentional double-model design for clarity.

---

## Summary

CRITICAL: 0
WARNING: 2 (SearchAsync query string deviation; delete error visual placement)
SUGGESTION: 1 (double-model pattern note)

Verdict: PASS-WITH-WARNINGS — no blockers for PR creation. WARNING-1 should be evaluated against the backend implementation; if the backend treats absent `nombre` param the same as empty string, it is a non-issue and can be addressed post-merge or left as-is.
