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
        response.EnsureSuccessStatusCode();
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
            ProblemDetailsResponse? problem = null;
            try
            {
                problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>(JsonOptions, ct);
            }
            catch { /* ignore deserialization errors */ }

            var message = problem?.Detail ?? problem?.Title ?? "No se pudo marcar la orden como lista.";
            throw new ApiException(message);
        }
    }
}
