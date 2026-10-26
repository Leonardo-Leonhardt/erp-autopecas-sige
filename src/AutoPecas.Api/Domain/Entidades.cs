namespace AutoPecas.Api.Domain;

public class Fornecedor
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public int PrazoEntregaDias { get; set; }
}

public class Cliente
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public string? Documento { get; set; }

    /// <summary>Desconto padrão do cliente (ex.: oficina parceira 5%).</summary>
    public decimal DescontoPercentual { get; set; }
}

public enum StatusVenda { Orcamento, Finalizada, Cancelada }

public class Venda
{
    public int Id { get; set; }
    public int? ClienteId { get; set; }
    public Cliente? Cliente { get; set; }
    public StatusVenda Status { get; set; }
    public string FormaPagamento { get; set; } = "";
    public string Vendedor { get; set; } = "";
    public DateTime CriadaEm { get; set; }
    public DateTime? FinalizadaEm { get; set; }
    public decimal DescontoPercentual { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Desconto { get; set; }
    public decimal Total { get; set; }
    public List<ItemVenda> Itens { get; set; } = [];

    public void CalcularTotais()
    {
        Subtotal = Itens.Sum(i => i.Quantidade * i.PrecoUnitario);
        Desconto = Math.Round(Subtotal * DescontoPercentual / 100m, 2);
        Total = Subtotal - Desconto;
    }
}

public class ItemVenda
{
    public int Id { get; set; }
    public int VendaId { get; set; }
    public Venda? Venda { get; set; }
    public int PecaId { get; set; }
    public Peca? Peca { get; set; }
    public int Quantidade { get; set; }
    public decimal PrecoUnitario { get; set; }
    public decimal CustoUnitario { get; set; }
}

public enum StatusCompra { Aberto, Recebido, Cancelado }

public class PedidoCompra
{
    public int Id { get; set; }
    public int FornecedorId { get; set; }
    public Fornecedor? Fornecedor { get; set; }
    public StatusCompra Status { get; set; }
    public DateTime CriadoEm { get; set; }
    public DateTime? RecebidoEm { get; set; }
    public string? NotaFiscal { get; set; }
    public string Solicitante { get; set; } = "";
    public List<ItemCompra> Itens { get; set; } = [];
}

public class ItemCompra
{
    public int Id { get; set; }
    public int PedidoCompraId { get; set; }
    public PedidoCompra? PedidoCompra { get; set; }
    public int PecaId { get; set; }
    public Peca? Peca { get; set; }
    public int Quantidade { get; set; }
    public decimal CustoUnitario { get; set; }
}

public enum TipoMovimentacao { Entrada, Saida, Ajuste, Reserva, LiberacaoReserva }

/// <summary>
/// Lançamento no histórico de estoque. Toda alteração de saldo ou de reserva gera uma linha,
/// permitindo auditar divergências entre estoque físico e sistema.
/// </summary>
public class Movimentacao
{
    public int Id { get; set; }
    public int PecaId { get; set; }
    public Peca? Peca { get; set; }
    public TipoMovimentacao Tipo { get; set; }

    /// <summary>Efeito no saldo físico (negativo em saídas). Em reservas, é a quantidade reservada.</summary>
    public int Quantidade { get; set; }

    public int SaldoApos { get; set; }
    public string? Documento { get; set; }
    public string? Descricao { get; set; }
    public string Usuario { get; set; } = "";
    public DateTime Data { get; set; }
    public int? VendaId { get; set; }
    public Venda? Venda { get; set; }
    public int? PedidoCompraId { get; set; }
}
