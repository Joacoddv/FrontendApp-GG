# Proposal: asignar-cocinero-ui

## Intent

**Problem.** The kitchen board (`Pages/Cocina.razor`, route `/ordenes-trabajo`) shows work orders (OTs) grouped by state, but a cook or administrator has no way to take ownership of an OT in the `Creada` column from the UI. The assignment endpoints already exist and are merged on the backend, yet the only interactive action available today is `marcar-lista` on `Preparandose` OTs. Assigning a cook currently requires hitting the API outside the app, which makes the board read-only at the most important decision point of the kitchen workflow.

**Why now.** The backend contracts (`GET /usuarios/cocineros` and `POST /pedidos/{pedidoId}/ordenes-trabajo/{otId}/asignar-cocinero`) are live. Closing the UI gap unblocks the core kitchen flow: an OT must move `Creada -> Preparandose` by assignment before a cook can later mark it `Lista`. Without this, the board's first column is a dead end.

**Success looks like.** A Cocinero or Administrador opens the board, picks a cook for any `Creada` OT, clicks "Asignar", and the OT moves to the `Preparandose` column automatically (via the existing SignalR `OtChanged` echo) with the assigned cook now visible — no manual refresh, no page-local state guessing, and server-side errors surfaced inline in Spanish.

## Scope

### In scope
- Add a per-card cook picker (`<select>`) plus an "Asignar" button to each `Creada` card in `Pages/Cocina.razor`.
- Add `GetCocinerosAsync` and `AsignarCocineroAsync` to `Services/KitchenBoardService.cs`.
- Create the frontend contract record `Contracts/Usuarios/CocineroResponse.cs` mirroring the backend `CocineroResponse(Guid Id, string NombreCompleto)` exactly.
- Echo-driven refresh: success path mutates no board state; the OT moves columns when the `OtChanged` SignalR message arrives (clone of the `marcar-lista` pattern).
- Per-OT in-flight tracking (`HashSet<Guid> _assigning`) and per-OT selection state (`Dictionary<Guid, Guid> _pickerSelection`) so each card is independent.
- Inline server-error surfacing through the existing `_errorMessage` alert and `ThrowApiExceptionAsync` ProblemDetails plumbing.

### Out of scope (non-goals)
- No backend changes — both endpoints exist and are merged.
- No automated frontend tests — none exist in this repo and `strict_tdd` is false.
- No catalog CRUD UI — that is a separate change.
- No child-component extraction (e.g. an `OtCard` component) unless trivially required.
- No new service class — the cocinero fetch lives on `KitchenBoardService` to keep the kitchen domain cohesive; no standalone `UsuarioService` for a single endpoint.
- No DI changes — `KitchenBoardService` is already registered scoped with the `AuthorizedApi` HttpClient.
- No live/streaming refresh of the cocinero list — it is loaded once on `OnInitializedAsync` (see edge cases).

## Approach

Clone the existing `marcar-lista` interaction end to end so the new action inherits the board's proven echo-driven, error-safe contract.

1. **Contract.** Add `CocineroResponse(Guid Id, string NombreCompleto)` under `Contracts/Usuarios/`. `Id` is the value sent as `cocineroLegajoId`; the picker displays `NombreCompleto`.

2. **Service layer (`KitchenBoardService.cs`).**
   - `GetCocinerosAsync(CancellationToken)` -> `GET usuarios/cocineros` -> `List<CocineroResponse>`. On non-success, route through `ThrowApiExceptionAsync` with a Spanish fallback.
   - `AsignarCocineroAsync(Guid pedidoId, Guid otId, Guid cocineroLegajoId, CancellationToken)` -> `POST pedidos/{pedidoId}/ordenes-trabajo/{otId}/asignar-cocinero` with body `{ "cocineroLegajoId": "<guid>" }`. Discard the `200 OK` body (board state arrives via echo); on non-success, `ThrowApiExceptionAsync` with a Spanish fallback message.

3. **UI (`Cocina.razor`).**
   - Load the cocineros list once in `OnInitializedAsync` alongside the board.
   - In the `Creada` column card, render a `<select>` bound to `_pickerSelection[OtId]` (options from the loaded list) plus an "Asignar" button.
   - `OnAsignar(item)`: add `OtId` to `_assigning`, call `BoardService.AsignarCocineroAsync(item.PedidoId, item.OtId, selectedId)`, catch `ApiException` and generic `Exception` to set `_errorMessage` and remove from `_assigning` on error. Do NOT remove from `_assigning` and do NOT mutate board state on success — the `OtChanged` echo handler (`OnOtChanged`) already removes the in-flight marker and calls `StateHasChanged()`.
   - Extend `OnOtChanged` to also clear `_assigning` (mirroring how it clears `_marking`).
   - Button `disabled` when the OT is in `_assigning` OR no cook is selected (guards against sending `Guid.Empty`).

No additional UI role check is needed: the whole page is gated at `@attribute [Authorize(Roles = "Cocinero,Administrador")]`, and the backend enforces the same roles (403 otherwise).

## Edge cases
- **Empty / `Guid.Empty` selection** -> "Asignar" button disabled so no request is sent (avoids the FluentValidation 400 for an empty `cocineroLegajoId`).
- **Empty cocineros list** -> picker has no selectable cook, so the button stays disabled; no assignment is possible until cooks exist.
- **Concurrent assignment race** -> the losing call returns `422` because the OT is no longer `Creada`; the Spanish `ProblemDetails.detail` is surfaced via `ThrowApiExceptionAsync` + `_errorMessage`.
- **Stale cocinero list** -> the list is loaded once on init; cooks added afterward will not appear until a page reload. Acceptable for MVP (could later be refreshed on SignalR reconnect via `ReHydrateAsync`).
- **State transition on success** -> after a successful assign the OT moves `Creada -> Preparandose`; do not assume it stays in the `Creada` column. The echo handler relocates the card and clears the in-flight marker.

## Risks
- **Echo dependency.** If the `OtChanged` SignalR message never arrives (dropped connection), the card's button stays disabled and the card stays in `Creada` until rehydration/reconnect. This is the same exposure the existing `marcar-lista` action already carries; no new risk surface is introduced.
- **Contract drift.** `CocineroResponse` must match the backend record exactly (`Guid Id, string NombreCompleto`); a mismatch silently breaks deserialization. Mitigated by mirroring the verified backend contract.

## Open questions
None blocking. Scope is locked by the user; backend contracts are verified at file:line in the exploration artifact (`sdd/asignar-cocinero-ui/explore`). Proceed to spec.
