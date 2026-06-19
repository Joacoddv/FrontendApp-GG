using GastroGestionBlazor.Contracts.Menus;
using GastroGestionBlazor.Contracts.Common;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

public class MenuService
{
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public MenuService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<MenuResponse>> GetAllAsync(CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync("menus", ct);
        if (!response.IsSuccessStatusCode)
            await ThrowApiExceptionAsync(response, "No se pudo cargar la lista de menús.", ct);

        var result = await response.Content.ReadFromJsonAsync<List<MenuResponse>>(JsonOptions, ct);
        return result ?? new List<MenuResponse>();
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
