using GastroGestionBlazor.Contracts.Common;
using GastroGestionBlazor.Contracts.Proveedores;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class ProveedorService
{
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public ProveedorService(HttpClient httpClient) => _httpClient = httpClient;

    public Task<List<ProveedorResponse>> GetAllAsync(CancellationToken ct = default)
        => SearchAsync(null, false, ct);

    public async Task<List<ProveedorResponse>> SearchAsync(
        string? nombre, bool incluirInactivos = false, CancellationToken ct = default)
    {
        var query = $"proveedores?incluirInactivos={incluirInactivos}";
        if (!string.IsNullOrEmpty(nombre))
            query += $"&nombre={Uri.EscapeDataString(nombre)}";

        var response = await _httpClient.GetAsync(query, ct);
        if (!response.IsSuccessStatusCode)
            await ThrowApiExceptionAsync(response, "No se pudo cargar la lista de proveedores.", ct);

        return await response.Content.ReadFromJsonAsync<List<ProveedorResponse>>(JsonOptions, ct)
               ?? new List<ProveedorResponse>();
    }

    public async Task<Guid> CreateAsync(CrearProveedorRequest request, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync("proveedores", request, JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
            await ThrowApiExceptionAsync(response, "No se pudo crear el proveedor.", ct);

        try
        {
            var id = await response.Content.ReadFromJsonAsync<Guid>(JsonOptions, ct);
            if (id != Guid.Empty) return id;
        }
        catch { /* body may not be a bare Guid */ }

        return Guid.Empty;
    }

    public async Task UpdateAsync(Guid id, EditarProveedorRequest request, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"proveedores/{id}", request, JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
            await ThrowApiExceptionAsync(response, "No se pudo actualizar el proveedor.", ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"proveedores/{id}", ct);
        if (!response.IsSuccessStatusCode)
            await ThrowApiExceptionAsync(response, "No se pudo eliminar el proveedor.", ct);
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
