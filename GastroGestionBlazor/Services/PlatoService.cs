using GastroGestionBlazor.Contracts.Platos;
using GastroGestionBlazor.Contracts.Common;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

public class PlatoService
{
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public PlatoService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<PlatoResponse>> GetAllAsync(CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync("platos", ct);
        if (!response.IsSuccessStatusCode)
            await ThrowApiExceptionAsync(response, "No se pudo cargar la lista de platos.", ct);

        var result = await response.Content.ReadFromJsonAsync<List<PlatoResponse>>(JsonOptions, ct);
        return result ?? new List<PlatoResponse>();
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
