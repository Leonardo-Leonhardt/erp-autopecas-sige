// Cliente HTTP da API. Erros de regra de negócio chegam como { erro: "mensagem" }.

async function requisitar(metodo, caminho, corpo) {
  const resposta = await fetch(`/api${caminho}`, {
    method: metodo,
    headers: corpo === undefined ? {} : { 'Content-Type': 'application/json' },
    body: corpo === undefined ? undefined : JSON.stringify(corpo),
  });
  const texto = await resposta.text();
  const dados = texto ? JSON.parse(texto) : null;
  if (!resposta.ok) {
    throw new Error(dados?.erro ?? dados?.title ?? `Erro ${resposta.status} ao acessar o servidor.`);
  }
  return dados;
}

function query(params) {
  const q = new URLSearchParams();
  for (const [chave, valor] of Object.entries(params)) {
    if (valor !== undefined && valor !== null && valor !== '') q.set(chave, valor);
  }
  const s = q.toString();
  return s ? `?${s}` : '';
}

export const api = {
  get: (caminho, params = {}) => requisitar('GET', caminho + query(params)),
  post: (caminho, corpo = {}) => requisitar('POST', caminho, corpo),
  put: (caminho, corpo = {}) => requisitar('PUT', caminho, corpo),
};
