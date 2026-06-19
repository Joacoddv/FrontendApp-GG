using GastroGestionBlazor.Contracts.Common;
using GastroGestionBlazor.Contracts.OrdenesTrabajo;
using GastroGestionBlazor.Contracts.Usuarios;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class KitchenBoardService
{
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public KitchenBoardService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<OrdenTrabajoBoardItem>> GetBoardAsync(CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync("ordenes-trabajo", ct);
        if (!response.IsSuccessStatusCode)
        {
            await ThrowApiExceptionAsync(response, "No se pudo cargar el tablero de cocina.", ct);
        }

        var result = await response.Content.ReadFromJsonAsync<List<OrdenTrabajoBoardItem>>(JsonOptions, ct);
        return result ?? new List<OrdenTrabajoBoardItem>();
    }

    public async Task MarcarListaAsync(Guid pedidoId, Guid otId, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsync(
            $"pedidos/{pedidoId}/ordenes-trabajo/{otId}/marcar-lista",
            content: null,
            ct);

        if (!response.IsSuccessStatusCode)
        {
            await ThrowApiExceptionAsync(response, "No se pudo marcar la orden como lista.", ct);
        }
    }

    public async Task<List<CocineroResponse>> GetCocinerosAsync(CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync("usuarios/cocineros", ct);
        if (!response.IsSuccessStatusCode)
        {
            await ThrowApiExceptionAsync(response, "No se pudo cargar la lista de cocineros.", ct);
        }

        var result = await response.Content.ReadFromJsonAsync<List<CocineroResponse>>(JsonOptions, ct);
        return result ?? new List<CocineroResponse>();
    }

    public async Task AsignarCocineroAsync(Guid pedidoId, Guid otId, Guid cocineroLegajoId, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"pedidos/{pedidoId}/ordenes-trabajo/{otId}/asignar-cocinero",
            new { cocineroLegajoId },
            JsonOptions,
            ct);

        if (!response.IsSuccessStatusCode)
        {
            await ThrowApiExceptionAsync(response, "No se pudo asignar el cocinero.", ct);
        }
    }

    /// <summary>
    /// Surfaces the server's RFC 7807 ProblemDetails (Spanish Detail/Title) as an ApiException,
    /// falling back to the supplied message when the body cannot be parsed.
    /// </summary>
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
