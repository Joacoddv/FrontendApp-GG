# Design: catalog-crud-ui

## Technical Approach

Pure frontend wiring against already-merged backend CRUD endpoints. Each page (Clientes, Ingredientes) gains search, inline edit, and delete — following the existing inline detail/create panel pattern, no modal framework. Delivered as two independent, stacked-to-main PRs (PR-A Clientes, PR-B Ingredientes). The two entities share a pattern, not code, so each PR depends only on `main`.

## Architecture Decisions

### Decision: Per-service inline error helper (no shared extraction)
**Choice**: Add a `private static ThrowApiExceptionAsync(response, fallback, ct)` to ClienteService and IngredienteService, copied verbatim from KitchenBoardService.
**Alternatives considered**: Extract a shared `Services/HttpExtensions.cs`.
**Rationale**: The existing `ThrowApiExceptionAsync` is already a `private static` per-service helper in KitchenBoardService — duplication is the established codebase convention, not a smell to fix here. Extracting a shared file would create a third touched file that BOTH PRs depend on, breaking stacked-to-main independence (PR-B would need PR-A merged first). Duplication keeps each PR self-contained. The pre-existing inline `JsonOptions` (with `JsonStringEnumConverter`) is reused — no new options object.

### Decision: GetAllAsync kept as thin wrapper over SearchAsync
**Choice**: Keep `GetAllAsync()` signature; reimplement its body to call `SearchAsync(null, false, ct)`. Pages' initial `Cargar*` keeps calling the same method.
**Alternatives considered**: Rename to SearchAsync at all call sites; delete GetAllAsync.
**Rationale**: `GET /clientes` already routes to BuscarClientesHandler with default params, so `GetAllAsync()` and `SearchAsync(null, false)` are behaviorally identical (zero-risk). Keeping the wrapper minimizes the page diff and avoids touching unrelated call sites.

### Decision: Three-panel mutual exclusion via explicit toggle methods
**Choice**: One open-panel action resets the other two flags. Methods: `MostrarDetalle(item)`, `MostrarCrear()`, `MostrarEditar(item)`, `CerrarPaneles()`. Flags: `clienteSeleccionado` (detail), `showCreateForm` (create), `showEditForm` (edit).
**Alternatives considered**: Single enum `PanelActivo { None, Detail, Create, Edit }`.
**Rationale**: The page already uses separate bool/object flags for detail+create; adding `showEditForm` plus reset-on-open keeps the existing idiom and the smallest diff. Each open method sets its own state and clears the other two.

### Decision: AuthorizeView wrapper for Edit/Delete; window.confirm for delete
**Choice**: Wrap Edit + Delete buttons in `<AuthorizeView Roles="Administrador">`. Delete handler calls `await JS.InvokeAsync<bool>("confirm", msg)` before `DeleteAsync`. `@inject IJSRuntime JS`.
**Rationale**: Pattern B (AuthorizeView) is already used in NavMenu; page stays open to all auth roles for view/search; backend 403 is the second layer. No modal exists in the codebase — window.confirm is the lightest confirmation.

## Data Flow

    [Buscar click] -> Buscar*() -> Service.SearchAsync(nombre,false) -> GET ?nombre= -> list rebind
    [Edit click]   -> MostrarEditar(row) -> edit VM populated -> [Guardar] -> UpdateAsync(id,req) -> PUT -> reload
    [Delete click] -> JS.confirm -> DeleteAsync(id) -> DELETE 204 -> reload
    ApiException (4xx ProblemDetails) -> editErrorMessage/errorMessage -> <p class="text-danger">

## File Changes

### PR-A — Clientes (depends on main only)
| File | Action | Description |
|------|--------|-------------|
| `Contracts/Clientes/EditarClienteRequest.cs` | Create | record `(string Nombre, CondicionIVA CondicionIVA, string? Cuit, string? Email)` |
| `Services/ClienteService.cs` | Modify | Add `SearchAsync`, `UpdateAsync`, `DeleteAsync`, `ThrowApiExceptionAsync`; rewire `GetAllAsync` body |
| `Pages/Clientes.razor` | Modify | Enable search; AuthorizeView Edit/Delete; inline edit form; edit state + Buscar/Editar/Eliminar handlers; `@inject IJSRuntime JS` |

### PR-B — Ingredientes (depends on main only, independent of PR-A)
| File | Action | Description |
|------|--------|-------------|
| `Contracts/Ingredientes/EditarIngredienteRequest.cs` | Create | record `(string Nombre)` — Nombre ONLY |
| `Services/IngredienteService.cs` | Modify | Add `SearchAsync`, `UpdateAsync`, `DeleteAsync`, `ThrowApiExceptionAsync`; rewire `GetAllAsync` body |
| `Pages/Ingredientes.razor` | Modify | Same as Clientes; edit form = Nombre only; UnidadBase read-only display (NOT in PUT body) |

**Merge sequencing**: PR-A and PR-B are mutually independent (no shared file). Either may merge first. Recommended order PR-A then PR-B for review cadence, but neither blocks the other. No cross-PR coupling because the helper is duplicated, not shared.

## Interfaces / Contracts

```csharp
// PR-A: Contracts/Clientes/EditarClienteRequest.cs
public record EditarClienteRequest(string Nombre, CondicionIVA CondicionIVA, string? Cuit, string? Email);

// PR-B: Contracts/Ingredientes/EditarIngredienteRequest.cs
public record EditarIngredienteRequest(string Nombre);
```

Field casing: backend uses `JsonSerializerDefaults.Web` (case-insensitive read) and the services serialize with `JsonStringEnumConverter`, so `CondicionIVA` is sent as its string name (`"ResponsableInscripto"` etc.) — matching the backend string-enum PUT body. PascalCase property names serialize to camelCase; backend reads case-insensitively. No casing change needed.

Service method signatures (both services):
```csharp
Task<List<TResponse>> SearchAsync(string? nombre, bool incluirInactivos = false, CancellationToken ct = default); // GET ?nombre=&incluirInactivos=
Task<TResponse?>       UpdateAsync(Guid id, TEditarRequest request, CancellationToken ct = default);              // PUT /{entity}/{id} -> 200 Response
Task                   DeleteAsync(Guid id, CancellationToken ct = default);                                       // DELETE /{entity}/{id} -> 204 (idempotent)
```
`SearchAsync` builds query as `clientes?nombre={Uri.EscapeDataString(nombre)}&incluirInactivos=false`; omit `nombre` param when null/empty. `DeleteAsync` treats 204 as success; already-inactive returns 204 (idempotent) — no special handling.

CondicionIVA enum (confirmed): `ResponsableInscripto, Monotributista, ConsumidorFinal, ExentoIVA` — render as a `<select>` over these four values in the Cliente edit form.

## Field Rules
- **Cliente edit form**: Nombre (text), CondicionIVA (select of 4 enum values), Cuit (text, nullable), Email (text, nullable). NumeroCliente shown read-only (disabled), never in PUT body.
- **Ingrediente edit form**: Nombre (text) only. UnidadBase shown read-only (disabled input/display), absent from PUT body.

## DI / Registration
Confirmed in `Program.cs` lines 53-54: `ClienteService` and `IngredienteService` already registered as scoped. **No Program.cs change.**

## Testing Strategy
| Layer | What to Test | Approach |
|-------|-------------|----------|
| Manual | Search/edit/delete as Administrador; list/search as non-admin; Edit/Delete hidden for non-admin | Manual smoke per page |
| Manual | Error surfacing (409 Cuit conflict, 400 empty Nombre, 422 RI-needs-Cuit) | Trigger via UI, confirm inline text-danger |
| Manual | Three-panel mutual exclusion; delete confirm cancel/accept | Click-through |

No automated tests (out of scope per proposal).

## Migration / Rollout
No migration required. Pure additive frontend wiring; no schema, no backend change.

## Open Questions
None — all backend contracts, enum values, DI, and the helper-sharing decision are resolved against source.
