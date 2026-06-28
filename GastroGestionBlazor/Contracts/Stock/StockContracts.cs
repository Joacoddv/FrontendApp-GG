using GastroGestionBlazor.Contracts.Enums;

namespace GastroGestionBlazor.Contracts.Stock;

/// <summary>Per-ingredient stock balance row (GET /stock/balances).</summary>
public sealed record IngredienteBalanceResponse(
    Guid IngredienteId,
    string Nombre,
    UnidadDeMedida Unidad,
    bool Activo,
    decimal Balance,
    decimal StockMinimo,
    bool EnAlerta);

/// <summary>Body for PUT /ingredientes/{id}/stock-minimo — sets the reorder threshold.</summary>
public sealed record ActualizarStockMinimoRequest(decimal StockMinimo);

/// <summary>Body for POST /stock/movimientos (manual types: Compra, Ajuste, Merma).</summary>
public sealed record RegistrarMovimientoStockRequest(
    Guid IngredienteId,
    TipoMovimientoStock Tipo,
    decimal Cantidad,
    Guid? OrdenTrabajoId,
    Guid? LineaPedidoId,
    Guid? ProveedorId);

/// <summary>A single stock-ledger entry (GET /stock/movimientos/{ingredienteId}).</summary>
public sealed record MovimientoStockResponse(
    Guid Id,
    Guid IngredienteId,
    TipoMovimientoStock Tipo,
    decimal Cantidad,
    DateTime FechaMovimiento,
    Guid? ProveedorId);

/// <summary>Per-dish producible count (GET /stock/producibles).</summary>
public sealed record PlatoProducibleResponse(
    Guid PlatoId,
    string Nombre,
    int MaxProducible);
