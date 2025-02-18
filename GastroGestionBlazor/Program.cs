using GastroGestionBlazor;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using AutoMapper;
using DTO.Mappers;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<ClienteService>();
builder.Services.AddScoped<IngredienteService>();
builder.Services.AddAutoMapper(typeof(AutoMapperProfiles));

await builder.Build().RunAsync();
