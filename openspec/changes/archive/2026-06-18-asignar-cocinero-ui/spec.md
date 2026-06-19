# Delta for Kitchen — asignar-cocinero-ui

**Change**: asignar-cocinero-ui
**Extends capability**: kitchen-board-realtime
**Affects**: BC-05 context (new parallel action), BC-07 (new contract record)
**Date**: 2026-06-18

---

## ADDED Requirements

---

### Requirement: BC-08 — Cocinero List Loading

On board initialization the system MUST call `GET /usuarios/cocineros` via the authenticated HTTP
client and cache the result in a page-scoped list. The list MUST be loaded once (not per card). If
the list is empty the assign action MUST be disabled for all Creada cards. The system MUST NOT
re-fetch the cocinero list on every `OtChanged` event.

#### Scenario: Cocineros loaded on init — success

- GIVEN the user opens `/ordenes-trabajo`
- WHEN `OnInitializedAsync` completes
- THEN `GET /usuarios/cocineros` has been called exactly once
- AND the resulting `List<CocineroResponse>` is available for all Creada card pickers

#### Scenario: Cocineros list is empty

- GIVEN `GET /usuarios/cocineros` returns `200 []`
- WHEN the board renders Creada OTs
- THEN the picker has no options and the "Asignar" button is disabled for every Creada card

#### Scenario: Cocineros load fails (network / 403)

- GIVEN `GET /usuarios/cocineros` throws or returns a non-200 status
- WHEN the exception is caught
- THEN the cocinero list remains empty, the assign action is disabled
- AND the existing `_errorMessage` mechanism surfaces a Spanish error message

---

### Requirement: BC-09 — Cook Picker on Creada Cards

Each OT card with `Estado == Creada` MUST render a `<select>` element populated with the loaded
cocinero list (display: `NombreCompleto`; value: `Id`). The picker MUST be bound per-OT via a
`Dictionary<Guid, Guid> _pickerSelection` keyed by `OtId`. Cards in `Preparandose` or `Lista` MUST
NOT render the picker or the "Asignar" button. No inline role check is required inside the card —
the page-level `[Authorize]` gate is sufficient.

#### Scenario: Picker renders only on Creada cards

- GIVEN the board contains OTs in Creada, Preparandose, and Lista states
- WHEN the page renders
- THEN the `<select>` + "Asignar" button appear exclusively on Creada cards
- AND Preparandose / Lista cards are unaffected

#### Scenario: Each card has independent picker state

- GIVEN two OTs in Creada state are visible
- WHEN the user selects different cocineros in each card's picker
- THEN both selections are retained independently and do not interfere with each other

---

### Requirement: BC-10 — Asignar Cocinero Submission

Clicking the "Asignar" button MUST POST to
`/pedidos/{pedidoId}/ordenes-trabajo/{otId}/asignar-cocinero` with body
`{ "cocineroLegajoId": "<selectedGuid>" }`. The transition to `Preparandose` is **echo-driven**:
the client MUST NOT mutate local state on success; the server broadcasts `OtChanged` and the
existing BC-04 handler moves the card. The button MUST be disabled during the in-flight request
(controlled via `HashSet<Guid> _assigning`). On the `OtChanged` echo the system MUST remove the OT
from `_assigning` (mirrors the `_marking` pattern from BC-05).

#### Scenario: Asignar — success (echo-driven card move)

- GIVEN a Creada OT card with a cocinero selected in the picker
- WHEN the user clicks "Asignar"
- THEN the button is disabled immediately (OtId added to `_assigning`)
- AND a POST is sent with the selected `cocineroLegajoId`
- AND on `OtChanged` echo the card moves to "En preparación" and `_assigning` is cleared

#### Scenario: Asignar — button stays disabled until echo

- GIVEN the POST returns 200
- WHEN the response is received but before `OtChanged` arrives
- THEN the button remains disabled
- AND local board state is NOT mutated by the client

#### Scenario: Asignar — 422 concurrent race

- GIVEN another user assigned the same OT first
- WHEN the POST returns 422
- THEN the Spanish `ProblemDetails.Detail` message is shown via `_errorMessage`
- AND the OtId is removed from `_assigning` (button re-enabled)
- AND local board state is NOT changed by the client

#### Scenario: Asignar — 404 pedido not found

- GIVEN the pedido was deleted after the board loaded
- WHEN the POST returns 404
- THEN a Spanish error message is shown
- AND the OtId is removed from `_assigning` (button re-enabled)

#### Scenario: Asignar — network error

- GIVEN the POST fails with a network/timeout exception
- WHEN the exception is caught
- THEN a generic Spanish error message is shown
- AND the OtId is removed from `_assigning` (button re-enabled)
- AND local board state is NOT changed

---

### Requirement: BC-11 — Submit Guard (Empty / No-Selection)

The "Asignar" button MUST be disabled when no cocinero is selected in the picker for that OT
(i.e., the bound value is `Guid.Empty` or the picker has no options). The system MUST NOT
submit a `POST` with an empty `cocineroLegajoId` (avoids backend 400 from FluentValidation).

#### Scenario: Button disabled with no cocinero selected

- GIVEN a Creada card is rendered with a populated cocinero picker
- WHEN no cocinero has been selected (default / Guid.Empty)
- THEN the "Asignar" button is disabled
- AND no POST is attempted on click

#### Scenario: Button enabled when cocinero is selected

- GIVEN a Creada card with at least one cocinero in the picker
- WHEN the user selects a cocinero from the dropdown
- THEN the "Asignar" button becomes enabled (provided the OT is not in-flight)

---

### Requirement: BC-12 — CocineroResponse Client Contract

The frontend MUST define a `CocineroResponse` sealed record in `Contracts/Usuarios/` matching the
backend contract exactly: `(Guid Id, string NombreCompleto)`. `Id` is used as `cocineroLegajoId`
in the POST body; `NombreCompleto` is the display label in the picker. No additional fields are
permitted that could cause deserialization drift.

#### Scenario: Contract round-trip

- GIVEN the backend returns `[{ "id": "<guid>", "nombreCompleto": "Jane Doe" }]`
- WHEN deserialized into `List<CocineroResponse>`
- THEN `Id` and `NombreCompleto` are populated without errors
- AND the `Id` value is used verbatim as `cocineroLegajoId` in the assignment POST

---

## Out of Scope

- Backend changes (all endpoints are live).
- Automated tests (strict_tdd: false for this project).
- Live refresh of the cocinero list after initial load (deferred to a future slice).
- Child-component extraction for OT cards.
- Catalog CRUD or any other kitchen action beyond assignment.
