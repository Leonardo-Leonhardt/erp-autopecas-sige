// Roteador da SPA (hash) e contexto compartilhado entre as telas.
import { esc, toast } from './ui.js';
import * as dashboard from './pages/dashboard.js';
import * as venda from './pages/venda.js';
import * as vendas from './pages/vendas.js';
import * as estoque from './pages/estoque.js';
import * as peca from './pages/peca.js';
import * as movimentacoes from './pages/movimentacoes.js';
import * as reposicao from './pages/reposicao.js';

const rotas = [
  { padrao: /^dashboard$/, pagina: dashboard, menu: 'dashboard' },
  { padrao: /^venda$/, pagina: venda, menu: 'venda' },
  { padrao: /^vendas$/, pagina: vendas, menu: 'vendas' },
  { padrao: /^estoque$/, pagina: estoque, menu: 'estoque' },
  { padrao: /^estoque\/(\d+)$/, pagina: peca, menu: 'estoque' },
  { padrao: /^movimentacoes$/, pagina: movimentacoes, menu: 'movimentacoes' },
  { padrao: /^reposicao$/, pagina: reposicao, menu: 'reposicao' },
];

let view = document.getElementById('view');
const seletorUsuario = document.getElementById('usuario');
let limparPaginaAtual = null;
let navegacao = 0;

const USUARIO_KEY = 'autopecas.usuario';
try {
  const salvo = localStorage.getItem(USUARIO_KEY);
  if (salvo) seletorUsuario.value = salvo;
} catch { /* armazenamento indisponível: segue com o padrão */ }

function atualizarUsuario() {
  const opcao = seletorUsuario.selectedOptions[0];
  document.getElementById('papel').textContent = opcao.dataset.papel;
  document.getElementById('avatar').textContent = opcao.value.split(/\s+/).map((p) => p[0]).join('').slice(0, 2).toUpperCase();
  try { localStorage.setItem(USUARIO_KEY, opcao.value); } catch { /* ignora */ }
}
seletorUsuario.addEventListener('change', atualizarUsuario);
atualizarUsuario();

function cabecalho({ titulo, crumb = '', acoes = '' }) {
  document.getElementById('titulo').innerHTML = titulo;
  document.getElementById('crumb').innerHTML = crumb;
  document.getElementById('acoes').innerHTML = acoes;
  document.title = `${document.getElementById('titulo').textContent} · AutoPeças Gestão`;
}

async function renderizar() {
  const [caminhoBruto, busca = ''] = location.hash.replace(/^#\/?/, '').split('?');
  const caminho = caminhoBruto || 'dashboard';
  const id = ++navegacao;
  const rota = rotas.find((r) => r.padrao.test(caminho));
  if (!rota) { location.hash = '#/dashboard'; return; }

  limparPaginaAtual?.();
  limparPaginaAtual = null;
  document.querySelectorAll('.nav').forEach((n) => n.classList.toggle('active', n.dataset.rota === rota.menu));
  document.getElementById('menu').classList.remove('aberto');
  // Troca o elemento da tela para descartar os event listeners da página anterior.
  const novaView = view.cloneNode(false);
  view.replaceWith(novaView);
  view = novaView;
  view.innerHTML = '<div class="carregando">Carregando…</div>';

  const ctx = {
    view,
    params: caminho.match(rota.padrao).slice(1),
    query: new URLSearchParams(busca),
    usuario: () => seletorUsuario.value,
    cabecalho,
    // Evita que uma tela lenta sobrescreva a tela para a qual o usuário já navegou.
    ativa: () => id === navegacao,
    recarregar: renderizar,
  };

  try {
    const limpar = await rota.pagina.render(ctx);
    if (ctx.ativa()) limparPaginaAtual = limpar ?? null;
    else limpar?.();
  } catch (erro) {
    if (!ctx.ativa()) return;
    cabecalho({ titulo: 'Erro' });
    view.innerHTML = `<div class="card vazio">Não foi possível carregar esta tela.<br><span class="c-danger">${esc(erro.message)}</span><br><br><button class="btn" id="tentar">Tentar novamente</button></div>`;
    view.querySelector('#tentar').onclick = renderizar;
    toast(erro.message, 'erro');
  }
}

document.getElementById('menu-toggle').addEventListener('click', () => {
  document.getElementById('menu').classList.toggle('aberto');
});

window.addEventListener('hashchange', renderizar);
renderizar();
