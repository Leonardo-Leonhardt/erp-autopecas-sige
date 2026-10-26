// Formatação, componentes visuais e utilidades compartilhadas pelas telas.

const moedaFmt = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' });
const numeroFmt = new Intl.NumberFormat('pt-BR');

export const moeda = (v) => moedaFmt.format(v ?? 0);
export const numero = (v) => numeroFmt.format(v ?? 0);
export const percentual = (v) => `${numeroFmt.format(v ?? 0)}%`;

/** Escapa texto vindo da API antes de interpolar em HTML. */
export function esc(valor) {
  return String(valor ?? '').replace(/[&<>"']/g, (c) => ({
    '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;',
  })[c]);
}

export function dataHora(iso) {
  const d = new Date(iso);
  return `${d.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit' })} · ${d.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })}`;
}

export function diasAtras(iso) {
  if (!iso) return 'nunca';
  const inicioHoje = new Date(); inicioHoje.setHours(0, 0, 0, 0);
  const dia = new Date(iso); dia.setHours(0, 0, 0, 0);
  const dias = Math.round((inicioHoje - dia) / 86_400_000);
  if (dias <= 0) return 'hoje';
  if (dias === 1) return 'ontem';
  return `há ${dias} dias`;
}

export const STATUS_ESTOQUE = {
  Normal: { rotulo: 'Normal', classe: 't-ok', cor: 'var(--ok)' },
  AbaixoMinimo: { rotulo: 'Abaixo do mín.', classe: 't-warn', cor: 'var(--warn)' },
  Ruptura: { rotulo: 'Ruptura', classe: 't-danger', cor: 'var(--danger)' },
  SemGiro: { rotulo: 'Sem giro', classe: 't-info', cor: 'var(--info)' },
};

export const tagEstoque = (status) => {
  const s = STATUS_ESTOQUE[status] ?? STATUS_ESTOQUE.Normal;
  return `<span class="tag ${s.classe}">${s.rotulo}</span>`;
};

export const TIPO_MOV = {
  Entrada: { rotulo: 'Entrada', classe: 't-ok' },
  Saida: { rotulo: 'Saída', classe: 't-danger' },
  Ajuste: { rotulo: 'Ajuste', classe: 't-info' },
  Reserva: { rotulo: 'Reserva', classe: 't-warn' },
  LiberacaoReserva: { rotulo: 'Liberação', classe: 't-mute' },
};

export const STATUS_VENDA = {
  Orcamento: { rotulo: 'Orçamento · reservado', classe: 't-warn' },
  Finalizada: { rotulo: 'Finalizada · baixado', classe: 't-ok' },
  Cancelada: { rotulo: 'Cancelada', classe: 't-mute' },
};

export const STATUS_COMPRA = {
  Aberto: { rotulo: 'Aguardando entrega', classe: 't-warn' },
  Recebido: { rotulo: 'Recebido', classe: 't-ok' },
  Cancelado: { rotulo: 'Cancelado', classe: 't-mute' },
};

export const tag = (mapa, chave) => {
  const s = mapa[chave] ?? { rotulo: chave, classe: 't-mute' };
  return `<span class="tag ${s.classe}">${esc(s.rotulo)}</span>`;
};

/** Linha de movimentação usada no detalhe da peça e no histórico geral. */
export function linhaMovimentacao(m, { mostrarPeca = false } = {}) {
  const reserva = m.tipo === 'Reserva' || m.tipo === 'LiberacaoReserva';
  const qtd = reserva
    ? `<span class="q muted">${m.tipo === 'Reserva' ? '🔒' : '🔓'} ${m.quantidade}</span>`
    : `<span class="q ${m.quantidade < 0 ? 'down' : 'up'}">${m.quantidade > 0 ? '+' : '−'}${Math.abs(m.quantidade)}</span>`;
  const peca = mostrarPeca
    ? `<a href="#/estoque/${m.pecaId}"><b>${esc(m.pecaDescricao)}</b> <span class="mono">${esc(m.pecaCodigo)}</span></a><br>`
    : '';
  return `
    <div class="tl">
      ${tag(TIPO_MOV, m.tipo)}
      <div style="flex:1;min-width:180px">
        ${peca}<b>${esc(m.documento ?? '')}</b>${m.descricao ? ` · ${esc(m.descricao)}` : ''}
        <div class="muted">${dataHora(m.data)} · ${esc(m.usuario)}</div>
      </div>
      ${qtd}
      <span class="mono s">saldo ${m.saldoApos}</span>
    </div>`;
}

// ---------- toasts ----------

export function toast(mensagem, tipo = '') {
  const el = document.createElement('div');
  el.className = `toast ${tipo}`;
  el.textContent = mensagem;
  document.getElementById('toasts').append(el);
  setTimeout(() => el.remove(), tipo === 'erro' ? 6000 : 3500);
}

// ---------- modal ----------

/**
 * Abre um formulário em modal. `aoConfirmar` recebe os campos (FormData → objeto) e pode lançar erro,
 * que é mostrado sem fechar o modal. Resolve com o retorno de `aoConfirmar`, ou null se cancelado.
 */
export function formularioModal({ titulo, corpo, confirmar = 'Salvar', aoConfirmar }) {
  const dialog = document.getElementById('modal');
  dialog.innerHTML = `
    <form method="dialog" novalidate>
      <h2>${esc(titulo)}</h2>
      ${corpo}
      <div class="botoes">
        <button type="button" class="btn" data-cancelar>Cancelar</button>
        <button type="submit" class="btn primary">${esc(confirmar)}</button>
      </div>
    </form>`;
  const form = dialog.querySelector('form');

  return new Promise((resolve) => {
    const fechar = (valor) => { dialog.close(); resolve(valor); };
    dialog.querySelector('[data-cancelar]').onclick = () => fechar(null);
    dialog.oncancel = () => resolve(null);
    form.onsubmit = async (e) => {
      e.preventDefault();
      if (!form.reportValidity()) return;
      const botao = form.querySelector('[type=submit]');
      botao.disabled = true;
      try {
        fechar(await aoConfirmar(Object.fromEntries(new FormData(form))));
      } catch (erro) {
        toast(erro.message, 'erro');
      } finally {
        botao.disabled = false;
      }
    };
    dialog.showModal();
    form.querySelector('input, select, textarea')?.focus();
  });
}
