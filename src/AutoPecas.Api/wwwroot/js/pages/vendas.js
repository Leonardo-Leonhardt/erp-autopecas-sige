// Vendas e orçamentos: converte orçamentos (reservas) em venda ou libera a reserva.
import { api } from '../api.js';
import { dataHora, esc, moeda, tag, toast, STATUS_VENDA } from '../ui.js';

const FILTROS = [
  ['', 'Todas'],
  ['Orcamento', 'Orçamentos em aberto'],
  ['Finalizada', 'Finalizadas'],
  ['Cancelada', 'Canceladas'],
];

export async function render(ctx) {
  const filtro = ctx.query.get('status') ?? '';
  ctx.cabecalho({
    titulo: 'Vendas e orçamentos',
    crumb: 'orçamentos reservam as peças até serem finalizados ou cancelados',
    acoes: '<a class="btn primary" href="#/venda">+ Nova venda</a>',
  });

  const vendas = await api.get('/vendas', { status: filtro, limite: 100 });
  if (!ctx.ativa()) return;

  ctx.view.innerHTML = `
    <div class="chips">
      ${FILTROS.map(([valor, rotulo]) => `<a class="chip ${valor === filtro ? 'on' : ''}" href="#/vendas${valor ? `?status=${valor}` : ''}">${rotulo}</a>`).join('')}
    </div>
    <div class="card flush"><div class="tabela"><table>
      <tr><th>Venda</th><th>Data</th><th>Cliente</th><th>Itens</th><th>Vendedor</th><th>Pagamento</th><th class="num">Total</th><th>Situação</th><th></th></tr>
      ${vendas.length === 0 ? '<tr><td colspan="9" class="vazio">Nenhuma venda encontrada.</td></tr>' : vendas.map((v) => `
        <tr>
          <td class="mono">#${v.id}</td>
          <td class="muted" style="white-space:nowrap">${dataHora(v.finalizadaEm ?? v.criadaEm)}</td>
          <td>${esc(v.cliente)}</td>
          <td>${v.itens.map((i) => `<a href="#/estoque/${i.pecaId}">${esc(i.descricao)}</a>${i.quantidade > 1 ? ` ×${i.quantidade}` : ''}`).join(', ')}</td>
          <td>${esc(v.vendedor)}</td>
          <td>${esc(v.formaPagamento)}</td>
          <td class="num">${moeda(v.total)}</td>
          <td>${tag(STATUS_VENDA, v.status)}</td>
          <td style="white-space:nowrap">${v.status === 'Orcamento' ? `
            <button class="btn sm ok" data-finalizar="${v.id}">Finalizar</button>
            <button class="btn sm danger" data-cancelar="${v.id}">Cancelar</button>` : ''}</td>
        </tr>`).join('')}
    </table></div></div>`;

  ctx.view.addEventListener('click', async (e) => {
    const botao = e.target.closest('[data-finalizar], [data-cancelar]');
    if (!botao) return;
    const { finalizar, cancelar } = botao.dataset;
    botao.disabled = true;
    try {
      if (finalizar) {
        await api.post(`/vendas/${finalizar}/finalizar`, { usuario: ctx.usuario() });
        toast(`Venda #${finalizar} finalizada · estoque baixado`, 'ok');
      } else {
        await api.post(`/vendas/${cancelar}/cancelar`, { usuario: ctx.usuario() });
        toast(`Orçamento #${cancelar} cancelado · reserva liberada`);
      }
      ctx.recarregar();
    } catch (erro) {
      toast(erro.message, 'erro');
      botao.disabled = false;
    }
  });
}
