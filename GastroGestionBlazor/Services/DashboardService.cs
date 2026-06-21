using GastroGestionBlazor.Contracts.Common;
using GastroGestionBlazor.Contracts.Dashboard;
using System.Net.Http.Json;
using System.Text.Json;

public sealed class DashboardService
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public DashboardService(HttpClient httpClient) => _httpClient = httpClient;

    /// <summary>GET /dashboard — operational metrics.</summary>
    public async Task<DashboardResponse?> GetAsync(CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync("dashboard", ct);
        if (!response.IsSuccessStatusCode)
        {
            ProblemDetailsResponse? problem = null;
            try { problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>(JsonOptions, ct); }
            catch { /* ignore */ }
            throw new ApiException(problem?.Detail ?? problem?.Title ?? "No se pudo cargar el dashboard.");
        }

        return await response.Content.ReadFromJsonAsync<DashboardResponse>(JsonOptions, ct);
    }
}
