using GastroGestionBlazor.Contracts.Enums;

namespace GastroGestionBlazor.Contracts.Stock;

/// <summary>Per-ingredient stock balance row (GET /stock/balances).</summary>
public sealed record IngredienteBalanceResponse(
    Guid IngredienteId,
    string Nombre,
    UnidadDeMedida Unidad,
    bool Activo,
    decimal Balance);

/// <summary>Body for POST /stock/movimientos (manual types: Compra, Ajuste, Merma).</summary>
public sealed record RegistrarMovimientoStockRequest(
    Guid IngredienteId,
    TipoMovimientoStock Tipo,
    decimal Cantidad,
    Guid? OrdenTrabajoId,
    Guid? LineaPedidoId);

/// <summary>A single stock-ledger entry (GET /stock/movimientos/{ingredienteId}).</summary>
public sealed record MovimientoStockResponse(
    Guid Id,
    Guid IngredienteId,
    TipoMovimientoStock Tipo,
    decimal Cantidad,
    DateTime FechaMovimiento);
