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

    public async Task<List<ClienteResponse>> GetAllAsync()
    {
        var response = await _httpClient.GetAsync("clientes");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<List<ClienteResponse>>(JsonOptions);
        return result ?? new List<ClienteResponse>();
    }

    public async Task<ClienteResponse?> GetByIdAsync(Guid id)
    {
        var response = await _httpClient.GetAsync($"clientes/{id}");
        if (!response.IsSuccessStatusCode)
            return null;
        return await response.Content.ReadFromJsonAsync<ClienteResponse>(JsonOptions);
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
}
