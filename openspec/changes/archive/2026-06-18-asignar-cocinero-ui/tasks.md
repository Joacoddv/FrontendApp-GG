# Tasks: asignar-cocinero-ui

**Change**: asignar-cocinero-ui
**Spec topic**: sdd/asignar-cocinero-ui/spec
**Design topic**: sdd/asignar-cocinero-ui/design
**Date**: 2026-06-18
**Apply status**: ALL DONE (2026-06-18)

---

## Review Workload Forecast

| Metric                        | Estimate             |
|-------------------------------|----------------------|
| Files touched                 | 2 modified + 1 new   |
| Estimated lines added         | ~72                  |
| Estimated lines deleted       | 0                    |
| Total estimated changed lines | ~72                  |
| 400-line budget risk          | **Low**              |
| Chained PRs recommended       | No                   |
| Decision needed before apply  | No                   |

All changes fit comfortably within a single PR. No splitting required.

---

## Delivery

- **Branch**: `feat/asignar-cocinero-ui`
- **Strategy**: single PR to `main`
- **PR type label**: `type:feature`
- **Commit shape**: one work-unit commit per task; squash-merge or keep as-is.

---

## Task List

### AC-T01 — Create `Contracts/Usuarios/CocineroResponse.cs`

| Field       | Value                                             |
|-------------|---------------------------------------------------|
| Status      | [x] done                                          |
| Requires    | nothing (standalone new file)                     |
| Parallelism | can run first or alongside AC-T02                 |
| Satisfies   | BC-12 (CocineroResponse client contract)          |
| Commit msg  | `feat(contracts): add CocineroResponse for cook assignment` |
| Commit SHA  | 906b96a |

**Verification:** `dotnet build` from frontend repo root produces 0 errors.

---

### AC-T02 — Add `GetCocinerosAsync` to `KitchenBoardService.cs`

| Field       | Value                                                        |
|-------------|--------------------------------------------------------------|
| Status      | [x] done                                                     |
| Requires    | AC-T01 (needs `CocineroResponse` type to compile)            |
| Parallelism | sequential after AC-T01                                      |
| Satisfies   | BC-08 (cocinero list loading), BC-12 (uses new contract)     |
| Commit msg  | `feat(kitchen): add GetCocinerosAsync and AsignarCocineroAsync` |
| Commit SHA  | 09cf106 |

**Verification:** `dotnet build` from frontend repo root produces 0 errors.

---

### AC-T03 — Add `AsignarCocineroAsync` to `KitchenBoardService.cs`

| Field       | Value                                                              |
|-------------|--------------------------------------------------------------------|
| Status      | [x] done                                                           |
| Requires    | AC-T01 (CocineroResponse type present — shares the using)          |
| Parallelism | can be done in the same commit as AC-T02 (same file, same PR unit) |
| Satisfies   | BC-10 (asignar cocinero submission)                                |
| Commit SHA  | 09cf106 (same as AC-T02) |

**Note:** AC-T02 and AC-T03 both modify `KitchenBoardService.cs` and were done in the same editing pass (not split across separate edits). They are listed separately to track requirement coverage but shipped in one commit.

---

### AC-T04 — Add fields + `@using` + `GetCocinerosAsync` call in `Cocina.razor`

| Field       | Value                                                              |
|-------------|------|
| Status      | [x] done                                                           |
| Requires    | AC-T01, AC-T02 (CocineroResponse and GetCocinerosAsync must exist) |
| Parallelism | sequential after AC-T02                                            |
| Satisfies   | BC-08 (load cocineros on init, page-scoped, once)                  |
| Commit msg  | `feat(cocina): add cook picker and assignment to Creada OT cards`  |

**Verification:** part of full `dotnet build` in AC-T06.

---

### AC-T05 — Add picker markup + `IsAsignarDisabled` + `OnAsignarCocinero` in `Cocina.razor`

| Field       | Value                                                                   |
|-------------|---------|
| Status      | [x] done                                                                |
| Requires    | AC-T04 (fields must exist), AC-T03 (AsignarCocineroAsync must exist)    |
| Parallelism | sequential after AC-T04                                                 |
| Satisfies   | BC-09 (picker on Creada cards only), BC-10 (submit), BC-11 (guard)      |
| Commit msg  | `feat(cocina): add cook picker and assignment to Creada OT cards`  |

**Verification:** part of full `dotnet build` in AC-T06.

---

### AC-T06 — Extend `OnOtChanged` to clear `_assigning` + build verification

| Field       | Value                                                              |
|-------------|----|
| Status      | [x] done                                                           |
| Requires    | AC-T05 (`_assigning` field must exist)                             |
| Parallelism | sequential after AC-T05                                            |
| Satisfies   | BC-10 (echo clears in-flight state), BC-08 (no re-fetch on echo)   |
| Commit msg  | `feat(cocina): add cook picker and assignment to Creada OT cards`  |
| Commit SHA  | 2c34b4f |

**Build verification result:**
```
dotnet build GastroGestionBlazor.sln
Result: 0 errors, 2 pre-existing warnings (no new warnings introduced)
```

---

## Execution Order (Sequential)

```
AC-T01 (CocineroResponse.cs — new file)
  └─> AC-T02 + AC-T03 (KitchenBoardService.cs — both methods, single pass)
        └─> AC-T04 (Cocina.razor fields + @using + init load)
              └─> AC-T05 (Cocina.razor markup + handlers)
                    └─> AC-T06 (OnOtChanged _assigning.Remove + dotnet build)
```

All tasks were strictly sequential; each depended on the previous output. No parallel execution occurred.

---

## Commit Plan (work-unit commits)

| Commit | Tasks | Message | SHA |
|--------|-------|---------|-----|
| 1 | AC-T01 | `feat(contracts): add CocineroResponse for cook assignment` | 906b96a |
| 2 | AC-T02 + AC-T03 | `feat(kitchen): add GetCocinerosAsync and AsignarCocineroAsync` | 09cf106 |
| 3 | AC-T04 + AC-T05 + AC-T06 | `feat(cocina): add cook picker and assignment to Creada OT cards` | 2c34b4f |

---

## Task Completion Status

- [x] AC-T01: CocineroResponse.cs created with correct signature — SHA 906b96a
- [x] AC-T02: GetCocinerosAsync added — SHA 09cf106
- [x] AC-T03: AsignarCocineroAsync added — SHA 09cf106
- [x] AC-T04: Fields, @using, init load call added — SHA 2c34b4f
- [x] AC-T05: Picker markup, IsAsignarDisabled, OnAsignarCocinero added — SHA 2c34b4f
- [x] AC-T06: OnOtChanged extended to clear _assigning — SHA 2c34b4f (line 218)

All 6 tasks confirmed done. Code state matches apply-progress artifact.
