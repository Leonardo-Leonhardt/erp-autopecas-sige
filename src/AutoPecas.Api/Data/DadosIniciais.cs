using AutoPecas.Api.Domain;

namespace AutoPecas.Api.Data;

/// <summary>
/// Carga de demonstração: catálogo do protótipo e ~90 dias de histórico de vendas gerado de forma
/// determinística. O histórico é aplicado pelas próprias regras da entidade <see cref="Peca"/>,
/// então saldo, reservas e movimentações ficam sempre consistentes entre si.
/// </summary>
public static class DadosIniciais
{
    private record PecaSemente(
        string Codigo, string? Fabricante, string Descricao, string Categoria, string Marca, string Aplicacao,
        string Local, string Fornecedor, int SaldoFinal, int Minimo, int Maximo, decimal Custo, decimal Preco,
        int MediaMensal, int DiasSemVenda = 0);

    private record EventoVenda(Peca Peca, DateTime Quando, int Quantidade);

    private static readonly string[] Vendedores = ["Juliana S.", "Carlos M."];
    private static readonly string[] Pagamentos = ["PIX", "PIX", "Cartão", "Dinheiro", "A prazo"];

    public static void Popular(AppDbContext db)
    {
        if (db.Pecas.Any()) return;

        var agora = DateTime.Now;
        var rnd = new Random(2026);

        var fornecedores = new Dictionary<string, Fornecedor>
        {
            ["Minas"] = new() { Nome = "Distribuidora Minas Peças", PrazoEntregaDias = 3 },
            ["NGK"] = new() { Nome = "NGK Distribuidora", PrazoEntregaDias = 5 },
            ["Cofap"] = new() { Nome = "Cofap Direto", PrazoEntregaDias = 7 },
            ["Tecfil"] = new() { Nome = "Tecfil", PrazoEntregaDias = 4 },
            ["Philips"] = new() { Nome = "Philips Distribuidora", PrazoEntregaDias = 5 },
            ["Bosch"] = new() { Nome = "Bosch Autopeças", PrazoEntregaDias = 6 },
            ["Hipper"] = new() { Nome = "Hipper Freios", PrazoEntregaDias = 4 },
            ["LuK"] = new() { Nome = "Schaeffler LuK", PrazoEntregaDias = 7 },
            ["Urba"] = new() { Nome = "Urba Bombas", PrazoEntregaDias = 5 },
            ["Arteb"] = new() { Nome = "Arteb Iluminação e Carroceria", PrazoEntregaDias = 10 },
        };
        db.Fornecedores.AddRange(fornecedores.Values);

        var clientes = new List<Cliente>
        {
            new() { Nome = "Consumidor final" },
            new() { Nome = "Oficina Mecânica do Zé", Documento = "12.345.678/0001-90", DescontoPercentual = 5 },
            new() { Nome = "Auto Center Savassi", Documento = "23.456.789/0001-01", DescontoPercentual = 5 },
            new() { Nome = "Oficina Irmãos Reis", Documento = "34.567.890/0001-12", DescontoPercentual = 3 },
        };
        db.Clientes.AddRange(clientes);

        PecaSemente[] sementes =
        [
            new("FR-1052", "N-1052", "Pastilha de freio dianteira", "Freios › Pastilhas", "Cobreq", "Chevrolet Onix 2013–22 · Prisma 2013–19 · Cobalt 1.4", "A-03-2", "Minas", 3, 8, 30, 62.40m, 119.90m, 21),
            new("FR-2210", "PD-2210", "Pastilha de freio dianteira cerâmica", "Freios › Pastilhas", "Fras-le", "Chevrolet Onix, Prisma 2013–22", "A-03-3", "Minas", 11, 6, 24, 88.00m, 164.00m, 8),
            new("FR-3307", "0986BB0307", "Pastilha de freio dianteira", "Freios › Pastilhas", "Bosch", "Chevrolet Onix 2013–22", "A-03-4", "Bosch", 0, 4, 16, 72.00m, 139.50m, 5),
            new("DF-4471", "HF-4471", "Disco de freio ventilado", "Freios › Discos", "Hipper Freios", "Chevrolet Onix, Cobalt", "A-04-1", "Hipper", 14, 6, 24, 98.00m, 189.00m, 10),
            new("FL-0090", "DOT4-500", "Fluido de freio DOT 4 500ml", "Freios › Fluidos", "Bosch", "Universal", "C-07-1", "Bosch", 40, 15, 60, 13.50m, 29.90m, 30),
            new("CD-5463", "5463XS", "Correia dentada", "Motor › Correias", "Gates", "VW Gol, Voyage 1.0 8v", "B-11-1", "Minas", 0, 4, 16, 71.00m, 134.00m, 9),
            new("VL-6E", "BKR6E", "Vela de ignição BKR6E", "Motor › Ignição", "NGK", "Universal", "C-02-2", "NGK", 0, 20, 80, 11.90m, 24.90m, 48),
            new("FO-0055", "PSL55", "Filtro de óleo PSL 55", "Filtros", "Tecfil", "Linha GM 1.0/1.4", "C-01-4", "Tecfil", 46, 20, 80, 14.20m, 32.90m, 40),
            new("FA-3310", "ARL3310", "Filtro de ar", "Filtros", "Tecfil", "Chevrolet Onix 2013–22", "C-01-6", "Tecfil", 4, 10, 30, 22.00m, 49.90m, 11),
            new("AM-7710", "GP32771", "Amortecedor traseiro", "Suspensão", "Cofap", "Chevrolet Onix 1.0 2017–22", "D-05-1", "Cofap", 2, 6, 12, 280.00m, 590.00m, 5),
            new("EB-2231", "620322100", "Kit embreagem", "Transmissão", "LuK", "Hyundai HB20 1.0 2013–19", "D-02-2", "LuK", 5, 3, 10, 410.00m, 799.00m, 3),
            new("BB-0321", "UB0321", "Bomba d'água", "Arrefecimento", "Urba", "Fiat Palio, Uno Fire", "B-04-2", "Urba", 7, 4, 12, 96.00m, 185.00m, 4),
            new("LP-H4", "12342", "Lâmpada H4 60/55W", "Iluminação", "Philips", "Universal", "C-05-1", "Philips", 6, 12, 40, 14.50m, 34.90m, 14),
            new("FA-G4E", null, "Farol lado esquerdo", "Iluminação", "Arteb", "VW Gol G4 2006–13", "E-09-3", "Arteb", 9, 2, 6, 185.00m, 349.00m, 0, 240),
            new("RT-CC1", null, "Retrovisor Corsa Classic", "Carroceria", "Metagal", "Chevrolet Corsa Classic 2003–16", "E-02-1", "Arteb", 6, 2, 4, 170.00m, 299.00m, 0, 310),
            new("RD-P96", null, "Radiador Palio 1.0 96–00", "Arrefecimento", "Visconde", "Fiat Palio 1.0 1996–2000", "B-08-2", "Urba", 3, 1, 3, 290.00m, 520.00m, 0, 198),
            new("PC-F08", null, "Para-choque dianteiro Fiesta 2008", "Carroceria", "Plastisul", "Ford Fiesta 2007–10", "E-11-2", "Arteb", 4, 1, 3, 190.00m, 360.00m, 0, 185),
        ];

        // 1) Gera os eventos de venda de cada peça.
        var pecas = new Dictionary<Peca, PecaSemente>();
        var eventos = new List<EventoVenda>();
        foreach (var s in sementes)
        {
            var peca = new Peca
            {
                Codigo = s.Codigo, CodigoFabricante = s.Fabricante, Descricao = s.Descricao, Categoria = s.Categoria,
                Marca = s.Marca, Aplicacao = s.Aplicacao, Localizacao = s.Local, Fornecedor = fornecedores[s.Fornecedor],
                EstoqueMinimo = s.Minimo, EstoqueMaximo = s.Maximo, PrecoVenda = s.Preco,
                CadastradaEm = agora.Date.AddDays(s.DiasSemVenda > 0 ? -365 : -90).AddHours(7),
            };
            peca.DefinirCustoInicial(s.Custo);
            pecas[peca] = s;

            if (s.DiasSemVenda > 0)
            {
                eventos.Add(new EventoVenda(peca, agora.Date.AddDays(-s.DiasSemVenda).AddHours(10), 1));
                continue;
            }

            // Acumula a demanda diária com ruído; quando passa de 1 unidade vira uma venda.
            var porDia = s.MediaMensal / 30.0;
            var acumulado = 0.0;
            for (var d = 89; d >= 0; d--)
            {
                acumulado += porDia * (0.4 + 1.2 * rnd.NextDouble());
                while (acumulado >= 1)
                {
                    var qtd = Math.Min((int)acumulado, 1 + rnd.Next(4));
                    acumulado -= qtd;
                    var quando = agora.Date.AddDays(-d).AddHours(8 + rnd.Next(10)).AddMinutes(rnd.Next(60));
                    if (quando > agora) quando = agora.AddMinutes(-rnd.Next(5, 90));
                    eventos.Add(new EventoVenda(peca, quando, qtd));
                }
            }
        }

        // 2) Agrupa eventos do mesmo dia em vendas com mais de um item, como no balcão.
        var vendas = new List<Venda>();
        var itensPorPeca = new Dictionary<Peca, List<(DateTime Quando, int Quantidade, Venda Venda)>>();
        Venda? atual = null;
        foreach (var e in eventos.OrderBy(e => e.Quando))
        {
            var juntar = atual is not null && atual.CriadaEm.Date == e.Quando.Date && atual.Itens.Count < 3
                         && atual.Itens.All(i => i.Peca != e.Peca) && rnd.NextDouble() < 0.35;
            if (!juntar)
            {
                var cliente = clientes[rnd.Next(clientes.Count)];
                atual = new Venda
                {
                    Cliente = cliente, Status = StatusVenda.Finalizada,
                    FormaPagamento = Pagamentos[rnd.Next(Pagamentos.Length)], Vendedor = Vendedores[rnd.Next(Vendedores.Length)],
                    CriadaEm = e.Quando, FinalizadaEm = e.Quando, DescontoPercentual = cliente.DescontoPercentual,
                };
                vendas.Add(atual);
            }
            atual!.Itens.Add(new ItemVenda
            {
                Peca = e.Peca, Quantidade = e.Quantidade, PrecoUnitario = e.Peca.PrecoVenda, CustoUnitario = pecas[e.Peca].Custo,
            });
            if (!itensPorPeca.TryGetValue(e.Peca, out var lista)) itensPorPeca[e.Peca] = lista = [];
            lista.Add((atual.CriadaEm, e.Quantidade, atual));
        }
        for (var i = 0; i < vendas.Count; i++)
        {
            vendas[i].Id = 10300 + i;
            vendas[i].CalcularTotais();
        }
        db.Vendas.AddRange(vendas);

        // 3) Reconstrói o histórico de cada peça: implantação → (NF-e no meio do período) → vendas.
        var notaFiscal = 45100;
        foreach (var (peca, s) in pecas)
        {
            var saidas = itensPorPeca.GetValueOrDefault(peca) ?? [];
            var totalVendido = saidas.Sum(x => x.Quantidade);

            var dataEntrada = agora.Date.AddDays(-45).AddHours(9);
            var vendidoDepois = saidas.Where(x => x.Quando > dataEntrada).Sum(x => x.Quantidade);
            // A entrada nunca pode ser maior que o que o saldo comporta, para o histórico não ficar negativo.
            var entrada = s.DiasSemVenda == 0 && s.MediaMensal >= 5 ? Math.Min(s.Maximo, s.SaldoFinal + vendidoDepois) : 0;
            var inicial = s.SaldoFinal + totalVendido - entrada;

            var linhas = new List<(DateTime Quando, Action Aplicar)>();
            if (inicial > 0)
                linhas.Add((peca.CadastradaEm, () =>
                {
                    peca.ReceberEntrada(inicial, s.Custo);
                    db.Movimentacoes.Add(Movimento(peca, TipoMovimentacao.Entrada, inicial, peca.CadastradaEm,
                        "Saldo inicial", "Implantação do sistema", "Roberto G."));
                }));
            if (entrada > 0)
            {
                var numero = notaFiscal++;
                var nf = $"NF-e {numero / 1000}.{numero % 1000:000}";
                linhas.Add((dataEntrada, () =>
                {
                    peca.ReceberEntrada(entrada, s.Custo);
                    db.Movimentacoes.Add(Movimento(peca, TipoMovimentacao.Entrada, entrada, dataEntrada,
                        nf, peca.Fornecedor!.Nome, "Roberto G."));
                }));
            }
            foreach (var (quando, quantidade, venda) in saidas)
                linhas.Add((quando, () =>
                {
                    peca.BaixarDireto(quantidade, quando);
                    var mov = Movimento(peca, TipoMovimentacao.Saida, -quantidade, quando, null,
                        venda.Cliente!.Nome, venda.Vendedor);
                    mov.Venda = venda;
                    db.Movimentacoes.Add(mov);
                }));

            foreach (var linha in linhas.OrderBy(l => l.Quando)) linha.Aplicar();
            db.Pecas.Add(peca);
        }

        // 4) Um orçamento em aberto, para demonstrar a reserva de estoque.
        var disco = pecas.Keys.First(p => p.Codigo == "DF-4471");
        var savassi = clientes.First(c => c.Nome == "Auto Center Savassi");
        var orcamento = new Venda
        {
            Id = 10300 + vendas.Count, Cliente = savassi, Status = StatusVenda.Orcamento, FormaPagamento = "A prazo",
            Vendedor = "Juliana S.", CriadaEm = agora, DescontoPercentual = savassi.DescontoPercentual,
        };
        orcamento.Itens.Add(new ItemVenda { Peca = disco, Quantidade = 2, PrecoUnitario = disco.PrecoVenda, CustoUnitario = disco.CustoMedio });
        orcamento.CalcularTotais();
        disco.Reservar(2);
        var reserva = Movimento(disco, TipoMovimentacao.Reserva, 2, orcamento.CriadaEm, null, savassi.Nome, "Juliana S.");
        reserva.Venda = orcamento;
        db.Movimentacoes.Add(reserva);
        db.Vendas.Add(orcamento);

        db.SaveChanges();
    }

    private static Movimentacao Movimento(
        Peca peca, TipoMovimentacao tipo, int quantidade, DateTime quando, string? documento, string? descricao,
        string usuario) => new()
    {
        Peca = peca, Tipo = tipo, Quantidade = quantidade, SaldoApos = peca.Saldo, Data = quando,
        Documento = documento, Descricao = descricao, Usuario = usuario,
    };
}
