using GastroGestionBlazor.Contracts.Enums;

namespace GastroGestionBlazor.Contracts.Facturacion;

public sealed record CrearFacturaRequest(
    Guid ClienteId,
    Guid[] PedidoIds,
    TipoComprobanteSolicitado Tipo);

public sealed record RegistrarPagoRequest(
    decimal Monto,
    MetodoPago MetodoPago);

public sealed record AnularFacturaRequest(string Motivo);

public sealed record AsignarCaeRequest(string Cae, DateOnly Vencimiento);
