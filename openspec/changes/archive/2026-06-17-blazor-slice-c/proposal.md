# Proposal: Blazor Slice C — Realtime Kitchen Board (Cocina)

## Intent

Phase 7 final slice. The backend kitchen workflow (Phase 6) and SignalR prep are complete, but the WASM frontend has ZERO kitchen UI and ZERO SignalR client. Cooks have no live screen. This slice delivers the headline feature: a role-gated `Cocina` page that loads the kitchen board and updates LIVE — no refresh — when an OT changes server-side. Completes the Phase 7 frontend (Slice A auth + Slice B screens already merged).

## Verified Backend Contract (read from source, not assumed)

- **Board GET**: `GET /ordenes-trabajo?estado={EstadoOT?}` → `200` `OrdenTrabajoBoardResponse[]`. Role-gated in-handler to `Cocinero`/`Administrador` (else `403`). Invalid `estado` → `400`.
- **Hub**: `/hubs/kitchen` (`MapHub<KitchenHub>`), `[Authorize(Roles="Cocinero,Administrador")]`, joins group `"kitchen"` on connect.
- **Event**: server broadcasts `"OtChanged"` with payload `OrdenTrabajoBoardResponse` (same shape as a board item).
- **Payload `OrdenTrabajoBoardResponse`**: `OtId:Guid, PedidoId:Guid, PedidoTipo:string(TipoPedido), PlatoId:Guid, LineaPedidoId:Guid, Estado:string(EstadoOT), CocineroAsignadoLegajoId:Guid?`. Enums serialize as STRINGS globally (`JsonStringEnumConverter`).
- **EstadoOT**: `Creada, Preparandose, Lista, Cancelada`. **TipoPedido**: `Salon, TakeAway, Delivery`.
- **Auth on WS**: browsers cannot set the `Authorization` header on WebSocket upgrade; backend reads `?access_token=` for `/hubs/kitchen` paths. Client MUST supply the JWT via `AccessTokenProvider`. Token source: `IAuthService.GetTokenAsync()`. API base: `https://localhost:7126` (`ApiBaseUrl`).
- **Mutations** (out of A-scope): `POST /pedidos/{pedidoId}/ordenes-trabajo/{otId}/asignar-cocinero` body `{CocineroLegajoId:Guid}`; `POST .../marcar-lista` (no body). Both Cocinero/Admin, return `OrdenTrabajoResponse`. `POST /pedidos/{pedidoId}/ordenes-trabajo` (generar) is Mozo/Admin.

## Scope (drafted as Option A — read-only board)

### In Scope
- Add `Microsoft.AspNetCore.SignalR.Client` package.
- `Cocina.razor` at `/cocina`, page-level `[Authorize(Roles="Cocinero,Administrador")]`, registered nav (already gated, Slice A).
- Initial board load via `AuthorizedApi` `GET /ordenes-trabajo`; contracts/DTO for `OrdenTrabajoBoardResponse` + `EstadoOT`/`TipoPedido` enums (Slice B pattern).
- `HubConnection` to `/hubs/kitchen` with `AccessTokenProvider` = `GetTokenAsync()`; auto-reconnect.
- Listen `"OtChanged"` → upsert item by `OtId`, re-render; if `Cancelada`, drop from board.
- Connection lifecycle: connect on init, `DisposeAsync` on navigate-away.
- Board grouped by `EstadoOT` with Spanish labels; loading / empty / disconnected states.

### Out of Scope (non-goals)
- Mutation actions (asignar cocinero, marcar lista) → deferred to Slice C2 (Option B).
- OT generation (mozo / Pedido flow) — different role/workflow, not the kitchen screen.
- Any backend change. Automated tests (none in repo; manual smoke test).

## Capabilities

### New Capabilities
- `kitchen-board-realtime`: WASM page rendering the live kitchen board grouped by EstadoOT, hydrated by GET and kept current by the SignalR `OtChanged` event.

### Modified Capabilities
- None.

## Approach

Slice B DTO/enum pattern for the board contract. New `OrdenTrabajoService` (or page-inline) does the initial GET via `AuthorizedApi`. `HubConnectionBuilder.WithUrl(ApiBaseUrl + "/hubs/kitchen", o => o.AccessTokenProvider = GetTokenAsync).WithAutomaticReconnect()`. `On<OrdenTrabajoBoardResponse>("OtChanged", ...)` merges by `OtId` into an in-memory list grouped by Estado via `InvokeAsync(StateHasChanged)`. Page implements `IAsyncDisposable` to stop the connection.

## KEY PRODUCT DECISION (open question — do NOT silently decide)

**A (recommended) vs B for Slice C:**
- **(A) READ-ONLY realtime board** — display + live update only. Smaller, lower-risk, delivers the "live kitchen screen" value cleanly. **Recommended as Slice C**, with mutations as a fast-follow **Slice C2**.
- **(B) INTERACTIVE board** — A plus cocinero actions (asignar cocinero, marcar lista) wired to POSTs, board reflecting changes via the SignalR echo.

Recommendation: ship **A** now (proves realtime + role gate, ends Phase 7), unless the user judges the two POSTs trivial enough to fold in. `asignar-cocinero` needs a cocinero/legajo picker (no list endpoint surfaced) — that lookup gap argues for deferring B.

## Other Open Questions
1. Confirm `wss://` works at `https://localhost:7126` dev cert; CORS/WebSocket origin already allows the Blazor dev origin? (Backend CORS noted for Blazor ports in recon.)
2. Filter by `EstadoOT` (server `?estado=`) or always load all and group client-side? (Draft: load all, group client-side.)
3. Should `Cancelada` OTs be hidden or shown in a column? (Draft: hidden.)

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `GastroGestionBlazor.csproj` | Modified | Add `Microsoft.AspNetCore.SignalR.Client` |
| `Pages/Cocina.razor` | New | Board page + hub client + lifecycle |
| `Contracts/Pedidos/*` | New | `OrdenTrabajoBoardResponse`, `EstadoOT`, `TipoPedido` |
| `Services/` | New | Board GET via `AuthorizedApi` (optional service) |
| `Layout/NavMenu.razor` | None | Kitchen entry already role-gated (Slice A) |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| WS auth: token not sent / expired on reconnect | Med | `AccessTokenProvider` re-invoked each (re)connect; pull fresh from `GetTokenAsync()` |
| Event-name/payload coupling to backend | Med | Lock to verified `"OtChanged"` + `OrdenTrabajoBoardResponse`; document in code |
| Reconnect gaps lose updates | Med | `WithAutomaticReconnect()` + re-run GET on `Reconnected` to resync |
| No automated tests | High | Manual smoke test; keep page logic thin |
| Dev TLS / WebSocket origin | Low | Verify dev cert + CORS before sign-off |

## Rollback Plan

Revert the Slice C commit/PR: removes `Cocina.razor`, the SignalR package, and the board contracts. Slices A and B are untouched (kitchen nav entry was added in A and simply leads nowhere again). No backend or migration impact.

## Dependencies

- Slice A (merged): JWT auth, `AuthorizedApi`, `GetTokenAsync()`, role-gated nav.
- Slice B (merged): Contracts DTO/enum + 422 pattern.
- Backend Phase 6 + SignalR prep (merged on backend `main`).

## Success Criteria

- [ ] Cocinero/Administrador opens `/cocina` and sees the board grouped by EstadoOT (Spanish labels).
- [ ] A backend OT state change pushes a live board update without page refresh.
- [ ] Non-Cocinero/Admin is gated out of `/cocina` (and the hub rejects them).
- [ ] Connection survives transient drops (auto-reconnect + resync) and disposes on navigate-away.
