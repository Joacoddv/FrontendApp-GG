# Screens — Data Screens Capability Spec

**Capability**: blazor-screens  
**First delivered by**: blazor-slice-b (Phase 7, Slice B — PR #2, merge commit 2e8721a)  
**Status**: Active  
**Last updated**: 2026-06-17

---

## Purpose

Client-side data screens for the Blazor WASM frontend. Covers the Cliente and Ingrediente entity screens: list, create, and view-detail flows using the authenticated `AuthorizedApi` HttpClient (Slice A). Backend endpoints for edit, delete, and search do not yet exist; those controls are present in the UI but disabled with a "no disponible aún" note until Slice C or a future slice provides the backend.

---

## Backend Contracts (Locked)

### Cliente (`/clientes` — RequireAuthorization)

| Verb | Path | Request | Response |
|------|------|---------|----------|
| `GET` | `/clientes` | — | `ClienteResponse[]` |
| `GET` | `/clientes/{id:guid}` | — | `ClienteResponse` or 404 |
| `POST` | `/clientes` | `CrearClienteRequest` | 201 + Guid id + `Location: /clientes/{id}` |

**Absent (backend does not expose)**: PUT/edit, DELETE/baja, search/Buscar.

### Ingrediente (`/ingredientes` — RequireAuthorization)

| Verb | Path | Request | Response |
|------|------|---------|----------|
| `GET` | `/ingredientes` | — | `IngredienteResponse[]` |
| `GET` | `/ingredientes/{id:guid}` | — | `IngredienteResponse` or 404 |
| `POST` | `/ingredientes` | `CrearIngredienteRequest` | 201 + Guid id |

**Absent (backend does not expose)**: PUT/edit, DELETE/baja, search/Buscar.

### Enum Wire Format

Both `CondicionIVA` and `UnidadDeMedida` are serialized as **string names** on output (global `JsonStringEnumConverter` in the backend's `Program.cs`). The backend accepts both strings and integers on input; the client always sends strings.

---

## Client Contract Records

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

```csharp
namespace GastroGestionBlazor.Contracts.Clientes;

public sealed record ClienteResponse(
    Guid Id, string Nombre, CondicionIVA CondicionIVA,
    string? Cuit, string? Email, bool Activo);

public sealed record CrearClienteRequest(
    string Nombre, CondicionIVA CondicionIVA,
    string? Cuit, string? Email);
```

```csharp
namespace GastroGestionBlazor.Contracts.Ingredientes;

public sealed record IngredienteResponse(
    Guid Id, string Nombre, UnidadDeMedida UnidadBase, bool Activo);

public sealed record CrearIngredienteRequest(
    string Nombre, UnidadDeMedida UnidadBase);
```

```csharp
namespace GastroGestionBlazor.Contracts.Common;

public sealed record ProblemDetailsResponse(
    string? Title, string? Detail, int? Status);

public sealed class ApiException : Exception
{
    public ApiException(string message) : base(message) { }
}
```

---

## Requirements

---

### Requirement: BSB-C01 — ClienteService uses AuthorizedApi with relative URLs

ClienteService MUST call `GET /clientes`, `GET /clientes/{id}`, and `POST /clientes` via the injected `AuthorizedApi` named HttpClient using relative paths only. The service MUST NOT contain any hardcoded host (e.g. `localhost:5001`, `localhost:7126`, or any absolute URL).

#### Scenario: List loads from backend

- GIVEN the user is authenticated and navigates to the Clientes page
- WHEN the page initializes
- THEN ClienteService calls `GET /clientes` via AuthorizedApi
- AND the request carries the Bearer token from BearerTokenHandler
- AND the response is deserialized as `ClienteResponse[]`

#### Scenario: No hardcoded host remains

- GIVEN the compiled ClienteService source
- WHEN searched for "localhost" or absolute URLs
- THEN no match is found

---

### Requirement: BSB-C02 — Cliente DTO matches verified backend contract

The client-side `ClienteResponse` record MUST contain exactly: `Id (Guid)`, `Nombre (string)`, `CondicionIVA (CondicionIVA enum)`, `Cuit (string?)`, `Email (string?)`, `Activo (bool)`. The client-side `CrearClienteRequest` record MUST contain exactly: `Nombre (string)`, `CondicionIVA (CondicionIVA enum)`, `Cuit (string?)`, `Email (string?)`. Legacy fields (`Apellido`, `Nro_Doc`, `Tipo_Doc`, `Numero_Cliente`, `Id_Empresa`) MUST NOT exist in any DTO or AutoMapper profile.

> Note: The spec originally listed `CondicionIVA` as `string`; the implementation correctly uses the typed `CondicionIVA` enum with `JsonStringEnumConverter` (ADR-4). The wire format IS a JSON string, satisfying the backend contract and adding compile-time safety.

#### Scenario: Legacy fields removed

- GIVEN the DTO source files for Cliente
- WHEN inspected
- THEN none of the fields `Apellido`, `Nro_Doc`, `Tipo_Doc`, `Numero_Cliente`, `Id_Empresa` are declared

#### Scenario: New fields present

- GIVEN the `ClienteResponse` record
- WHEN compiled
- THEN it exposes `Id`, `Nombre`, `CondicionIVA`, `Cuit`, `Email`, `Activo`

---

### Requirement: BSB-C03 — Clientes list renders Activo badge

The Clientes list page MUST display the `Activo` field as a visual badge for each row. Rows where `Activo == false` MUST be visually de-emphasized (reduced opacity or a `text-muted` style class).

#### Scenario: Active cliente shown with active badge

- GIVEN the list returns a cliente with `Activo = true`
- WHEN the page renders
- THEN a `bg-success` badge indicating active status is visible for that row

#### Scenario: Inactive cliente de-emphasized

- GIVEN the list returns a cliente with `Activo = false`
- WHEN the page renders
- THEN that row is visually de-emphasized (`text-muted`) and shows a `bg-secondary` inactive badge

---

### Requirement: BSB-C04 — CondicionIVA rendered as select with enum string values

The create-Cliente form MUST render CondicionIVA as an HTML `<select>` element. Options MUST be the four string values: `ResponsableInscripto`, `Monotributista`, `ConsumidorFinal`, `ExentoIVA`. The form MUST NOT send numeric enum values.

#### Scenario: Select renders all four options

- GIVEN the user opens the create-Cliente form
- WHEN the CondicionIVA select renders
- THEN it shows exactly four options driven by `Enum.GetValues<CondicionIVA>()`

#### Scenario: POST sends string value

- GIVEN the user selects "Monotributista" and submits the form
- WHEN the request body is inspected
- THEN `CondicionIVA` is the string `"Monotributista"`, not an integer

---

### Requirement: BSB-C05 — Create Cliente surfaces 422 domain errors in Spanish

When the backend returns HTTP 422, the UI MUST display the server's `ProblemDetails.Detail` message in Spanish to the user. The client MUST NOT duplicate CUIT validation logic — domain enforcement lives exclusively on the server.

#### Scenario: CUIT required error surfaced

- GIVEN the user selects "ResponsableInscripto" and leaves CUIT blank, then submits
- WHEN the backend returns 422 with a Spanish error message
- THEN the page displays that error message to the user
- AND no client-side validation intercepts the request before it reaches the server

#### Scenario: Successful create shows new row

- GIVEN the user fills Nombre + valid CondicionIVA and submits
- WHEN the backend returns 201
- THEN the new cliente appears in the list without a full page reload

---

### Requirement: BSB-C06 — Buscar/Editar/Eliminar controls present but disabled

The Clientes page MUST retain Buscar, Editar, and Eliminar controls in the UI. All three MUST be disabled (not hidden) and MUST display a Spanish note "no disponible aún" adjacent to or as a tooltip on each control. None of these controls MUST trigger any HTTP call.

#### Scenario: Disabled controls visible

- GIVEN the Clientes page renders
- WHEN the user views the page
- THEN Buscar, Editar, and Eliminar controls are visible but disabled
- AND a "no disponible aún" note is visible near each disabled control

#### Scenario: No HTTP call on interaction attempt

- GIVEN the controls are disabled
- WHEN the user attempts to interact with any of them
- THEN no network request is made and no error is thrown

---

### Requirement: BSB-I01 — IngredienteService uses AuthorizedApi with relative URLs

IngredienteService MUST call `GET /ingredientes`, `GET /ingredientes/{id}`, and `POST /ingredientes` via the injected `AuthorizedApi` named HttpClient using relative paths only. The service MUST NOT contain any hardcoded host.

#### Scenario: List loads from backend

- GIVEN the user is authenticated and navigates to the Ingredientes page
- WHEN the page initializes
- THEN IngredienteService calls `GET /ingredientes` via AuthorizedApi
- AND the request carries the Bearer token

#### Scenario: No hardcoded host remains

- GIVEN the compiled IngredienteService source
- WHEN searched for "localhost" or absolute URLs
- THEN no match is found

---

### Requirement: BSB-I02 — Ingrediente DTO matches verified backend contract

The client-side `IngredienteResponse` record MUST contain exactly: `Id (Guid)`, `Nombre (string)`, `UnidadBase (UnidadDeMedida enum)`, `Activo (bool)`. The client-side `CrearIngredienteRequest` record MUST contain exactly: `Nombre (string)`, `UnidadBase (UnidadDeMedida enum)`. Legacy fields (`Descripcion`, free-text `Medida`) MUST NOT exist in any DTO or AutoMapper profile.

#### Scenario: Legacy fields removed

- GIVEN the DTO source files for Ingrediente
- WHEN inspected
- THEN neither `Descripcion` nor a free-text `Medida` field is declared

#### Scenario: New fields present

- GIVEN the `IngredienteResponse` record
- WHEN compiled
- THEN it exposes `Id`, `Nombre`, `UnidadBase`, `Activo`

---

### Requirement: BSB-I03 — Ingredientes list renders Activo badge

The Ingredientes list page MUST display the `Activo` field as a visual badge for each row. Rows where `Activo == false` MUST be visually de-emphasized.

#### Scenario: Active ingrediente shown with active badge

- GIVEN the list returns an ingrediente with `Activo = true`
- WHEN the page renders
- THEN a `bg-success` badge indicating active status is visible for that row

#### Scenario: Inactive ingrediente de-emphasized

- GIVEN the list returns an ingrediente with `Activo = false`
- WHEN the page renders
- THEN that row is visually de-emphasized and shows a `bg-secondary` inactive badge

---

### Requirement: BSB-I04 — UnidadBase rendered as select with enum string values

The create-Ingrediente form MUST render UnidadBase as an HTML `<select>` element. Options MUST be the six string values: `Gramo`, `Kilogramo`, `Mililitro`, `Litro`, `Unidad`, `Porcion`. The form MUST NOT send numeric enum values.

#### Scenario: Select renders all six options

- GIVEN the user opens the create-Ingrediente form
- WHEN the UnidadBase select renders
- THEN it shows exactly six options driven by `Enum.GetValues<UnidadDeMedida>()`

#### Scenario: POST sends string value

- GIVEN the user selects "Kilogramo" and submits
- WHEN the request body is inspected
- THEN `UnidadBase` is the string `"Kilogramo"`, not an integer

---

### Requirement: BSB-I05 — Create Ingrediente surfaces 422 errors in Spanish

When the backend returns HTTP 422, the UI MUST display the server's error message in Spanish. The client MUST NOT duplicate server-side validation rules.

#### Scenario: Nombre required error surfaced

- GIVEN the user submits the form with Nombre blank
- WHEN the backend returns 422
- THEN the page displays the server error in Spanish

#### Scenario: Successful create shows new row

- GIVEN the user fills Nombre + valid UnidadBase and submits
- WHEN the backend returns 201
- THEN the new ingrediente appears in the list

---

### Requirement: BSB-I06 — Buscar/Editar/Eliminar controls present but disabled

The Ingredientes page MUST retain Buscar, Editar, and Eliminar controls. All three MUST be disabled and MUST display a Spanish note "no disponible aún". None MUST trigger any HTTP call.

#### Scenario: Disabled controls visible

- GIVEN the Ingredientes page renders
- WHEN the user views the page
- THEN Buscar, Editar, and Eliminar controls are visible but disabled
- AND a "no disponible aún" note is visible near each disabled control

#### Scenario: No HTTP call on interaction attempt

- GIVEN the controls are disabled
- WHEN the user attempts to interact with any of them
- THEN no network request is made

---

### Requirement: BSB-X01 — 401 handling intact (Slice A unchanged)

Slice A's 401 handling (redirect to login or error display) MUST remain unmodified. ClienteService and IngredienteService MUST NOT bypass or replace the BearerTokenHandler attached to AuthorizedApi.

#### Scenario: Unauthenticated request handled

- GIVEN the user session has expired or is missing
- WHEN ClienteService or IngredienteService calls any endpoint
- THEN the 401 response is handled by the existing Slice A mechanism
- AND the user is redirected to login or sees an appropriate auth error

---

### Requirement: BSB-X02 — AutoMapper profiles cleaned of legacy fields

AutoMapperProfiles.cs MUST NOT contain mapping rules for removed fields (`Apellido`, `Nro_Doc`, `Tipo_Doc`, `Numero_Cliente`, `Id_Empresa`, `Descripcion`, free-text `Medida`). AutoMapper package and registration are retained (other entities still use it).

#### Scenario: No mapping for removed fields

- GIVEN the AutoMapperProfiles.cs source
- WHEN inspected
- THEN no CreateMap or ForMember references to the legacy fields are present

---

## Service Signatures (Locked)

```csharp
public class ClienteService
{
    public ClienteService(HttpClient httpClient);
    public Task<List<ClienteResponse>> GetAllAsync();
    public Task<ClienteResponse?> GetByIdAsync(Guid id);
    public Task<Guid> CreateAsync(CrearClienteRequest request);
}

public class IngredienteService
{
    public IngredienteService(HttpClient httpClient);
    public Task<List<IngredienteResponse>> GetAllAsync();
    public Task<IngredienteResponse?> GetByIdAsync(Guid id);
    public Task<Guid> CreateAsync(CrearIngredienteRequest request);
}
```

JSON options (shared static per service):
```csharp
private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
{
    Converters = { new JsonStringEnumConverter() }
};
```

---

## Key Design Decisions (ADRs)

| ADR | Decision | Rationale |
|-----|----------|-----------|
| ADR-1 | Client DTOs as `record` types in new `GastroGestionBlazor.Contracts.*` namespace | Avoids clashing with legacy `DTO.*` classes; makes legacy types obviously removable. |
| ADR-2 | Drop AutoMapper for Cliente + Ingrediente; use response records directly in UI | No valid mapping target exists — `Dominio.*` is the OLD domain model; the response record IS the view model. |
| ADR-3 | `Program.cs`: zero changes in Slice B for HttpClient wiring | Slice A factory registration already returns `AuthorizedApi` as the default `HttpClient`; no further DI change needed. |
| ADR-4 | Client mirror enums + `JsonStringEnumConverter` | Compile-time safety; wire format is string (backend and client agree); avoids integer coupling to enum ordinal. |
| ADR-5 | 422 / RFC7807: thin client validation; server is source of truth | `ProblemDetails.Detail` carries the Spanish domain-rule message; client only validates `Nombre` non-empty as a cheap UX gate. |
| ADR-6 | Disable (not remove) Buscar/Editar/Eliminar | Preserves screen layout, signals future intent, keeps diff minimal; future slice enables controls when backend endpoints land. |

---

## Legacy DTO Cross-References (Kept by Design)

These files were NOT deleted because they are referenced outside the Cliente/Ingrediente screens:

| File | Referenced by | Decision |
|------|--------------|----------|
| `DTO/Cliente/ClienteBusquedaDTO.cs` | `DireccionEdicionDTO`, `DireccionCreacionDTO`, `DireccionBusquedaDTO` | Keep until Direccion screen is migrated |
| `DTO/Cliente/ClienteToListDTO.cs` | `PedidoBusquedaDTO`, `PedidoEdicionDTO`, `PedidoToListDTO`, `PedidoCreacionDTO`, `DireccionToListDTO` | Keep until Pedido/Direccion screens are migrated |
| `DTO/Ingrediente/IngredienteToListDTO.cs` | 4 Plato_Ingrediente DTOs | Keep until Plato_Ingrediente screen is migrated |

Legacy DTO fields (`Descripcion`, free-text `Medida`) inside kept files are NOT spec violations — they are not present in the new contract records used by the screens.

---

## Known Deferred Items

| Item | Detail | Target |
|------|--------|--------|
| Counter.razor orphan | `Pages/Counter.razor` at `/counter` duplicates Clientes.razor without a create form; not in nav. Recommend delete. | Follow-up PR |
| Legacy DTOs cross-refs | `ClienteBusquedaDTO.cs`, `ClienteToListDTO.cs`, `IngredienteToListDTO.cs` kept due to cross-entity references. Delete when those screens are migrated. | Future slices |
| Edit / Delete / Search | Backend does not expose PUT, DELETE, or search endpoints for Cliente/Ingrediente. Controls are disabled in UI. | Slice C or separate backend task |
| Stale Program.cs comment | Line 45-46 comment "URL rewrites are Slice B — services still have their hardcoded legacy URLs" is factually wrong after Slice B merge. Update in a follow-up. | Follow-up PR |

---

## Non-Goals (Out of Scope for this Capability)

- Backend edit/delete/search endpoints (no PUT, PATCH, DELETE, or Buscar on the server)
- Kitchen screens / SignalR (Slice C)
- Auth changes (Slice A delivered and frozen — see `openspec/specs/Auth/spec.md`)
- Test project additions (`strict_tdd: false`)
- Field-level 422 error mapping (single `ProblemDetails.Detail` message is sufficient for Slice B)
- Friendly display labels for enum values (raw enum names used; polishing is a later non-goal)
