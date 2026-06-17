using GastroGestionBlazor.Contracts.Enums;

namespace GastroGestionBlazor.Contracts.OrdenesTrabajo;

public sealed record OrdenTrabajoBoardItem(
    Guid OtId,
    Guid PedidoId,
    TipoPedido PedidoTipo,
    Guid PlatoId,
    Guid LineaPedidoId,
    EstadoOT Estado,
    Guid? CocineroAsignadoLegajoId);
