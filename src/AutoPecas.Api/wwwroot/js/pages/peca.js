// Detalhe da peça: indicadores, cadastro e histórico de movimentações (entradas, saídas, ajustes, reservas).
import { api } from '../api.js';
import { esc, formularioModal, linhaMovimentacao, moeda, numero, percentual, tagEstoque, toast } from '../ui.js';
import { formularioPeca } from './estoque.js';

export async function render(ctx) {
  const [id] = ctx.params;
  const [detalhe, movimentacoes] = await Promise.all([
    api.get(`/pecas/${id}`),
    api.get('/movimentacoes', { pecaId: id, limite: 50 }),
  ]);
  if (!ctx.ativa()) return;

  const { peca: p } = detalhe;
  ctx.cabecalho({
    titulo: `${esc(p.descricao)} · ${esc(p.marca)} ${tagEstoque(p.status)}`,
    crumb: '<a href="#/estoque">Estoque</a> /',
    acoes: '<button class="btn" id="ajuste">Ajuste de inventário</button><button class="btn" id="editar">Editar</button>',
  });

  const cobertura = detalhe.coberturaDias;
  ctx.view.innerHTML = `
    <div class="grid4">
      <div class="card kpi"><div class="l">Disponível para venda</div><div class="v">${Math.max(0, p.disponivel)} un.</div>
        <div class="d muted">Saldo físico ${p.saldo}${p.reservado ? ` · ${p.reservado} reservada(s)` : ''} · Mín. ${p.estoqueMinimo} · Máx. ${p.estoqueMaximo}</div></div>
      <div class="card kpi"><div class="l">Vendidas (30 dias)</div><div class="v">${numero(detalhe.vendidasUltimos30Dias)} un.</div>
        <div class="d ${detalhe.curva === 'A' ? 'up' : 'muted'}">Curva ${detalhe.curva}${detalhe.curva === 'A' ? ' · giro alto' : ''}</div></div>
      <div class="card kpi"><div class="l">Cobertura estimada</div>
        <div class="v ${cobertura !== null && cobertura < (p.prazoEntregaDias ?? 0) + 3 ? 'c-warn' : ''}">${cobertura === null ? '—' : `${numero(cobertura)} dias`}</div>
        <div class="d muted">${cobertura === null ? 'sem vendas nos últimos 30 dias' : 'pela média de vendas'}</div></div>
      <div class="card kpi"><div class="l">Margem</div><div class="v">${percentual(detalhe.margemPercentual)}</div>
        <div class="d muted">custo méd. ${moeda(p.custoMedio)} · preço ${moeda(p.precoVenda)}</div></div>
    </div>

    <div class="row">
      <div class="card">
        <h3>Dados da peça</h3>
        <div class="grid2">
          ${campo('Código interno', p.codigo, true)}
          ${campo('Código do fabricante', p.codigoFabricante ?? '—', true)}
          ${campo('Categoria', p.categoria)}
          ${campo('Localização', p.localizacao, true)}
          <div class="field span2"><span>Aplicação (veículos compatíveis)</span><div class="input">${esc(p.aplicacao)}</div></div>
          ${campo('Fornecedor principal', p.fornecedor ?? '—')}
          ${campo('Prazo de entrega', p.prazoEntregaDias ? `${p.prazoEntregaDias} dias` : '—')}
        </div>
      </div>
      <div class="card" style="flex:1.3">
        <h3>Movimentações <span class="sub">saldo recalculado a cada lançamento</span></h3>
        <div class="timeline">
          ${movimentacoes.length === 0 ? '<p class="muted">Sem movimentações.</p>' : movimentacoes.map((m) => linhaMovimentacao(m)).join('')}
        </div>
      </div>
    </div>`;

  document.getElementById('editar').addEventListener('click', async () => {
    if (await formularioPeca(ctx, p)) ctx.recarregar();
  });

  document.getElementById('ajuste').addEventListener('click', async () => {
    const ok = await formularioModal({
      titulo: `Ajuste de inventário · ${p.codigo}`,
      confirmar: 'Registrar ajuste',
      corpo: `
        <p class="muted">Saldo no sistema: <b>${p.saldo}</b>${p.reservado ? ` (${p.reservado} reservada(s))` : ''}. Informe a quantidade contada na prateleira; a diferença fica registrada no histórico.</p>
        <div class="field"><label for="a-saldo">Saldo contado *</label><input class="input" id="a-saldo" name="saldoContado" type="number" min="0" required value="${p.saldo}"></div>
        <div class="field"><label for="a-motivo">Motivo</label><input class="input" id="a-motivo" name="motivo" placeholder="Divergência física, avaria, extravio…"></div>`,
      aoConfirmar: (dados) => api.post(`/pecas/${p.id}/ajustes`, {
        saldoContado: Number(dados.saldoContado), motivo: dados.motivo, usuario: ctx.usuario(),
      }),
    });
    if (ok) {
      toast('Ajuste registrado', 'ok');
      ctx.recarregar();
    }
  });
}

const campo = (rotulo, valor, mono = false) =>
  `<div class="field"><span>${rotulo}</span><div class="input ${mono ? 'mono' : ''}">${esc(valor)}</div></div>`;
