// Reposição: sugestão de compra para evitar ruptura, capital parado e recebimento de pedidos (NF-e).
import { api } from '../api.js';
import { dataHora, esc, formularioModal, moeda, numero, tag, toast, STATUS_COMPRA, STATUS_ESTOQUE } from '../ui.js';

export async function render(ctx) {
  const [reposicao, pedidos] = await Promise.all([api.get('/reposicao'), api.get('/compras')]);
  if (!ctx.ativa()) return;

  const { sugestoes, excessos, janelaDias } = reposicao;
  // Seleção e quantidade editáveis antes de gerar o pedido.
  const selecao = new Map(sugestoes.map((s) => [s.pecaId, s.sugerido]));

  ctx.cabecalho({
    titulo: 'Reposição',
    crumb: `calculado a partir das vendas dos últimos ${janelaDias} dias`,
    acoes: '<button class="btn primary" id="gerar"></button>',
  });

  const abertos = pedidos.filter((p) => p.status === 'Aberto');
  ctx.view.innerHTML = `
    ${sugestoes.length > 0
      ? `<div class="banner warn">⚠ ${sugestoes.length} ite${sugestoes.length === 1 ? 'm precisa' : 'ns precisam'} de reposição. A sugestão considera giro, prazo do fornecedor, estoque máximo e o que já está em pedido.</div>`
      : '<div class="banner">✓ Nenhuma peça abaixo do ponto de pedido.</div>'}

    <div class="row">
      <div class="card flush" style="flex:2.8">
        <h3>Sugestão de compra <span class="sub">ponto de pedido = mínimo + vendas previstas no prazo de entrega</span></h3>
        <div class="tabela"><table>
          <tr><th><input type="checkbox" id="todos" checked aria-label="Selecionar todos"></th><th>Peça</th><th>Curva</th><th>Nível</th>
            <th class="num">Disp.</th><th class="num">Mín.</th><th class="num">Média/mês</th><th class="num">Em pedido</th>
            <th class="num">Comprar</th><th>Fornecedor</th><th class="num">Custo</th></tr>
          ${sugestoes.length === 0 ? '<tr><td colspan="11" class="vazio">Nada a sugerir.</td></tr>' : sugestoes.map((s) => {
            const nivel = Math.max(2, Math.min(100, Math.round((Math.max(0, s.disponivel) / Math.max(1, s.estoqueMaximo)) * 100)));
            return `<tr>
              <td><input type="checkbox" data-sel="${s.pecaId}" checked aria-label="Selecionar ${esc(s.codigo)}"></td>
              <td><a href="#/estoque/${s.pecaId}"><b>${esc(s.descricao)}</b></a><div class="muted mono">${esc(s.codigo)}</div></td>
              <td><span class="tag t-blue">${s.curva}</span></td>
              <td><div class="progress" title="${nivel}% do máximo"><i style="width:${nivel}%;background:${STATUS_ESTOQUE[s.status].cor}"></i></div></td>
              <td class="num ${s.disponivel <= 0 ? 'c-danger' : ''}">${Math.max(0, s.disponivel)}</td>
              <td class="num">${s.estoqueMinimo}</td>
              <td class="num">${numero(s.mediaMensal)}</td>
              <td class="num muted">${s.emPedido || ''}</td>
              <td class="num"><input class="input" style="width:72px;min-height:32px;text-align:right" type="number" min="1" value="${s.sugerido}" data-qtd="${s.pecaId}" aria-label="Quantidade a comprar"></td>
              <td>${esc(s.fornecedor ?? '—')}</td>
              <td class="num" data-custo="${s.pecaId}">${moeda(s.custoTotal)}</td>
            </tr>`;
          }).join('')}
        </table></div>
        <div class="rodape" id="totais"></div>
      </div>

      <div class="card">
        <h3>Excesso / sem giro <span class="sub">capital parado</span></h3>
        ${excessos.length === 0 ? '<p class="muted">Nenhum item parado há mais de 180 dias.</p>' : excessos.map((e) => `
          <a class="alert" href="#/estoque/${e.pecaId}"><div style="flex:1"><b>${esc(e.descricao)}</b>
            <div class="muted">${e.saldo} un. · ${e.diasSemVenda} dias sem venda</div></div><b>${moeda(e.valorParado)}</b></a>`).join('')}
        <div class="card dica"><b>Sugestão</b>
          <p style="margin-top:6px;line-height:1.5">Bloquear novas compras desses itens e avaliar promoção para oficinas parceiras.</p></div>
      </div>
    </div>

    <div class="card flush">
      <h3>Pedidos de compra <span class="sub">${abertos.length} aguardando entrega · ao receber a NF-e o estoque é atualizado automaticamente</span></h3>
      <div class="tabela"><table>
        <tr><th>Pedido</th><th>Criado</th><th>Fornecedor</th><th>Itens</th><th class="num">Total</th><th>Situação</th><th></th></tr>
        ${pedidos.length === 0 ? '<tr><td colspan="7" class="vazio">Nenhum pedido de compra ainda.</td></tr>' : pedidos.map((p) => `
          <tr>
            <td class="mono">PC-${p.id}</td>
            <td class="muted">${dataHora(p.criadoEm)} · ${esc(p.solicitante)}</td>
            <td>${esc(p.fornecedor)}<div class="muted">prazo ${p.prazoEntregaDias} dias</div></td>
            <td>${p.itens.map((i) => `${esc(i.codigo)} ×${i.quantidade}`).join(', ')}</td>
            <td class="num">${moeda(p.total)}</td>
            <td>${tag(STATUS_COMPRA, p.status)}${p.notaFiscal ? `<div class="muted">NF-e ${esc(p.notaFiscal)}</div>` : ''}</td>
            <td style="white-space:nowrap">${p.status === 'Aberto' ? `
              <button class="btn sm ok" data-receber="${p.id}">Receber NF-e</button>
              <button class="btn sm danger" data-cancelar="${p.id}">Cancelar</button>` : ''}</td>
          </tr>`).join('')}
      </table></div>
    </div>`;

  const $ = (sel) => ctx.view.querySelector(sel);
  const custoUnit = new Map(sugestoes.map((s) => [s.pecaId, s.custoUnitario]));
  const botaoGerar = document.getElementById('gerar');

  const atualizarTotais = () => {
    let unidades = 0;
    let total = 0;
    for (const [pecaId, qtd] of selecao) {
      unidades += qtd;
      total += qtd * custoUnit.get(pecaId);
    }
    $('#totais').innerHTML = `<span class="muted">${selecao.size} itens · ${numero(unidades)} un.</span><span>Total estimado: ${moeda(total)}</span>`;
    botaoGerar.textContent = `Gerar pedido de compra (${selecao.size} ${selecao.size === 1 ? 'item' : 'itens'})`;
    botaoGerar.disabled = selecao.size === 0;
    $('#todos').checked = selecao.size === sugestoes.length && sugestoes.length > 0;
  };

  const quantidadeDigitada = (pecaId) => Math.max(1, Math.floor(Number($(`[data-qtd="${pecaId}"]`).value) || 1));

  ctx.view.addEventListener('change', (e) => {
    const { sel, qtd } = e.target.dataset;
    if (e.target.id === 'todos') {
      for (const s of sugestoes) {
        $(`[data-sel="${s.pecaId}"]`).checked = e.target.checked;
        if (e.target.checked) selecao.set(s.pecaId, quantidadeDigitada(s.pecaId));
        else selecao.delete(s.pecaId);
      }
    } else if (sel) {
      if (e.target.checked) selecao.set(Number(sel), quantidadeDigitada(sel));
      else selecao.delete(Number(sel));
    } else if (qtd) {
      const valor = quantidadeDigitada(qtd);
      e.target.value = valor;
      $(`[data-custo="${qtd}"]`).textContent = moeda(valor * custoUnit.get(Number(qtd)));
      if (selecao.has(Number(qtd))) selecao.set(Number(qtd), valor);
    } else return;
    atualizarTotais();
  });

  botaoGerar.addEventListener('click', async () => {
    botaoGerar.disabled = true;
    try {
      const criados = await api.post('/compras', {
        usuario: ctx.usuario(),
        itens: [...selecao].map(([pecaId, quantidade]) => ({ pecaId, quantidade })),
      });
      toast(`${criados.length} pedido(s) de compra gerado(s), um por fornecedor`, 'ok');
      ctx.recarregar();
    } catch (erro) {
      toast(erro.message, 'erro');
      botaoGerar.disabled = false;
    }
  });

  ctx.view.addEventListener('click', async (e) => {
    const botao = e.target.closest('[data-receber], [data-cancelar]');
    if (!botao) return;
    const { receber, cancelar } = botao.dataset;
    try {
      if (receber) {
        const pedido = pedidos.find((p) => p.id === Number(receber));
        const recebido = await formularioModal({
          titulo: `Receber pedido PC-${pedido.id} · ${pedido.fornecedor}`,
          confirmar: 'Dar entrada no estoque',
          corpo: `
            <p class="muted">${pedido.itens.map((i) => `${esc(i.codigo)} ×${i.quantidade}`).join(', ')} — o saldo e o custo médio serão atualizados.</p>
            <div class="field"><label for="nf">Número da NF-e *</label><input class="input mono" id="nf" name="notaFiscal" required placeholder="45.221"></div>`,
          aoConfirmar: (dados) => api.post(`/compras/${pedido.id}/receber`, { notaFiscal: dados.notaFiscal, usuario: ctx.usuario() }),
        });
        if (!recebido) return;
        toast(`NF-e ${recebido.notaFiscal} recebida · estoque atualizado`, 'ok');
      } else {
        botao.disabled = true;
        await api.post(`/compras/${cancelar}/cancelar`);
        toast(`Pedido PC-${cancelar} cancelado`);
      }
      ctx.recarregar();
    } catch (erro) {
      toast(erro.message, 'erro');
      botao.disabled = false;
    }
  });

  atualizarTotais();
}
