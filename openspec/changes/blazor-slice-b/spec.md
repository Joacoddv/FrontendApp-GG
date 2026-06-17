# Spec — blazor-slice-b: Re-point Cliente + Ingrediente Blazor screens at .NET 8 backend

Change: blazor-slice-b
Scope: Option A — list + create + view only; Buscar/Editar/Eliminar disabled with Spanish "no disponible aún" note.
Artifact store: hybrid

---

## Domain A — Cliente Screen

### Requirement: BSB-C01 — ClienteService uses AuthorizedApi with relative URLs

ClienteService MUST call `GET /clientes`, `GET /clientes/{id}`, and `POST /clientes` via the injected `AuthorizedApi` named HttpClient using relative paths only.
The service MUST NOT contain any hardcoded host (e.g. `localhost:5001`, `localhost:7126`, or any absolute URL).

#### Scenario: List loads from new backend

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

The client-side `ClienteResponse` record MUST contain exactly: `Id (Guid)`, `Nombre (string)`, `CondicionIVA (string)`, `Cuit (string?)`, `Email (string?)`, `Activo (bool)`.
The client-side `CrearClienteRequest` record MUST contain exactly: `Nombre (string)`, `CondicionIVA (string)`, `Cuit (string?)`, `Email (string?)`.
Legacy fields (`Apellido`, `Nro_Doc`, `Tipo_Doc`, `Numero_Cliente`, `Id_Empresa`) MUST NOT exist in any DTO or AutoMapper profile.

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

The Clientes list page MUST display the `Activo` field as a visual badge for each row.
Rows where `Activo == false` MUST be visually de-emphasized (e.g. reduced opacity or a muted style class).

#### Scenario: Active cliente shown with active badge

- GIVEN the list returns a cliente with `Activo = true`
- WHEN the page renders
- THEN a badge indicating active status is visible for that row

#### Scenario: Inactive cliente de-emphasized

- GIVEN the list returns a cliente with `Activo = false`
- WHEN the page renders
- THEN that row is visually de-emphasized and shows an inactive indicator

---

### Requirement: BSB-C04 — CondicionIVA rendered as select with enum string values

The create-Cliente form MUST render CondicionIVA as an HTML `<select>` element.
Options MUST be the four string values: `ResponsableInscripto`, `Monotributista`, `ConsumidorFinal`, `ExentoIVA`.
The form MUST NOT send numeric enum values.

#### Scenario: Select renders all four options

- GIVEN the user opens the create-Cliente form
- WHEN the CondicionIVA select renders
- THEN it shows exactly four options corresponding to the four CondicionIVA string values

#### Scenario: POST sends string value

- GIVEN the user selects "Monotributista" and submits the form
- WHEN the request body is inspected
- THEN `CondicionIVA` is the string `"Monotributista"`, not an integer

---

### Requirement: BSB-C05 — Create Cliente surfaces 422 domain errors in Spanish

When the backend returns HTTP 422, the UI MUST display the server's error message in Spanish to the user.
The client MUST NOT duplicate CUIT validation logic — domain enforcement lives exclusively on the server.

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

The Clientes page MUST retain Buscar, Editar, and Eliminar controls in the UI.
All three MUST be disabled (not hidden) and MUST display a Spanish note "no disponible aún" adjacent to or as a tooltip on each control.
None of these controls MUST trigger any HTTP call.

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

## Domain B — Ingrediente Screen

### Requirement: BSB-I01 — IngredienteService uses AuthorizedApi with relative URLs

IngredienteService MUST call `GET /ingredientes`, `GET /ingredientes/{id}`, and `POST /ingredientes` via the injected `AuthorizedApi` named HttpClient using relative paths only.
The service MUST NOT contain any hardcoded host.

#### Scenario: List loads from new backend

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

The client-side `IngredienteResponse` record MUST contain exactly: `Id (Guid)`, `Nombre (string)`, `UnidadBase (string)`, `Activo (bool)`.
The client-side `CrearIngredienteRequest` record MUST contain exactly: `Nombre (string)`, `UnidadBase (string)`.
Legacy fields (`Descripcion`, free-text `Medida`) MUST NOT exist in any DTO or AutoMapper profile.

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

The Ingredientes list page MUST display the `Activo` field as a visual badge for each row.
Rows where `Activo == false` MUST be visually de-emphasized.

#### Scenario: Active ingrediente shown with active badge

- GIVEN the list returns an ingrediente with `Activo = true`
- WHEN the page renders
- THEN a badge indicating active status is visible for that row

#### Scenario: Inactive ingrediente de-emphasized

- GIVEN the list returns an ingrediente with `Activo = false`
- WHEN the page renders
- THEN that row is visually de-emphasized and shows an inactive indicator

---

### Requirement: BSB-I04 — UnidadBase rendered as select with enum string values

The create-Ingrediente form MUST render UnidadBase as an HTML `<select>` element.
Options MUST be the six string values: `Gramo`, `Kilogramo`, `Mililitro`, `Litro`, `Unidad`, `Porcion`.
The form MUST NOT send numeric enum values.

#### Scenario: Select renders all six options

- GIVEN the user opens the create-Ingrediente form
- WHEN the UnidadBase select renders
- THEN it shows exactly six options corresponding to the six UnidadDeMedida string values

#### Scenario: POST sends string value

- GIVEN the user selects "Kilogramo" and submits
- WHEN the request body is inspected
- THEN `UnidadBase` is the string `"Kilogramo"`, not an integer

---

### Requirement: BSB-I05 — Create Ingrediente surfaces 422 errors in Spanish

When the backend returns HTTP 422, the UI MUST display the server's error message in Spanish.
The client MUST NOT duplicate server-side validation rules.

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

The Ingredientes page MUST retain Buscar, Editar, and Eliminar controls.
All three MUST be disabled and MUST display a Spanish note "no disponible aún".
None MUST trigger any HTTP call.

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

## Domain C — Auth + Cross-Cutting

### Requirement: BSB-X01 — 401 handling intact (Slice A unchanged)

Slice A's 401 handling (redirect to login or error display) MUST remain unmodified.
ClienteService and IngredienteService MUST NOT bypass or replace the BearerTokenHandler attached to AuthorizedApi.

#### Scenario: Unauthenticated request handled

- GIVEN the user session has expired or is missing
- WHEN ClienteService or IngredienteService calls any endpoint
- THEN the 401 response is handled by the existing Slice A mechanism
- AND the user is redirected to login or sees an appropriate auth error

---

### Requirement: BSB-X02 — AutoMapper profiles cleaned of legacy fields

AutoMapperProfiles.cs MUST NOT contain mapping rules for removed fields (`Apellido`, `Nro_Doc`, `Tipo_Doc`, `Numero_Cliente`, `Id_Empresa`, `Descripcion`, free-text `Medida`).
If AutoMapper is no longer needed after the DTO simplification, the profile file MAY be removed entirely.

#### Scenario: No mapping for removed fields

- GIVEN the AutoMapperProfiles.cs source
- WHEN inspected
- THEN no CreateMap or ForMember references to the legacy fields are present

---

## Non-Goals (out of scope for this spec)

- Backend edit/delete/search endpoints (no PUT, PATCH, DELETE, Buscar on server)
- Kitchen screens / SignalR (Slice C)
- Auth changes (Slice A delivered and frozen)
- Test project additions (strict_tdd = false for this change)
- Any new backend code
