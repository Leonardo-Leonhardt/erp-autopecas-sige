using AutoPecas.Api.Data;
using AutoPecas.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace AutoPecas.Api.Services;

/// <summary>
/// Processamento de vendas integrado ao estoque:
/// orçamento salvo → reserva as peças; venda finalizada → baixa automática do saldo.
/// </summary>
public class VendaService(AppDbContext db)
{
    public async Task<List<VendaDto>> ListarAsync(StatusVenda? status, int limite)
    {
        var query = Consulta();
        if (status is not null) query = query.Where(v => v.Status == status);
        var vendas = await query.OrderByDescending(v => v.Id).Take(limite).ToListAsync();
        return vendas.Select(ParaDto).ToList();
    }

    public async Task<VendaDto> ObterAsync(int id) =>
        ParaDto(await Consulta().FirstOrDefaultAsync(v => v.Id == id)
                ?? throw RegraNegocioException.NaoEncontrado($"Venda #{id} não encontrada."));

    public async Task<VendaDto> CriarAsync(NovaVendaRequest req)
    {
        var itens = (req.Itens ?? [])
            .GroupBy(i => i.PecaId)
            .Select(g => (PecaId: g.Key, Quantidade: g.Sum(i => i.Quantidade)))
            .ToList();
        if (itens.Count == 0) throw new RegraNegocioException("A venda precisa de ao menos um item.");

        Cliente? cliente = null;
        if (req.ClienteId is int clienteId)
            cliente = await db.Clientes.FindAsync(clienteId)
                      ?? throw RegraNegocioException.NaoEncontrado("Cliente não encontrado.");

        var ids = itens.Select(i => i.PecaId).ToList();
        var pecas = await db.Pecas.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id);

        var agora = DateTime.Now;
        var usuario = Usuario(req.Usuario);
        var venda = new Venda
        {
            Cliente = cliente,
            Status = req.Finalizar ? StatusVenda.Finalizada : StatusVenda.Orcamento,
            FormaPagamento = string.IsNullOrWhiteSpace(req.FormaPagamento) ? "Dinheiro" : req.FormaPagamento,
            Vendedor = usuario,
            CriadaEm = agora,
            FinalizadaEm = req.Finalizar ? agora : null,
            DescontoPercentual = cliente?.DescontoPercentual ?? 0,
        };

        foreach (var (pecaId, quantidade) in itens)
        {
            var peca = pecas.GetValueOrDefault(pecaId)
                       ?? throw RegraNegocioException.NaoEncontrado($"Peça {pecaId} não encontrada.");

            venda.Itens.Add(new ItemVenda
            {
                Peca = peca, Quantidade = quantidade, PrecoUnitario = peca.PrecoVenda, CustoUnitario = peca.CustoMedio,
            });

            if (req.Finalizar)
            {
                peca.BaixarDireto(quantidade, agora);
                db.Movimentacoes.Add(Movimento(peca, venda, TipoMovimentacao.Saida, -quantidade, usuario, agora));
            }
            else
            {
                peca.Reservar(quantidade);
                db.Movimentacoes.Add(Movimento(peca, venda, TipoMovimentacao.Reserva, quantidade, usuario, agora));
            }
        }

        venda.CalcularTotais();
        db.Vendas.Add(venda);
        // Um único SaveChanges: venda, itens, saldo e histórico são gravados na mesma transação.
        await db.SaveChangesAsync();
        return await ObterAsync(venda.Id);
    }

    public async Task<VendaDto> FinalizarAsync(int id, string? usuarioInformado)
    {
        var venda = await CarregarParaAlterar(id);
        if (venda.Status != StatusVenda.Orcamento)
            throw new RegraNegocioException($"Venda #{id} não é um orçamento em aberto.");

        var agora = DateTime.Now;
        var usuario = Usuario(usuarioInformado);
        foreach (var item in venda.Itens)
        {
            item.Peca!.BaixarReservado(item.Quantidade, agora);
            db.Movimentacoes.Add(Movimento(item.Peca, venda, TipoMovimentacao.Saida, -item.Quantidade, usuario, agora));
        }
        venda.Status = StatusVenda.Finalizada;
        venda.FinalizadaEm = agora;

        await db.SaveChangesAsync();
        return await ObterAsync(id);
    }

    public async Task<VendaDto> CancelarAsync(int id, string? usuarioInformado)
    {
        var venda = await CarregarParaAlterar(id);
        if (venda.Status != StatusVenda.Orcamento)
            throw new RegraNegocioException("Somente orçamentos em aberto podem ser cancelados.");

        var agora = DateTime.Now;
        var usuario = Usuario(usuarioInformado);
        foreach (var item in venda.Itens)
        {
            item.Peca!.LiberarReserva(item.Quantidade);
            db.Movimentacoes.Add(Movimento(item.Peca, venda, TipoMovimentacao.LiberacaoReserva, item.Quantidade, usuario, agora));
        }
        venda.Status = StatusVenda.Cancelada;

        await db.SaveChangesAsync();
        return await ObterAsync(id);
    }

    private IQueryable<Venda> Consulta() =>
        db.Vendas.AsNoTracking().Include(v => v.Cliente).Include(v => v.Itens).ThenInclude(i => i.Peca);

    private async Task<Venda> CarregarParaAlterar(int id) =>
        await db.Vendas.Include(v => v.Cliente).Include(v => v.Itens).ThenInclude(i => i.Peca)
            .FirstOrDefaultAsync(v => v.Id == id)
        ?? throw RegraNegocioException.NaoEncontrado($"Venda #{id} não encontrada.");

    private static Movimentacao Movimento(
        Peca peca, Venda venda, TipoMovimentacao tipo, int quantidade, string usuario, DateTime quando) => new()
    {
        Peca = peca,
        Venda = venda,
        Tipo = tipo,
        Quantidade = quantidade,
        SaldoApos = peca.Saldo,
        Descricao = venda.Cliente?.Nome ?? "Consumidor final",
        Usuario = usuario,
        Data = quando,
    };

    internal static string Usuario(string? usuario) => string.IsNullOrWhiteSpace(usuario) ? "Sistema" : usuario.Trim();

    internal static VendaDto ParaDto(Venda v) => new(
        v.Id, v.Status, v.CriadaEm, v.FinalizadaEm, v.ClienteId, v.Cliente?.Nome ?? "Consumidor final",
        v.Vendedor, v.FormaPagamento, v.Subtotal, v.Desconto, v.Total,
        v.Itens.Select(i => new ItemVendaDto(
            i.PecaId, i.Peca?.Codigo ?? "", i.Peca?.Descricao ?? "", i.Quantidade, i.PrecoUnitario,
            i.Quantidade * i.PrecoUnitario)).ToList());
}
