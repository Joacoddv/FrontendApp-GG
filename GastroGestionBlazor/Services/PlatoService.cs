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

    public async Task<Guid> CreateAsync(CrearPlatoRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("platos", request, JsonOptions);

        if (!response.IsSuccessStatusCode)
        {
            ProblemDetailsResponse? problem = null;
            try
            {
                problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>(JsonOptions);
            }
            catch { /* ignore deserialization errors */ }

            throw new ApiException(problem?.Detail ?? problem?.Title ?? "Error al crear el plato.");
        }

        // POST returns the new Guid in the body; fall back to the Location header.
        try
        {
            var id = await response.Content.ReadFromJsonAsync<Guid>(JsonOptions);
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
