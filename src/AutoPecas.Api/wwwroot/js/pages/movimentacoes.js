// Histórico geral de movimentações de estoque, para auditoria de divergências.
import { api } from '../api.js';
import { linhaMovimentacao, TIPO_MOV } from '../ui.js';

export async function render(ctx) {
  const tipo = ctx.query.get('tipo') ?? '';
  ctx.cabecalho({
    titulo: 'Movimentações',
    crumb: 'toda alteração de saldo ou reserva gera um lançamento',
  });

  const movimentacoes = await api.get('/movimentacoes', { tipo, limite: 200 });
  if (!ctx.ativa()) return;

  ctx.view.innerHTML = `
    <div class="chips">
      <a class="chip ${tipo === '' ? 'on' : ''}" href="#/movimentacoes">Todas</a>
      ${Object.entries(TIPO_MOV).map(([valor, { rotulo }]) =>
        `<a class="chip ${valor === tipo ? 'on' : ''}" href="#/movimentacoes?tipo=${valor}">${rotulo}</a>`).join('')}
    </div>
    <div class="card">
      <h3>Últimos lançamentos <span class="sub">${movimentacoes.length} registros</span></h3>
      <div class="timeline">
        ${movimentacoes.length === 0 ? '<p class="muted">Nenhuma movimentação.</p>'
          : movimentacoes.map((m) => linhaMovimentacao(m, { mostrarPeca: true })).join('')}
      </div>
    </div>`;
}
