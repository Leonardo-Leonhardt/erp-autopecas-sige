// Dashboard: visão unificada de vendas e estoque, atualizada automaticamente.
import { api } from '../api.js';
import { esc, moeda, numero, tag, STATUS_ESTOQUE, STATUS_VENDA } from '../ui.js';

const ATUALIZAR_A_CADA_MS = 30_000;
const DIAS = ['Dom', 'Seg', 'Ter', 'Qua', 'Qui', 'Sex', 'Sáb'];

export async function render(ctx) {
  const hoje = new Date().toLocaleDateString('pt-BR', { day: '2-digit', month: 'short', year: 'numeric' });
  ctx.cabecalho({
    titulo: 'Dashboard',
    crumb: `Hoje, ${hoje} · atualiza a cada 30 s`,
    acoes: '<a class="btn primary" href="#/venda">+ Nova venda</a>',
  });

  const desenhar = async () => {
    const d = await api.get('/dashboard');
    if (ctx.ativa()) ctx.view.innerHTML = html(d);
  };
  await desenhar();

  const timer = setInterval(() => desenhar().catch(() => { /* tenta de novo no próximo ciclo */ }), ATUALIZAR_A_CADA_MS);
  return () => clearInterval(timer);
}

function html(d) {
  const variacao = d.vendasOntem > 0 ? Math.round((d.vendasHoje / d.vendasOntem - 1) * 100) : null;
  const max = Math.max(...d.ultimosDias.map((x) => x.vendas), 1);

  return `
    <div class="grid4">
      <div class="card kpi"><div class="l">Vendas hoje</div><div class="v">${moeda(d.vendasHoje)}</div>
        <div class="d ${variacao === null ? 'muted' : variacao >= 0 ? 'up' : 'down'}">
          ${variacao === null ? '' : `${variacao >= 0 ? '▲' : '▼'} ${Math.abs(variacao)}% vs. ontem · `}${d.qtdVendasHoje} vendas</div></div>
      <div class="card kpi"><div class="l">Valor em estoque (custo)</div><div class="v">${moeda(d.valorEmEstoque)}</div>
        <div class="d muted">${numero(d.skusAtivos)} SKUs · ${d.orcamentosAbertos} orçamento(s) reservando peças</div></div>
      <a class="card kpi link" href="#/estoque?status=Ruptura"><div class="l">Itens em ruptura</div><div class="v c-danger">${d.rupturas}</div>
        <div class="d c-warn">+ ${d.abaixoMinimo} abaixo do mínimo</div></a>
      <a class="card kpi link" href="#/reposicao"><div class="l">Capital parado (&gt;180 dias)</div><div class="v c-warn">${moeda(d.capitalParado)}</div>
        <div class="d muted">${d.semGiro} itens sem giro</div></a>
    </div>

    <div class="row">
      <div class="card" style="flex:1.6">
        <h3>Vendas × baixas de estoque <span class="sub">últimos 7 dias</span></h3>
        <div class="bars">
          ${d.ultimosDias.map((x, i) => {
            const dia = new Date(x.dia);
            const ehHoje = i === d.ultimosDias.length - 1;
            return `<div class="bar ${ehHoje ? 'hoje' : ''}" title="${moeda(x.vendas)} · ${x.unidadesBaixadas} un. baixadas">
              <b>${x.unidadesBaixadas} un.</b><i style="height:${Math.round((x.vendas / max) * 100)}%"></i>${ehHoje ? 'Hoje' : DIAS[dia.getDay()]}</div>`;
          }).join('')}
        </div>

        <h3 style="margin-top:24px">Últimas vendas <span class="sub">estoque baixado ou reservado automaticamente</span></h3>
        <div class="tabela"><table>
          <tr><th>Venda</th><th>Cliente</th><th>Itens</th><th class="num">Total</th><th>Estoque</th></tr>
          ${d.ultimasVendas.map((v) => `
            <tr><td class="mono">#${v.id}</td><td>${esc(v.cliente)}</td>
              <td>${v.itens.map((i) => `${esc(i.descricao)}${i.quantidade > 1 ? ` ×${i.quantidade}` : ''}`).join(', ')}</td>
              <td class="num">${moeda(v.total)}</td><td>${tag(STATUS_VENDA, v.status)}</td></tr>`).join('')}
        </table></div>
      </div>

      <div class="card">
        <h3>Alertas de estoque <span class="sub">${d.rupturas + d.abaixoMinimo} críticos</span></h3>
        ${d.alertas.length === 0 ? '<p class="muted">Nenhum alerta. Estoque saudável.</p>' : d.alertas.map((a) => `
          <a class="alert" href="#/estoque/${a.pecaId}">
            <span class="dot" style="background:${STATUS_ESTOQUE[a.status].cor}"></span>
            <div style="flex:1"><b>${esc(a.descricao)}</b> <span class="mono">${esc(a.codigo)}</span><div class="muted">${esc(a.detalhe)}</div></div>
            ${tagAlerta(a.status)}
          </a>`).join('')}
        <a class="btn" style="margin-top:12px;width:100%" href="#/reposicao">Ver sugestão de compra</a>
      </div>
    </div>`;
}

function tagAlerta(status) {
  const rotulos = { Ruptura: ['Ruptura', 't-danger'], AbaixoMinimo: ['Repor', 't-warn'], SemGiro: ['Excesso', 't-info'] };
  const [rotulo, classe] = rotulos[status] ?? [status, 't-mute'];
  return `<span class="tag ${classe}">${rotulo}</span>`;
}
