using AutoPecas.Api.Domain;

namespace AutoPecas.Api.Services;

// ---------- Peças ----------

public record PecaDto(
    int Id, string Codigo, string? CodigoFabricante, string Descricao, string Categoria, string Marca,
    string Aplicacao, string Localizacao, int? FornecedorId, string? Fornecedor, int? PrazoEntregaDias,
    int Saldo, int Reservado, int Disponivel, int EstoqueMinimo, int EstoqueMaximo,
    decimal CustoMedio, decimal PrecoVenda, DateTime? UltimaVenda, StatusEstoque Status)
{
    public static PecaDto De(Peca p, DateTime agora) => new(
        p.Id, p.Codigo, p.CodigoFabricante, p.Descricao, p.Categoria, p.Marca,
        p.Aplicacao, p.Localizacao, p.FornecedorId, p.Fornecedor?.Nome, p.Fornecedor?.PrazoEntregaDias,
        p.Saldo, p.Reservado, p.Disponivel, p.EstoqueMinimo, p.EstoqueMaximo,
        p.CustoMedio, p.PrecoVenda, p.UltimaVenda, p.StatusEm(agora));
}

public record PecaDetalheDto(
    PecaDto Peca, int VendidasUltimos30Dias, decimal? CoberturaDias, decimal MargemPercentual, string Curva);

public record PecaRequest(
    string Codigo, string? CodigoFabricante, string Descricao, string Categoria, string Marca,
    string Aplicacao, string Localizacao, int? FornecedorId, int EstoqueMinimo, int EstoqueMaximo,
    decimal PrecoVenda, decimal CustoInicial, int SaldoInicial, string? Usuario);

public record ResumoEstoqueDto(int Total, int Ruptura, int AbaixoMinimo, int SemGiro);

public record ListaPecasDto(List<PecaDto> Itens, ResumoEstoqueDto Resumo);

// ---------- Movimentações ----------

public record MovimentacaoDto(
    int Id, DateTime Data, TipoMovimentacao Tipo, int Quantidade, int SaldoApos,
    string? Documento, string? Descricao, string Usuario, int PecaId, string PecaCodigo, string PecaDescricao);

public record AjusteRequest(int SaldoContado, string? Motivo, string? Usuario);

// ---------- Vendas ----------

public record ItemVendaRequest(int PecaId, int Quantidade);

public record NovaVendaRequest(
    int? ClienteId, string? FormaPagamento, string? Usuario, List<ItemVendaRequest>? Itens, bool Finalizar);

public record AcaoUsuarioRequest(string? Usuario);

public record ItemVendaDto(int PecaId, string Codigo, string Descricao, int Quantidade, decimal PrecoUnitario, decimal Subtotal);

public record VendaDto(
    int Id, StatusVenda Status, DateTime CriadaEm, DateTime? FinalizadaEm, int? ClienteId, string Cliente,
    string Vendedor, string FormaPagamento, decimal Subtotal, decimal Desconto, decimal Total,
    List<ItemVendaDto> Itens);

// ---------- Reposição / compras ----------

public record SugestaoCompraDto(
    int PecaId, string Codigo, string Descricao, string Curva, int Disponivel, int EstoqueMinimo, int EstoqueMaximo,
    decimal MediaMensal, int PontoPedido, int EmPedido, int Sugerido, int? FornecedorId, string? Fornecedor,
    decimal CustoUnitario, decimal CustoTotal, StatusEstoque Status);

public record ExcessoDto(int PecaId, string Codigo, string Descricao, int Saldo, int DiasSemVenda, decimal ValorParado);

public record ReposicaoDto(List<SugestaoCompraDto> Sugestoes, List<ExcessoDto> Excessos, int JanelaDias);

public record NovoPedidoCompraRequest(List<ItemVendaRequest>? Itens, string? Usuario);

public record ReceberCompraRequest(string? NotaFiscal, string? Usuario);

public record ItemCompraDto(int PecaId, string Codigo, string Descricao, int Quantidade, decimal CustoUnitario);

public record PedidoCompraDto(
    int Id, StatusCompra Status, DateTime CriadoEm, DateTime? RecebidoEm, string? NotaFiscal, int FornecedorId,
    string Fornecedor, int PrazoEntregaDias, string Solicitante, decimal Total, List<ItemCompraDto> Itens);

// ---------- Dashboard ----------

public record SerieDiaDto(DateTime Dia, decimal Vendas, int UnidadesBaixadas);

public record AlertaDto(int PecaId, string Codigo, string Descricao, StatusEstoque Status, string Detalhe);

public record DashboardDto(
    decimal VendasHoje, int QtdVendasHoje, decimal VendasOntem, decimal ValorEmEstoque, int SkusAtivos,
    int Rupturas, int AbaixoMinimo, int SemGiro, decimal CapitalParado, int OrcamentosAbertos,
    List<SerieDiaDto> UltimosDias, List<VendaDto> UltimasVendas, List<AlertaDto> Alertas);
