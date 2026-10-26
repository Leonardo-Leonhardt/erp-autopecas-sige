using AutoPecas.Api.Data;
using AutoPecas.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace AutoPecas.Api.Services;

/// <summary>Catálogo de peças, saldo, inventário e histórico de movimentações.</summary>
public class EstoqueService(AppDbContext db, ReposicaoService reposicao)
{
    public async Task<ListaPecasDto> ListarAsync(string? busca, StatusEstoque? status, string? categoria)
    {
        var agora = DateTime.Now;
        var query = db.Pecas.AsNoTracking().Include(p => p.Fornecedor).AsQueryable();

        if (!string.IsNullOrWhiteSpace(busca))
        {
            // Cada termo precisa aparecer em algum campo: "pastilha onix" acha a pastilha que serve no Onix.
            foreach (var termo in busca.Trim().ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                query = query.Where(p =>
                    p.Codigo.ToLower().Contains(termo) ||
                    p.Descricao.ToLower().Contains(termo) ||
                    p.Marca.ToLower().Contains(termo) ||
                    p.Aplicacao.ToLower().Contains(termo) ||
                    (p.CodigoFabricante != null && p.CodigoFabricante.ToLower().Contains(termo)));
            }
        }
        if (!string.IsNullOrWhiteSpace(categoria))
            query = query.Where(p => p.Categoria.StartsWith(categoria));

        var pecas = (await query.OrderBy(p => p.Codigo).ToListAsync()).Select(p => PecaDto.De(p, agora)).ToList();

        // O resumo considera a busca, mas não o filtro de status, para os contadores dos filtros fazerem sentido.
        var resumo = new ResumoEstoqueDto(
            pecas.Count,
            pecas.Count(p => p.Status == StatusEstoque.Ruptura),
            pecas.Count(p => p.Status == StatusEstoque.AbaixoMinimo),
            pecas.Count(p => p.Status == StatusEstoque.SemGiro));

        if (status is not null) pecas = pecas.Where(p => p.Status == status).ToList();
        return new ListaPecasDto(pecas, resumo);
    }

    public async Task<List<string>> CategoriasAsync() =>
        await db.Pecas.Select(p => p.Categoria).Distinct().OrderBy(c => c).ToListAsync();

    public async Task<PecaDetalheDto> DetalharAsync(int id)
    {
        var agora = DateTime.Now;
        var peca = await db.Pecas.AsNoTracking().Include(p => p.Fornecedor).FirstOrDefaultAsync(p => p.Id == id)
                   ?? throw RegraNegocioException.NaoEncontrado("Peça não encontrada.");

        var desde = agora.AddDays(-30);
        var vendidas = await db.ItensVenda
            .Where(i => i.PecaId == id && i.Venda!.Status == StatusVenda.Finalizada && i.Venda.FinalizadaEm >= desde)
            .SumAsync(i => (int?)i.Quantidade) ?? 0;

        decimal? cobertura = vendidas > 0 ? Math.Round(Math.Max(0, peca.Disponivel) / (vendidas / 30m), 1) : null;
        var margem = peca.PrecoVenda > 0 ? Math.Round((peca.PrecoVenda - peca.CustoMedio) / peca.PrecoVenda * 100, 1) : 0;
        var curvas = await reposicao.CurvaAbcAsync();

        return new PecaDetalheDto(PecaDto.De(peca, agora), vendidas, cobertura, margem, curvas.GetValueOrDefault(id, "C"));
    }

    public async Task<PecaDto> CriarAsync(PecaRequest req)
    {
        Validar(req);
        if (await db.Pecas.AnyAsync(p => p.Codigo == req.Codigo.Trim().ToUpper()))
            throw new RegraNegocioException($"Já existe uma peça com o código {req.Codigo}.", StatusCodes.Status409Conflict);
        if (req.SaldoInicial < 0) throw new RegraNegocioException("O saldo inicial não pode ser negativo.");

        var agora = DateTime.Now;
        var peca = new Peca { CadastradaEm = agora };
        Preencher(peca, req);
        peca.DefinirCustoInicial(req.CustoInicial);
        db.Pecas.Add(peca);

        if (req.SaldoInicial > 0)
        {
            peca.ReceberEntrada(req.SaldoInicial, req.CustoInicial);
            db.Movimentacoes.Add(new Movimentacao
            {
                Peca = peca, Tipo = TipoMovimentacao.Entrada, Quantidade = req.SaldoInicial, SaldoApos = peca.Saldo,
                Documento = "Saldo inicial", Descricao = "Cadastro da peça", Usuario = VendaService.Usuario(req.Usuario),
                Data = agora,
            });
        }

        await db.SaveChangesAsync();
        return await ObterAsync(peca.Id);
    }

    public async Task<PecaDto> AtualizarAsync(int id, PecaRequest req)
    {
        Validar(req);
        var peca = await db.Pecas.FindAsync(id) ?? throw RegraNegocioException.NaoEncontrado("Peça não encontrada.");
        if (await db.Pecas.AnyAsync(p => p.Id != id && p.Codigo == req.Codigo.Trim().ToUpper()))
            throw new RegraNegocioException($"Já existe outra peça com o código {req.Codigo}.", StatusCodes.Status409Conflict);

        // Saldo e custo médio não são editáveis aqui: só mudam por venda, compra ou ajuste de inventário.
        Preencher(peca, req);
        await db.SaveChangesAsync();
        return await ObterAsync(id);
    }

    public async Task<PecaDto> AjustarAsync(int id, AjusteRequest req)
    {
        var peca = await db.Pecas.FindAsync(id) ?? throw RegraNegocioException.NaoEncontrado("Peça não encontrada.");
        var diferenca = peca.AjustarPara(req.SaldoContado);
        if (diferenca == 0) throw new RegraNegocioException("O saldo contado é igual ao do sistema; nada a ajustar.");

        db.Movimentacoes.Add(new Movimentacao
        {
            Peca = peca, Tipo = TipoMovimentacao.Ajuste, Quantidade = diferenca, SaldoApos = peca.Saldo,
            Documento = "Inventário",
            Descricao = string.IsNullOrWhiteSpace(req.Motivo) ? "Divergência física" : req.Motivo.Trim(),
            Usuario = VendaService.Usuario(req.Usuario), Data = DateTime.Now,
        });
        await db.SaveChangesAsync();
        return await ObterAsync(id);
    }

    public async Task<List<MovimentacaoDto>> MovimentacoesAsync(int? pecaId, TipoMovimentacao? tipo, int limite)
    {
        var query = db.Movimentacoes.AsNoTracking();
        if (pecaId is not null) query = query.Where(m => m.PecaId == pecaId);
        if (tipo is not null) query = query.Where(m => m.Tipo == tipo);

        return await query
            .OrderByDescending(m => m.Data).ThenByDescending(m => m.Id)
            .Take(limite)
            .Select(m => new MovimentacaoDto(
                m.Id, m.Data, m.Tipo, m.Quantidade, m.SaldoApos,
                m.VendaId != null ? "Venda #" + m.VendaId : m.Documento,
                m.Descricao, m.Usuario, m.PecaId, m.Peca!.Codigo, m.Peca.Descricao))
            .ToListAsync();
    }

    private async Task<PecaDto> ObterAsync(int id)
    {
        var peca = await db.Pecas.AsNoTracking().Include(p => p.Fornecedor).FirstAsync(p => p.Id == id);
        return PecaDto.De(peca, DateTime.Now);
    }

    private static void Validar(PecaRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Codigo)) throw new RegraNegocioException("Informe o código da peça.");
        if (string.IsNullOrWhiteSpace(req.Descricao)) throw new RegraNegocioException("Informe a descrição da peça.");
        if (req.EstoqueMinimo < 0 || req.EstoqueMaximo < req.EstoqueMinimo)
            throw new RegraNegocioException("O estoque máximo deve ser maior ou igual ao mínimo.");
        if (req.PrecoVenda <= 0) throw new RegraNegocioException("O preço de venda deve ser maior que zero.");
    }

    private static void Preencher(Peca p, PecaRequest r)
    {
        p.Codigo = r.Codigo.Trim().ToUpper();
        p.CodigoFabricante = string.IsNullOrWhiteSpace(r.CodigoFabricante) ? null : r.CodigoFabricante.Trim();
        p.Descricao = r.Descricao.Trim();
        p.Categoria = r.Categoria?.Trim() ?? "";
        p.Marca = r.Marca?.Trim() ?? "";
        p.Aplicacao = r.Aplicacao?.Trim() ?? "";
        p.Localizacao = r.Localizacao?.Trim() ?? "";
        p.FornecedorId = r.FornecedorId;
        p.EstoqueMinimo = r.EstoqueMinimo;
        p.EstoqueMaximo = r.EstoqueMaximo;
        p.PrecoVenda = r.PrecoVenda;
    }
}
