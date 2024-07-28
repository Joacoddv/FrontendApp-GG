using DTO.Ingredientes;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;
using System;

public class IngredienteService
{
    private readonly HttpClient _httpClient;

    public IngredienteService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<IngredienteToListDTO>> GetAllIngredientesAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("https://localhost:5001/api/Ingrediente");
            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<IngredienteToListDTO>>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (HttpRequestException httpEx)
        {
            var responseContent = httpEx.Data.Contains("ResponseContent") ? httpEx.Data["ResponseContent"].ToString() : "Sin contenido";
            throw new Exception($"Error al obtener los ingrediente: {responseContent}");
        }
        catch (Exception ex)
        {
            throw new Exception($"Error al procesar la solicitud: {ex.Message}");
        }
    }

    public async Task<List<IngredienteToListDTO>> BuscarIngredienteAsync(string campoBusqueda, string valorBusqueda)
    {
        try
        {
            var response = await _httpClient.GetAsync($"https://localhost:5001/api/ingrediente/Buscar?campoBusqueda={campoBusqueda}&valorBusqueda={valorBusqueda}");
            if (!response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Error al buscar los ingrediente: {responseContent}");
            }

            var responseContentSuccess = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<IngredienteToListDTO>>(responseContentSuccess, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (HttpRequestException httpEx)
        {
            var responseContent = httpEx.Data.Contains("ResponseContent") ? httpEx.Data["ResponseContent"].ToString() : "Sin contenido";
            throw new Exception($"Error al buscar los ingredientes: {responseContent}");
        }
        catch (Exception ex)
        {
            throw new Exception($"Error al procesar la solicitud: {ex.Message}");
        }
    }

    public async Task AgregarIngredienteAsync(IngredienteCreacionDTO nuevoIngrediente)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("https://localhost:5001/api/Ingrediente/Alta", nuevoIngrediente);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException httpEx)
        {
            var responseContent = httpEx.Data.Contains("ResponseContent") ? httpEx.Data["ResponseContent"].ToString() : "Sin contenido";
            throw new Exception($"Error al agregar el ingrediente: {responseContent}");
        }
        catch (Exception ex)
        {
            throw new Exception($"Error al procesar la solicitud: {ex.Message}");
        }
    }

    public async Task EditarIngredienteAsync(IngredienteEdicionDTO ingredienteEditado)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync("https://localhost:5001/api/Ingrediente/Editar", ingredienteEditado);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException httpEx)
        {
            var responseContent = httpEx.Data.Contains("ResponseContent") ? httpEx.Data["ResponseContent"].ToString() : "Sin contenido";
            throw new Exception($"Error al editar el ingrediente: {responseContent}");
        }
        catch (Exception ex)
        {
            throw new Exception($"Error al procesar la solicitud: {ex.Message}");
        }
    }

    public async Task EliminarIngredienteAsync(IngredienteEdicionDTO ingredienteEditado)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Delete, "https://localhost:5001/api/Ingrediente/Baja")
            {
                Content = JsonContent.Create(ingredienteEditado)
            };
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException httpEx)
        {
            var responseContent = httpEx.Data.Contains("ResponseContent") ? httpEx.Data["ResponseContent"].ToString() : "Sin contenido";
            throw new Exception($"Error al eliminar el ingrediente: {responseContent}");
        }
        catch (Exception ex)
        {
            throw new Exception($"Error al procesar la solicitud: {ex.Message}");
        }
    }
}
