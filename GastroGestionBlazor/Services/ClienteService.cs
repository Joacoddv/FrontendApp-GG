using GastroGestionBlazor.Contracts.Clientes;
using GastroGestionBlazor.Contracts.Common;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

public class ClienteService
{
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public ClienteService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<ClienteResponse>> GetAllAsync(CancellationToken ct = default)
    {
        return await SearchAsync(null, false, ct);
    }

    public async Task<ClienteResponse?> GetByIdAsync(Guid id)
    {
        var response = await _httpClient.GetAsync($"clientes/{id}");
        if (!response.IsSuccessStatusCode)
            return null;
        return await response.Content.ReadFromJsonAsync<ClienteResponse>(JsonOptions);
    }

    public async Task<List<ClienteResponse>> SearchAsync(
        string? nombre,
        bool incluirInactivos = false,
        CancellationToken ct = default)
    {
        var query = $"clientes?incluirInactivos={incluirInactivos}";
        if (!string.IsNullOrEmpty(nombre))
            query += $"&nombre={Uri.EscapeDataString(nombre)}";

        var response = await _httpClient.GetAsync(query, ct);
        if (!response.IsSuccessStatusCode)
            await ThrowApiExceptionAsync(response, "No se pudo cargar la lista de clientes.", ct);

        var result = await response.Content.ReadFromJsonAsync<List<ClienteResponse>>(JsonOptions, ct);
        return result ?? new List<ClienteResponse>();
    }

    public async Task<ClienteResponse?> UpdateAsync(
        Guid id,
        EditarClienteRequest req,
        CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"clientes/{id}", req, JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
            await ThrowApiExceptionAsync(response, "No se pudo actualizar el cliente.", ct);

        return await response.Content.ReadFromJsonAsync<ClienteResponse>(JsonOptions, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"clientes/{id}", ct);
        if (!response.IsSuccessStatusCode)
            await ThrowApiExceptionAsync(response, "No se pudo eliminar el cliente.", ct);
    }

    public async Task<Guid> CreateAsync(CrearClienteRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("clientes", request, JsonOptions);

        if (!response.IsSuccessStatusCode)
        {
            ProblemDetailsResponse? problem = null;
            try
            {
                problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>(JsonOptions);
            }
            catch { /* ignore deserialization errors */ }

            var message = problem?.Detail ?? problem?.Title ?? "Error al crear el cliente.";
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
