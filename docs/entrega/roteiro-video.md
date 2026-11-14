# Roteiro do vídeo de apresentação

Duração máxima permitida: **10:00**. Planejado: **8:40**, para fechar perto de 9:00 com as variações da gravação.

Este roteiro só usa demonstrações que já foram executadas no projeto ou que devem ser **ensaiadas antes da
gravação** ([checklist de gravação](checklist-gravacao.md), seção 7). Cada bloco separa o que é feito **ao vivo** do
que é **evidência anterior** (arquivo, registro do plano ou saída gravada antes). Nenhum resultado deve ser anunciado
sem estar na tela.

Todas as operações que criam pedidos usam o ambiente **descartável** `joya-video` (checklist de gravação, seção 5),
nunca o banco principal.

## Visão geral

| Bloco | Início | Duração | Tema |
|---|---|---|---|
| A | 0:00 | 0:25 | Apresentação do projeto |
| B | 0:25 | 0:45 | Spec-Driven Development |
| C | 1:10 | 1:25 | `CLAUDE.md`, hooks, agentes e skill |
| D | 2:35 | 0:40 | MCP com consulta real ao SQL Server |
| E | 3:15 | 0:45 | Docker Compose e arquitetura |
| F | 4:00 | 1:05 | Interface React, filtros e criação de pedido |
| G | 5:05 | 1:10 | Estoque insuficiente e integridade transacional |
| H | 6:15 | 0:50 | Testes automatizados e concorrência |
| I | 7:05 | 1:10 | Parte 4: análise SQL e desempenho |
| J | 8:15 | 0:25 | Histórico Git e encerramento |
| | **8:40** | | |

**Regra de corte**: se o cronômetro passar de **9:15** ao terminar o bloco I, o bloco J fica só com a frase final.

Telas usadas:

- **T1**: Git Bash no repositório principal (`C:\Projetos\joya-mini-gestao-pedidos`).
- **T2**: Git Bash no clone do ambiente `joya-video`, com `COMPOSE_PROJECT_NAME=joya-video` exportado.
- **T3**: terminal com a saída da suíte completa, executada **antes** da gravação.
- **Claude Code**: app desktop aberto no repositório principal, com o MCP `sqlserver-joya` conectado.
- **Navegador**: http://localhost:3000 (ambiente `joya-video`) e o repositório no GitHub numa janela anônima.
- **Editor**: arquivos do roteiro já abertos em abas (checklist de gravação, seção 4).

---

## A. Apresentação rápida (0:00 – 0:25)

- **Tela**: README no GitHub (janela anônima), topo e sumário.
- **Comando**: nenhum.
- **Narração sugerida**: "Este é o Mini Gestão de Pedidos, o teste técnico Full Stack da Joya: SQL Server, API em
  .NET 10 e frontend em React com TypeScript, tudo no Docker. A regra de negócio é simples de propósito; o foco é o
  processo de engenharia com o Claude Code."
- **Evidência**: README versionado.
- **Risco**: repositório ainda privado. **Contorno**: torná-lo público antes de gravar (checklist de publicação,
  sequência sugerida) ou mostrar o README local no editor.

## B. Spec-Driven Development (0:25 – 1:10)

- **Tela**: `docs/spec.md` (cabeçalho "Aprovada", §3 regras, §7.2 estratégia transacional, §12.1 critérios
  CA-01 a CA-30); `docs/plan.md` (§2 ajustes à ordem, §13 decisões humanas, §14 registro de execução).
- **Ao vivo (T1)**:
  ```bash
  git log --oneline --reverse | head -3
  ```
  A spec (`f8e7549`) e o plano (`bcd6696`) vêm antes de qualquer código.
- **Narração sugerida**: "Comecei pela especificação, em plan mode: requisitos rastreados até o enunciado, regras
  numeradas, contratos HTTP, catálogo de testes e 30 critérios de aceite mensuráveis. Ela passou por revisões minhas e
  por uma revisão independente antes de ser aprovada. O plano dividiu o trabalho em entregas; um agente revisou o
  plano e, na minha revisão, rejeitei propostas dele, como suprimir de forma genérica os avisos de vulnerabilidade do
  NuGet e apagar o banco nos testes. Cada entrega executada tem um registro com os números das validações, os achados
  das revisões e as minhas decisões. Quando a implementação divergiu da spec, eu registrei e decidi, sem alterar a
  spec em silêncio."
- **Evidência anterior**: spec aprovada em 12/11/2026; plano §13; registro das entregas E0–E11 e E13 (a E12, com os
  diferenciais, não foi executada); divergência do D3 aceita na E8.
- **Risco**: rolar arquivos longos consome tempo. **Contorno**: abas já posicionadas nas seções.

## C. `CLAUDE.md`, hooks, agentes e skill (1:10 – 2:35)

- **Tela**: `CLAUDE.md` (comandos e regras de Git); `.claude/settings.json` (hooks `PreToolUse` e `PostToolUse`);
  `.claude/rules/`; `.claude/agents/`; `.claude/skills/novo-endpoint/SKILL.md`.
- **Demonstração do hook pós-edição (ao vivo, Claude Code)**. Pedir ao Claude:
  > Para demonstrar o hook, adicione temporariamente o campo `private static readonly int Quebra = "texto";` dentro
  > da classe `RegrasPedido`, em `backend/src/JoyaPedidos.Dominio/RegrasPedido.cs`. Depois de ver o retorno do hook,
  > remova o campo.

  O hook compila só o projeto `JoyaPedidos.Dominio` e devolve o erro `CS0029` ao Claude; a edição fica no arquivo e o
  Claude a desfaz. Ao final, em T1:
  ```bash
  git status --short
  ```
  A saída deve estar vazia.
- **Evidência da skill (anterior, T1)**:
  ```bash
  git log --oneline --no-walk 29068b6 54489f7 6d64220 a3772de
  ```
  Mostra o commit da skill e os três commits dos endpoints criados com ela. Opcional, a transcrição local da E6, só
  com as chamadas da ferramenta (a primeira foi recusada porque a pasta da skill ainda não existia; as quatro seguintes
  são as reais):
  ```bash
  grep -o '"name":"Skill","input":{[^}]*}' ~/.claude/projects/C--Projetos-joya-mini-gestao-pedidos/14f30726-0e03-4313-b49e-2b8a84303fa2.jsonl
  ```
- **Narração sugerida**: "O `CLAUDE.md` concentra arquitetura, convenções, segurança e só comandos executados de
  fato; as regras por área entram quando o Claude toca cada pasta. O hook de proteção roda antes de qualquer comando e
  bloqueia reescrita de histórico e acesso ao `.env`. O pós-edição compila o projeto editado e devolve o erro ao
  Claude, que corrige na hora, como agora. A skill `novo-endpoint` foi usada quatro vezes, uma por endpoint de
  leitura, como mostram os commits. Os dois agentes revisores fizeram revisões independentes que acharam problemas
  reais, listados no README."
- **Evidência anterior**: as 4 invocações da skill na E6, com horários no registro do plano; verificador dos hooks
  com 164 verificações aprovadas e 1 pulada no Windows; tabela "O que precisou ser corrigido" do README.
- **Risco**: o Claude editar outro trecho, pedir permissão ou não desfazer exatamente. **Contorno**: definir o modo de
  permissão no ensaio; conferir `git diff`; se sobrar diferença só nesse arquivo,
  `git restore backend/src/JoyaPedidos.Dominio/RegrasPedido.cs`. Gravar com a árvore limpa, depois dos commits da E13.

## D. MCP com consulta real ao SQL Server (2:35 – 3:15)

- **Tela**: `.mcp.json` (a senha aparece só como `${JOYA_MCP_SENHA}`) e o Claude Code.
- **Ao vivo (Claude Code)**. Pedir ao Claude:
  > Pelo MCP sqlserver-joya, execute esta consulta e mostre o resultado em tabela:

  ```sql
  SELECT SYSTEM_USER AS Usuario, DB_NAME() AS Banco, (SELECT COUNT(*) FROM dbo.Pedidos) AS TotalPedidos;

  SELECT i.name AS Indice, i.type_desc AS Tipo, i.is_unique AS Unico,
         STRING_AGG(CASE WHEN ic.is_included_column = 0 THEN c.name END, ', ') WITHIN GROUP (ORDER BY ic.key_ordinal) AS Chave,
         STRING_AGG(CASE WHEN ic.is_included_column = 1 THEN c.name END, ', ') AS Incluidas
  FROM sys.indexes i
  JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
  JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
  WHERE i.object_id IN (OBJECT_ID(N'dbo.Pedidos'), OBJECT_ID(N'dbo.ItensPedido'))
  GROUP BY i.object_id, i.name, i.type_desc, i.is_unique
  ORDER BY i.object_id, i.name;
  ```
  Esperado: usuário `joya_mcp`, banco `JoyaPedidos`, o total de pedidos do banco principal (35 em 13/11/2026) e os 5
  índices de `Pedidos` e `ItensPedido`: `PK_Pedidos`, `IX_Pedidos_DataPedido` (com `ClienteId` incluído),
  `IX_Pedidos_ClienteId_DataPedido`, `PK_ItensPedido` e `UX_ItensPedido_PedidoId_ProdutoId` (com
  `Quantidade, PrecoUnitario` incluídos). A consulta foi testada pelo MCP em 13/11/2026.
- **Opcional (ao vivo)**: `UPDATE dbo.Produtos SET Estoque = Estoque WHERE 1 = 0;` pelo MCP, que falha com o erro 229
  (permissão negada). Não altera nada: a permissão não existe e o `WHERE 1 = 0` não afeta linhas. O Claude pode pedir
  confirmação antes de rodar; ensaie.
- **Narração sugerida**: "O Claude Code se conecta ao SQL Server do compose pelo MCP, com um login que só lê: a senha
  vem de variável de ambiente e não está no repositório, e o próprio banco nega escrita. Este é o banco de
  desenvolvimento, com 35 pedidos; a demonstração da interface, daqui a pouco, usa um ambiente descartável com o seed
  original de 33. Usei o MCP para conferir esquema, índices e seed, para ler DMVs e planos em cache e na investigação
  da Parte 4."
- **Evidência anterior**: privilégios e escritas negadas (E7, tabela no README); planos em cache e `STATISTICS IO`
  pelo MCP (E8); reconexão e consulta real em 13/11/2026.
- **Risco**: MCP em `failed` (partida do `npx` acima de 30 s) ou sem a senha no ambiente. **Contorno**: checklist de
  gravação, seção 4.

## E. Docker Compose e arquitetura (3:15 – 4:00)

- **Tela**: trecho acelerado, **gravado antes**, do `git clone`, do `ls -a` sem `.env` e do
  `docker compose -p joya-video up --build -d` no T2 (cerca de 75 s reais; mostrar 10 a 15 s, com o rótulo
  "gravado antes"). Depois, `docker-compose.yml` (serviços e `depends_on`) e o diagrama do README.
- **Ao vivo (T2)**:
  ```bash
  docker compose -p joya-video ps -a
  ```
  ```bash
  curl -s -w '\nHTTP %{http_code}\n' http://localhost:5080/health
  ```
  Esperado: `db-init` com `Exited (0)`, `api` e `web` `Up`, `sqlserver` `healthy` com `1433/tcp` sem endereço do host
  (porta não publicada), e `Healthy` com `HTTP 200`.
- **Narração sugerida**: "Um único `docker compose up`, num clone limpo, sem `.env` e sem ajuste manual, sobe quatro
  serviços. O `db-init` aplica os scripts idempotentes e só então a API sobe. O banco não publica porta no host: o
  `1433/tcp` sem endereço é só a porta interna. O nginx serve o React e encaminha `/api` para a API, na mesma origem. A
  API é um monólito modular, com um projeto de domínio de regras puras."
- **Evidência anterior**: clone limpo validado na E11 (75 s, sem passo manual, CA-01 a CA-03).
- **Risco**: portas 5080 e 3000 ocupadas pela stack principal. **Contorno**: parar `api` e `web` do principal antes
  (checklist de gravação, seção 5).

## F. Interface React, filtros e criação de pedido (4:00 – 5:05)

- **Tela**: navegador em http://localhost:3000 (ambiente `joya-video`, seed com 33 pedidos).
- **Ao vivo**:
  1. Listagem: "Página 1 de 4 · 33 pedidos"; **Próxima** e volta.
  2. Filtro "Ana Souza (ana.souza@exemplo.com)" com período 01/01/2026 a 31/12/2026 → **Filtrar**; **Limpar**.
  3. Data inicial depois da final → erro sob as datas, sem chamada à API.
  4. **Novo pedido**: Bruno Lima, Garrafa térmica × 2 e Caneca de cerâmica × 1 → **Criar pedido** →
     "Pedido nº 34 criado com sucesso." e o pedido no topo da lista.
- **Narração sugerida**: "A listagem filtra por cliente e período, com datas no calendário de São Paulo, e pagina de
  dez em dez; homônimos aparecem com o e-mail. Período inválido é barrado antes de chamar a API. No modal, o catálogo
  é recarregado ao abrir, produto sem estoque fica desabilitado e o total é só uma estimativa: o valor oficial é o que
  a API grava."
- **Evidência anterior**: fluxos da spec §6 conferidos no navegador nas E10 e E11 (CA-16, CA-17).
- **Risco**: criar o pedido no banco principal. **Contorno**: antes do bloco, a conferência de 33 pedidos do
  checklist de gravação.

## G. Estoque insuficiente e integridade transacional (5:05 – 6:15)

- **Tela**: navegador (modal) e T2 lado a lado; comandos do T2 já digitados.
- **Ao vivo**:
  1. Em T2, mostrar o estoque atual (Caneca é o produto 1; Mochila, o 6):
     ```bash
     curl -s http://localhost:5080/produtos | grep -oE '"id":(1|6),[^}]*'
     ```
  2. No modal: Diego Alves, Mochila executiva × 2 (estoque 2 no seed) e Caneca de cerâmica × 1. **Não** clicar ainda.
  3. Em T2, outra cliente compra 1 mochila:
     ```bash
     curl -s -w '\nHTTP %{http_code}\n' -X POST http://localhost:5080/pedidos -H "Content-Type: application/json" -d '{"clienteId":4,"itens":[{"produtoId":6,"quantidade":1}]}'
     ```
     Esperado: `HTTP 201`.
  4. No modal, **Criar pedido** → 409: "Mochila executiva: solicitado 2, disponível 1", a linha marcada com
     "Disponível agora: 1." e o modal aberto com os dados.
  5. Em T2, repetir o comando do passo 1: Mochila com estoque 1 (só a compra do passo 3) e Caneca com o mesmo
     estoque do passo 1.
- **Narração sugerida**: "Abri o pedido com duas mochilas em estoque; enquanto isso, outra compra levou uma. A API lê
  cada produto sob lock na hora de confirmar e confere todos os itens antes de baixar qualquer estoque: recusou com
  409, listou o que falta, e nada foi gravado, nem a caneca. Se uma falha acontecesse depois de uma baixa, a transação
  desfaria tudo; isso também é testado."
- **Evidência anterior**: IT-04, IT-05 (rejeição antes da primeira escrita) e IT-25 (rollback depois de baixas já
  executadas).
- **Risco**: enviar o `curl` para a API errada. **Contorno**: o `curl` só roda em T2, com a stack principal parada e a
  conferência de 33 pedidos feita.

## H. Testes automatizados e concorrência (6:15 – 7:05)

- **Tela**: T3 com a saída da suíte completa executada antes da gravação (`total: 254`, `falhou: 0`); tabela de
  testes do README (114 unitários e 140 de integração); `RepositorioPedidos.cs`, linhas 30 (`UPDLOCK, ROWLOCK`) e 42
  (`UPDATE` condicional); `ConcorrenciaPedidosTestes.cs` (IT-09 e IT-10).
- **Ao vivo (T1, alguns segundos)**:
  ```bash
  dotnet test --project backend/tests/JoyaPedidos.TestesUnitarios --no-build
  ```
  Esperado: 114 aprovados (usa os binários compilados pela execução feita antes da gravação).
- **Narração sugerida**: "São 114 testes unitários e 140 de integração contra SQL Server real em container, com os
  mesmos scripts do compose. Cada produto é lido com lock de atualização, sempre em ordem crescente de id, e a baixa só
  acontece se ainda houver estoque. No IT-09, 20 pedidos simultâneos disputam 5 unidades: exatamente 5 criados, 15
  recusados, estoque final zero e nenhum erro 500. No IT-10, pedidos com os produtos em ordens inversas, em três
  rodadas: todos criados, estoques exatos e nenhum deadlock que chegue à API. Também fiz testes de mutação: desfiz cada
  proteção numa cópia e confirmei que algum teste reprova."
- **Evidência anterior**: suíte completa com 254/254 na publicação da E11 e na execução antes da gravação; mutações da
  E5 (sem a ordem crescente, o IT-10 reprova com 503).
- **Risco**: a suíte falhar no ensaio. **Contorno**: não gravar sem uma execução completa com 0 falhas na tela.

## I. Parte 4: análise SQL e desempenho (7:05 – 8:15)

- **Tela**: `database/consultas/ranking-2026-original.sql` ao lado de `ranking-2026-corrigida.sql`; README, seções
  "O que estava errado (D1–D6)", "Medições", "Planos de execução" e "Decisão sobre índices". Para números, usar as
  tabelas do README (a linha 15 do `consolidado.txt` tem acentuação corrompida pela geração do arquivo).
- **Comando**: nenhum benchmark ao vivo (a massa de 120 mil pedidos só existe no projeto isolado `joya-perf`).
- **Narração sugerida**: "A query original tem seis problemas: ordena ao contrário, agrupa por nome e junta
  homônimos, usa um `LEFT JOIN` que o `WHERE` anula, usa o ano UTC em vez do ano civil de São Paulo, não desempata e
  aplica função sobre a coluna, o que impede a busca no índice. A corrigida agrupa por cliente, usa os limites UTC do
  ano local num intervalo semiaberto e desempata por id; testes provam o resultado exato sobre dados de referência.
  Medi num ambiente isolado com 120 mil pedidos, em cinco passadas: de 2.416 para 1.517 leituras lógicas e de 963 para
  54 ms de CPU. Boa parte dessa diferença vem do paralelismo da original, então medi também em série, para isolar o
  efeito do predicado. Mantive os índices: o de data corta 70% das leituras de pedidos, o índice único de itens cobre a
  soma, e o de cliente atende o filtro da listagem, que não foi medido."
- **Evidência anterior**: medições da E8 (5 passadas, logs e planos reais em `docs/evidencias/parte4`); IT-23 e IT-24.
- **Risco**: números ditos de memória divergirem do README. **Contorno**: ler os números da tabela na tela.

## J. Histórico Git e encerramento (8:15 – 8:40)

- **Tela**: T1 e a página de commits no GitHub.
- **Ao vivo (T1)**:
  ```bash
  git log --oneline | head -15
  ```
  ```bash
  git log --oneline | wc -l
  ```
  ```bash
  git status -sb
  ```
  Esperado: Conventional Commits em português, sem merge, e `## main...origin/main` sem alterações pendentes.
- **Narração sugerida**: "O histórico conta a evolução em commits pequenos; as funcionalidades do backend entraram com
  seus testes no mesmo commit. Não há squash, amend nem rebase: o hook bloqueia essas operações e o reflog confirma que
  nenhum commit local foi reescrito. O README traz como rodar, as decisões, o que funcionou com o Claude, o que precisei
  corrigir e as limitações conhecidas. Obrigado."
- **Evidência anterior**: verificação do reflog nas E11 e E13.
- **Risco**: passar de 10:00. **Contorno**: regra de corte no início deste roteiro.

## O que nunca deve aparecer na tela

- O conteúdo de `.env`, variáveis de ambiente, a tela "Variáveis de ambiente" do Windows ou qualquer comando que
  imprima `JOYA_MCP_SENHA`.
- `docker compose config`, `docker inspect` e as abas Inspect ou Env do Docker Desktop: eles expandem
  `MSSQL_SA_PASSWORD`, `SQLCMDPASSWORD` e a string de conexão da API.
- DSN expandida do MCP ou detalhes do servidor no `/mcp` que mostrem valores de ambiente.
- Histórico do terminal (seta para cima, `Ctrl+R`), e-mails, notificações, abas pessoais, favoritos, senhas salvas do
  navegador e a conta logada no GitHub.
