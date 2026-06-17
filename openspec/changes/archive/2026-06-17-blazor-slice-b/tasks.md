# Tasks: blazor-slice-b — Re-point Cliente + Ingrediente Blazor screens at .NET 8 backend

Change: blazor-slice-b
Mode: Standard (strict_tdd: false)
Build: `dotnet build GastroGestionBlazor.sln`

---

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 420–520 (new files ~200 + rewrites ~200 + cleanup ~80) |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1: Contracts + Services (foundation + services) → PR 2: Razor pages + cleanup |
| Delivery strategy | ask-on-risk |
| Chain strategy | pending |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: pending
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Contracts (enums + DTOs + common) + rewritten Services + build green | PR 1 | Base: `feat/ot-workflow-pr1a`; no UI changes yet; verifiable by build alone |
| 2 | Razor rewrites (Clientes + Ingredientes) + AutoMapper cleanup + legacy DTO deletions | PR 2 | Base: PR 1 branch; diff stays focused on pages + dead code |

---

## Phase 1: Foundation — New Contract Types (can parallel with Phase 2 read tasks)

- [x] **BSB-T01** — Create `GastroGestionBlazor/Contracts/Enums/CondicionIVA.cs` with enum `CondicionIVA { ResponsableInscripto=0, Monotributista=1, ConsumidorFinal=2, ExentoIVA=3 }` in namespace `GastroGestionBlazor.Contracts.Enums`. Member names MUST match backend exactly (ADR-4, BSB-C04).
- [x] **BSB-T02** — Create `GastroGestionBlazor/Contracts/Enums/UnidadDeMedida.cs` with enum `UnidadDeMedida { Gramo=0, Kilogramo=1, Mililitro=2, Litro=3, Unidad=4, Porcion=5 }` in namespace `GastroGestionBlazor.Contracts.Enums`. Member names MUST match backend exactly (ADR-4, BSB-I04).
- [x] **BSB-T03** — Create `GastroGestionBlazor/Contracts/Clientes/ClienteResponse.cs`: `sealed record ClienteResponse(Guid Id, string Nombre, CondicionIVA CondicionIVA, string? Cuit, string? Email, bool Activo)` in namespace `GastroGestionBlazor.Contracts.Clientes` (ADR-1, BSB-C02).
- [x] **BSB-T04** — Create `GastroGestionBlazor/Contracts/Clientes/CrearClienteRequest.cs`: `sealed record CrearClienteRequest(string Nombre, CondicionIVA CondicionIVA, string? Cuit, string? Email)` in namespace `GastroGestionBlazor.Contracts.Clientes` (ADR-1, BSB-C02).
- [x] **BSB-T05** — Create `GastroGestionBlazor/Contracts/Ingredientes/IngredienteResponse.cs`: `sealed record IngredienteResponse(Guid Id, string Nombre, UnidadDeMedida UnidadBase, bool Activo)` in namespace `GastroGestionBlazor.Contracts.Ingredientes` (ADR-1, BSB-I02).
- [x] **BSB-T06** — Create `GastroGestionBlazor/Contracts/Ingredientes/CrearIngredienteRequest.cs`: `sealed record CrearIngredienteRequest(string Nombre, UnidadDeMedida UnidadBase)` in namespace `GastroGestionBlazor.Contracts.Ingredientes` (ADR-1, BSB-I02).
- [x] **BSB-T07** — Create `GastroGestionBlazor/Contracts/Common/ProblemDetailsResponse.cs`: `sealed record ProblemDetailsResponse(string? Title, string? Detail, int? Status)` in namespace `GastroGestionBlazor.Contracts.Common` (ADR-5, BSB-C05, BSB-I05).
- [x] **BSB-T08** — Create `GastroGestionBlazor/Contracts/Common/ApiException.cs`: `sealed class ApiException : Exception { public ApiException(string message) : base(message) { } }` in namespace `GastroGestionBlazor.Contracts.Common` (ADR-5, BSB-C05, BSB-I05).

---

## Phase 2: Core — Service Rewrites (depends on Phase 1)

- [x] **BSB-T09** — Rewrite `GastroGestionBlazor/Services/ClienteService.cs`: constructor takes `HttpClient`; add `private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } }`; implement `GetAllAsync()` → `GetFromJsonAsync<List<ClienteResponse>>("clientes", JsonOptions)`; `GetByIdAsync(Guid id)` → `GetFromJsonAsync<ClienteResponse>($"clientes/{id}", JsonOptions)`; `CreateAsync(CrearClienteRequest)` → `PostAsJsonAsync("clientes", request, JsonOptions)` — on success return Guid (body or parse Location header `/clientes/{id}`), on failure deserialize `ProblemDetailsResponse` and throw `ApiException(detail ?? title ?? "No se pudo completar la operación.")`. DELETE all legacy methods (`GetAllClientesAsync`, `BuscarClientesAsync`, `AgregarClienteAsync`, `EditarClienteAsync`, `EliminarClienteAsync`). NO hardcoded host (BSB-C01, ADR-3, ADR-5).
- [x] **BSB-T10** — Rewrite `GastroGestionBlazor/Services/IngredienteService.cs`: same pattern as BSB-T09 but for `ingredientes` path; uses `IngredienteResponse`, `CrearIngredienteRequest`; same `JsonOptions`; same `CreateAsync` error pattern. DELETE all legacy methods. NO hardcoded host (BSB-I01, ADR-3, ADR-5).
- [x] **BSB-T11** — Run `dotnet build GastroGestionBlazor.sln` and confirm zero errors from Phase 1+2 changes before proceeding to Phase 3.

---

## Phase 3: Integration — Razor Page Rewrites (depends on Phase 2)

- [x] **BSB-T12** — Rewrite `GastroGestionBlazor/Pages/Clientes.razor`: remove `@inject IMapper Mapper`; inject `ClienteService`; add `@using GastroGestionBlazor.Contracts.Clientes` + `@using GastroGestionBlazor.Contracts.Enums`; implement `OnInitializedAsync` with `isLoading`/`errorMessage` state; list table columns: Nombre, Condición IVA, CUIT, Email, Activo (badge `bg-success`/`bg-secondary` + `text-muted` on inactive row), Acciones (eye enabled; edit + trash `disabled` with `title="No disponible aún"`); search panel `disabled` with muted note "Búsqueda no disponible aún" (BSB-C01, BSB-C03, BSB-C06, ADR-6).
- [x] **BSB-T13** — Add create form to `Clientes.razor`: bound fields `_nombre (string)`, `_condicionIva (CondicionIVA)`, `_cuit (string?)`, `_email (string?)`; `<select>` using `@foreach (var c in Enum.GetValues<CondicionIVA>())` pattern (BSB-T12 file); on Guardar — validate `_nombre` non-empty, build `CrearClienteRequest`, call `ClienteService.CreateAsync`, reload list, close form; catch `ApiException` → set `errorMessage` (BSB-C04, BSB-C05, ADR-4, ADR-5).
- [x] **BSB-T14** — Rewrite `GastroGestionBlazor/Pages/Ingredientes.razor`: remove `@inject IMapper Mapper`; inject `IngredienteService`; add usings for Ingredientes + Enums contracts; list table columns: Nombre, Unidad base, Activo (badge), Acciones (eye enabled; edit + trash disabled with note); search panel disabled with note; same isLoading/errorMessage states (BSB-I01, BSB-I03, BSB-I06, ADR-6).
- [x] **BSB-T15** — Add create form to `Ingredientes.razor`: bound fields `_nombre (string)`, `_unidadBase (UnidadDeMedida)`; `<select>` using `Enum.GetValues<UnidadDeMedida>()` pattern; Guardar → build `CrearIngredienteRequest`, call `IngredienteService.CreateAsync`, reload list; catch `ApiException` → set `errorMessage` (BSB-I04, BSB-I05, ADR-4, ADR-5).

---

## Phase 4: Cleanup — AutoMapper + Legacy DTOs (depends on Phase 3)

- [x] **BSB-T16** — Reference-search (grep) `ClienteBusquedaDTO` across all `.cs` and `.razor` files. CONFIRM `DireccionEdicionDTO.cs` is the only cross-reference. If other files reference it, document them before proceeding. DO NOT delete `ClienteBusquedaDTO.cs` (referenced by `DTO/Direccion/DireccionEdicionDTO.cs`). (BSB-X02, design Cleanup HAZARD).
- [x] **BSB-T17** — Reference-search (grep) `IngredienteToListDTO` across all `.cs` and `.razor` files. CONFIRM it is referenced only by `Plato_Ingrediente*DTO` files. DO NOT delete `IngredienteToListDTO.cs` (referenced by `DTO/Plato_Ingrediente/Plato_IngredienteToListDTO.cs`, `Plato_IngredienteEdicionDTO.cs`, `Plato_IngredienteBusquedaDTO.cs`, `Plato_IngredienteCreacionDTO.cs`). (BSB-X02, design Cleanup HAZARD).
- [x] **BSB-T18** — Delete safely-unreferenced legacy Cliente DTOs: `GastroGestionBlazor/DTO/Cliente/ClienteCreacionDTO.cs`, `ClienteToListDTO.cs`, `ClienteEdicionDTO.cs`. Leave `ClienteBusquedaDTO.cs` in place (BSB-T16 confirmed). (BSB-C02, BSB-X02).
- [x] **BSB-T19** — Delete safely-unreferenced legacy Ingrediente DTOs: `GastroGestionBlazor/DTO/Ingrediente/IngredienteCreacionDTO.cs`, `IngredienteEdicionDTO.cs`, `IngredienteBusquedaDTO.cs`. Leave `IngredienteToListDTO.cs` in place (BSB-T17 confirmed). (BSB-I02, BSB-X02).
- [x] **BSB-T20** — Remove the Cliente and Ingrediente `CreateMap<...>` blocks from `GastroGestionBlazor/DTO/Mappers/AutoMapperProfiles.cs` (approximately lines 26–46 region). Keep all other entity maps (Plato, Menu, Pedido, etc.) and the `AddAutoMapper` registration. (BSB-X02, ADR-2).
- [x] **BSB-T21** — Grep the entire solution for `localhost:5001`. Assert zero matches. If any remain, replace with relative URI or remove the dead code. (BSB-C01, BSB-I01).

---

## Phase 5: Verification — Build Green + Manual Smoke

- [x] **BSB-T22** — Run `dotnet build GastroGestionBlazor.sln`. Confirm zero errors and zero warnings related to this change. Fix any compilation failures before proceeding to smoke test.
- [x] **BSB-T23** — Manual smoke test checklist (ready for execution — checklist below) (run against live backend at `https://localhost:7126`):
  - [ ] Navigate to Clientes page authenticated → list loads (not empty, no spinner stuck). DevTools Network: request goes to `https://localhost:7126/clientes`, NOT `localhost:5001`. Bearer header present.
  - [ ] Open create form → CondicionIVA `<select>` shows exactly 4 options (ResponsableInscripto, Monotributista, ConsumidorFinal, ExentoIVA). Select one and submit with valid Nombre → new row appears in list without full reload.
  - [ ] Select ResponsableInscripto, leave CUIT blank, submit → page shows the Spanish 422 error text from the backend (not a generic client error).
  - [ ] Activo badge renders green for active and grey+muted for inactive rows.
  - [ ] Buscar panel disabled; edit and trash row buttons disabled; "no disponible aún" note visible. Clicking disabled controls makes no network request.
  - [ ] Navigate to Ingredientes page → list loads from `https://localhost:7126/ingredientes`. UnidadBase `<select>` shows exactly 6 options.
  - [ ] Create an ingrediente with valid Nombre + UnidadBase → new row appears.
  - [ ] Auth still works: expire session or clear token → both pages show auth error or redirect to login (Slice A behavior unchanged).
  - [ ] Grep solution for `localhost:5001` → zero matches.
  - [ ] Grep `ClienteService.cs` and `IngredienteService.cs` for `localhost` → zero matches.

---

## Dependency Order

```
BSB-T01, BSB-T02 (enums)
    └─ BSB-T03, BSB-T04, BSB-T05, BSB-T06, BSB-T07, BSB-T08 (records)
        └─ BSB-T09, BSB-T10 (services)
            └─ BSB-T11 (build gate)
                └─ BSB-T12, BSB-T13, BSB-T14, BSB-T15 (pages)
                    └─ BSB-T16, BSB-T17 (ref-search — MUST precede deletes)
                        └─ BSB-T18, BSB-T19, BSB-T20, BSB-T21 (cleanup)
                            └─ BSB-T22, BSB-T23 (build green + smoke)
```

Parallel opportunities: BSB-T01 and BSB-T02 can be written simultaneously. BSB-T03–T08 can be written simultaneously after T01+T02. BSB-T09 and BSB-T10 can be written simultaneously after T03–T08. BSB-T12+T13 and BSB-T14+T15 can be written simultaneously after BSB-T11. BSB-T16 and BSB-T17 can be run simultaneously.

## PR Split Recommendation (High risk — ask-on-risk)

**PR 1** (Unit 1): BSB-T01 through BSB-T11 — new `Contracts/` files + service rewrites. Build-verifiable independently. Base: `feat/ot-workflow-pr1a`.

**PR 2** (Unit 2): BSB-T12 through BSB-T23 — Razor rewrites + AutoMapper cleanup + legacy DTO deletions + smoke. Base: PR 1 branch. Diff stays focused on pages and dead-code removal.

Decision needed before apply: Yes — confirm chain strategy (stacked-to-main vs feature-branch-chain) before sdd-apply starts.
