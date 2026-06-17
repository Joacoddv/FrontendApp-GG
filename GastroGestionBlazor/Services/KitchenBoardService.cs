using GastroGestionBlazor.Contracts.Common;
using GastroGestionBlazor.Contracts.OrdenesTrabajo;
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
