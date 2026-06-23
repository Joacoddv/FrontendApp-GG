using GastroGestionBlazor.Contracts.Enums;

namespace GastroGestionBlazor.Contracts.Facturacion;

/// <summary>
/// Aggregated sales figures over an optional date range.
/// Excludes Cancelada / Anulada invoices.
/// </summary>
public sealed record ReporteVentasResponse(
    DateTime? Desde,
    DateTime? Hasta,
    int CantidadFacturas,
    decimal TotalFacturado,
    decimal TotalCobrado,
    IReadOnlyList<ReporteTipoResponse> PorTipo,
    IReadOnlyList<ReporteMetodoResponse> PorMetodoPago);

public sealed record ReporteTipoResponse(
    TipoComprobante Tipo,
    int Cantidad,
    decimal Total);

public sealed record ReporteMetodoResponse(
    MetodoPago Metodo,
    decimal Total);
