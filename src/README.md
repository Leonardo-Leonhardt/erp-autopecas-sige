# Código fonte

> Código fonte do projeto

```
src/
└── AutoPecas.Api/
    ├── Domain/       # Entidades e regras de negócio (Peca: reservar, baixar, receber, ajustar)
    ├── Data/         # AppDbContext (EF Core) e dados de demonstração
    ├── Services/     # Vendas, Estoque, Reposição/Compras e Dashboard
    ├── Endpoints/    # Rotas da API REST (/api/...) e tratamento de erros
    └── wwwroot/      # Frontend HTML/CSS/JS (SPA servida pela própria API)
```

O protótipo estático original das telas fica em [`prototipo/`](../prototipo/).

## Regras de negócio implementadas

| Processo | Como funciona |
|---|---|
| **Venda finalizada** | Baixa automática do saldo, com uma movimentação de *Saída* por item. |
| **Orçamento** | Reserva as peças: o saldo físico não muda, mas o *disponível* (saldo − reservado) diminui. Finalizar converte a reserva em baixa; cancelar libera a reserva. |
| **Venda sem estoque** | Bloqueada: a API recusa vender mais que o disponível. |
| **Vendas simultâneas** | Controle de concorrência otimista (`Peca.Versao`): se duas vendas disputam a mesma unidade, a segunda recebe erro 409 em vez de deixar o saldo negativo. |
| **Inventário** | Ajuste para o saldo contado, registrando a diferença e o motivo. |
| **Status do estoque** | Ruptura (disponível ≤ 0), Abaixo do mínimo, Sem giro (> 180 dias sem venda) e Normal. |
| **Sugestão de compra** | Ponto de pedido = mínimo + vendas previstas no prazo do fornecedor (média dos últimos 90 dias). Sugere completar até o máximo, descontando o que já está em pedido aberto. Curva ABC por faturamento. |
| **Pedido de compra** | Gerado um pedido por fornecedor; ao receber a NF-e, entra no estoque e recalcula o custo médio ponderado. |

## API

| Método | Rota | Descrição |
|---|---|---|
| GET | `/api/dashboard` | Indicadores, vendas dos últimos 7 dias e alertas |
| GET | `/api/pecas?busca=&status=&categoria=` | Catálogo com saldo, reservado e disponível |
| GET | `/api/pecas/{id}` | Detalhe com vendas em 30 dias, cobertura, margem e curva |
| POST / PUT | `/api/pecas`, `/api/pecas/{id}` | Cadastro e edição de peça |
| POST | `/api/pecas/{id}/ajustes` | Ajuste de inventário |
| GET | `/api/movimentacoes?pecaId=&tipo=` | Histórico de movimentações |
| GET / POST | `/api/vendas` | Lista / cria venda (`finalizar: true`) ou orçamento (`finalizar: false`) |
| POST | `/api/vendas/{id}/finalizar`, `/cancelar` | Converte ou cancela orçamento |
| GET | `/api/reposicao` | Sugestão de compra e itens sem giro |
| GET / POST | `/api/compras` | Lista / gera pedidos de compra |
| POST | `/api/compras/{id}/receber`, `/cancelar` | Recebe NF-e (entrada no estoque) ou cancela |
| GET | `/api/clientes`, `/api/fornecedores` | Cadastros de apoio |
