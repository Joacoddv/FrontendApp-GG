namespace GastroGestionBlazor.Contracts.Enums;

/// <summary>Order state — mirrors the backend Domain.Enums.EstadoPedido.</summary>
public enum EstadoPedido
{
    Abierto           = 0,
    Creado            = 1,
    Modificado        = 2,
    Preparandose      = 3,
    ListoParaEntregar = 4,
    Entregado         = 5,
    Cerrado           = 6,
    Cancelado         = 7
}
