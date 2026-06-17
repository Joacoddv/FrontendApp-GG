# Proposal — blazor-slice-b: Re-point Cliente + Ingrediente Blazor screens at .NET 8 backend

**What**: SDD proposal for Phase 7 Slice B (blazor-slice-b) — re-point the existing Blazor WASM Cliente + Ingrediente screens at the new .NET 8 backend REST contracts via the authenticated AuthorizedApi client. Drafted under Option A.

**Why**: After the strangler migration, the legacy net48 API at https://localhost:5001 is DEAD. ClienteService.cs and IngredienteService.cs still hardcode that host and use legacy DTO shapes, so both screens are non-functional. Slice A delivered JWT auth + AuthorizedApi client + config-driven ApiBaseUrl (https://localhost:7126) but intentionally left URL/contract rewrites to Slice B.

**Where**: Code to change: Services/ClienteService.cs, Services/IngredienteService.cs, Pages/Clientes.razor, Pages/Ingredientes.razor, DTO/Cliente/*, DTO/Ingrediente/*, DTO/Mappers/AutoMapperProfiles.cs.

---

## Verified Backend Contracts (read from source)

**Cliente `/clientes` (RequireAuthorization)**:
- `POST /clientes` (CrearClienteRequest → 201 + Guid id, Location /clientes/{id})
- `GET /clientes/{id:guid}` → ClienteResponse or 404
- `GET /clientes` → ClienteResponse[]
- ABSENT: NO PUT/edit, NO DELETE/baja, NO Buscar/search

**Ingrediente `/ingredientes` (RequireAuthorization)**:
- `POST /ingredientes` (CrearIngredienteRequest → 201 + Guid id)
- `GET /ingredientes/{id:guid}`
- `GET /ingredientes`
- ABSENT: NO PUT, NO DELETE, NO Buscar

**CrearClienteRequest**: `(string Nombre, CondicionIVA CondicionIVA, string? Cuit, string? Email)`
**ClienteResponse**: `(Guid Id, string Nombre, CondicionIVA CondicionIVA, string? Cuit, string? Email, bool Activo)`
**CrearIngredienteRequest**: `(string Nombre, UnidadDeMedida UnidadBase)`
**IngredienteResponse**: `(Guid Id, string Nombre, UnidadDeMedida UnidadBase, bool Activo)`

Enums serialized as STRINGS on output (global JsonStringEnumConverter in backend Program.cs), integers accepted on input.
- CondicionIVA: ResponsableInscripto(0), Monotributista(1), ConsumidorFinal(2), ExentoIVA(3)
- UnidadDeMedida: Gramo(0), Kilogramo(1), Mililitro(2), Litro(3), Unidad(4), Porcion(5)

Validation: Nombre required (422). CUIT-required-for-ResponsableInscripto is enforced by the DOMAIN (Cliente.Crear throws → 422), not the FluentValidation validator — UI must surface the 422.

---

## Decision: Option A (chosen)

**Option A** (list + create + view only) — remove/disable the unsupported controls (RECOMMENDED — Slice B is re-pointing, not feature expansion). This was confirmed and selected.

**Option B** (expand backend first) — separate src/ repo work then wire full CRUD. Rejected for Slice B.

---

## Scope (Option A)

- Rewrite both services to relative URLs on AuthorizedApi
- Replace frontend DTOs with records matching verified contracts 1:1
- Update both .razor pages to new fields with enum `<select>` for CondicionIVA/UnidadBase
- Drop legacy fields (Apellido/Nro_Doc/Tipo_Doc/Numero_Cliente for Cliente; Descripcion/free-text Medida for Ingrediente)
- Disable (not remove) Buscar/Editar/Eliminar with "no disponible aún" note
- Trim AutoMapper profiles (remove Cliente + Ingrediente maps; keep other entities)

## Non-Goals

- Kitchen/SignalR (Slice C)
- Backend edit/delete/search
- Test project
- Auth changes (Slice A done and frozen)

## Success Criteria

- List + create work end-to-end against real backend (localhost:7126)
- Enum select sends accepted values; 422 surfaced for domain-rule violations
- Zero localhost:5001 references remain; no 404s from removed controls
- dotnet build green

---

> Note: proposal.md was saved to Engram (observation #126) but never committed to git as a tracked file. Added to archive directly from Engram at archive time (2026-06-17).
