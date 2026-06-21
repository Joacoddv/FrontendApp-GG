namespace GastroGestionBlazor.Contracts.Dashboard;

public sealed record EstadoCountResponse(string Estado, int Cantidad);
public sealed record PlatoRankingResponse(string Plato, int Cantidad);
public sealed record AlertaStockResponse(string Ingrediente, decimal Balance, decimal StockMinimo);

public sealed record DashboardResponse(
    int TotalPedidos,
    decimal MontoTotalPedidos,
    IReadOnlyList<EstadoCountResponse> PedidosPorEstado,
    IReadOnlyList<PlatoRankingResponse> TopPlatos,
    IReadOnlyList<AlertaStockResponse> AlertasStock);
