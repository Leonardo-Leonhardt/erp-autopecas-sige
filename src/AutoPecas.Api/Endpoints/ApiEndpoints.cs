using AutoPecas.Api.Data;
using AutoPecas.Api.Domain;
using AutoPecas.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace AutoPecas.Api.Endpoints;

public static class ApiEndpoints
{
    public static void MapApi(this WebApplication app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/dashboard", (DashboardService s) => s.ObterAsync());

        // ---------- Catálogo e estoque ----------
        var pecas = api.MapGroup("/pecas");
        pecas.MapGet("/", (EstoqueService s, string? busca, StatusEstoque? status, string? categoria) =>
            s.ListarAsync(busca, status, categoria));
        pecas.MapGet("/categorias", (EstoqueService s) => s.CategoriasAsync());
        pecas.MapGet("/{id:int}", (EstoqueService s, int id) => s.DetalharAsync(id));
        pecas.MapPost("/", async (EstoqueService s, PecaRequest req) =>
        {
            var peca = await s.CriarAsync(req);
            return Results.Created($"/api/pecas/{peca.Id}", peca);
        });
        pecas.MapPut("/{id:int}", (EstoqueService s, int id, PecaRequest req) => s.AtualizarAsync(id, req));
        pecas.MapPost("/{id:int}/ajustes", (EstoqueService s, int id, AjusteRequest req) => s.AjustarAsync(id, req));

        api.MapGet("/movimentacoes", (EstoqueService s, int? pecaId, TipoMovimentacao? tipo, int? limite) =>
            s.MovimentacoesAsync(pecaId, tipo, Math.Clamp(limite ?? 100, 1, 500)));

        // ---------- Vendas ----------
        var vendas = api.MapGroup("/vendas");
        vendas.MapGet("/", (VendaService s, StatusVenda? status, int? limite) =>
            s.ListarAsync(status, Math.Clamp(limite ?? 50, 1, 200)));
        vendas.MapGet("/{id:int}", (VendaService s, int id) => s.ObterAsync(id));
        vendas.MapPost("/", async (VendaService s, NovaVendaRequest req) =>
        {
            var venda = await s.CriarAsync(req);
            return Results.Created($"/api/vendas/{venda.Id}", venda);
        });
        vendas.MapPost("/{id:int}/finalizar", (VendaService s, int id, AcaoUsuarioRequest req) => s.FinalizarAsync(id, req.Usuario));
        vendas.MapPost("/{id:int}/cancelar", (VendaService s, int id, AcaoUsuarioRequest req) => s.CancelarAsync(id, req.Usuario));

        // ---------- Reposição e compras ----------
        api.MapGet("/reposicao", (ReposicaoService s) => s.CalcularAsync());

        var compras = api.MapGroup("/compras");
        compras.MapGet("/", (ReposicaoService s, StatusCompra? status) => s.ListarPedidosAsync(status));
        compras.MapPost("/", (ReposicaoService s, NovoPedidoCompraRequest req) => s.GerarPedidosAsync(req));
        compras.MapPost("/{id:int}/receber", (ReposicaoService s, int id, ReceberCompraRequest req) => s.ReceberAsync(id, req));
        compras.MapPost("/{id:int}/cancelar", (ReposicaoService s, int id) => s.CancelarPedidoAsync(id));

        // ---------- Cadastros de apoio ----------
        api.MapGet("/clientes", (AppDbContext db) => db.Clientes.AsNoTracking().OrderBy(c => c.Id).ToListAsync());
        api.MapGet("/fornecedores", (AppDbContext db) => db.Fornecedores.AsNoTracking().OrderBy(f => f.Nome).ToListAsync());
    }

    /// <summary>Converte erros de regra de negócio e de concorrência em respostas JSON legíveis pelo frontend.</summary>
    public static void UseTratamentoDeErros(this WebApplication app)
    {
        app.Use(async (ctx, next) =>
        {
            try
            {
                await next(ctx);
            }
            catch (RegraNegocioException ex)
            {
                ctx.Response.StatusCode = ex.StatusCode;
                await ctx.Response.WriteAsJsonAsync(new { erro = ex.Message });
            }
            catch (DbUpdateConcurrencyException)
            {
                ctx.Response.StatusCode = StatusCodes.Status409Conflict;
                await ctx.Response.WriteAsJsonAsync(new
                {
                    erro = "O estoque foi alterado por outra operação ao mesmo tempo. Atualize a tela e tente novamente.",
                });
            }
        });
    }
}
