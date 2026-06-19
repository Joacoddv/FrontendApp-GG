using GastroGestionBlazor.Contracts.Ingredientes;
using GastroGestionBlazor.Contracts.Common;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

public class IngredienteService
{
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public IngredienteService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<IngredienteResponse>> GetAllAsync(CancellationToken ct = default)
    {
        return await SearchAsync(null, false, ct);
    }

    public async Task<IngredienteResponse?> GetByIdAsync(Guid id)
    {
        var response = await _httpClient.GetAsync($"ingredientes/{id}");
        if (!response.IsSuccessStatusCode)
            return null;
        return await response.Content.ReadFromJsonAsync<IngredienteResponse>(JsonOptions);
    }

    public async Task<List<IngredienteResponse>> SearchAsync(
        string? nombre,
        bool incluirInactivos = false,
        CancellationToken ct = default)
    {
        var query = $"ingredientes?incluirInactivos={incluirInactivos}";
        if (!string.IsNullOrEmpty(nombre))
            query += $"&nombre={Uri.EscapeDataString(nombre)}";

        var response = await _httpClient.GetAsync(query, ct);
        if (!response.IsSuccessStatusCode)
            await ThrowApiExceptionAsync(response, "No se pudo cargar la lista de ingredientes.", ct);

        var result = await response.Content.ReadFromJsonAsync<List<IngredienteResponse>>(JsonOptions, ct);
        return result ?? new List<IngredienteResponse>();
    }

    public async Task<IngredienteResponse?> UpdateAsync(
        Guid id,
        EditarIngredienteRequest req,
        CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"ingredientes/{id}", req, JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
            await ThrowApiExceptionAsync(response, "No se pudo actualizar el ingrediente.", ct);

        return await response.Content.ReadFromJsonAsync<IngredienteResponse>(JsonOptions, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"ingredientes/{id}", ct);
        if (!response.IsSuccessStatusCode)
            await ThrowApiExceptionAsync(response, "No se pudo eliminar el ingrediente.", ct);
    }

    public async Task<Guid> CreateAsync(CrearIngredienteRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("ingredientes", request, JsonOptions);

        if (!response.IsSuccessStatusCode)
        {
            ProblemDetailsResponse? problem = null;
            try
            {
                problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>(JsonOptions);
            }
            catch { /* ignore deserialization errors */ }

            var message = problem?.Detail ?? problem?.Title ?? "Error al crear el ingrediente.";
            throw new ApiException(message);
        }

        // Try to read the Guid from the response body first
        try
        {
            var id = await response.Content.ReadFromJsonAsync<Guid>(JsonOptions);
            if (id != Guid.Empty)
                return id;
        }
        catch { /* body may not be a bare Guid */ }

        // Fall back to Location header
        if (response.Headers.Location is { } location)
        {
            var segments = location.AbsolutePath.Split('/');
            if (Guid.TryParse(segments[^1], out var locationId))
                return locationId;
        }

        // Fall back to empty Guid (caller will reload the list)
        return Guid.Empty;
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
