using System.Text.Json.Serialization;
using AutoPecas.Api.Data;
using AutoPecas.Api.Endpoints;
using AutoPecas.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Sem string de conexão, a aplicação sobe com banco em memória e dados de demonstração.
var conexaoMySql = builder.Configuration.GetConnectionString("MySql");
builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (string.IsNullOrWhiteSpace(conexaoMySql))
        options.UseInMemoryDatabase("autopecas");
    else
        options.UseMySql(conexaoMySql, new MySqlServerVersion(new Version(8, 0, 36)));
});

builder.Services.AddScoped<VendaService>();
builder.Services.AddScoped<EstoqueService>();
builder.Services.AddScoped<ReposicaoService>();
builder.Services.AddScoped<DashboardService>();

builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    DadosIniciais.Popular(db);
}

app.UseTratamentoDeErros();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapApi();

app.Run();
