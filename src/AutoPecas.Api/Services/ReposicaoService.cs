using AutoPecas.Api.Data;
using AutoPecas.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace AutoPecas.Api.Services;

/// <summary>
/// Aquisição e reposição: sugere compras para evitar ruptura, aponta capital parado
/// e dá entrada no estoque quando o pedido de compra é recebido.
/// </summary>
public class ReposicaoService(AppDbContext db)
{
    /// <summary>Janela de vendas usada para calcular giro e curva ABC.</summary>
    public const int JanelaDias = 90;

    public async Task<ReposicaoDto> CalcularAsync()
    {
        var agora = DateTime.Now;
        var pecas = await db.Pecas.AsNoTracking().Include(p => p.Fornecedor).ToListAsync();
        var vendidas = await VendidasNaJanelaAsync(agora);
        var curvas = ClassificarAbc(vendidas.ToDictionary(v => v.Key, v => v.Value.Receita));

        var emPedido = await db.ItensCompra.AsNoTracking()
            .Where(i => i.PedidoCompra!.Status == StatusCompra.Aberto)
            .GroupBy(i => i.PecaId)
            .Select(g => new { PecaId = g.Key, Quantidade = g.Sum(i => i.Quantidade) })
            .ToDictionaryAsync(x => x.PecaId, x => x.Quantidade);

        var sugestoes = new List<SugestaoCompraDto>();
        foreach (var p in pecas)
        {
            var status = p.StatusEm(agora);
            if (status == StatusEstoque.SemGiro) continue;

            var vendidasJanela = vendidas.TryGetValue(p.Id, out var v) ? v.Quantidade : 0;
            var mediaDiaria = vendidasJanela / (decimal)JanelaDias;
            var prazo = p.Fornecedor?.PrazoEntregaDias ?? 7;

            // Ponto de pedido = estoque mínimo + o que deve vender enquanto o fornecedor entrega.
            var pontoPedido = p.EstoqueMinimo + (int)Math.Ceiling(mediaDiaria * prazo);
            var jaPedido = emPedido.GetValueOrDefault(p.Id);
            var posicao = Math.Max(0, p.Disponivel) + jaPedido;
            if (posicao > pontoPedido) continue;

            var sugerido = Math.Max(p.EstoqueMaximo, pontoPedido) - posicao;
            if (sugerido <= 0) continue;

            sugestoes.Add(new SugestaoCompraDto(
                p.Id, p.Codigo, p.Descricao, curvas.GetValueOrDefault(p.Id, "C"), p.Disponivel,
                p.EstoqueMinimo, p.EstoqueMaximo, Math.Round(mediaDiaria * 30, 1), pontoPedido, jaPedido, sugerido,
                p.FornecedorId, p.Fornecedor?.Nome, p.CustoMedio, sugerido * p.CustoMedio, status));
        }

        var excessos = pecas
            .Where(p => p.StatusEm(agora) == StatusEstoque.SemGiro)
            .Select(p => new ExcessoDto(
                p.Id, p.Codigo, p.Descricao, p.Saldo,
                (int)(agora - (p.UltimaVenda ?? p.CadastradaEm)).TotalDays, p.Saldo * p.CustoMedio))
            .OrderByDescending(e => e.ValorParado)
            .ToList();

        return new ReposicaoDto(
            sugestoes.OrderBy(s => s.Status == StatusEstoque.Ruptura ? 0 : 1).ThenBy(s => s.Curva).ThenBy(s => s.Codigo).ToList(),
            excessos, JanelaDias);
    }

    /// <summary>Curva ABC por faturamento: A = 80% da receita, B = próximos 15%, C = restante.</summary>
    public async Task<Dictionary<int, string>> CurvaAbcAsync()
    {
        var vendidas = await VendidasNaJanelaAsync(DateTime.Now);
        return ClassificarAbc(vendidas.ToDictionary(v => v.Key, v => v.Value.Receita));
    }

    public async Task<List<PedidoCompraDto>> ListarPedidosAsync(StatusCompra? status)
    {
        var query = ConsultaPedidos();
        if (status is not null) query = query.Where(p => p.Status == status);
        var pedidos = await query.OrderByDescending(p => p.Id).Take(50).ToListAsync();
        return pedidos.Select(ParaDto).ToList();
    }

    /// <summary>Gera um pedido de compra por fornecedor a partir dos itens selecionados.</summary>
    public async Task<List<PedidoCompraDto>> GerarPedidosAsync(NovoPedidoCompraRequest req)
    {
        var itens = (req.Itens ?? []).Where(i => i.Quantidade > 0).ToList();
        if (itens.Count == 0) throw new RegraNegocioException("Selecione ao menos um item para comprar.");

        var ids = itens.Select(i => i.PecaId).Distinct().ToList();
        var pecas = await db.Pecas.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        var semFornecedor = pecas.Values.Where(p => p.FornecedorId is null).Select(p => p.Codigo).ToList();
        if (semFornecedor.Count > 0)
            throw new RegraNegocioException($"Cadastre o fornecedor das peças: {string.Join(", ", semFornecedor)}.");

        var agora = DateTime.Now;
        var pedidos = itens
            .GroupBy(i => pecas.GetValueOrDefault(i.PecaId)?.FornecedorId
                          ?? throw RegraNegocioException.NaoEncontrado($"Peça {i.PecaId} não encontrada."))
            .Select(g => new PedidoCompra
            {
                FornecedorId = g.Key,
                Status = StatusCompra.Aberto,
                CriadoEm = agora,
                Solicitante = VendaService.Usuario(req.Usuario),
                Itens = g.GroupBy(i => i.PecaId).Select(x => new ItemCompra
                {
                    PecaId = x.Key, Quantidade = x.Sum(i => i.Quantidade), CustoUnitario = pecas[x.Key].CustoMedio,
                }).ToList(),
            })
            .ToList();

        db.PedidosCompra.AddRange(pedidos);
        await db.SaveChangesAsync();

        var criados = pedidos.Select(p => p.Id).ToList();
        return (await ConsultaPedidos().Where(p => criados.Contains(p.Id)).ToListAsync()).Select(ParaDto).ToList();
    }

    /// <summary>Recebimento da NF-e: entrada automática no estoque e recálculo do custo médio.</summary>
    public async Task<PedidoCompraDto> ReceberAsync(int id, ReceberCompraRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.NotaFiscal)) throw new RegraNegocioException("Informe o número da NF-e.");

        var pedido = await db.PedidosCompra
                         .Include(p => p.Fornecedor).Include(p => p.Itens).ThenInclude(i => i.Peca)
                         .FirstOrDefaultAsync(p => p.Id == id)
                     ?? throw RegraNegocioException.NaoEncontrado($"Pedido de compra {id} não encontrado.");
        if (pedido.Status != StatusCompra.Aberto)
            throw new RegraNegocioException("Este pedido de compra não está em aberto.");

        var agora = DateTime.Now;
        var usuario = VendaService.Usuario(req.Usuario);
        var nf = req.NotaFiscal.Trim();
        foreach (var item in pedido.Itens)
        {
            item.Peca!.ReceberEntrada(item.Quantidade, item.CustoUnitario);
            db.Movimentacoes.Add(new Movimentacao
            {
                Peca = item.Peca, Tipo = TipoMovimentacao.Entrada, Quantidade = item.Quantidade,
                SaldoApos = item.Peca.Saldo, Documento = $"NF-e {nf}", Descricao = pedido.Fornecedor?.Nome,
                Usuario = usuario, Data = agora, PedidoCompraId = pedido.Id,
            });
        }
        pedido.Status = StatusCompra.Recebido;
        pedido.RecebidoEm = agora;
        pedido.NotaFiscal = nf;

        await db.SaveChangesAsync();
        return ParaDto(await ConsultaPedidos().FirstAsync(p => p.Id == id));
    }

    public async Task<PedidoCompraDto> CancelarPedidoAsync(int id)
    {
        var pedido = await db.PedidosCompra.FindAsync(id)
                     ?? throw RegraNegocioException.NaoEncontrado($"Pedido de compra {id} não encontrado.");
        if (pedido.Status != StatusCompra.Aberto)
            throw new RegraNegocioException("Somente pedidos em aberto podem ser cancelados.");
        pedido.Status = StatusCompra.Cancelado;
        await db.SaveChangesAsync();
        return ParaDto(await ConsultaPedidos().FirstAsync(p => p.Id == id));
    }

    private async Task<Dictionary<int, (int Quantidade, decimal Receita)>> VendidasNaJanelaAsync(DateTime agora)
    {
        var desde = agora.AddDays(-JanelaDias);
        var linhas = await db.ItensVenda.AsNoTracking()
            .Where(i => i.Venda!.Status == StatusVenda.Finalizada && i.Venda.FinalizadaEm >= desde)
            .GroupBy(i => i.PecaId)
            .Select(g => new
            {
                PecaId = g.Key,
                Quantidade = g.Sum(i => i.Quantidade),
                Receita = g.Sum(i => i.Quantidade * i.PrecoUnitario),
            })
            .ToListAsync();
        return linhas.ToDictionary(l => l.PecaId, l => (l.Quantidade, l.Receita));
    }

    private static Dictionary<int, string> ClassificarAbc(Dictionary<int, decimal> receitaPorPeca)
    {
        var total = receitaPorPeca.Values.Sum();
        var resultado = new Dictionary<int, string>();
        if (total <= 0) return resultado;

        decimal acumulado = 0;
        foreach (var (pecaId, receita) in receitaPorPeca.OrderByDescending(kv => kv.Value))
        {
            // A classe é decidida pelo acumulado *antes* do item, para o item que cruza 80% ainda ser A.
            var antes = acumulado / total;
            resultado[pecaId] = antes < 0.80m ? "A" : antes < 0.95m ? "B" : "C";
            acumulado += receita;
        }
        return resultado;
    }

    private IQueryable<PedidoCompra> ConsultaPedidos() =>
        db.PedidosCompra.AsNoTracking().Include(p => p.Fornecedor).Include(p => p.Itens).ThenInclude(i => i.Peca);

    private static PedidoCompraDto ParaDto(PedidoCompra p) => new(
        p.Id, p.Status, p.CriadoEm, p.RecebidoEm, p.NotaFiscal, p.FornecedorId, p.Fornecedor?.Nome ?? "",
        p.Fornecedor?.PrazoEntregaDias ?? 0, p.Solicitante, p.Itens.Sum(i => i.Quantidade * i.CustoUnitario),
        p.Itens.Select(i => new ItemCompraDto(
            i.PecaId, i.Peca?.Codigo ?? "", i.Peca?.Descricao ?? "", i.Quantidade, i.CustoUnitario)).ToList());
}
