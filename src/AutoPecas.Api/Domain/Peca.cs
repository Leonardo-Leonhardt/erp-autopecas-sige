namespace AutoPecas.Api.Domain;

public enum StatusEstoque { Normal, AbaixoMinimo, Ruptura, SemGiro }

public class Peca
{
    /// <summary>Dias sem venda a partir dos quais a peça é considerada capital parado.</summary>
    public const int DiasSemGiro = 180;

    public int Id { get; set; }
    public string Codigo { get; set; } = "";
    public string? CodigoFabricante { get; set; }
    public string Descricao { get; set; } = "";
    public string Categoria { get; set; } = "";
    public string Marca { get; set; } = "";
    public string Aplicacao { get; set; } = "";
    public string Localizacao { get; set; } = "";

    public int? FornecedorId { get; set; }
    public Fornecedor? Fornecedor { get; set; }

    /// <summary>Saldo físico na prateleira (inclui o que está reservado).</summary>
    public int Saldo { get; private set; }

    /// <summary>Unidades reservadas por orçamentos ainda não finalizados.</summary>
    public int Reservado { get; private set; }

    public int EstoqueMinimo { get; set; }
    public int EstoqueMaximo { get; set; }
    public decimal CustoMedio { get; private set; }
    public decimal PrecoVenda { get; set; }
    public DateTime? UltimaVenda { get; private set; }
    public DateTime CadastradaEm { get; set; }

    /// <summary>Token de concorrência: impede que duas vendas simultâneas vendam a mesma unidade.</summary>
    public int Versao { get; private set; }

    /// <summary>O que pode ser vendido agora: saldo físico menos reservas.</summary>
    public int Disponivel => Saldo - Reservado;

    public StatusEstoque StatusEm(DateTime agora)
    {
        if (Disponivel <= 0) return StatusEstoque.Ruptura;
        if (Disponivel < EstoqueMinimo) return StatusEstoque.AbaixoMinimo;
        var referencia = UltimaVenda ?? CadastradaEm;
        if ((agora - referencia).TotalDays > DiasSemGiro) return StatusEstoque.SemGiro;
        return StatusEstoque.Normal;
    }

    public void DefinirCustoInicial(decimal custo) => CustoMedio = custo;

    public void Reservar(int quantidade)
    {
        ValidarQuantidade(quantidade);
        GarantirDisponivel(quantidade);
        Reservado += quantidade;
        Versao++;
    }

    public void LiberarReserva(int quantidade)
    {
        Reservado = Math.Max(0, Reservado - quantidade);
        Versao++;
    }

    /// <summary>Baixa de uma venda que já estava reservada (orçamento convertido em venda).</summary>
    public void BaixarReservado(int quantidade, DateTime quando)
    {
        ValidarQuantidade(quantidade);
        if (quantidade > Saldo)
            throw new RegraNegocioException(
                $"Saldo físico de {Codigo} ({Saldo}) é menor que o reservado ({quantidade}). Faça a conferência do inventário.");
        Reservado = Math.Max(0, Reservado - quantidade);
        Saldo -= quantidade;
        UltimaVenda = quando;
        Versao++;
    }

    /// <summary>Baixa de uma venda finalizada direto no balcão, sem reserva prévia.</summary>
    public void BaixarDireto(int quantidade, DateTime quando)
    {
        ValidarQuantidade(quantidade);
        GarantirDisponivel(quantidade);
        Saldo -= quantidade;
        UltimaVenda = quando;
        Versao++;
    }

    /// <summary>Entrada por compra (NF-e). Recalcula o custo médio ponderado.</summary>
    public void ReceberEntrada(int quantidade, decimal custoUnitario)
    {
        ValidarQuantidade(quantidade);
        CustoMedio = Saldo > 0
            ? Math.Round((Saldo * CustoMedio + quantidade * custoUnitario) / (Saldo + quantidade), 2)
            : custoUnitario;
        Saldo += quantidade;
        Versao++;
    }

    /// <summary>Ajuste de inventário: o saldo passa a ser o contado. Retorna a diferença aplicada.</summary>
    public int AjustarPara(int saldoContado)
    {
        if (saldoContado < 0) throw new RegraNegocioException("O saldo contado não pode ser negativo.");
        var diferenca = saldoContado - Saldo;
        Saldo = saldoContado;
        Versao++;
        return diferenca;
    }

    private static void ValidarQuantidade(int quantidade)
    {
        if (quantidade <= 0) throw new RegraNegocioException("A quantidade deve ser maior que zero.");
    }

    private void GarantirDisponivel(int quantidade)
    {
        if (quantidade > Disponivel)
            throw new RegraNegocioException(
                $"Estoque insuficiente para {Codigo}: disponível {Math.Max(0, Disponivel)}, solicitado {quantidade}.");
    }
}
