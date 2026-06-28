using GastroGestionBlazor.Contracts.Enums;

namespace GastroGestionBlazor.Contracts.Pedidos;

/// <summary>
/// Request body for POST /pedidos. Mirrors the backend CrearPedidoRequest.
/// Salon requires <see cref="MesaId"/>; Delivery requires <see cref="DireccionEntrega"/>.
/// </summary>
public sealed record CrearPedidoRequest(
    TipoPedido Tipo,
    Guid? MesaId,
    Guid? ClienteId,
    DireccionEntregaRequest? DireccionEntrega);

public sealed record DireccionEntregaRequest(
    string Calle,
    string Numero,
    string Ciudad,
    string Provincia,
    string CodigoPostal,
    string? Piso,
    string? Departamento,
    string? Zona = null);

/// <summary>Request body for POST /pedidos/{id}/lineas.</summary>
public sealed record AgregarLineaRequest(
    Guid PlatoId,
    int Cantidad,
    string? Observaciones);

/// <summary>Request body for POST /pedidos/{id}/transicion.</summary>
public sealed record TransicionarEstadoRequest(EstadoPedido EstadoNuevo);

/// <summary>Request body for PUT /pedidos/{id}/lineas/{lineaId} — edit quantity/notes.</summary>
public sealed record ActualizarLineaRequest(int Cantidad, string? Observaciones);
