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

### Requirement: BSB-C06 — Buscar/Editar/Eliminar controls enabled for Clientes

The Clientes page MUST enable and wire the Buscar, Editar, and Eliminar controls. The disabled state and "no disponible aún" note MUST be removed. Edit and Delete actions MUST be wrapped in `<AuthorizeView Roles="Administrador">` so they are hidden for non-admin authenticated users. Search (Buscar) MUST be available to all authenticated roles.

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

### Requirement: BSB-I06 — Buscar/Editar/Eliminar controls enabled for Ingredientes

The Ingredientes page MUST enable and wire the Buscar, Editar, and Eliminar controls under the same rules as BSB-C06: Edit/Delete wrapped in `<AuthorizeView Roles="Administrador">`, Buscar open to all authenticated roles.

#### Scenario: Admin sees enabled edit and delete buttons

- GIVEN an authenticated user with role Administrador on the Ingredientes page
- WHEN the list renders
- THEN Edit and Delete buttons are visible and enabled for each row

#### Scenario: Non-admin cannot see edit or delete buttons

- GIVEN an authenticated user without role Administrador
- WHEN the Ingredientes page renders
- THEN Edit and Delete buttons are NOT rendered

---

### Requirement: CUI-C01 — EditarClienteRequest contract

A new client-side record `EditarClienteRequest` MUST be added under `Contracts/Clientes/`. It MUST contain exactly: `Nombre (string)`, `CondicionIVA (CondicionIVA)`, `Cuit (string?)`, `Email (string?)`. `NumeroCliente` MUST NOT appear in this record.

#### Scenario: Contract mirrors backend PUT /clientes/{id} body

- GIVEN the EditarClienteRequest record
- WHEN serialized as JSON
- THEN the body contains Nombre, CondicionIVA (string), Cuit, Email
- AND NumeroCliente is absent from the serialized body

---

### Requirement: CUI-I01 — EditarIngredienteRequest contract

A new client-side record `EditarIngredienteRequest` MUST be added under `Contracts/Ingredientes/`. It MUST contain exactly: `Nombre (string)`. `UnidadBase` MUST NOT appear in this record.

#### Scenario: Contract mirrors backend PUT /ingredientes/{id} body

- GIVEN the EditarIngredienteRequest record
- WHEN serialized as JSON
- THEN the body contains only Nombre
- AND UnidadBase is absent from the serialized body

---

### Requirement: CUI-C02 — ClienteService CRUD methods

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

### Requirement: CUI-I02 — IngredienteService CRUD methods

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

### Requirement: CUI-C03 — Clientes page: search behavior

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

### Requirement: CUI-I03 — Ingredientes page: search behavior

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

### Requirement: CUI-C04 — Clientes page: inline edit form

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

### Requirement: CUI-I04 — Ingredientes page: inline edit form

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

### Requirement: CUI-C05 — Clientes page: soft-delete with confirmation

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

### Requirement: CUI-I05 — Ingredientes page: soft-delete with confirmation

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

### Requirement: CUI-X01 — Edit error surfacing (both entities)

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
