# Design: asignar-cocinero-ui (FRONTEND repo GastroGestionBlazor)

## Executive Summary

Clone the existing `marcar-lista` interaction end-to-end (contract + service method + per-card
in-flight button + SignalR-echo refresh) to add a per-card cook picker on `Creada` cards in
`Pages/Cocina.razor`. No net-new architecture: every layer mirrors a verified existing pattern.
The only genuine new element is a `Dictionary<Guid,Guid>` per-OT picker selection and a JSON
request body on the POST (marcar-lista posts a null body; assign posts `{ cocineroLegajoId }`).

## Architecture Approach

- **Pattern**: Clone-an-existing-pattern. The kitchen board already implements the
  command → service → SignalR-echo → board-refresh loop for `marcar-lista`. Cook assignment is
  structurally identical: a per-card command whose success is observed only through the
  `OtChanged` echo (the card moves `Creada` → `Preparandose` on its own).
- **Layering / boundaries**: unchanged. UI (`Cocina.razor`) → `KitchenBoardService` (HTTP) →
  backend. No new service class — `GetCocinerosAsync` lives on `KitchenBoardService` (cohesive
  with the kitchen domain; avoids a single-method `UsuarioService`). One new contract record.
- **State ownership**: board state remains owned solely by SignalR echo. No optimistic mutation.
  Picker selection and in-flight tracking are transient UI state local to the page.

## Components

### 1. New contract — `Contracts/Usuarios/CocineroResponse.cs` (NEW FILE)

```csharp
namespace GastroGestionBlazor.Contracts.Usuarios;

public sealed record CocineroResponse(Guid Id, string NombreCompleto);
```

- Mirrors backend `record CocineroResponse(Guid Id, string NombreCompleto)`
  (`src/GastroGestion.Contracts/Usuarios/UsuarioResponses.cs:6`).
- Style follows `Contracts/Clientes/ClienteResponse.cs`: `public sealed record`, file-scoped
  namespace `GastroGestionBlazor.Contracts.{Area}`. New folder `Contracts/Usuarios/` is created
  (does not exist yet — confirmed via Glob).
- **JSON casing — RESOLVED**: `KitchenBoardService.JsonOptions` is built from
  `JsonSerializerDefaults.Web` (`KitchenBoardService.cs:11`), so deserialization is
  case-insensitive and the camelCase wire payload (`{ "id", "nombreCompleto" }`) maps cleanly to
  the PascalCase record. No `[JsonPropertyName]` attributes needed — follows existing pattern
  (`OrdenTrabajoBoardItem` deserializes the same way).
- `Id` is the value sent as `cocineroLegajoId` in the assign POST; `NombreCompleto` is the
  display label in the `<select>`.

### 2. Service methods — `Services/KitchenBoardService.cs`

**`GetCocinerosAsync`** — clone of `GetBoardAsync` (`KitchenBoardService.cs:21-31`):

```csharp
public async Task<List<CocineroResponse>> GetCocinerosAsync(CancellationToken ct = default)
{
    var response = await _httpClient.GetAsync("usuarios/cocineros", ct);
    if (!response.IsSuccessStatusCode)
    {
        await ThrowApiExceptionAsync(response, "No se pudo cargar la lista de cocineros.", ct);
    }

    var result = await response.Content.ReadFromJsonAsync<List<CocineroResponse>>(JsonOptions, ct);
    return result ?? new List<CocineroResponse>();
}
```

- Follows existing GET pattern: relative route (no leading slash — base address handles it,
  same as `"ordenes-trabajo"`), `JsonOptions`, `ThrowApiExceptionAsync` with Spanish fallback,
  null-coalesce to empty list.
- Requires `using GastroGestionBlazor.Contracts.Usuarios;` added at top of file.

**`AsignarCocineroAsync`** — clone of `MarcarListaAsync` (`KitchenBoardService.cs:33-44`) but with
a JSON body:

```csharp
public async Task AsignarCocineroAsync(
    Guid pedidoId, Guid otId, Guid cocineroLegajoId, CancellationToken ct = default)
{
    var response = await _httpClient.PostAsJsonAsync(
        $"pedidos/{pedidoId}/ordenes-trabajo/{otId}/asignar-cocinero",
        new { cocineroLegajoId },
        JsonOptions,
        ct);

    if (!response.IsSuccessStatusCode)
    {
        await ThrowApiExceptionAsync(response, "No se pudo asignar el cocinero.", ct);
    }
}
```

- **POST, not PATCH — RESOLVED**: backend route is `MapPost`
  (`OrdenTrabajoEndpoints.cs:43-65`). Design uses POST. The exploration noted a PATCH-vs-POST
  discrepancy elsewhere; the implemented/merged route is POST, which is authoritative.
- **Body serialization — RESOLVED**: `PostAsJsonAsync` with `JsonOptions`
  (`JsonSerializerDefaults.Web`) serializes the anonymous object property `cocineroLegajoId`
  to camelCase `"cocineroLegajoId"`, exactly matching the backend `AsignarCocineroRequest`
  property that FluentValidation reads. The C# anonymous-object member name is already
  `cocineroLegajoId` (lower camel via the local parameter), and Web defaults would camelCase it
  regardless.
- Differs from `MarcarListaAsync` only by passing a body (marcar-lista uses `content: null`).
  Uses `System.Net.Http.Json.PostAsJsonAsync` — `using System.Net.Http.Json;` already present
  (`KitchenBoardService.cs:3`).
- 200 body (`OrdenTrabajoResponse`) is intentionally discarded — board refresh comes via echo.

### 3. UI — `Pages/Cocina.razor`

**Load cocineros (once, on init)** — in `OnInitializedAsync` alongside the board load
(`Cocina.razor:143-164`), inside the existing try block after `GetBoardAsync`:

```csharp
_items = await BoardService.GetBoardAsync();
_cocineros = await BoardService.GetCocinerosAsync();
```

- New field: `private List<CocineroResponse> _cocineros = new();`
- Loaded once (MVP). Stale-list risk accepted per proposal (could refresh in `ReHydrateAsync`
  later — explicitly out of scope now).
- Requires `@using GastroGestionBlazor.Contracts.Usuarios` directive.

**Picker state model**:

```csharp
private Dictionary<Guid, Guid> _pickerSelection = new();  // keyed by OtId → selected cocinero Id
private HashSet<Guid> _assigning = new();                 // in-flight OtIds (clone of _marking)
```

- `_pickerSelection` keyed by `OtId` per the exploration; isolates each card's selection without
  child-component extraction.
- `_assigning` is the structural clone of `_marking` (`Cocina.razor:135`). Named `_assigning`
  per the proposal (authoritative over the exploration's generic "clone of `_marking`").

**Markup — placed in the `Creada` column card body** (`Cocina.razor:58-67`, after the existing
`CocineroAsignadoLegajoId` display block, inside the same `card-body`):

```razor
<div class="mt-2">
    <select class="form-select form-select-sm mb-1"
            @bind="_pickerSelection[item.OtId]"
            disabled="@_assigning.Contains(item.OtId)">
        <option value="@Guid.Empty">Seleccionar cocinero&hellip;</option>
        @foreach (var c in _cocineros)
        {
            <option value="@c.Id">@c.NombreCompleto</option>
        }
    </select>
    <button class="btn btn-primary btn-sm"
            @onclick="() => OnAsignarCocinero(item)"
            disabled="@IsAsignarDisabled(item)">
        @(_assigning.Contains(item.OtId) ? "Asignando..." : "Asignar")
    </button>
</div>
```

- The `<select>` two-way binds to `_pickerSelection[item.OtId]`. The dictionary entry is
  lazily initialized in the handler/guard (see below) so binding always has a key.
- Disabled-conditions helper keeps the markup readable:

```csharp
private bool IsAsignarDisabled(OrdenTrabajoBoardItem item) =>
    _assigning.Contains(item.OtId)                                   // in-flight
    || _cocineros.Count == 0                                          // no cooks to assign
    || !_pickerSelection.TryGetValue(item.OtId, out var sel)         // nothing selected
    || sel == Guid.Empty;                                            // placeholder selected
```

**Handler `OnAsignarCocinero`** — clone of `OnMarcarLista` (`Cocina.razor:224-247`):

```csharp
private async Task OnAsignarCocinero(OrdenTrabajoBoardItem item)
{
    if (_assigning.Contains(item.OtId))
        return;

    if (!_pickerSelection.TryGetValue(item.OtId, out var cocineroId) || cocineroId == Guid.Empty)
        return;  // guard: never POST an empty guid (would 400)

    _assigning.Add(item.OtId);
    _errorMessage = null;

    try
    {
        await BoardService.AsignarCocineroAsync(item.PedidoId, item.OtId, cocineroId);
        // Echo-driven: button stays disabled until OtChanged echo arrives and removes from _assigning.
    }
    catch (ApiException ex)
    {
        _errorMessage = ex.Message;
        _assigning.Remove(item.OtId);
    }
    catch (Exception ex)
    {
        _errorMessage = ex.Message;
        _assigning.Remove(item.OtId);
    }
}
```

- Identical control flow to `OnMarcarLista`: add to in-flight set, clear error, call service, on
  error set `_errorMessage` and remove from in-flight; on success do NOT mutate state (echo
  handles it). Adds the empty-guid guard before the call as a second line of defence behind the
  disabled button.

**Success path — extend `OnOtChanged`** (`Cocina.razor:176-192`): the echo handler currently
removes only from `_marking` (line 188). Add the parallel cleanup for assign:

```csharp
_marking.Remove(item.OtId);
_assigning.Remove(item.OtId);   // NEW: echo arrived — unblock the Asignar button for this OT
```

- This is the single mutation to existing code in the echo handler. When the OT echoes back as
  `Preparandose`, the card moves columns (existing `RemoveAll` + `Add`), and `_assigning` is
  cleared so a re-rendered card (if any) is interactive again. NO manual board mutation in the
  handler — relies entirely on the existing echo logic.

### 4. Error surfacing

- Reuses the existing `_errorMessage` + `alert-danger` block (`Cocina.razor:33-39`) and
  `ThrowApiExceptionAsync` (`KitchenBoardService.cs:50-61`).
- Backend returns Spanish RFC 7807 `ProblemDetails.detail` for domain errors;
  `ThrowApiExceptionAsync` surfaces `Detail ?? Title ?? fallback`. Status mapping (all surfaced
  via the same path, no per-status branching needed):
  - **403** wrong role → ProblemDetails detail (page is already role-gated, so practically rare).
  - **404** pedido not found → ProblemDetails detail.
  - **422** OT not in `Creada` (e.g. concurrent assign race — loser) → Spanish detail.
  - **400** empty `cocineroLegajoId` → guarded out by disabled button + handler guard, but if it
    reaches the server the detail is still surfaced.

### 5. DI — RESOLVED, no change

`KitchenBoardService` is already registered scoped with the `AuthorizedApi` HttpClient
(`Program.cs:55`). Both new methods live on that service. No `Program.cs` change.

## Data Flow

```
OnInitializedAsync
  ├─ GetBoardAsync()      → _items        (existing)
  └─ GetCocinerosAsync()  → _cocineros    (new, once)

User picks cook → _pickerSelection[OtId] = cocineroId
User clicks Asignar
  └─ OnAsignarCocinero(item)
       ├─ guard: in-flight? empty selection? → no-op
       ├─ _assigning.Add(OtId); _errorMessage = null
       └─ AsignarCocineroAsync(PedidoId, OtId, cocineroId)
            └─ POST pedidos/{pedidoId}/ordenes-trabajo/{otId}/asignar-cocinero  body { cocineroLegajoId }
                 ├─ success (200, body discarded)
                 │     └─ backend commits → NotifyOtChangedAsync → SignalR OtChanged
                 │          └─ OnOtChanged(item: Preparandose)
                 │               ├─ _items.RemoveAll(OtId) + Add(item)   (card moves column)
                 │               └─ _assigning.Remove(OtId)              (button unblocked)
                 └─ non-success → ThrowApiExceptionAsync → ApiException
                      └─ catch → _errorMessage = ex.Message; _assigning.Remove(OtId)
```

## Integration Points

- **Backend GET** `/usuarios/cocineros` (`UsuarioEndpoints.cs:21-39`) — bearer JWT + role.
- **Backend POST** `/pedidos/{pedidoId:guid}/ordenes-trabalho/{otId:guid}/asignar-cocinero`
  (`OrdenTrabajoEndpoints.cs:43-65`) — bearer JWT + role; body `{ cocineroLegajoId }`.
- **SignalR** `OtChanged` echo (existing `KitchenRealtimeConnection` wired in
  `OnInitializedAsync`) — drives the board refresh.
- **HttpClient** `AuthorizedApi` with `BearerTokenHandler` (existing DI).

## ADR-style Decisions

### ADR-1: Method placement on KitchenBoardService (no new UsuarioService)
- **Decision**: add `GetCocinerosAsync` to `KitchenBoardService` rather than create a
  `UsuarioService`.
- **Rationale**: single endpoint, consumed only by the kitchen board, cohesive with kitchen
  domain. A dedicated service for one read would add DI wiring and a file for no gain.
- **Rejected**: standalone `UsuarioService` — premature; revisit if usuarios endpoints grow.

### ADR-2: Inline per-card `<select>` + button (no child OtCard component)
- **Decision**: inline markup in the `Creada` column, mirroring the marcar-lista button.
- **Rationale**: exact clone of the existing pattern; touches the fewest files; page stays small.
- **Rejected**: extract an `OtCard` child component with `EventCallback` — adds files,
  event-callback plumbing, and state-lifting for no current benefit (per exploration comparison).

### ADR-3: Echo-driven refresh, no optimistic mutation
- **Decision**: on success, mutate nothing; let the `OtChanged` echo move the card.
- **Rationale**: identical to marcar-lista; single source of truth for board state; avoids
  divergence between local guess and server truth.
- **Rejected**: optimistic local move to `Preparandose` — risks desync if the server rejects, and
  duplicates the echo's job.
- **Accepted risk**: if the echo never arrives (SignalR drop), the button stays disabled until
  reconnect/rehydrate — same exposure as the existing marcar-lista action.

### ADR-4: POST with JSON body via PostAsJsonAsync (not null-body POST, not PATCH)
- **Decision**: use `PostAsJsonAsync(route, new { cocineroLegajoId }, JsonOptions, ct)`.
- **Rationale**: backend route is `MapPost` and expects a body the FluentValidator reads.
  `JsonSerializerDefaults.Web` produces the camelCase `cocineroLegajoId` the backend expects.
- **Rejected**: PATCH (route is POST); null-body POST (marcar-lista's shape — assign needs a body).

### ADR-5: `_pickerSelection` as `Dictionary<Guid,Guid>` keyed by OtId
- **Decision**: per-OT selection in a dictionary keyed by `OtId`.
- **Rationale**: isolates each card's `<select>` value without child components; idiomatic Blazor.
- **Rejected**: a single shared "selected cocinero" field — would bleed selection across cards.

## Risks / Assumptions

- **Echo dependency**: success is only observable via `OtChanged`. If the hub drops between POST
  and echo, the card's button stays disabled until rehydrate. Mitigation deferred (same as
  marcar-lista). Assumption: SignalR reliability is acceptable for MVP.
- **Contract drift**: `CocineroResponse` must stay aligned with the backend record. Web-defaults
  case-insensitivity reduces fragility but a renamed backend field would silently null the value.
- **Stale cocinero list**: loaded once on init; new cooks added later are invisible until refresh.
  Accepted for MVP.
- **422 on concurrent assign**: the losing assigner gets the Spanish ProblemDetails detail via the
  existing surface — no special handling required.

## Files

- `GastroGestionBlazor/Contracts/Usuarios/CocineroResponse.cs` (NEW)
- `GastroGestionBlazor/Services/KitchenBoardService.cs` (+`GetCocinerosAsync`, +`AsignarCocineroAsync`, +`using ...Contracts.Usuarios;`)
- `GastroGestionBlazor/Pages/Cocina.razor` (+`@using ...Contracts.Usuarios`, +`_cocineros`, +`_pickerSelection`, +`_assigning`, +`IsAsignarDisabled`, +`OnAsignarCocinero`, picker markup in Creada column, +`_assigning.Remove` in `OnOtChanged`, load cocineros in `OnInitializedAsync`)
- `Program.cs` — NO change (DI already in place)
