// Nova venda (PDV): mostra o impacto no estoque antes de finalizar.
// "Salvar orçamento" reserva as peças; "Finalizar" baixa o saldo na hora.
import { api } from '../api.js';
import { esc, moeda, toast, tagEstoque } from '../ui.js';

const PAGAMENTOS = ['Dinheiro', 'PIX', 'Cartão', 'A prazo'];

// O carrinho sobrevive à navegação entre telas, como um rascunho.
const estado = {
  itens: new Map(), // pecaId → { peca, quantidade }
  clienteId: null,
  pagamento: 'PIX',
  busca: '',
};

export async function render(ctx) {
  ctx.cabecalho({
    titulo: 'Nova venda',
    crumb: 'rascunho · o estoque só muda ao salvar ou finalizar',
    acoes: '<button class="btn" id="limpar">Limpar</button>',
  });

  const clientes = await api.get('/clientes');
  if (estado.clienteId === null) estado.clienteId = clientes[0]?.id ?? null;
  if (!ctx.ativa()) return;

  ctx.view.innerHTML = `
    <div class="row">
      <div class="col" style="flex:1.7">
        <div class="card">
          <h3>Buscar peça</h3>
          <div class="field">
            <label for="busca">Código, descrição, marca ou veículo</label>
            <input class="input" id="busca" type="search" autocomplete="off" placeholder="ex.: pastilha onix" value="${esc(estado.busca)}">
          </div>
          <div class="tabela" id="resultados" style="margin-top:14px"></div>
        </div>
        <div class="card">
          <h3>Itens da venda <span class="sub" id="qtd-itens"></span></h3>
          <div class="tabela" id="carrinho"></div>
        </div>
      </div>

      <div class="col" style="flex:1">
        <div class="card">
          <h3>Cliente</h3>
          <div class="field">
            <label for="cliente">Cliente</label>
            <select class="input" id="cliente">
              ${clientes.map((c) => `<option value="${c.id}" ${c.id === estado.clienteId ? 'selected' : ''}>${esc(c.nome)}${c.documento ? ` · ${esc(c.documento)}` : ''}</option>`).join('')}
            </select>
          </div>
          <div class="field" style="margin-top:12px"><span>Forma de pagamento</span>
            <div class="chips" id="pagamentos">
              ${PAGAMENTOS.map((p) => `<button class="chip ${p === estado.pagamento ? 'on' : ''}" data-pagamento="${p}">${p}</button>`).join('')}
            </div>
          </div>
        </div>
        <div class="card" id="resumo"></div>
        <div class="card aviso" id="impacto"></div>
        <button class="btn ok lg" id="finalizar">Finalizar venda e baixar estoque</button>
        <button class="btn" id="orcamento">Salvar orçamento (reserva as peças)</button>
      </div>
    </div>`;

  const $ = (sel) => ctx.view.querySelector(sel);
  const clienteAtual = () => clientes.find((c) => c.id === estado.clienteId);

  // ---------- busca ----------
  let timer;
  let buscaAtual = 0;
  const buscar = async () => {
    const id = ++buscaAtual;
    const termo = estado.busca.trim();
    if (!termo) {
      $('#resultados').innerHTML = '<p class="muted">Digite para buscar no catálogo.</p>';
      return;
    }
    const { itens } = await api.get('/pecas', { busca: termo });
    if (id !== buscaAtual || !ctx.ativa()) return;
    $('#resultados').innerHTML = itens.length === 0
      ? '<p class="muted">Nenhuma peça encontrada.</p>'
      : `<table>
          <tr><th>Código</th><th>Descrição</th><th>Marca</th><th class="num">Disponível</th><th class="num">Preço</th><th></th></tr>
          ${itens.slice(0, 8).map((p) => {
            const noCarrinho = estado.itens.get(p.id)?.quantidade ?? 0;
            const livre = p.disponivel - noCarrinho;
            return `<tr class="${p.disponivel <= 0 ? 'apagado' : ''}">
              <td class="mono">${esc(p.codigo)}</td>
              <td>${esc(p.descricao)}<div class="muted">${esc(p.aplicacao)}</div></td>
              <td>${esc(p.marca)}</td>
              <td class="num">${Math.max(0, p.disponivel)}${p.reservado > 0 ? `<div class="muted" title="reservado em orçamentos">+${p.reservado} res.</div>` : ''}</td>
              <td class="num">${moeda(p.precoVenda)}</td>
              <td>${p.disponivel <= 0
                ? '<span class="tag t-danger">Sem estoque</span>'
                : `<button class="btn sm" data-adicionar="${p.id}" ${livre <= 0 ? 'disabled title="Todo o disponível já está na venda"' : ''}>Adicionar</button>`}</td>
            </tr>`;
          }).join('')}
        </table>`;
    resultadosPorId = new Map(itens.map((p) => [p.id, p]));
  };
  let resultadosPorId = new Map();

  $('#busca').addEventListener('input', (e) => {
    estado.busca = e.target.value;
    clearTimeout(timer);
    timer = setTimeout(() => buscar().catch((err) => toast(err.message, 'erro')), 250);
  });

  $('#resultados').addEventListener('click', (e) => {
    const botao = e.target.closest('[data-adicionar]');
    if (!botao) return;
    const peca = resultadosPorId.get(Number(botao.dataset.adicionar));
    const atual = estado.itens.get(peca.id);
    if (atual) atual.quantidade = Math.min(atual.quantidade + 1, peca.disponivel);
    else estado.itens.set(peca.id, { peca, quantidade: 1 });
    atualizar();
    buscar();
  });

  // ---------- carrinho ----------
  const desenharCarrinho = () => {
    const itens = [...estado.itens.values()];
    $('#qtd-itens').textContent = `${itens.length} ${itens.length === 1 ? 'item' : 'itens'}`;
    if (itens.length === 0) {
      $('#carrinho').innerHTML = '<p class="muted">Nenhuma peça adicionada.</p>';
      return;
    }
    $('#carrinho').innerHTML = `<table>
      <tr><th>Peça</th><th>Qtd.</th><th class="num">Unit.</th><th class="num">Subtotal</th><th>Estoque após venda</th><th></th></tr>
      ${itens.map(({ peca: p, quantidade }) => {
        const depois = p.disponivel - quantidade;
        const situacao = depois <= 0 ? 'Ruptura' : depois < p.estoqueMinimo ? 'AbaixoMinimo' : 'Normal';
        return `<tr>
          <td><b>${esc(p.descricao)}</b> <span class="muted">${esc(p.marca)}</span><div class="muted mono">${esc(p.codigo)}</div></td>
          <td><span class="qty">
            <button data-menos="${p.id}" aria-label="Diminuir">−</button>
            <input type="number" min="1" max="${p.disponivel}" value="${quantidade}" data-qtd="${p.id}" aria-label="Quantidade">
            <button data-mais="${p.id}" aria-label="Aumentar" ${quantidade >= p.disponivel ? 'disabled' : ''}>+</button>
          </span></td>
          <td class="num">${moeda(p.precoVenda)}</td>
          <td class="num">${moeda(p.precoVenda * quantidade)}</td>
          <td><span class="impact">${p.disponivel} <span class="arrow">→</span> <b>${depois}</b>
            ${situacao === 'AbaixoMinimo' ? `<span class="tag t-warn">abaixo do mín. (${p.estoqueMinimo})</span>`
              : situacao === 'Ruptura' ? '<span class="tag t-danger">zera o estoque</span>' : tagEstoque('Normal')}</span></td>
          <td><button class="btn sm icon danger" data-remover="${p.id}" aria-label="Remover">✕</button></td>
        </tr>`;
      }).join('')}
    </table>`;
  };

  const alterarQuantidade = (pecaId, quantidade) => {
    const item = estado.itens.get(pecaId);
    if (!item) return;
    if (quantidade <= 0) estado.itens.delete(pecaId);
    else item.quantidade = Math.min(quantidade, item.peca.disponivel);
    atualizar();
  };

  $('#carrinho').addEventListener('click', (e) => {
    const alvo = e.target.closest('button');
    if (!alvo) return;
    const { menos, mais, remover } = alvo.dataset;
    if (menos) alterarQuantidade(Number(menos), estado.itens.get(Number(menos)).quantidade - 1);
    if (mais) alterarQuantidade(Number(mais), estado.itens.get(Number(mais)).quantidade + 1);
    if (remover) alterarQuantidade(Number(remover), 0);
  });
  $('#carrinho').addEventListener('change', (e) => {
    if (e.target.dataset.qtd) alterarQuantidade(Number(e.target.dataset.qtd), Math.floor(Number(e.target.value) || 0));
  });

  // ---------- cliente, pagamento, resumo ----------
  $('#cliente').addEventListener('change', (e) => { estado.clienteId = Number(e.target.value); atualizar(); });
  $('#pagamentos').addEventListener('click', (e) => {
    const chip = e.target.closest('[data-pagamento]');
    if (!chip) return;
    estado.pagamento = chip.dataset.pagamento;
    ctx.view.querySelectorAll('[data-pagamento]').forEach((c) => c.classList.toggle('on', c === chip));
  });

  const desenharResumo = () => {
    const itens = [...estado.itens.values()];
    const subtotal = itens.reduce((s, i) => s + i.peca.precoVenda * i.quantidade, 0);
    const cliente = clienteAtual();
    const pct = cliente?.descontoPercentual ?? 0;
    const desconto = Math.round(subtotal * pct) / 100;
    $('#resumo').innerHTML = `
      <h3>Resumo</h3>
      <div class="total"><span class="muted">Subtotal</span><span>${moeda(subtotal)}</span></div>
      ${pct > 0 ? `<div class="total"><span class="muted">Desconto (${esc(cliente.nome)} ${pct}%)</span><span>− ${moeda(desconto)}</span></div>` : ''}
      <div class="total big"><span>Total</span><span>${moeda(subtotal - desconto)}</span></div>`;

    const unidades = itens.reduce((s, i) => s + i.quantidade, 0);
    const criticos = itens.filter((i) => i.peca.disponivel - i.quantidade < i.peca.estoqueMinimo);
    $('#impacto').innerHTML = itens.length === 0
      ? '<h3>⚠ Impacto no estoque</h3><p>Adicione peças para ver o efeito no estoque.</p>'
      : `<h3>⚠ Impacto no estoque</h3>
         <p style="line-height:1.5">Ao finalizar, <b>${unidades} unidade(s)</b> serão baixadas automaticamente.
         ${criticos.length > 0
           ? `${criticos.map((i) => `<b>${esc(i.peca.codigo)}</b>`).join(', ')} ficará(ão) abaixo do estoque mínimo e entrará(ão) na sugestão de compra.`
           : 'Nenhuma peça ficará abaixo do mínimo.'}</p>`;

    $('#finalizar').disabled = itens.length === 0;
    $('#orcamento').disabled = itens.length === 0;
  };

  const atualizar = () => { desenharCarrinho(); desenharResumo(); };

  // ---------- envio ----------
  const enviar = async (finalizar) => {
    const botoes = [$('#finalizar'), $('#orcamento')];
    botoes.forEach((b) => { b.disabled = true; });
    try {
      const venda = await api.post('/vendas', {
        clienteId: estado.clienteId,
        formaPagamento: estado.pagamento,
        usuario: ctx.usuario(),
        finalizar,
        itens: [...estado.itens.values()].map((i) => ({ pecaId: i.peca.id, quantidade: i.quantidade })),
      });
      const unidades = venda.itens.reduce((s, i) => s + i.quantidade, 0);
      toast(finalizar
        ? `Venda #${venda.id} finalizada · ${unidades} un. baixadas do estoque`
        : `Orçamento #${venda.id} salvo · ${unidades} un. reservadas`, 'ok');
      estado.itens.clear();
      estado.busca = '';
      if (!finalizar) location.hash = '#/vendas';
      else ctx.recarregar();
    } catch (erro) {
      // Outro vendedor pode ter vendido a peça: atualiza os saldos mostrados.
      toast(erro.message, 'erro');
      await sincronizarCarrinho();
      botoes.forEach((b) => { b.disabled = estado.itens.size === 0; });
    }
  };

  const sincronizarCarrinho = async () => {
    for (const item of estado.itens.values()) {
      const { peca } = await api.get(`/pecas/${item.peca.id}`);
      item.peca = peca;
      item.quantidade = Math.min(item.quantidade, Math.max(0, peca.disponivel));
      if (item.quantidade === 0) estado.itens.delete(peca.id);
    }
    if (ctx.ativa()) { atualizar(); buscar(); }
  };

  $('#finalizar').addEventListener('click', () => enviar(true));
  $('#orcamento').addEventListener('click', () => enviar(false));
  document.getElementById('limpar').addEventListener('click', () => {
    estado.itens.clear();
    atualizar();
    buscar();
  });

  // Ao voltar para a tela, saldos do rascunho podem estar desatualizados.
  if (estado.itens.size > 0) await sincronizarCarrinho();
  atualizar();
  await buscar();
  $('#busca').focus();

  return () => clearTimeout(timer);
}
