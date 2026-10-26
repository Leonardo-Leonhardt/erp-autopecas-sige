// Estoque: catálogo de peças com saldo e disponibilidade em tempo real.
import { api } from '../api.js';
import { diasAtras, esc, formularioModal, moeda, numero, tagEstoque, toast } from '../ui.js';

export async function render(ctx) {
  const filtro = {
    busca: ctx.query.get('busca') ?? '',
    status: ctx.query.get('status') ?? '',
    categoria: ctx.query.get('categoria') ?? '',
  };

  const categorias = await api.get('/pecas/categorias');
  if (!ctx.ativa()) return;

  ctx.cabecalho({
    titulo: 'Estoque',
    crumb: '<span id="total-pecas"></span>',
    acoes: '<a class="btn" href="#/reposicao">Receber compra (NF-e)</a><button class="btn primary" id="nova-peca">+ Nova peça</button>',
  });

  ctx.view.innerHTML = `
    <div class="filtros">
      <input class="input search" id="busca" type="search" placeholder="Buscar por código, descrição, marca ou veículo…" value="${esc(filtro.busca)}">
      <div class="chips" id="status"></div>
      <div class="spacer"></div>
      <select class="input" id="categoria" style="width:auto">
        <option value="">Todas as categorias</option>
        ${categorias.map((c) => `<option ${c === filtro.categoria ? 'selected' : ''}>${esc(c)}</option>`).join('')}
      </select>
    </div>
    <div class="card flush"><div class="tabela" id="tabela"></div></div>`;

  const $ = (sel) => ctx.view.querySelector(sel);
  let consulta = 0;

  const carregar = async () => {
    const id = ++consulta;
    const { itens, resumo } = await api.get('/pecas', filtro);
    if (id !== consulta || !ctx.ativa()) return;

    document.getElementById('total-pecas').textContent = `${numero(resumo.total)} peças no filtro`;
    const chips = [
      ['', `Todos (${resumo.total})`],
      ['Ruptura', `Ruptura (${resumo.ruptura})`],
      ['AbaixoMinimo', `Abaixo do mínimo (${resumo.abaixoMinimo})`],
      ['SemGiro', `Sem giro (${resumo.semGiro})`],
    ];
    $('#status').innerHTML = chips.map(([valor, rotulo]) =>
      `<button class="chip ${valor === filtro.status ? 'on' : ''}" data-status="${valor}">${rotulo}</button>`).join('');

    $('#tabela').innerHTML = `<table>
      <tr><th>Código</th><th>Descrição</th><th>Aplicação</th><th>Marca</th><th>Local</th>
        <th class="num">Saldo</th><th class="num">Reserv.</th><th class="num">Disp.</th><th class="num">Mín.</th>
        <th class="num">Custo méd.</th><th class="num">Preço</th><th>Última venda</th><th>Status</th></tr>
      ${itens.length === 0 ? '<tr><td colspan="13" class="vazio">Nenhuma peça encontrada.</td></tr>' : itens.map((p) => `
        <tr class="clicavel" data-id="${p.id}">
          <td class="mono">${esc(p.codigo)}</td>
          <td>${esc(p.descricao)}</td>
          <td>${esc(p.aplicacao)}</td>
          <td>${esc(p.marca)}</td>
          <td class="mono">${esc(p.localizacao)}</td>
          <td class="num">${p.saldo}</td>
          <td class="num muted">${p.reservado || ''}</td>
          <td class="num ${p.disponivel <= 0 ? 'c-danger' : ''}" style="font-weight:700">${Math.max(0, p.disponivel)}</td>
          <td class="num">${p.estoqueMinimo}</td>
          <td class="num">${moeda(p.custoMedio)}</td>
          <td class="num">${moeda(p.precoVenda)}</td>
          <td>${diasAtras(p.ultimaVenda)}</td>
          <td>${tagEstoque(p.status)}</td>
        </tr>`).join('')}
    </table>`;

    // Mantém os filtros na URL sem disparar nova navegação, para o link poder ser compartilhado.
    const q = new URLSearchParams(Object.entries(filtro).filter(([, v]) => v)).toString();
    history.replaceState(null, '', `#/estoque${q ? `?${q}` : ''}`);
  };

  let timer;
  $('#busca').addEventListener('input', (e) => {
    filtro.busca = e.target.value;
    clearTimeout(timer);
    timer = setTimeout(() => carregar().catch((err) => toast(err.message, 'erro')), 250);
  });
  $('#categoria').addEventListener('change', (e) => { filtro.categoria = e.target.value; carregar(); });
  $('#status').addEventListener('click', (e) => {
    const chip = e.target.closest('[data-status]');
    if (!chip) return;
    filtro.status = chip.dataset.status;
    carregar();
  });
  $('#tabela').addEventListener('click', (e) => {
    const linha = e.target.closest('[data-id]');
    if (linha) location.hash = `#/estoque/${linha.dataset.id}`;
  });
  document.getElementById('nova-peca').addEventListener('click', async () => {
    const peca = await formularioPeca(ctx);
    if (peca) location.hash = `#/estoque/${peca.id}`;
  });

  await carregar();
  return () => clearTimeout(timer);
}

/** Cadastro (sem `peca`) ou edição de peça. Saldo e custo só são informados no cadastro. */
export async function formularioPeca(ctx, peca = null) {
  const fornecedores = await api.get('/fornecedores');
  const v = (campo, padrao = '') => esc(peca?.[campo] ?? padrao);

  return formularioModal({
    titulo: peca ? `Editar ${peca.codigo}` : 'Nova peça',
    confirmar: peca ? 'Salvar alterações' : 'Cadastrar peça',
    corpo: `
      <div class="grid2">
        <div class="field"><label for="f-codigo">Código interno *</label><input class="input mono" id="f-codigo" name="codigo" required maxlength="30" value="${v('codigo')}"></div>
        <div class="field"><label for="f-fab">Código do fabricante</label><input class="input mono" id="f-fab" name="codigoFabricante" value="${v('codigoFabricante')}"></div>
        <div class="field span2"><label for="f-desc">Descrição *</label><input class="input" id="f-desc" name="descricao" required value="${v('descricao')}"></div>
        <div class="field"><label for="f-cat">Categoria</label><input class="input" id="f-cat" name="categoria" placeholder="Freios › Pastilhas" value="${v('categoria')}"></div>
        <div class="field"><label for="f-marca">Marca</label><input class="input" id="f-marca" name="marca" value="${v('marca')}"></div>
        <div class="field span2"><label for="f-apl">Aplicação (veículos compatíveis)</label><input class="input" id="f-apl" name="aplicacao" value="${v('aplicacao')}"></div>
        <div class="field"><label for="f-local">Localização</label><input class="input mono" id="f-local" name="localizacao" placeholder="A-03-2" value="${v('localizacao')}"></div>
        <div class="field"><label for="f-forn">Fornecedor principal</label>
          <select class="input" id="f-forn" name="fornecedorId"><option value="">—</option>
            ${fornecedores.map((f) => `<option value="${f.id}" ${f.id === peca?.fornecedorId ? 'selected' : ''}>${esc(f.nome)} (${f.prazoEntregaDias} dias)</option>`).join('')}
          </select></div>
        <div class="field"><label for="f-min">Estoque mínimo *</label><input class="input" id="f-min" name="estoqueMinimo" type="number" min="0" required value="${v('estoqueMinimo', 0)}"></div>
        <div class="field"><label for="f-max">Estoque máximo *</label><input class="input" id="f-max" name="estoqueMaximo" type="number" min="0" required value="${v('estoqueMaximo', 0)}"></div>
        <div class="field"><label for="f-preco">Preço de venda (R$) *</label><input class="input" id="f-preco" name="precoVenda" type="number" min="0.01" step="0.01" required value="${v('precoVenda')}"></div>
        ${peca ? '' : `
        <div class="field"><label for="f-custo">Custo unitário (R$)</label><input class="input" id="f-custo" name="custoInicial" type="number" min="0" step="0.01" value="0"></div>
        <div class="field"><label for="f-saldo">Saldo inicial</label><input class="input" id="f-saldo" name="saldoInicial" type="number" min="0" value="0"></div>`}
      </div>`,
    aoConfirmar: async (dados) => {
      const corpo = {
        ...dados,
        fornecedorId: dados.fornecedorId ? Number(dados.fornecedorId) : null,
        estoqueMinimo: Number(dados.estoqueMinimo),
        estoqueMaximo: Number(dados.estoqueMaximo),
        precoVenda: Number(dados.precoVenda),
        custoInicial: Number(dados.custoInicial ?? 0),
        saldoInicial: Number(dados.saldoInicial ?? 0),
        usuario: ctx.usuario(),
      };
      const salva = peca ? await api.put(`/pecas/${peca.id}`, corpo) : await api.post('/pecas', corpo);
      toast(peca ? 'Peça atualizada' : `Peça ${salva.codigo} cadastrada`, 'ok');
      return salva;
    },
  });
}
