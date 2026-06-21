using GastroGestionBlazor.Contracts.Enums;

namespace GastroGestionBlazor.Contracts.Pedidos;

/// <summary>
/// Frontend mirror of the backend Domain.Pedidos.PedidoTransicionRegistry, used to offer only the
/// state transitions that are valid for an order's type/state AND allowed for the current user's
/// role. The backend remains the source of truth and re-validates every transition; this mirror
/// just keeps the UI from showing buttons that would 403. KEEP IN SYNC with the registry.
/// </summary>
public static class PedidoTransiciones
{
    private static readonly string[] CajeroAdmin = { "Cajero", "Administrador" };
    private static readonly string[] MozoAdmin   = { "Mozo", "Administrador" };
    private static readonly string[] Cocinero    = { "Cocinero" };

    public sealed record Opcion(EstadoPedido Destino, string[] Roles);

    private static readonly Dictionary<(TipoPedido, EstadoPedido), Opcion[]> Map = new()
    {
        // ── Salón ──
        [(TipoPedido.Salon, EstadoPedido.Abierto)] = new[]
        {
            new Opcion(EstadoPedido.Cerrado,   MozoAdmin),
            new Opcion(EstadoPedido.Cancelado, MozoAdmin),
        },

        // ── Mostrador (TakeAway) ──
        [(TipoPedido.TakeAway, EstadoPedido.Creado)] = new[]
        {
            new Opcion(EstadoPedido.Modificado,   CajeroAdmin),
            new Opcion(EstadoPedido.Preparandose, CajeroAdmin),
            new Opcion(EstadoPedido.Cancelado,    CajeroAdmin),
        },
        [(TipoPedido.TakeAway, EstadoPedido.Modificado)] = new[]
        {
            new Opcion(EstadoPedido.Preparandose, CajeroAdmin),
            new Opcion(EstadoPedido.Cancelado,    CajeroAdmin),
        },
        [(TipoPedido.TakeAway, EstadoPedido.Preparandose)] = new[]
        {
            new Opcion(EstadoPedido.ListoParaEntregar, Cocinero),
            new Opcion(EstadoPedido.Cancelado,         CajeroAdmin),
        },
        [(TipoPedido.TakeAway, EstadoPedido.ListoParaEntregar)] = new[]
        {
            new Opcion(EstadoPedido.Entregado, CajeroAdmin),
        },

        // ── Delivery (same logical flow as counter) ──
        [(TipoPedido.Delivery, EstadoPedido.Creado)] = new[]
        {
            new Opcion(EstadoPedido.Modificado,   CajeroAdmin),
            new Opcion(EstadoPedido.Preparandose, CajeroAdmin),
            new Opcion(EstadoPedido.Cancelado,    CajeroAdmin),
        },
        [(TipoPedido.Delivery, EstadoPedido.Modificado)] = new[]
        {
            new Opcion(EstadoPedido.Preparandose, CajeroAdmin),
            new Opcion(EstadoPedido.Cancelado,    CajeroAdmin),
        },
        [(TipoPedido.Delivery, EstadoPedido.Preparandose)] = new[]
        {
            new Opcion(EstadoPedido.ListoParaEntregar, Cocinero),
            new Opcion(EstadoPedido.Cancelado,         CajeroAdmin),
        },
        [(TipoPedido.Delivery, EstadoPedido.ListoParaEntregar)] = new[]
        {
            new Opcion(EstadoPedido.Entregado, CajeroAdmin),
        },
    };

    /// <summary>Target states reachable from (tipo, desde) that the given role may trigger.</summary>
    public static IReadOnlyList<EstadoPedido> Permitidas(TipoPedido tipo, EstadoPedido desde, string? rol)
        => Map.TryGetValue((tipo, desde), out var opciones)
            ? opciones.Where(o => rol is not null && o.Roles.Contains(rol)).Select(o => o.Destino).ToList()
            : Array.Empty<EstadoPedido>();
}
