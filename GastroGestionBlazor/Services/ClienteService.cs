using DTO.Cliente;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;
using System;

public class ClienteService
{
    private readonly HttpClient _httpClient;

    public ClienteService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<ClienteToListDTO>> GetAllClientesAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("https://localhost:5001/api/Cliente");
            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<ClienteToListDTO>>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (HttpRequestException httpEx)
        {
            var responseContent = httpEx.Data.Contains("ResponseContent") ? httpEx.Data["ResponseContent"].ToString() : "Sin contenido";
            throw new Exception($"Error al obtener los clientes: {responseContent}");
        }
        catch (Exception ex)
        {
            throw new Exception($"Error al procesar la solicitud: {ex.Message}");
        }
    }

    public async Task<List<ClienteToListDTO>> BuscarClientesAsync(string campoBusqueda, string valorBusqueda)
    {
        try
        {
            var response = await _httpClient.GetAsync($"https://localhost:5001/api/cliente/Buscar?campoBusqueda={campoBusqueda}&valorBusqueda={valorBusqueda}");
            if (!response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Error al buscar los clientes: {responseContent}");
            }

            var responseContentSuccess = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<ClienteToListDTO>>(responseContentSuccess, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (HttpRequestException httpEx)
        {
            var responseContent = httpEx.Data.Contains("ResponseContent") ? httpEx.Data["ResponseContent"].ToString() : "Sin contenido";
            throw new Exception($"Error al buscar los clientes: {responseContent}");
        }
        catch (Exception ex)
        {
            throw new Exception($"Error al procesar la solicitud: {ex.Message}");
        }
    }

    public async Task AgregarClienteAsync(ClienteCreacionDTO nuevoCliente)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("https://localhost:5001/api/Cliente/Alta", nuevoCliente);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException httpEx)
        {
            var responseContent = httpEx.Data.Contains("ResponseContent") ? httpEx.Data["ResponseContent"].ToString() : "Sin contenido";
            throw new Exception($"Error al agregar el cliente: {responseContent}");
        }
        catch (Exception ex)
        {
            throw new Exception($"Error al procesar la solicitud: {ex.Message}");
        }
    }

    public async Task EditarClienteAsync(ClienteEdicionDTO clienteEditado)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync("https://localhost:5001/api/Cliente/Editar", clienteEditado);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException httpEx)
        {
            var responseContent = httpEx.Data.Contains("ResponseContent") ? httpEx.Data["ResponseContent"].ToString() : "Sin contenido";
            throw new Exception($"Error al editar el cliente: {responseContent}");
        }
        catch (Exception ex)
        {
            throw new Exception($"Error al procesar la solicitud: {ex.Message}");
        }
    }

    public async Task EliminarClienteAsync(Guid idCliente)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"https://localhost:5001/api/Cliente/{idCliente}");
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException httpEx)
        {
            var responseContent = httpEx.Data.Contains("ResponseContent") ? httpEx.Data["ResponseContent"].ToString() : "Sin contenido";
            throw new Exception($"Error al eliminar el cliente: {responseContent}");
        }
        catch (Exception ex)
        {
            throw new Exception($"Error al procesar la solicitud: {ex.Message}");
        }
    }
}
