# Tasks: Blazor Slice C — Realtime Kitchen Board

Change: `blazor-slice-c`
Mode: STANDARD (strict_tdd: false)
Build: `dotnet build GastroGestionBlazor.sln`
Source root: `GastroGestionBlazor/GastroGestionBlazor/`

---

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 280–340 (6 new files + 3 file edits) |
| 400-line budget risk | Medium |
| Chained PRs recommended | No |
| Suggested split | Single PR — all work shares one coherent dependency chain |
| Delivery strategy | ask-on-risk |
| Chain strategy | pending (not needed at Medium risk) |

Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: pending
400-line budget risk: Medium

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | All 9 tasks below | Single PR | Foundation → services → page → wiring; self-contained |

---

## Phase 1: Foundation (package + contracts)

- [x] **BC-T01** — `GastroGestionBlazor.csproj`: add `<PackageReference Include="Microsoft.AspNetCore.SignalR.Client" Version="8.0.6" />` inside the existing `<ItemGroup>` that holds other 8.0.x refs. *(BC-07, ADR-1)*

- [x] **BC-T02** — `Contracts/Enums/EstadoOT.cs` (NEW): declare `public enum EstadoOT { Creada = 0, Preparandose = 1, Lista = 2, Cancelada = 3 }` under `namespace GastroGestionBlazor.Contracts.Enums`. Member names and ordinal values MUST match the locked design shape exactly. *(BC-07, ADR-4)*

- [x] **BC-T03** — `Contracts/Enums/TipoPedido.cs` (NEW): declare `public enum TipoPedido { Salon = 0, TakeAway = 1, Delivery = 2 }` under `namespace GastroGestionBlazor.Contracts.Enums`. *(BC-07, ADR-4)*

- [x] **BC-T04** — `Contracts/OrdenesTrabajo/OrdenTrabajoBoardItem.cs` (NEW): declare the sealed record with exactly 7 members `(Guid OtId, Guid PedidoId, TipoPedido PedidoTipo, Guid PlatoId, Guid LineaPedidoId, EstadoOT Estado, Guid? CocineroAsignadoLegajoId)` under `namespace GastroGestionBlazor.Contracts.OrdenesTrabajo`. Member name casing must match backend JSON for STJ round-trip. *(BC-07, ADR-3, ADR-5)*

---

## Phase 2: Services

- [x] **BC-T05** — `Services/KitchenBoardService.cs` (NEW): sealed class, ctor `(HttpClient httpClient)`. Static `JsonOptions = new(JsonSerializerDefaults.Web){ Converters = { new JsonStringEnumConverter() } }`. `GetBoardAsync(CancellationToken ct = default)`: GET `"ordenes-trabajo"` (relative), deserialize `List<OrdenTrabajoBoardItem>`, return empty list on null. `MarcarListaAsync(Guid pedidoId, Guid otId, CancellationToken ct = default)`: POST `$"pedidos/{pedidoId}/ordenes-trabajo/{otId}/marcar-lista"` with null content; on `!IsSuccessStatusCode` deserialize `ProblemDetailsResponse`, throw `ApiException(problem?.Detail ?? problem?.Title ?? "No se pudo marcar la orden como lista.")`. Return body ignored. *(BC-02, BC-05, ADR-6, ADR-7)*

- [x] **BC-T06** — `Services/KitchenRealtimeConnection.cs` (NEW): sealed class implementing `IAsyncDisposable`. Ctor `(ApiOptions apiOptions, IAuthService authService)`. Public property `HubConnectionState State`. `StartAsync(Action<OrdenTrabajoBoardItem> onOtChanged, Func<Task> onReconnected, Action onStateChanged, CancellationToken ct = default)`: build connection via `new HubConnectionBuilder().WithUrl($"{apiOptions.ApiBaseUrl.TrimEnd('/')}/hubs/kitchen", o => o.AccessTokenProvider = async () => await authService.GetTokenAsync()).WithAutomaticReconnect().AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter())).Build()`; wire `.On<OrdenTrabajoBoardItem>("OtChanged", onOtChanged)`; wire `Reconnected`, `Reconnecting`, `Closed` handlers (all call `onStateChanged`; `Reconnected` additionally calls `onReconnected`); then `await _connection.StartAsync(ct)`. `DisposeAsync()`: `await _connection.DisposeAsync()`. *(BC-03, BC-04, BC-06, ADR-1, ADR-10)*

---

## Phase 3: DI Wiring

- [x] **BC-T07** — `Program.cs` (MODIFY): add `builder.Services.AddScoped<KitchenBoardService>();` after the existing `AddScoped<IngredienteService>()` line (line 54). No changes to `HttpClient` registration — the default `AuthorizedApi` client is already the factory output. `ApiOptions` singleton and `IAuthService` are already registered. *(BC-02, ADR-10)*

- [x] **BC-T08** — `_Imports.razor` (MODIFY): append two `@using` directives — `@using Microsoft.AspNetCore.SignalR.Client` and `@using GastroGestionBlazor.Contracts.OrdenesTrabajo` — after the existing `@using AutoMapper` line. *(BC-03, BC-07)*

---

## Phase 4: Page

- [x] **BC-T09** — `Pages/Cocina.razor` (NEW): full page implementation per locked design.
  - Directives: `@page "/ordenes-trabajo"`, `@attribute [Authorize(Roles="Cocinero,Administrador")]`, `@implements IAsyncDisposable`, inject `KitchenBoardService BoardService`, inject `ApiOptions ApiOptions`, inject `IAuthService AuthService`.
  - State fields: `List<OrdenTrabajoBoardItem> _items = new()`, `bool _isLoading`, `string? _errorMessage`, `HashSet<Guid> _marking = new()`, `KitchenRealtimeConnection? _conn`, `HubConnectionState _connState`.
  - `OnInitializedAsync`: set `_isLoading = true`; `_items = await BoardService.GetBoardAsync()`; build `_conn = new KitchenRealtimeConnection(ApiOptions, AuthService)`; `await _conn.StartAsync(OnOtChanged, ReHydrateAsync, OnConnStateChanged)`; set `_isLoading = false`.
  - `DisposeAsync()`: `if (_conn != null) await _conn.DisposeAsync()`.
  - `OnOtChanged(OrdenTrabajoBoardItem item)` — async lambda marshaled via `await InvokeAsync(...)`: `_items.RemoveAll(x => x.OtId == item.OtId)`, add if `!= Cancelada`, `_marking.Remove(item.OtId)`, `StateHasChanged()`.
  - `ReHydrateAsync()`: `_items = await BoardService.GetBoardAsync(); await InvokeAsync(StateHasChanged)`.
  - `OnConnStateChanged()`: `_connState = _conn?.State ?? HubConnectionState.Disconnected; InvokeAsync(StateHasChanged)`.
  - `OnMarcarLista(OrdenTrabajoBoardItem item)`: guard `_marking.Contains(item.OtId)` → return; `_marking.Add(item.OtId)`; `try { await BoardService.MarcarListaAsync(item.PedidoId, item.OtId); } catch (ApiException ex) { _errorMessage = ex.Message; _marking.Remove(item.OtId); }`.
  - Markup: connection-status badge (Conectado/Reconectando…/Desconectado); `_isLoading` → "Cargando tablero…" spinner; `_errorMessage` → dismissible red alert; board renders 3 columns (Pendientes / En preparación / Listas) from `_items.Where(i => i.Estado != EstadoOT.Cancelada).GroupBy(i => i.Estado)`; each card shows PedidoTipo (Spanish label map: Salon→Salón, TakeAway→TakeAway, Delivery→Delivery), OtId short ref, Estado, cocinero if assigned; Preparandose cards include "Marcar lista" button disabled when `_marking.Contains(item.OtId)`; empty board → "No hay órdenes de trabajo activas." *(BC-01, BC-02, BC-03, BC-04, BC-05, BC-06, ADR-2, ADR-6, ADR-8, ADR-9)*

---

## Phase 5: Verification

- [x] **BC-T10** — NavMenu verification (READ-ONLY): confirm `Layout/NavMenu.razor` already contains `<AuthorizeView Roles="Cocinero,Administrador">` wrapping `href="ordenes-trabajo"`. Assert ZERO edits needed. If the entry is missing or uses a different href, raise as a blocker. *(BC-01, ADR-2)*

- [x] **BC-T11** — Build green: run `dotnet build GastroGestionBlazor.sln` from the repo root; confirm 0 errors, 0 warnings related to this change. *(all)*

---

## Manual Smoke Checklist

Run after BC-T11 passes. Backend must be running (`https://localhost:7126`).

1. **Board loads (BC-02)**: log in as `Cocinero`; navigate to `/ordenes-trabajo`; confirm "Cargando tablero…" appears briefly, then OTs appear grouped under Pendientes / En preparación / Listas. No Cancelada column.
2. **Live update (BC-04)**: from a second session (or direct API call), transition an OT estado on the backend; confirm the card moves columns in the browser without a page refresh.
3. **Marcar lista (BC-05)**: click "Marcar lista" on a Preparandose OT; confirm button disables during POST; confirm the card moves to Listas via the SignalR echo.
4. **Marcar lista — 422 banner (BC-05)**: attempt to marcar-lista an OT already in Lista; confirm the Spanish 422 error message appears on the card/banner and the button re-enables.
5. **Role gate (BC-01)**: log in as a non-Cocinero/non-Administrador role; navigate to `/ordenes-trabajo`; confirm redirect to login or access-denied, board never renders.
6. **Reconnect re-hydrates (BC-03)**: disable and re-enable the backend; confirm "Reconectando…" badge appears, then "Conectado" after reconnect, and the board resyncs without a manual refresh.
7. **Dispose on navigate-away (BC-03)**: navigate to /clientes from /ordenes-trabajo; confirm no JS console errors or background SignalR traffic after leaving.
8. **Unauthenticated redirect (BC-01)**: open `/ordenes-trabajo` without a session token; confirm redirect to /login.
