using GastroGestionBlazor.Contracts.Common;
using GastroGestionBlazor.Contracts.Mesas;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Reads tables and manages their lifecycle (create / edit / deactivate) plus floor positioning
/// against the API. Mirrors the StockService / ProveedorService HttpClient + ApiException pattern.
/// </summary>
public sealed class MesaService
{
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public MesaService(HttpClient httpClient) => _httpClient = httpClient;

    /// <summary>GET /mesas — every table with its floor position (PosicionX/Y may be null).</summary>
    public async Task<List<MesaResponse>> GetAllAsync(CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync("mesas", ct);
        if (!response.IsSuccessStatusCode)
            await ThrowApiExceptionAsync(response, "No se pudo cargar la lista de mesas.", ct);

        return await response.Content.ReadFromJsonAsync<List<MesaResponse>>(JsonOptions, ct)
               ?? new List<MesaResponse>();
    }

    /// <summary>POST /mesas — create a new table.</summary>
    public async Task CrearAsync(CrearMesaRequest request, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync("mesas", request, JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
            await ThrowApiExceptionAsync(response, "No se pudo crear la mesa.", ct);
    }

    /// <summary>PUT /mesas/{id} — edit a table's number and capacity.</summary>
    public async Task EditarAsync(Guid id, EditarMesaRequest request, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"mesas/{id}", request, JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
            await ThrowApiExceptionAsync(response, "No se pudo actualizar la mesa.", ct);
    }

    /// <summary>DELETE /mesas/{id} — deactivate a table. Returns 422 if it has an active pedido.</summary>
    public async Task DesactivarAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"mesas/{id}", ct);
        if (!response.IsSuccessStatusCode)
            await ThrowApiExceptionAsync(response, "No se pudo desactivar la mesa.", ct);
    }

    /// <summary>PUT /mesas/{id}/posicion — persist a table's floor position (Admin-only).</summary>
    public async Task UbicarAsync(Guid id, UbicarMesaRequest request, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"mesas/{id}/posicion", request, JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
            await ThrowApiExceptionAsync(response, "No se pudo guardar la posición de la mesa.", ct);
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
