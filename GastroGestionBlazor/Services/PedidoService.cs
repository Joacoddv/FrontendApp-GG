using GastroGestionBlazor.Contracts.Common;
using GastroGestionBlazor.Contracts.Enums;
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

    /// <summary>GET /pedidos/{id} — full order detail, or null if not found.</summary>
    public async Task<PedidoResponse?> GetPedidoByIdAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync($"pedidos/{id}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        if (!response.IsSuccessStatusCode)
            await ThrowApiExceptionAsync(response, "No se pudo cargar el pedido.", ct);

        return await response.Content.ReadFromJsonAsync<PedidoResponse>(JsonOptions, ct);
    }

    /// <summary>
    /// POST /pedidos/{id}/transicion — moves the order to a new state and returns the updated order.
    /// The backend re-validates the transition against the state machine and the caller's role.
    /// </summary>
    public async Task<PedidoResponse?> TransicionarEstadoAsync(
        Guid id, EstadoPedido estadoNuevo, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"pedidos/{id}/transicion", new TransicionarEstadoRequest(estadoNuevo), JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
            await ThrowApiExceptionAsync(response, "No se pudo cambiar el estado del pedido.", ct);

        return await response.Content.ReadFromJsonAsync<PedidoResponse>(JsonOptions, ct);
    }

    /// <summary>
    /// PUT /pedidos/{id}/lineas/{lineaId} — edits an existing line's quantity/notes and returns
    /// the updated order. The backend enforces the edit-lock rules and recomputes line totals.
    /// </summary>
    public async Task<PedidoResponse?> ActualizarLineaAsync(
        Guid pedidoId, Guid lineaId, int cantidad, string? observaciones, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync(
            $"pedidos/{pedidoId}/lineas/{lineaId}",
            new ActualizarLineaRequest(cantidad, observaciones), JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
            await ThrowApiExceptionAsync(response, "No se pudo actualizar la línea del pedido.", ct);

        return await response.Content.ReadFromJsonAsync<PedidoResponse>(JsonOptions, ct);
    }

    /// <summary>DELETE /pedidos/{id}/lineas/{lineaId} — removes a line and returns the updated order.</summary>
    public async Task<PedidoResponse?> QuitarLineaAsync(Guid pedidoId, Guid lineaId, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"pedidos/{pedidoId}/lineas/{lineaId}", ct);
        if (!response.IsSuccessStatusCode)
            await ThrowApiExceptionAsync(response, "No se pudo quitar la línea del pedido.", ct);

        return await response.Content.ReadFromJsonAsync<PedidoResponse>(JsonOptions, ct);
    }

    /// <summary>
    /// POST /pedidos/{id}/lineas/{lineaId}/orden-trabajo — generates the kitchen work order for a
    /// single (newly added, already priced) line and returns the updated order.
    /// </summary>
    public async Task<PedidoResponse?> GenerarOrdenTrabajoLineaAsync(
        Guid pedidoId, Guid lineaId, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsync(
            $"pedidos/{pedidoId}/lineas/{lineaId}/orden-trabajo", content: null, ct);
        if (!response.IsSuccessStatusCode)
            await ThrowApiExceptionAsync(response, "No se pudo generar la orden de trabajo de la línea.", ct);

        return await response.Content.ReadFromJsonAsync<PedidoResponse>(JsonOptions, ct);
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
