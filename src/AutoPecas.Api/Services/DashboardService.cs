using AutoPecas.Api.Data;
using AutoPecas.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace AutoPecas.Api.Services;

/// <summary>Visão unificada de vendas e estoque para o gestor.</summary>
public class DashboardService(AppDbContext db)
{
    public async Task<DashboardDto> ObterAsync()
    {
        var agora = DateTime.Now;
        var hoje = agora.Date;
        var inicioSerie = hoje.AddDays(-6);

        var vendasPeriodo = await db.Vendas.AsNoTracking()
            .Include(v => v.Itens)
            .Where(v => v.Status == StatusVenda.Finalizada && v.FinalizadaEm >= hoje.AddDays(-7))
            .ToListAsync();

        var serie = Enumerable.Range(0, 7)
            .Select(i => inicioSerie.AddDays(i))
            .Select(dia =>
            {
                var doDia = vendasPeriodo.Where(v => v.FinalizadaEm!.Value.Date == dia).ToList();
                return new SerieDiaDto(dia, doDia.Sum(v => v.Total), doDia.Sum(v => v.Itens.Sum(i => i.Quantidade)));
            })
            .ToList();

        var deHoje = vendasPeriodo.Where(v => v.FinalizadaEm!.Value.Date == hoje).ToList();
        var deOntem = vendasPeriodo.Where(v => v.FinalizadaEm!.Value.Date == hoje.AddDays(-1)).ToList();

        var pecas = await db.Pecas.AsNoTracking().ToListAsync();
        var status = pecas.ToDictionary(p => p.Id, p => p.StatusEm(agora));
        var semGiro = pecas.Where(p => status[p.Id] == StatusEstoque.SemGiro).ToList();

        var alertas = pecas
            .Where(p => status[p.Id] != StatusEstoque.Normal)
            .OrderBy(p => status[p.Id] switch
            {
                StatusEstoque.Ruptura => 0,
                StatusEstoque.AbaixoMinimo => 1,
                _ => 2,
            })
            .ThenBy(p => p.Disponivel - p.EstoqueMinimo)
            .Take(8)
            .Select(p => new AlertaDto(p.Id, p.Codigo, p.Descricao, status[p.Id], DetalheAlerta(p, status[p.Id], agora)))
            .ToList();

        var ultimas = await db.Vendas.AsNoTracking()
            .Include(v => v.Cliente).Include(v => v.Itens).ThenInclude(i => i.Peca)
            .Where(v => v.Status != StatusVenda.Cancelada)
            .OrderByDescending(v => v.Id)
            .Take(6)
            .ToListAsync();

        return new DashboardDto(
            deHoje.Sum(v => v.Total), deHoje.Count, deOntem.Sum(v => v.Total),
            pecas.Sum(p => p.Saldo * p.CustoMedio), pecas.Count,
            status.Values.Count(s => s == StatusEstoque.Ruptura),
            status.Values.Count(s => s == StatusEstoque.AbaixoMinimo),
            semGiro.Count, semGiro.Sum(p => p.Saldo * p.CustoMedio),
            await db.Vendas.CountAsync(v => v.Status == StatusVenda.Orcamento),
            serie, ultimas.Select(VendaService.ParaDto).ToList(), alertas);
    }

    private static string DetalheAlerta(Peca p, StatusEstoque status, DateTime agora) => status switch
    {
        StatusEstoque.SemGiro =>
            $"Sem venda há {(int)(agora - (p.UltimaVenda ?? p.CadastradaEm)).TotalDays} dias · {p.Saldo} un. paradas",
        _ => $"Disponível {Math.Max(0, p.Disponivel)} · mínimo {p.EstoqueMinimo}"
             + (p.Reservado > 0 ? $" · {p.Reservado} reservada(s)" : ""),
    };
}
