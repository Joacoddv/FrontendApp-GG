# Design: Blazor Slice B — Re-point Cliente + Ingrediente at .NET 8 backend (Option A)

Phase 7, Slice B. Scope = OPTION A: list + create + view only. Buscar / Editar / Eliminar are disabled in the UI with a "no disponible aún" note (backend does not expose these endpoints). This is a re-pointing change, not a feature expansion.

## Technical Approach

Re-point the existing Blazor WASM (`net8.0`) Cliente and Ingrediente screens at the live .NET 8 backend REST contracts. Reuse the Slice A infrastructure as-is: the authenticated named `AuthorizedApi` HttpClient (BaseAddress = `ApiBaseUrl` from `wwwroot/appsettings.json`, `BearerTokenHandler` attached) is already injected as the default `HttpClient` for `ClienteService` and `IngredienteService` via the factory registration in `Program.cs`. Services therefore use **relative paths only** (`clientes`, `ingredientes`) — never a host. No `Program.cs` DI changes are required for the HttpClient wiring; only AutoMapper registration is removed (see ADR-3).

The legacy DTO shapes (Empresa/Sucursal scoping, Apellido, Nro_Doc, Tipo_Doc, free-text Medida, Numero_*) do not exist in the new backend contracts. They are replaced by thin client-side `record` types that match the verified backend contracts 1:1. The AutoMapper indirection (legacy DTO ↔ `Dominio.*` entity) is unusable — `Dominio.*` entities are the legacy domain model, not the new contracts — so it is retired for these two entities and the response records are used directly in the UI.

### Layering

```
Pages/Clientes.razor, Pages/Ingredientes.razor   (UI: list table + create form + disabled Buscar/Editar/Eliminar)
        │  inject ClienteService / IngredienteService
        ▼
Services/ClienteService.cs, IngredienteService.cs (relative paths on AuthorizedApi; STJ + JsonStringEnumConverter)
        │  HttpClient (AuthorizedApi, BaseAddress + Bearer from Slice A)
        ▼
.NET 8 backend  https://localhost:7126
   POST /clientes, GET /clientes, GET /clientes/{id}
   POST /ingredientes, GET /ingredientes, GET /ingredientes/{id}
```

Data flow (create): UI binds form fields + enum `<select>` → builds `Crear*Request` record → `Service.CreateAsync` → `PostAsJsonAsync("clientes", req, _jsonOptions)` → on success reload list; on 422 read RFC7807 `ProblemDetails.Detail` (Spanish) and surface it.

Data flow (list): `OnInitializedAsync` → `Service.GetAllAsync()` → `GetFromJsonAsync<List<*Response>>("clientes", _jsonOptions)` → bind to table; enum strings deserialize back to client enums via `JsonStringEnumConverter`.

## Key Decisions (ADRs)

### ADR-1 — Client DTOs as `record` types matching the backend contracts 1:1
New immutable `record` types live under `DTO/Cliente/` and `DTO/Ingrediente/` in a NEW namespace (`GastroGestionBlazor.Contracts.Clientes` / `.Ingredientes`) to avoid clashing with the legacy `DTO.Cliente` / `DTO.Ingredientes` classes during transition and to make the legacy types obviously removable. Records: `ClienteResponse`, `CrearClienteRequest`, `IngredienteResponse`, `CrearIngredienteRequest`. Property names match the C# contract record names exactly (`Nombre`, `CondicionIVA`, `Cuit`, `Email`, `Activo`, `UnidadBase`) so default System.Text.Json (PascalCase, case-insensitive on read) round-trips without attributes.
- Rejected: reusing the legacy `*CreacionDTO` / `*ToListDTO` classes. They carry Empresa/Sucursal/Apellido/Nro_Doc/Medida-free-text fields that do not exist server-side; keeping them would mean per-field mapping and dead properties.

### ADR-2 — Drop AutoMapper for Cliente + Ingrediente; use response records directly in the UI
The legacy maps go `DTO ↔ Dominio.Cliente` / `Dominio.Ingrediente` (the OLD domain model). The new backend speaks contract records, not `Dominio.*`. There is no value object to map to — the response record IS the view model. So:
- Remove the Cliente and Ingrediente `CreateMap<...>` blocks from `AutoMapperProfiles`.
- Remove `@inject IMapper Mapper` from both pages.
- The UI binds directly to `ClienteResponse` / `IngredienteResponse` for the list, and builds a `Crear*Request` from form-bound scalars/enum for create.
- `AutoMapper` package + `AddAutoMapper` registration: KEEP for now — other entities (Plato, Menu, Pedido, etc.) still use it. Only the Cliente/Ingrediente maps are removed. `@using AutoMapper` in `_Imports.razor` stays (still used elsewhere).
- Rejected: keep AutoMapper for these two. There is no legitimate mapping target; it would be ceremony with no payoff and forces keeping the legacy `Dominio.*` types alive for these entities.

### ADR-3 — `Program.cs`: no HttpClient changes; remove only is N/A
The Slice A factory registration already returns the `AuthorizedApi` client as the default `HttpClient`, which both services consume. No change needed there. `AddAutoMapper(typeof(AutoMapperProfiles))` stays (other entities). NET DI wiring for the two services stays (`AddScoped<ClienteService>`, `AddScoped<IngredienteService>`).
- Net effect on `Program.cs`: ZERO lines change in Slice B. (Documented explicitly so apply does not "tidy" it.)

### ADR-4 — Enums: client-side mirror enums + `JsonStringEnumConverter` for type safety
Define client enums `CondicionIVA` and `UnidadDeMedida` whose member names match the backend EXACTLY (string-name coupling). The backend serializes enums as STRING names on output (global `JsonStringEnumConverter`) and accepts both strings and integers on input. The client uses a shared `JsonSerializerOptions` with `JsonStringEnumConverter` registered, so:
- On READ: response `"CondicionIVA":"ResponsableInscripto"` deserializes into the client enum value.
- On WRITE: request enum serializes to its string name (`"ResponsableInscripto"`), which the backend accepts.
- `<select>` binds with `@bind` to the enum-typed field; `<option value="@CondicionIVA.ResponsableInscripto">` uses the enum, Blazor converts to/from the enum automatically.
- Rejected: `string` properties for the enum on the records. Loses compile-time safety, allows typos that the backend rejects at runtime, and makes the `<select>` options stringly-typed. The mirror-enum + converter approach catches name drift at compile time on the client side.
- Rejected: sending integers. Works today, but couples the UI to the numeric ordering of the backend enum, which is more fragile than the names and harder to read in network traces.

### ADR-5 — 422 / RFC7807 ProblemDetails handling (thin client validation)
Domain rules (e.g. CUIT required for `ResponsableInscripto`) are enforced server-side and surface as HTTP 422 with an RFC7807 `ProblemDetails` body whose `detail` carries a Spanish message. Client validation stays thin: only require `Nombre` client-side (cheap UX), and let the server be the source of truth for business rules.
- Services detect non-success and, when the response has a JSON body, deserialize a small `ProblemDetails` record (`{ string? Title, string? Detail, int? Status }`) and throw an `ApiException(string message)` carrying `Detail` (fallback to `Title`, then a generic Spanish message). The page `catch` shows that message in the existing `errorMessage` red text region.
- Rejected: parsing `ValidationProblemDetails.errors` per-field. The blocking rule here is a domain invariant returned as a flat 422 detail, not a field map; a single surfaced message is sufficient for Slice B. (Field-level mapping can be added later without breaking the contract.)

### ADR-6 — Disabled (not removed) Buscar/Editar/Eliminar with a note
Keep the controls visible but `disabled`, each with an inline "no disponible aún" note, rather than deleting the markup. Rationale: preserves the screen layout the user knows, signals intent ("coming later"), and makes the future Slice (backend edit/delete/search) a pure enable. The search panel's filter `<select>` and value input become disabled; the row action buttons for edit/delete become disabled; the eye (view) button stays enabled and drives an inline detail panel populated from the already-loaded list row (no extra GET needed, but `GetByIdAsync` is provided for completeness/future use).
- Rejected: deleting the controls. Larger diff, loses the visual affordance, and would need re-adding later.

## Locked Public Shapes (authoritative for tasks/apply)

### Enums (member names MUST match backend exactly)
```csharp
namespace GastroGestionBlazor.Contracts.Enums;

public enum CondicionIVA
{
    ResponsableInscripto = 0,
    Monotributista = 1,
    ConsumidorFinal = 2,
    ExentoIVA = 3
}

public enum UnidadDeMedida
{
    Gramo = 0,
    Kilogramo = 1,
    Mililitro = 2,
    Litro = 3,
    Unidad = 4,
    Porcion = 5
}
```

### DTO records
```csharp
namespace GastroGestionBlazor.Contracts.Clientes;

public sealed record ClienteResponse(
    Guid Id,
    string Nombre,
    CondicionIVA CondicionIVA,
    string? Cuit,
    string? Email,
    bool Activo);

public sealed record CrearClienteRequest(
    string Nombre,
    CondicionIVA CondicionIVA,
    string? Cuit,
    string? Email);
```
```csharp
namespace GastroGestionBlazor.Contracts.Ingredientes;

public sealed record IngredienteResponse(
    Guid Id,
    string Nombre,
    UnidadDeMedida UnidadBase,
    bool Activo);

public sealed record CrearIngredienteRequest(
    string Nombre,
    UnidadDeMedida UnidadBase);
```
```csharp
namespace GastroGestionBlazor.Contracts.Common;

// Minimal RFC7807 shape for surfacing 422 domain-rule messages.
public sealed record ProblemDetailsResponse(
    string? Title,
    string? Detail,
    int? Status);

public sealed class ApiException : Exception
{
    public ApiException(string message) : base(message) { }
}
```

### Service signatures (LOCKED)
The services keep the existing ctor (single injected `HttpClient` = `AuthorizedApi`). New method surface:
```csharp
public class ClienteService
{
    public ClienteService(HttpClient httpClient);

    public Task<List<ClienteResponse>> GetAllAsync();
    public Task<ClienteResponse?> GetByIdAsync(Guid id);
    public Task<Guid> CreateAsync(CrearClienteRequest request); // returns new id (from Location/body)
}

public class IngredienteService
{
    public IngredienteService(HttpClient httpClient);

    public Task<List<IngredienteResponse>> GetAllAsync();
    public Task<IngredienteResponse?> GetByIdAsync(Guid id);
    public Task<Guid> CreateAsync(CrearIngredienteRequest request);
}
```
Legacy methods (`GetAllClientesAsync`, `BuscarClientesAsync`, `AgregarClienteAsync`, `EditarClienteAsync`, `EliminarClienteAsync`, and the Ingrediente equivalents) are DELETED. No stubs — the UI is rewritten to the new surface, so dead stubs would only invite re-coupling.

Shared JSON options (one per service, or a shared static):
```csharp
private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
{
    Converters = { new JsonStringEnumConverter() }
};
```
`JsonSerializerDefaults.Web` gives case-insensitive property matching; the converter handles the string enums. Relative request URIs: `"clientes"`, `$"clientes/{id}"`, `"ingredientes"`, `$"ingredientes/{id}"`.

### CreateAsync behavior (both services)
1. `var resp = await _httpClient.PostAsJsonAsync("clientes", request, JsonOptions);`
2. If `resp.IsSuccessStatusCode` → read the created id: prefer the JSON body if the endpoint returns the Guid, else parse the `Location` header (`/clientes/{id}`); return the Guid.
3. Else → try deserialize `ProblemDetailsResponse` from the body; throw `ApiException(detail ?? title ?? "No se pudo completar la operación.")`.

## UI Design — Clientes.razor / Ingredientes.razor

### Clientes.razor
- Header + "Nuevo cliente" button (opens inline create form).
- Search panel: kept but `disabled`; small muted note: "Búsqueda no disponible aún."
- List table columns: **Nombre, Condición IVA, CUIT, Email, Activo, Acciones**.
  - Activo column: `<span class="badge bg-success">Activo</span>` vs `<span class="badge bg-secondary">Inactivo</span>`; inactive rows de-emphasized with a muted/`text-muted`/reduced-opacity row class.
  - Acciones: eye (view detail — ENABLED), edit + trash buttons `disabled` with `title="No disponible aún"`.
- Create form (shown when adding): `Nombre` text input (required client-side), `Condición IVA` `<select>` bound to `CondicionIVA` enum, `CUIT` text input (optional), `Email` text input (optional). Guardar / Cancelar.
  - On Guardar: thin validation (Nombre non-empty) → build `CrearClienteRequest` → `CreateAsync` → reload list + close form. On `ApiException` (e.g. CUIT-required 422) set `errorMessage` to the Spanish detail.
- Detail panel (eye): read-only display of the selected `ClienteResponse` row (no AutoMapper, direct fields).
- States: `isLoading` ("Cargando..."), `errorMessage` (red text), empty-list message ("No hay clientes.").

### Ingredientes.razor
- Same structure. List columns: **Nombre, Unidad base, Activo, Acciones**.
- Create form: `Nombre` text input (required), `Unidad base` `<select>` bound to `UnidadDeMedida` enum. Guardar / Cancelar.
- Search panel + edit/trash disabled with note; eye enabled.

### Enum `<select>` binding pattern
```razor
<select class="form-control" @bind="_condicionIva">
    @foreach (var c in Enum.GetValues<CondicionIVA>())
    {
        <option value="@c">@c</option>
    }
</select>
```
`@bind` on an enum-typed field handles parse/format automatically. (Display can stay the raw enum name for Slice B; friendly labels are a later polish, non-goal.)

## Cleanup

- DELETE legacy classes used only by these two screens:
  - `DTO/Cliente/ClienteCreacionDTO.cs`, `ClienteToListDTO.cs`, `ClienteEdicionDTO.cs`, `ClienteBusquedaDTO.cs`
  - `DTO/Ingrediente/IngredienteCreacionDTO.cs`, `IngredienteToListDTO.cs`, `IngredienteEdicionDTO.cs`, `IngredienteBusquedaDTO.cs`
  - BEFORE deleting, grep for references outside these two screens (e.g. `DireccionEdicionDTO` references `ClienteBusquedaDTO`; `Plato_Ingrediente` may reference Ingrediente DTOs). If a legacy cross-reference exists, KEEP that specific class and only stop using it from Cliente/Ingrediente screens (document which were kept). Apply must verify with a reference search, not delete blindly.
- Remove `EBusquedaCliente` / `EBusquedaIngrediente` usage from the screens (the enums in `DTO/Enumerables.cs` can stay; they are referenced by the legacy Busqueda DTOs — only remove if those DTOs are removed).
- Remove Cliente + Ingrediente `CreateMap` blocks from `AutoMapperProfiles.cs` (lines 26-46 region). Keep the rest.
- Remove `@inject IMapper Mapper` from both `.razor` pages.
- Remove legacy fields entirely (Apellido, Nro_Doc, Tipo_Doc, Estado_Civil, Fecha_Nacimiento, Sexo, Nacionalidad, Numero_Cliente, Id_Empresa, Id_Sucursal, Fecha_Alta_Cliente for Cliente; Descripcion, free-text Medida, Numero_ingrediente, Id_Empresa, Id_Sucursal for Ingrediente) — they vanish with the legacy DTOs.
- Confirm ZERO `localhost:5001` references remain (grep).

## Packages

NONE to add. `System.Text.Json` (incl. `JsonStringEnumConverter`) ships with the `net8.0` framework. AutoMapper 13.0.1 stays (used by other entities). Blazored.LocalStorage stays (Slice A). No package removed.

## Risks / Open Items

- **Feature regression (accepted)**: Edit, Delete, Search dropped for both entities. Mitigated by disabled controls + "no disponible aún" note; backend expansion is a future slice. Confirmed as Option A in the proposal.
- **Enum string-name coupling**: client enum member names MUST match backend names EXACTLY. Any rename on either side silently breaks (de)serialization at runtime. Mitigation: client mirror enums are the single source on the client; document the coupling at the enum definition; manual smoke test asserts a round-trip.
- **Created-id source ambiguity**: backend returns 201 + Guid id with `Location: /clientes/{id}`. `CreateAsync` must handle "body has Guid" OR "parse Location". Slice B uses the returned id only to reload; if parsing is brittle, fall back to just reloading the list (id is not strictly required by the UI). Low risk.
- **Legacy DTO cross-references**: deleting legacy DTOs may break other (out-of-scope) screens that import them. Apply MUST reference-search before delete and keep any cross-referenced class. This is the main apply-time hazard.
- **No automated tests** (`strict_tdd: false`): Standard Mode. Manual smoke test only: list loads against `localhost:7126` with Bearer; create persists; create `ResponsableInscripto` without CUIT shows the Spanish 422 detail; enum `<select>` sends an accepted value; Activo badge renders; disabled controls do nothing; no 404s; `dotnet build` green; zero `localhost:5001` refs.
- **Thin CUIT validation**: client does not replicate the CUIT-required-for-ResponsableInscripto rule; relies on server 422. Accepted (single source of truth = domain).

## Next
`sdd-tasks` (after spec is ready).
