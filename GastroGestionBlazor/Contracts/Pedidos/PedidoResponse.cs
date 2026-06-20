using GastroGestionBlazor.Contracts.Enums;

namespace GastroGestionBlazor.Contracts.Pedidos;

/// <summary>
/// Read model for an order, mirroring the backend GET /pedidos/{id} contract.
/// Estado is kept as a string — the API serializes enums as strings.
/// </summary>
public sealed record PedidoResponse(
    Guid Id,
    TipoPedido Tipo,
    string Estado,
    Guid? MesaId,
    Guid? ClienteId,
    DateTime CreadoEnUtc,
    IReadOnlyList<LineaPedidoResponse> Lineas);

public sealed record LineaPedidoResponse(
    Guid Id,
    Guid PlatoId,
    int Cantidad,
    string? Observaciones,
    decimal? PrecioUnitario,
    string? Moneda,
    decimal? SubtotalLinea,
    decimal? TotalLinea);
