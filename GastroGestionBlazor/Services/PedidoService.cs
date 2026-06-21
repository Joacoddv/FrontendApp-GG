using GastroGestionBlazor.Contracts.Common;
using GastroGestionBlazor.Contracts.Mesas;
using GastroGestionBlazor.Contracts.Pedidos;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Drives the waiter (mozo) order flow against the API: create an order, add
/// lines, confirm each line's price snapshot, and generate the kitchen OTs.
/// </summary>
public sealed class PedidoService
{
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public PedidoService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>POST /pedidos — creates an order and returns its id.</summary>
    public async Task<Guid> CrearPedidoAsync(CrearPedidoRequest request, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync("pedidos", request, JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
            await ThrowApiExceptionAsync(response, "No se pudo crear el pedido.", ct);

        return await ReadIdAsync(response, ct);
    }

    /// <summary>POST /pedidos/{id}/lineas — adds a line and returns its id.</summary>
    public async Task<Guid> AgregarLineaAsync(Guid pedidoId, AgregarLineaRequest request, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"pedidos/{pedidoId}/lineas", request, JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
            await ThrowApiExceptionAsync(response, "No se pudo agregar la línea al pedido.", ct);

        return await ReadIdAsync(response, ct);
    }

    /// <summary>
    /// POST /pedidos/{id}/lineas/{lineaId}/confirmar-precio — snapshots the dish's
    /// current price onto the line. Required before generating OTs.
    /// </summary>
    public async Task ConfirmarPrecioLineaAsync(Guid pedidoId, Guid lineaId, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsync(
            $"pedidos/{pedidoId}/lineas/{lineaId}/confirmar-precio", content: null, ct);
        if (!response.IsSuccessStatusCode)
            await ThrowApiExceptionAsync(response, "No se pudo confirmar el precio de la línea.", ct);
    }

    /// <summary>POST /pedidos/{id}/ordenes-trabajo — generates the kitchen work orders.</summary>
    public async Task GenerarOrdenesTrabajoAsync(Guid pedidoId, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsync(
            $"pedidos/{pedidoId}/ordenes-trabajo", content: null, ct);
        if (!response.IsSuccessStatusCode)
            await ThrowApiExceptionAsync(response, "No se pudieron generar las órdenes de trabajo.", ct);
    }

    /// <summary>GET /pedidos — lists orders (newest first), optionally filtered by estado.</summary>
    public async Task<List<PedidoResponse>> GetPedidosAsync(string? estado = null, CancellationToken ct = default)
    {
        var url = string.IsNullOrWhiteSpace(estado)
            ? "pedidos"
            : $"pedidos?estado={Uri.EscapeDataString(estado)}";

        var response = await _httpClient.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
            await ThrowApiExceptionAsync(response, "No se pudo cargar la lista de pedidos.", ct);

        var result = await response.Content.ReadFromJsonAsync<List<PedidoResponse>>(JsonOptions, ct);
        return result ?? new List<PedidoResponse>();
    }

    /// <summary>GET /mesas — active tables for the Salon picker.</summary>
    public async Task<List<MesaResponse>> GetMesasAsync(CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync("mesas", ct);
        if (!response.IsSuccessStatusCode)
            await ThrowApiExceptionAsync(response, "No se pudo cargar la lista de mesas.", ct);

        var result = await response.Content.ReadFromJsonAsync<List<MesaResponse>>(JsonOptions, ct);
        return result ?? new List<MesaResponse>();
    }

    // POST returns the new Guid in the body; fall back to the Location header.
    private static async Task<Guid> ReadIdAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var id = await response.Content.ReadFromJsonAsync<Guid>(JsonOptions, ct);
            if (id != Guid.Empty)
                return id;
        }
        catch { /* body may not be a bare Guid */ }

        if (response.Headers.Location is { } location)
        {
            var segments = location.AbsolutePath.Split('/');
            if (Guid.TryParse(segments[^1], out var locationId))
                return locationId;
        }

        return Guid.Empty;
    }

    private static async Task ThrowApiExceptionAsync(
        HttpResponseMessage response, string fallback, CancellationToken ct)
    {
        ProblemDetailsResponse? problem = null;
        try
        {
            problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>(JsonOptions, ct);
        }
        catch { /* ignore deserialization errors */ }

        throw new ApiException(problem?.Detail ?? problem?.Title ?? fallback);
    }
}
