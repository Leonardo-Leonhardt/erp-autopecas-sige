# Sistema Integrado de Gestão Empresarial (SIGE) — Comércio de Autopeças

> Projeto desenvolvido para a disciplina **Trabalho Interdisciplinar: Sistemas Integrados de Gestão Empresarial (TI: SIGE)** — Curso de Sistemas de Informação.

---

## 📑 Sumário

- [Sobre o Projeto](#-sobre-o-projeto)
- [Objetivos do Sistema](#-objetivos-do-sistema)
- [Processos de Negócio Cobertos](#️-processos-de-negócio-cobertos-bpmn--visão-erp)
- [Tecnologias Utilizadas](#️-tecnologias-utilizadas)
- [Como Executar o Projeto](#-como-executar-o-projeto)
- [Estrutura do Repositório](#-estrutura-do-repositório)
- [Documentação Completa](#-documentação-completa)
- [Capturas de Tela](#-capturas-de-tela)
- [Status do Projeto](#-status-do-projeto)
- [Professor Orientador](#-professor-orientador)
- [Integrantes da Equipe](#-integrantes-da-equipe)
- [Licença](#-licença)

---

## 📌 Sobre o Projeto

O setor de varejo de autopeças lida com alta diversidade de itens e rápida obsolescência, o que torna o controle manual ou descentralizado (planilhas isoladas, controle em papel etc.) altamente propenso a divergências entre o estoque físico e o saldo registrado no sistema.

Esse descompasso gera dois problemas recorrentes na operação:

- **Ruptura de estoque** — o cliente procura um item que, na prática, não está disponível;
- **Excesso de mercadorias paradas** — capital imobilizado em produtos sem giro.

Este projeto propõe uma aplicação de gestão empresarial com foco na **integração direta entre os processos de Vendas e Controle de Estoque**, automatizando baixas, reservas e atualizações em tempo real, de forma a reduzir divergências e oferecer aos gestores uma visão única e confiável da operação.

---

## 🎯 Objetivos do Sistema

- **Integração Vendas ↔ Estoque** — baixa e reserva automática de peças no fechamento de pedidos de venda, eliminando a atualização manual do saldo.
- **Centralização de Dados** — substituição de planilhas isoladas por uma base única, garantindo visão unificada das operações.
- **Controle Operacional** — histórico de movimentações de estoque, alertas de nível crítico e suporte à tomada de decisão de compras e reposição.

---

## ⚙️ Processos de Negócio Cobertos (BPMN / Visão ERP)

| Processo | Descrição |
|---|---|
| **Gestão de Catálogo e Estoque** | Cadastro de componentes, compatibilidade entre peças e controle do saldo físico. |
| **Processamento de Vendas** | Emissão de pedidos, conferência de disponibilidade em tempo real e baixa automática de saldo. |
| **Aquisição e Reposição** | Registro de entrada de insumos com atualização automática do almoxarifado. |

---

## 🛠️ Tecnologias Utilizadas

| Camada | Tecnologia |
|---|---|
| **Backend** | *(ex.: C# / .NET, Java / Spring, Node.js)* |
| **Frontend** | *(ex.: React, Blazor, HTML/CSS/JS)* |
| **Banco de Dados** | *(ex.: PostgreSQL, SQLite, MySQL)* |
| **Modelagem de Processos** | Camunda / Bizagi (BPMN) |

> Atualize esta tabela conforme o stack definitivo do projeto for definido.

---

## 🚀 Como Executar o Projeto

### Pré-requisitos

- [Git](https://git-scm.com/)
- *(SDK/ambiente de execução correspondente ao backend escolhido)*
- *(Instância do banco de dados escolhido, se aplicável)*

### Passo a passo

1. Clone o repositório:
   ```bash
   git clone https://github.com/Leonardo-Leonhardt/erp-autopecas-sige.git
   ```

2. Acesse a pasta do projeto:
   ```bash
   cd erp-autopecas-sige
   ```

3. Restaure as dependências:
   ```bash
   # exemplo — ajustar de acordo com o stack utilizado
   dotnet restore
   # ou
   npm install
   ```

4. Configure as variáveis de ambiente / string de conexão do banco de dados (`.env` ou `appsettings.json`).

5. Execute as migrations do banco de dados:
   ```bash
   # exemplo
   dotnet ef database update
   ```

6. Inicie a aplicação:
   ```bash
   # exemplo
   dotnet run
   # ou
   npm start
   ```

---

## 📂 Estrutura do Repositório

```
erp-autopecas-sige/
├── docs/
│   ├── artigo/       # Texto do TCC/artigo (introdução, referencial teórico, metodologia etc.)
│   │   └── TI_SIGE.docx
│   └── diagramas/    # Diagramas UML, BPMN e casos de uso
├── src/              # Código-fonte da aplicação
├── tests/            # Testes automatizados
└── README.md
```

> Ajuste esta árvore conforme a organização real das pastas do projeto.

---

## 📚 Documentação Completa

O texto completo do trabalho acadêmico (introdução, referencial teórico, metodologia, requisitos, desenvolvimento e resultados) está disponível em [`docs/artigo/TI_SIGE.docx`](docs/artigo/TI_SIGE.docx).

Diagramas UML, BPMN e casos de uso ficam em [`docs/diagramas/`](docs/diagramas/).

---

## 📸 Capturas de Tela

*(Adicionar aqui prints das principais telas do sistema assim que estiverem disponíveis: tela de login, dashboard de estoque, tela de vendas etc.)*

| Tela de Login | Dashboard de Estoque | Tela de Vendas |
|---|---|---|
| ![Login](docs/imagens/login.png) | ![Dashboard](docs/imagens/dashboard.png) | ![Vendas](docs/imagens/vendas.png) |

---

## 📈 Status do Projeto

🚧 Em desenvolvimento — projeto acadêmico em andamento.

---

## 🧑‍🏫 Professor Orientador

- Paulo Augusto Isnard Santos

---

## 👥 Integrantes da Equipe

- Arthur Vinícius Reis Rodrigues
- Emanuela Vitória Magalhães Alves
- Fantine de Fatima Bonfim
- Giovanna Ribeiro Santos
- Kayk da Silva Souza
- Leonardo Leonhardt Bispo
- Rodrigo Fernandes Ribeiro Pinto
- Yanne Assis Alves

---

## 📄 Licença

*(Definir a licença do projeto, ex.: MIT, ou indicar que é de uso exclusivamente acadêmico.)*