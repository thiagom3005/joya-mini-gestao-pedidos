# Mini Gestão de Pedidos

Teste técnico Full Stack da Joya Solutions: cadastro e consulta de pedidos de clientes com SQL Server, API .NET 10
e frontend React + TypeScript, desenvolvido com Claude Code em Spec-Driven Development.

> **Projeto em construção.** As seções abaixo serão preenchidas conforme cada entrega do
> [plano](docs/plan.md) for concluída. Nada aqui descreve resultados ainda não obtidos.

## Documentação

- [Especificação aprovada](docs/spec.md): objetivo, regras de negócio, contratos HTTP, modelo de dados, testes e
  critérios de aceite.
- [Plano de implementação](docs/plan.md): entregas E0–E13, decisões humanas e registro de execução.
- [CLAUDE.md](CLAUDE.md): convenções usadas pelo Claude Code neste repositório.

## Como rodar com Docker

Requisitos: Docker com Compose v2 e containers Linux x64. Nenhum `.env` é necessário, nem SDK .NET ou Node na
máquina: as imagens da API e do frontend são construídas pelo próprio compose.

> Estado atual (entrega E10): o compose sobe o **banco, a API e o frontend**.

```bash
docker compose up --build -d
```

Depois, abra **http://localhost:3000** no navegador.

- `sqlserver`: SQL Server 2022 CU28 (`mcr.microsoft.com/mssql/server:2022-CU28-ubuntu-22.04`), edição Developer,
  volume `joya-pedidos_dados-sql`, **sem porta publicada no host**.
- `db-init`: espera o banco ficar saudável, aplica `database/01..03*.sql` em ordem e encerra com código 0. Rodar de
  novo não duplica nada.
- `api`: API .NET 10 em [backend/Dockerfile](backend/Dockerfile) (build multi-stage `sdk:10.0.404-noble` →
  `aspnet:10.0.15-noble`), Kestrel na porta 8080 do container, usuário não root, `Production`. Só sobe depois que o
  `db-init` termina com sucesso; se algum script falhar, a API não sobe.
- `web`: frontend React em [frontend/Dockerfile](frontend/Dockerfile) (build multi-stage `node:24.22.3-alpine3.24` →
  `nginx:1.30.6-alpine3.24`). O nginx serve o build estático e encaminha `/api/*` para `http://api:8080/*`, removendo o
  prefixo ([frontend/nginx.conf](frontend/nginx.conf)): navegador e API ficam na mesma origem, sem CORS. Sobe depois
  que a API inicia; nos primeiros segundos o proxy pode responder 502, e a tela mostra o erro com "Tentar novamente".
- Confira com `docker compose ps -a`: `db-init` `Exited (0)`, `api` e `web` `Up`. Com as imagens base já baixadas, a
  primeira subida num volume novo levou cerca de 1 minuto (inicialização do SQL Server, scripts e build da API e do
  frontend). O primeiro download das imagens base leva mais.

API em **http://localhost:5080** (todos os endpoints da spec §5, sem prefixo):

```bash
curl http://localhost:5080/health
```

Responde `200` com `Healthy`. O `/health` indica que a API está no ar, não que o banco responde: o SQL Server tem o
próprio healthcheck no compose. No Windows PowerShell 5.1, `curl` é um alias de `Invoke-WebRequest`; use `curl.exe`.

| Objetivo | Comando |
|---|---|
| Logs da API / do frontend | `docker compose logs api` / `docker compose logs web` (acompanhar: `-f`) |
| Reconstruir e subir só a API depois de mudar o backend | `docker compose up --build -d --no-deps api` |
| Reconstruir e subir só o frontend depois de mudar `frontend/` | `docker compose up --build -d --no-deps web` |
| Só construir as imagens | `docker compose build api` / `docker compose build web` |
| Outra porta para a API | `API_PORTA=5090` no `.env` (ou no ambiente do shell); o padrão é `5080` |
| Outra porta para o frontend | `WEB_PORTA=3100` no `.env` (ou no ambiente do shell); o padrão é `3000` |

O `--no-deps` evita que o compose recrie também as dependências (`db-init` e `sqlserver`; para o `web`, a `api`). Os logs da API mostram a
inicialização e os erros (cada 500 registra a exceção com o mesmo `traceId` da resposta); o log de cada requisição
fica desligado (`Logging__LogLevel__Microsoft.AspNetCore=Warning` no compose).

O build da imagem roda a auditoria NuGet do backend com avisos tratados como erro (plano, E2). Se um advisory for
publicado depois desta entrega para um pacote usado, o `docker compose up --build` falha no `dotnet restore` com
`NU1901`–`NU1904`. A saída é atualizar ou fixar a versão do pacote em `backend/Directory.Packages.props`; o próprio
erro aponta o pacote e o advisory.

Credenciais de desenvolvimento: a senha padrão do `sa` (`Joya_Dev_2026_Inseguro`) é **pública, insegura e exclusiva
de desenvolvimento local**. A API usa o próprio `sa`, recebido pela variável `ConnectionStrings__BancoPedidos`
montada no compose com essa mesma senha. É uma limitação consciente deste ambiente (spec §11.4, RSK-10): em produção
haveria um login de privilégio mínimo e um gerenciador de segredos. Para trocar a senha, copie `.env.example` para
`.env` e defina `MSSQL_SA_PASSWORD` (sem `$`, aspas ou `;`). Nenhuma credencial vai para a imagem; ela chega só pelo
ambiente do container.

Para acessar o banco pelo host (MCP, SSMS, Azure Data Studio), use o arquivo opt-in que publica a porta só em
`127.0.0.1`:

```bash
docker compose -f docker-compose.yml -f docker-compose.dev.yml up --build -d
```

Se o banco já estiver no ar com esse arquivo (por exemplo, para o MCP), continue usando os **dois** arquivos. Um
`docker compose up` só com o `docker-compose.yml` recria o `sqlserver` sem a porta. Os dados continuam no volume,
mas o MCP perde a conexão até que você suba de novo com o `docker-compose.dev.yml`.

Reset completo (apaga apenas o volume deste projeto e recarrega os dados de exemplo):

```bash
docker compose down -v
```

## Como usar a interface

Página única "Gestão de Pedidos" em http://localhost:3000 (spec §6):

- **Listagem**: pedidos mais recentes primeiro, 10 por página, com Nº, Cliente, Data (horário de São Paulo, qualquer
  que seja o fuso do navegador), Itens (número de produtos distintos) e Valor total em reais. **Anterior** e
  **Próxima** navegam entre as páginas; o rodapé mostra "Página X de Y · N pedidos". Acima da tabela, um resumo
  mostra só números devolvidos pela API: pedidos encontrados (total do filtro) e exibidos nesta página.
- **Filtros**: cliente (os homônimos aparecem como "Nome (email)"), data inicial e data final, inclusivas, no
  calendário de São Paulo. **Filtrar** e **Limpar** voltam à página 1. Data inicial depois da final, ou data digitada
  pela metade, mostra o erro abaixo das datas, sem consultar a API.
- **Novo pedido**: abre um modal que recarrega clientes e produtos (estoque atual). Escolha o cliente e os itens (até
  50; produto sem estoque ou já escolhido em outra linha fica desabilitado; quantidade de 1 até o estoque, no máximo
  10.000). O total exibido é uma **estimativa**: o valor oficial é o que a API grava. Ele fica no rodapé fixo do
  modal, junto de **Cancelar** e **Criar pedido**, sempre visível com muitos itens. **Cancelar** ou Esc descartam o
  formulário.
- **Respostas da criação**: sucesso fecha o modal, mostra "Pedido nº X criado com sucesso." e volta à primeira
  página sem filtros. Erros mantêm o modal e os dados digitados: validação (erros junto aos campos e um resumo),
  cliente ou produto inexistente (listas recarregadas), estoque insuficiente ("solicitado X, disponível Y", estoques
  recarregados), conflito momentâneo de concorrência, e falhas de servidor ou de rede (mensagem genérica; nos erros
  500, um código para suporte).

Para conferir o proxy pelo terminal (no PowerShell 5.1, use `curl.exe`):

```bash
curl http://localhost:3000/api/health
```

```bash
curl "http://localhost:3000/api/pedidos?pagina=1&tamanhoPagina=10"
```

### Desenvolvimento do frontend

Requisitos: Node 22.12 ou mais recente (testado com o 24). Com a API no ar em `localhost:5080`, o servidor do Vite
(http://localhost:5173) faz o mesmo proxy `/api` do nginx:

```bash
npm --prefix frontend ci
```

```bash
npm --prefix frontend run dev
```

Verificações: `npm --prefix frontend run lint`, `npm --prefix frontend run typecheck` e `npm --prefix frontend run build`.
Dependências com versões exatas no `package.json` e no `package-lock.json`.

Stack: React 19, TypeScript `strict` e Vite, sem router, biblioteca de estado ou de componentes. CSS próprio em
[frontend/src/estilos.css](frontend/src/estilos.css), com tokens de cor, tipografia, espaçamento, sombras, raios e
foco; `prefers-reduced-motion` respeitado. Ícones do `lucide-react` (decorativos, com `aria-hidden`; botões só com
ícone têm nome acessível). Fontes do sistema, sem recursos externos, compatíveis com a CSP do nginx.

Paleta: azuis `#142C72` (principal), `#1D3F95` (secundário) e `#0E1E4D` (profundo); laranja `#FF6A13` (hover
`#E85C0D`); fundo `#F5F7FB`, superfícies `#FFFFFF`, bordas `#D9E1F0`; texto `#1B2559` e secundário `#667085`;
sucesso `#16A34A`, aviso `#D97706` e erro `#DC2626`. Contraste WCAG AA medido nos pares usados: o laranja leva texto
azul profundo (5,58:1; no hover 4,55:1), porque o branco sobre ele fica em 2,87:1; sucesso, aviso e erro puros ficam
em bordas e detalhes, e textos e ícones usam tons derivados mais escuros (`color-mix`), com 4,86:1 ou mais.

Acessibilidade: todo campo tem `<label>`; erros com `role="alert"` e ligados ao campo por `aria-describedby`; sucesso
em região `aria-live="polite"` sempre presente; modal com `<dialog>` nativo, foco inicial no Cliente, Esc e Cancelar
devolvem o foco a "Novo pedido"; foco visível também no modo de alto contraste do Windows. Responsividade: tabela em
cartões até 640 px, modal como painel inferior no celular e rodapé fixo com o total estimado, que nunca cobre um campo.
Envio sem duplicidade: "Criar pedido" fica desabilitado com "Criando…", e uma trava por `ref` barra disparos repetidos
antes da re-renderização (4 acionamentos seguidos geram 1 POST, conferido no navegador).

Validação da E10: lint, typecheck e build sem avisos; `npm audit --omit=dev` sem vulnerabilidades (as 9 moderadas de
`npm audit` são só de dependências de desenvolvimento, do advisory GHSA-g3hv-75wg-m249 do `minimatch`); fluxos da spec
§6.2–6.4 conferidos no navegador embutido, com respostas reais e simuladas, em larguras de 375 a 1440 px e telas
baixas; duas revisões independentes. O frontend ainda não tem testes automatizados: os E2E (Playwright) são da E12.
Pendências e riscos estão no registro da E10 em [docs/plan.md](docs/plan.md) §14.

## Como rodar os testes

Requisitos: .NET SDK 10 e Docker em execução (os testes de integração sobem SQL Server real via Testcontainers, com
a mesma imagem e os mesmos scripts do compose).

```bash
dotnet test backend/JoyaPedidos.slnx
```

- Unitários: `dotnet test --project backend/tests/JoyaPedidos.TestesUnitarios`.
- Integração: `dotnet test --project backend/tests/JoyaPedidos.TestesIntegracao`. Usa um container compartilhado
  pela suíte e um container próprio para o IT-21 (scripts aplicados duas vezes em banco novo).

## Principais decisões técnicas

_Resumo a consolidar na entrega E13. As decisões completas e justificadas estão na spec §13 (DEC-01 a DEC-15)._

## Como usei o Claude Code

_A preencher ao longo das entregas e consolidar na E13, com evidências reais: plan mode, `CLAUDE.md`, rules,
hooks, skill, MCP e agentes de revisão; o que funcionou bem e o que precisei corrigir no código gerado._

### MCP do SQL Server

Servidor: **DBHub `@bytebase/dbhub@1.6.1`** (transporte stdio), versão fixada no [.mcp.json](.mcp.json), posterior às
correções dos advisories GHSA-mwwr-p57h-56pf, GHSA-fm8p-53ww-hf6w e GHSA-j656-3hf2-fvjc.

Configuração (uma vez por máquina):

1. Suba o banco com a porta no loopback: `docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d sqlserver db-init`.
2. Defina `JOYA_MCP_SENHA` no ambiente do usuário (8+ caracteres; só letras, dígitos, `_` ou `-`; com maiúsculas,
   minúsculas e dígitos). Ela nunca vai para o repositório. Evite `setx JOYA_MCP_SENHA <valor>`: o valor ficaria no
   histórico do terminal e nos argumentos do processo. Use a tela "Variáveis de ambiente" do Windows ou, no
   PowerShell, leia a senha sem eco:
   `$s = Read-Host -AsSecureString; [Environment]::SetEnvironmentVariable('JOYA_MCP_SENHA', [Net.NetworkCredential]::new('', $s).Password, 'User')`.
   Em Linux ou macOS, use `read -rs JOYA_MCP_SENHA && export JOYA_MCP_SENHA` no perfil da sessão que abre o Claude
   Code.
3. Crie ou atualize o login `joya_mcp` (a senha é lida do ambiente; só o nome da variável aparece no comando):
   `docker compose run --rm --no-deps -e JOYA_MCP_SENHA db-init /opt/mssql-tools18/bin/sqlcmd -C -b -f 65001 -S sqlserver -U sa -i /database/dev/criar-login-mcp.sql`
   (`--no-deps` evita que o compose recrie o `sqlserver` sem a porta do loopback). No Git Bash, prefixe
   `MSYS_NO_PATHCONV=1`, senão o caminho `/opt/...` é convertido para um caminho do Windows.
4. Se usar porta diferente de 1433, defina `SQL_PORTA` também no ambiente do usuário: o `.mcp.json` lê a variável
   do ambiente do Claude Code, não do `.env` do compose.
5. Reinicie o Claude Code para carregar o `.mcp.json` com as variáveis.

Se o servidor aparecer como `failed` com "connection timed out after 30000ms", o problema costuma ser o tempo de
partida do `npx`, não a senha. Na E11 o DBHub levou 21,7 s até a primeira saída com o cache frio e 7 s com o cache
quente, perto do limite de 30 s do cliente, que estoura com a máquina carregada. Reconecte em `/mcp` (Reconnect) ou
aumente o limite de partida com a variável `MCP_TIMEOUT` (em milissegundos) no ambiente do Claude Code.

Segurança: o login `joya_mcp` tem só `db_datareader` no `JoyaPedidos` e `VIEW SERVER PERFORMANCE STATE`; a conexão
usa `sslmode=require` (criptografada). A senha fica em `env` do processo, nunca nos argumentos. O `.mcp.json` usa
`cmd /c npx` (exigido pelo Claude Code no Windows nativo); em Linux ou macOS, troque por `"command": "npx"` e retire
`"/c", "npx"` dos argumentos.

**Ressalva de segurança do `database/dev/criar-login-mcp.sql`.** O `sqlcmd` substitui `$(JOYA_MCP_SENHA)` no texto
do script **antes** da validação em SQL. Um valor malicioso nessa variável pode injetar SQL, que roda como `sa`. Por
isso o script:

- é exclusivo do ambiente de desenvolvimento local;
- deve ser executado manualmente, com uma senha gerada por procedimento controlado (por exemplo, um gerador de
  senhas aleatórias, respeitando os caracteres permitidos);
- não deve receber valores de origem não confiável (variáveis herdadas de terceiros, arquivos ou saídas de
  ferramentas);
- não deve ser reutilizado como mecanismo de provisionamento em produção.

Riscos aceitos (ambiente de desenvolvimento): o `npx` fixa a versão do DBHub, mas não as dependências transitivas
dele (sem lockfile publicado); e o DBHub também carrega um `.env` da pasta atual, o que coloca a senha do `sa` de
desenvolvimento no ambiente do processo do MCP, mesmo sem usá-la. A proteção efetiva é o login `joya_mcp`, que não
escreve nem administra.

Prova de conectividade (entrega E3, 12/11/2026): handshake MCP por stdio com o DBHub 1.6.1 e chamada da ferramenta
`execute_sql` com `SELECT @@VERSION`, que devolveu *Microsoft SQL Server 2022 (RTM-CU28) 16.0.4325.4, Developer
Edition*, conectado como `joya_mcp` ao `JoyaPedidos` com criptografia ativa. Essa prova usou um cliente MCP mínimo; a
validação pelo próprio Claude Code está abaixo.

#### Validação no Claude Code (entrega E7, 13/11/2026)

Feita numa sessão real do Claude Code (app desktop, Windows), pelo servidor `sqlserver-joya` carregado do
[.mcp.json](.mcp.json). Todas as consultas desta seção usaram **somente** as ferramentas MCP do DBHub:
`execute_sql` (SQL) e `search_objects` (catálogo). Nenhuma delas passou por `sqlcmd`, Docker ou conexão direta. A
transcrição da sessão (`~/.claude/projects/C--Projetos-joya-mini-gestao-pedidos/e0e76574-78c6-4550-8d7a-6dfecf33369a.jsonl`)
guarda cada chamada com a resposta, mas é local e não é versionada. Para o avaliador, a evidência é o clipe do vídeo
(CA-22) e as saídas reproduzidas nesta seção.

**Identidade autenticada** (`execute_sql`): `SYSTEM_USER` = `ORIGINAL_LOGIN()` = `joya_mcp`, usuário de banco
`joya_mcp`, `DB_NAME()` = `JoyaPedidos`. SQL Server 16.0.4325.4 (CU28), Developer Edition. Conexão TCP com
`encrypt_option = TRUE`, autenticação SQL, `program_name = node-mssql` (driver do DBHub).

**Permissões efetivas** (`fn_my_permissions(NULL, 'SERVER')` e `fn_my_permissions(NULL, 'DATABASE')`):

| Escopo | Permissões efetivas | Origem |
|---|---|---|
| Servidor | `CONNECT SQL`, `VIEW SERVER PERFORMANCE STATE` | concedidas pelo script |
| Servidor | `VIEW ANY DATABASE` | padrão do papel `public` |
| Banco | `CONNECT`, `SELECT` | usuário + `db_datareader` |
| Banco | `VIEW DATABASE PERFORMANCE STATE` | coberta pela permissão de servidor acima |
| Banco | `VIEW ANY COLUMN ENCRYPTION KEY DEFINITION`, `VIEW ANY COLUMN MASTER KEY DEFINITION` | padrão do papel `public` |

Papéis e concessões explícitas (`sys.database_role_members`, `sys.database_permissions`, `sys.server_permissions`,
`sys.server_role_members`): só `db_datareader`; nenhum papel de servidor. Verificações negativas, todas com resultado
`0`: os 8 papéis fixos de servidor (`sysadmin`, `serveradmin`, `securityadmin`, `dbcreator`, `bulkadmin`,
`processadmin`, `setupadmin`, `diskadmin`); `db_owner`, `db_datawriter`, `db_ddladmin`, `db_securityadmin`,
`db_accessadmin` e `db_backupoperator`; `CONTROL SERVER`, `VIEW SERVER STATE` e `ALTER ANY LOGIN`; e, no banco,
`CONTROL`, `ALTER`, `CREATE TABLE`, `INSERT`, `UPDATE`, `DELETE`, `EXECUTE` e `SHOWPLAN` (`HAS_PERMS_BY_NAME`).

**Escrita negada pelo SQL Server.** Cada tentativa foi montada para não ter efeito mesmo que a permissão existisse:
`WHERE 1 = 0` (zero linhas), `SET XACT_ABORT ON` e `ROLLBACK` explícito no mesmo lote. A tabela do `CREATE TABLE`
seria desfeita pelo rollback. O modo somente leitura do DBHub está desligado (veja abaixo), então quem negou foi o
banco, não a ferramenta.

| Operação via `execute_sql` | Erro | Mensagem do SQL Server |
|---|---|---|
| `UPDATE dbo.Produtos SET Estoque = Estoque WHERE 1 = 0` | 229 | The UPDATE permission was denied on the object 'Produtos', database 'JoyaPedidos', schema 'dbo'. |
| `INSERT INTO dbo.Clientes (Nome, Email) SELECT ... WHERE 1 = 0` | 229 | The INSERT permission was denied on the object 'Clientes', database 'JoyaPedidos', schema 'dbo'. |
| `DELETE FROM dbo.ItensPedido WHERE 1 = 0` | 229 | The DELETE permission was denied on the object 'ItensPedido', database 'JoyaPedidos', schema 'dbo'. |
| `CREATE TABLE dbo.TesteNegativoMcp (Id int NOT NULL)` | 262 | CREATE TABLE permission denied in database 'JoyaPedidos'. |

Depois das tentativas, `@@TRANCOUNT = 0`. Um retrato por tabela (linhas, `CHECKSUM_AGG(BINARY_CHECKSUM(*))` e
`IDENT_CURRENT`), mais a contagem e o checksum dos objetos de usuário, ficou **idêntico** antes e depois. A tabela
`TesteNegativoMcp` não existe.

**Consultas reais pelo MCP**:

- Estrutura: `search_objects` (tabelas de `dbo` com contagem de linhas) e `sys.columns` + `sys.types` (tipos,
  nulabilidade, identity) das 4 tabelas; chaves, FKs e CHECKs em `sys.key_constraints`, `sys.foreign_keys` e
  `sys.check_constraints`.
- Índices (`sys.indexes`, `sys.index_columns`, `sys.dm_db_partition_stats`): `Pedidos` tem `PK_Pedidos` (clustered
  `Id`), `IX_Pedidos_DataPedido` (`DataPedido` INCLUDE `ClienteId`) e `IX_Pedidos_ClienteId_DataPedido`;
  `ItensPedido` tem `PK_ItensPedido` (clustered `Id`) e `UX_ItensPedido_PedidoId_ProdutoId` (único, INCLUDE
  `Quantidade, PrecoUnitario`). Cada índice ocupa 2 páginas.
- Seed: 10 clientes (1 nome homônimo), 12 produtos (1 com estoque 0, 1 com estoque entre 1 e 2, 2 com 1.000 ou mais), 33
  pedidos, 52 itens. Primeiro pedido em `2025-01-15T13:20:00Z`, último em `2026-11-01T02:59:59Z` (31/10/2026 local).
  Os 2 pedidos de fronteira estão presentes, nenhum pedido fica sem itens e 3 itens têm preço diferente do atual
  (preço congelado). Tudo confere com o registro da E3.
- Pedidos por período (01/01 a 31/10/2026 local, intervalo UTC semiaberto `[2026-01-01T03:00Z, 2026-11-01T03:00Z)`):
  18 pedidos de 10 clientes, faturamento 5.513,57, de `2026-01-01T03:00:00Z` a `2026-11-01T02:59:59Z`.
- DMVs: `sys.dm_exec_query_stats` + `sys.dm_exec_sql_text` + `sys.dm_exec_plan_attributes` +
  `sys.dm_exec_query_plan` (leituras lógicas, CPU e operadores do plano em cache), `sys.dm_db_index_usage_stats` e
  `sys.dm_db_missing_index_*` (vazia).

**Validação da senha do login: problema encontrado e corrigido.** A primeira versão de
`database/dev/criar-login-mcp.sql` rejeitava toda senha com `_` ou `-`, embora a mensagem e a documentação dissessem
que esses caracteres eram permitidos. A causa foi medida no próprio SQL Server, com a `COLLATE Latin1_General_BIN2`
que o script usa: dentro de `[...]` no `LIKE`, a sequência `_-]` é lida como uma **faixa** de `_` (0x5F) a `]`
(0x5D). Por código de caractere, essa faixa é invertida e vazia, então `_` e `-` ficavam fora do conjunto permitido
(`'_' LIKE '[_-]'` e `'-' LIKE '[_-]'` não casam; `'-' LIKE '[-_]'` casa). Nas collations não binárias, a
comparação segue outra ordem: com a collation do banco e com `Latin1_General_CI_AS`, os dois casam. A
correção escapa os dois caracteres: `LIKE N'%[^A-Za-z0-9\_\-]%' ESCAPE N'\'`. A política não foi afrouxada: continuam
exigidos 8 ou mais caracteres, só letras ASCII, dígitos, `_` e `-`, e `CHECK_POLICY = ON` no login. Testes feitos só
com valores sintéticos, nunca com a senha real:

- Expressão via `execute_sql`, regra antiga × nova, em 28 casos: a nova aceita os 6 válidos (letras e dígitos; `_`;
  `-`; os dois juntos; `-` no início; `_` no fim) e rejeita os 22 proibidos (7 caracteres; vazia; espaço, apóstrofo,
  `@ : / ? # % \ ] [ ^ . + $ (` e crase; tab; `é` e `ç`). A antiga rejeitava os 5 válidos com `_` ou `-`.
- De ponta a ponta com o `sqlcmd` do container, executando **só o primeiro lote** do arquivo (a validação; o teste
  aborta se o lote tiver `CREATE`/`ALTER LOGIN`, `CREATE USER`, `ALTER ROLE`, `GRANT` ou `USE`): `Abcdef12`,
  `Abc_def12`, `Abc-def12` e `-Ab_c-De12` aceitas (exit 0); `Abcde12`, espaço, `@`, `:`, `\` e `]` rejeitadas com
  `Msg 50001` (exit 1); apóstrofo falha antes, com erro de sintaxe (`Msg 102`, exit 1), sem executar nada. A versão
  anterior (HEAD) rejeitava `Abc_def12` e `Abc-def12` com `Msg 50001`.

**Decisões da configuração** (revisadas na E7): DBHub 1.6.1 é a versão mais recente no npm e a instalada no cache
do `npx`. Os três advisories citados acima foram corrigidos até a 0.22.6; em 13/11/2026 não há advisory novo para
o pacote. Transporte só stdio (processo confirmado com `--transport stdio`, sem porta HTTP). A senha fica só em
`env` (a DSN); a linha de comando do processo não a contém. O **modo somente leitura do DBHub não foi ativado**: a
1.6.1 removeu a opção `--readonly` e só aceita `readonly = true` num arquivo de configuração TOML, que também
precisaria dos dados de conexão. A garantia continua sendo o banco, como o plano prevê.

**O que o MCP mudou no trabalho** (insumos para a E8):

- O DBHub 1.6.1 devolve **vários result sets e as mensagens informativas**: `SET STATISTICS IO ON` via `execute_sql`
  retornou `Table 'Pedidos'. Scan count 1, logical reads 2...` (mensagem 3615) no campo `messages`. Assim, as
  leituras lógicas também podem ser coletadas pelo MCP, não só pelas DMVs.
- O plano em cache é legível pelo MCP sem conceder `SHOWPLAN`: `VIEW SERVER PERFORMANCE STATE` basta para
  `sys.dm_exec_query_plan`. Nenhuma permissão nova é necessária para a E8.
- Para consultas ad hoc ou parametrizadas, `sys.dm_exec_sql_text(...).dbid` vem `NULL`. Para filtrar por banco, use
  `sys.dm_exec_plan_attributes(plan_handle)` com `attribute = 'dbid'`.
- Com variáveis locais (`DECLARE`), o otimizador estimou 5,4 linhas para um período que tinha 18; a API envia
  parâmetros (`sp_executesql`), cujos valores entram na estimativa. A medição da E8 deve usar a forma parametrizada.
- Na consulta de período, o plano usou `Index Seek` em `IX_Pedidos_DataPedido`; a contagem usou `Index Scan` em
  `IX_Pedidos_ClienteId_DataPedido`. Com 2 páginas por índice, esses planos não dizem nada sobre desempenho: a E8
  precisa da massa de dados.
- Catálogos (`Latin1_General_CI_AS_KS_WS`) e banco (`SQL_Latin1_General_CP1_CI_AS`) têm collations diferentes:
  `UNION` de nomes exige `COLLATE DATABASE_DEFAULT`.
- O `joya_mcp` não vê as definições de `CHECK` (`definition` vem `NULL`, sem `VIEW DEFINITION`). A fonte do esquema
  continua sendo `database/`.

**Riscos residuais**: o `sslmode=require` criptografa, mas o certificado autoassinado do container não é validado
(aceitável no loopback de desenvolvimento); dependências transitivas do DBHub não fixadas e `.env` carregado pelo
DBHub (riscos já aceitos acima); o script de login insere a senha no SQL por substituição do `sqlcmd` antes de
validá-la (um apóstrofo gera erro de sintaxe e o script para; quem roda o script já é o `sa` local); as respostas do
MCP, inclusive mensagens de erro do servidor, passam pelo contexto do modelo. `VIEW SERVER PERFORMANCE STATE`, exigida
pela §2.3.1, vale para a **instância inteira**: o `joya_mcp` vê texto e plano das consultas em cache de todos os bancos
(131 planos de 3 bancos na E7), não só do `JoyaPedidos`. É aceitável num container exclusivo deste projeto, mas
seria vazamento de informação numa instância compartilhada.

## Parte 4: debug da query de ranking

Entrega E8 (13/11/2026). Arquivos: [ranking-2026-original.sql](database/consultas/ranking-2026-original.sql),
[ranking-2026-corrigida.sql](database/consultas/ranking-2026-corrigida.sql), [gerar-massa.sql](database/performance/gerar-massa.sql),
[medir-ranking.sql](database/performance/medir-ranking.sql) e as evidências em [docs/evidencias/parte4](docs/evidencias/parte4).

### Query original (literal do enunciado)

```sql
SELECT c.Nome, SUM(i.Quantidade * i.PrecoUnitario) AS Total
FROM Clientes c
LEFT JOIN Pedidos p ON p.ClienteId = c.Id
LEFT JOIN ItensPedido i ON i.PedidoId = p.Id
WHERE YEAR(p.DataPedido) = 2026
GROUP BY c.Nome
ORDER BY Total
```

O arquivo é idêntico a esse texto; um teste compara o arquivo com o bloco da spec §9.1 (CA-12).

### O que estava errado (D1–D6)

| # | Tipo | Defeito | Efeito | Comprovação |
|---|---|---|---|---|
| D1 | Funcional | `ORDER BY Total` é crescente. | Lista primeiro quem **menos** comprou. | IT-24: totais em ordem crescente, a primeira linha com 400,00. |
| D2 | Funcional | `GROUP BY c.Nome`. | Homônimos viram uma linha só, com os totais somados; sem `Id`, não identifica o cliente. | IT-24: uma "Ana Souza" com 550,00 (300,00 + 250,00). No seed, via MCP: 1.952,35. |
| D3 | Funcional / clareza | `LEFT JOIN Pedidos` seguido de `WHERE` sobre `p.DataPedido`. | O `WHERE` descarta quem não tem pedido: a junção externa devolve o mesmo que uma interna, e o texto sugere uma intenção que não se cumpre. Pelo `LEFT JOIN ItensPedido`, pedido sem item daria `Total` nulo, que vem primeiro. | Plano real (ver abaixo): com `YEAR()`, a junção com `Pedidos` continua `Left Outer Join` e o filtro só vem depois dela. |
| D4 | Funcional | `YEAR(p.DataPedido)` sobre instantes UTC. | Usa o ano UTC, não o ano civil de São Paulo: entram os pedidos de 31/12/2025 21:00–23:59 e saem os de 31/12/2026 21:00–23:59 (horário local). | IT-24: Fábio (31/12/2025 23:59:59 local) entra com 1.000,00 e Elisa (31/12/2026 23:59:59 local) sai. Na massa: 2026 local = 39.982 pedidos; 2026 UTC = 39.983. |
| D5 | Determinismo | Sem desempate nem posição. | Ordem de empates instável. | IT-24 não verifica o nome nas duas primeiras linhas (Carla e Diego empatam em 400,00). |
| D6 | Desempenho (hipótese) | Função sobre a coluna (`YEAR(coluna)`). | Predicado não *SARGable*: impede a busca por intervalo em índice de `DataPedido`. | **Confirmada** na massa: `Index Scan` de todos os 120.033 pedidos + `Filter`, contra `Index Seek` de 39.983 na V1. |

**Observação sobre o D3: semântica relacional × plano físico.** Na semântica relacional, o `LEFT JOIN Pedidos`
seguido de `WHERE` sobre `p.DataPedido` equivale a uma junção interna: o `WHERE` descarta as linhas sem pedido, e o
resultado é o mesmo. A spec (§9.2) acrescenta que o otimizador faz essa simplificação. **Nos planos físicos
observados, ele não faz**:
- Com `YEAR()`, o plano real mantém `Left Outer Join` com `Pedidos` e aplica o `Filter` depois: na V0 paralela
  (`Hash Match`), na V0 serial (`Merge Join`) e no seed (`Nested Loops`, via MCP).
- Só trocando o predicado por intervalo (V1) a junção vira `Inner Join`.
- Evidências: [v0-original.sqlplan](docs/evidencias/parte4/planos/v0-original.sqlplan),
  [v0-maxdop-1-diagnostico.sqlplan](docs/evidencias/parte4/planos/v0-maxdop-1-diagnostico.sqlplan),
  [v1-original-com-intervalo.sqlplan](docs/evidencias/parte4/planos/v1-original-com-intervalo.sqlplan) e
  [mcp-banco-desenvolvimento.md](docs/evidencias/parte4/mcp-banco-desenvolvimento.md).

O efeito no resultado é o que a spec descreve; só a afirmação sobre o otimizador não vale para esta query. A
divergência foi **aceita pelo candidato** em 13/11/2026 e está registrada no plano (§14, E8), sem alterar a spec.

### Query corrigida

```sql
-- Ranking de clientes por valor comprado em 2026 (ano civil em America/Sao_Paulo).
-- DataPedido é armazenado em UTC. São Paulo = UTC-3 (sem horário de verão desde 2019):
--   01/01/2026 00:00 local = 2026-01-01 03:00 UTC (início, inclusivo)
--   01/01/2027 00:00 local = 2027-01-01 03:00 UTC (fim, exclusivo)
SELECT
    ROW_NUMBER() OVER (ORDER BY SUM(i.Quantidade * i.PrecoUnitario) DESC, c.Id) AS Posicao,
    c.Id   AS ClienteId,
    c.Nome,
    SUM(i.Quantidade * i.PrecoUnitario) AS Total
FROM Pedidos p
INNER JOIN Clientes c    ON c.Id = p.ClienteId
INNER JOIN ItensPedido i ON i.PedidoId = p.Id
WHERE p.DataPedido >= '2026-01-01T03:00:00'
  AND p.DataPedido <  '2027-01-01T03:00:00'
GROUP BY c.Id, c.Nome
ORDER BY Total DESC, c.Id;
```

D1: `DESC`. D2: `GROUP BY c.Id, c.Nome` e `ClienteId` no resultado. D3: `INNER JOIN` explícito. D4: limites UTC do
ano civil local, em intervalo semiaberto. D5: desempate por `c.Id` e `Posicao` por `ROW_NUMBER()`. D6: predicado
sobre a coluna sem função. Ranking completo (sem `TOP`), batch único sem `GO`; o total é `DECIMAL(38,2)`, com as 2
casas preservadas. O endpoint `GET /relatorios/top-clientes` usa a mesma lógica com `TOP (5)` e parâmetros (§9.4).

### Correção funcional (DR-RANK)

`RankingSqlTestes` executa os dois arquivos **sem alteração**, via SqlClient, sobre o DR-RANK da spec §9.5 em banco
de teste limpo (Testcontainers; a fixture da E6 não mudou).

| Posição | Cliente | Total (V2, IT-23) |
|---|---|---|
| 1 | Bruno Lima | 500,00 (o pedido de 01/01/2027 00:00 local, de 999,00, fica fora) |
| 2 | Elisa Rocha | 450,00 (31/12/2026 23:59:59 local, dentro) |
| 3 | Carla Dias | 400,00 |
| 4 | Diego Alves | 400,00 (01/01/2026 00:00:00 local, dentro; empate com Carla, que tem `Id` menor) |
| 5 | Ana Souza (ana1) | 300,00 (2 itens: 2 × 100,00 + 1 × 100,00) |
| 6 | Ana Souza (ana2) | 250,00 |

Fábio (31/12/2025 local) e o cliente sem pedidos ficam fora. Trocar o preço atual de todos os produtos para 9.999,99
não muda nada: o total usa o preço gravado no item. A V0 sobre o mesmo DR-RANK (IT-24) devolve 5 linhas só com
`Nome` e `Total`, nesta ordem de totais: 400,00, 400,00, 500,00, 550,00 ("Ana Souza") e 1.000,00 (Fábio).

### Ambiente isolado e massa

A massa **nunca** foi carregada no banco de desenvolvimento. Ela roda num projeto Compose separado, `joya-perf`
([docker-compose.perf.yml](docker-compose.perf.yml)): containers, rede e volume (`joya-perf_dados-sql`) próprios,
mesma imagem e mesmos scripts, sem porta no host, teto de 2.048 MB de memória e hostname fixo `joya-perf-sqlserver`:

```bash
docker compose -p joya-perf -f docker-compose.yml -f docker-compose.perf.yml up -d sqlserver db-init
```

```bash
docker compose -p joya-perf -f docker-compose.yml -f docker-compose.perf.yml run --rm --no-deps -T db-init /opt/mssql-tools18/bin/sqlcmd -C -b -f 65001 -S sqlserver -U sa -i /database/performance/gerar-massa.sql
```

```bash
docker compose -p joya-perf -f docker-compose.yml -f docker-compose.perf.yml run --rm --no-deps -T db-init /opt/mssql-tools18/bin/sqlcmd -C -b -f 65001 -y 0 -S sqlserver -U sa -d JoyaPedidos -i /database/performance/medir-ranking.sql
```

```bash
docker compose -p joya-perf -f docker-compose.yml -f docker-compose.perf.yml down -v
```

No Git Bash, prefixe `MSYS_NO_PATHCONV=1` nos comandos `run`. A saída da medição é grande (resultados e XML dos
planos): redirecione-a para um arquivo fora do repositório. O `down -v` apaga só o volume `joya-perf_dados-sql`.

**Travas.** Os dois scripts conferem, dentro do SQL Server, se `SERVERPROPERTY('MachineName')` é
`joya-perf-sqlserver`, hostname que só o `docker-compose.perf.yml` define. Em qualquer outra instância, inclusive a de
desenvolvimento, eles param com erro antes de tocar em dados. `gerar-massa.sql` é idempotente (§4.6): se a massa já
existe, não faz nada e sai com código 0. `medir-ranking.sql` remove e recria `IX_Pedidos_DataPedido` só nesse banco,
e pode ser executado de novo mesmo depois de abortar no meio: ele recria o índice se o encontrar ausente.

**Massa**: determinística e set-based, sobre uma tabela de números; sem `WHILE`, `NEWID()` ou `RAND()`. Leva 20 s e
se soma ao seed. Gerada duas vezes, em instâncias distintas, deu dados idênticos: mesmos `CHECKSUM_AGG` nas 4 tabelas
e mesma soma de `Quantidade × PrecoUnitario`.

| Item | Valor |
|---|---|
| Clientes | 1.210 (600 nomes da massa usados por 2 clientes cada; 50 sem pedidos) |
| Produtos | 262 |
| Pedidos | 120.033, de `2024-01-01T03:00:00Z` a `2027-01-01T02:54:51Z`; por ano local: 40.073 (2024), 39.978 (2025), 39.982 (2026) |
| Itens | 360.052 (3,0 por pedido; 1 a 5) |
| Páginas (índices reconstruídos após a carga) | `PK_Pedidos` 344, `IX_Pedidos_DataPedido` 300, `IX_Pedidos_ClienteId_DataPedido` 300, `PK_ItensPedido` 1.547, `UX_ItensPedido_PedidoId_ProdutoId` 1.410, `PK_Clientes` 17; ~31 MB no total |
| Estatísticas | `UPDATE STATISTICS ... WITH FULLSCAN` nas 4 tabelas |
| Ambiente | SQL Server 2022 CU28 Developer (compatibilidade 160), 12 CPUs visíveis, 2.048 MB, MAXDOP 0, limiar de paralelismo 5; Docker Desktop (WSL 2) num notebook i7-1255U |

### Protocolo de medição

[medir-ranking.sql](database/performance/medir-ranking.sql) inclui cada variante pelo próprio arquivo (`:r`), sem
alteração, e aplica a cada uma o [medir-variante.sql](database/performance/medir-variante.sql):
- 1 execução de aquecimento, descartada;
- 3 rodadas medidas com `SET STATISTICS IO, TIME ON`;
- 1 execução separada com `SET STATISTICS XML ON`, para o plano real (fora da medição de tempo).

O cache de dados fica aquecido: nenhum `DBCC DROPCLEANBUFFERS`/`FREEPROCCACHE`. O tempo de cada rodada é o bloco
`SQL Server Execution Times` da instrução; em `sp_executesql`, é o bloco externo.

**Condições controladas**, aplicadas só no banco isolado:
- Query Store desligado.
- Desligados os feedbacks que ajustam concessão de memória e cardinalidade entre execuções
  (`ROW_MODE_`/`BATCH_MODE_MEMORY_GRANT_FEEDBACK`, `MEMORY_GRANT_FEEDBACK_PERCENTILE_GRANT`,
  `MEMORY_GRANT_FEEDBACK_PERSISTENCE` e `CE_FEEDBACK`).

Assim, cada passada é independente e cada plano reflete só a escolha do otimizador. Esse controle entrou depois da
revisão independente. A primeira rodada usava as configurações padrão do SQL Server 2022, e nela todos os planos
vinham com a memória ajustada pelo feedback (`IsMemoryGrantFeedbackAdjusted="Yes"`): as passadas herdavam o ajuste
umas das outras. Dois planos dessa rodada estão em `planos/rodada-inicial-com-feedback`.

O roteiro inteiro rodou **5 vezes** (passadas A a E). Cada passada dá a mediana das suas 3 rodadas, e as tabelas
mostram a mediana das 5 medianas e a faixa entre elas. As leituras lógicas foram idênticas nas 5 passadas. O resumo é
calculado por [resumir-medicao.ps1](database/performance/resumir-medicao.ps1) a partir dos logs versionados em
[docs/evidencias/parte4](docs/evidencias/parte4), junto com o `consolidado.txt` e os planos reais (`.sqlplan`).

### Medições (massa de 120.033 pedidos, cache de dados aquecido)

| Variante | Leituras lógicas `Pedidos` / `ItensPedido` / `Clientes` (total) | CPU ms, mediana (faixa) | Decorrido ms, mediana (faixa) | Plano | Linhas devolvidas |
|---|---|---|---|---|---|
| **V0** original | 895 / 1.475 / 46 (2.416) | 963 (806–1.346) | 198 (165–284) | paralelo (DOP 12), row mode | 604 (por nome) |
| V1 = V0 só com o predicado em intervalo | 102 / 1.475 / 46 (1.623) | 613 (547–909) | 103 (97–163) | paralelo (DOP 12), row mode | 604 |
| V0 com `OPTION (MAXDOP 1)` (diagnóstico) | 300 / 1.398 / 17 (1.715) | 247 (223–339) | 248 (232–346) | serial, row mode | 604 |
| V1 com `OPTION (MAXDOP 1)` (diagnóstico) | 102 / 1.398 / 17 (1.517) | 157 (148–194) | 157 (152–198) | serial, row mode | 604 |
| **V2** corrigida | 102 / 1.398 / 17 (1.517) | 54 (50–89) | 60 (56–91) | serial, batch mode (exceto o seek) | 1.160 (por cliente) |
| **V2 sem `IX_Pedidos_DataPedido`** | 344 / 1.398 / 17 (1.759) | 86 (83–88) | 90 (89–92) | serial; `Merge Join` e varredura de `Pedidos` em row mode | 1.160 |
| V0 sem `IX_Pedidos_DataPedido` | 895 / 1.475 / 46 (2.416) | 912 (825–978) | 188 (154–204) | igual à V0 | 604 |

**V0 e V2 não devolvem o mesmo resultado**: a V0 está funcionalmente errada (agrupa por nome, usa o ano UTC, ordena
ao contrário). A comparação é de **custo**, não de equivalência. As variantes com `MAXDOP 1` não são a query
literal: servem só para separar o efeito do paralelismo.

### Variante parametrizada (forma da aplicação)

Mesma V2, mudando só a forma dos limites. "Estimado / real" é o `Index Seek` em `IX_Pedidos_DataPedido`.

| Variante | Busca em `IX_Pedidos_DataPedido` | Estimado / real (pedidos) | Junção com itens, estimado / real | CPU ms (faixa) | Decorrido ms (faixa) |
|---|---|---|---|---|---|
| V2 com literais (`'2026-01-01T03:00:00'`) | dinâmica (`GetRangeWithMismatchedTypes`, `Merge Interval`) | 39.975,1 / 39.982 | 119.910 / 119.924 | 54 (50–89) | 60 (56–91) |
| `sp_executesql`, `DATETIME2(0)` (tipo da coluna) | direta | 39.975,1 / 39.982 | 119.910 / 119.924 | 61 (55–88) | 63 (61–94) |
| `sp_executesql`, `DATETIME2(7)` (como o SqlClient envia) | dinâmica (`GetRangeWithMismatchedTypes`) | 39.975,1 / 39.982 | 119.910 / 119.924 | 60 (57–61) | 64 (60–69) |
| variáveis locais (`DECLARE`) | direta | **19.723,4** / 39.982 | **59.162,6** / 119.924 | 54 (49–57) | 61 (54–64) |
| SQL da API (`TOP (5)`, `DATETIME2(7)`) | dinâmica | 39.975,1 / 39.982 | 119.910 / 119.924 (`Adaptive Join`) | 56 (50–63) | 60 (54–65) |
| `DATETIME2(7)` sem `IX_Pedidos_DataPedido` | (sem índice: `Clustered Index Scan`) | 39.975,2 / 39.982 | 119.910 / 119.924 | 99 (91–104) | 101 (92–110) |

- Literais e parâmetros (com *parameter sniffing*) estimam o intervalo quase exatamente.
- Com variáveis locais, o otimizador não conhece os valores e usa uma seletividade fixa para as duas desigualdades:
  0,3 × √0,3 ≈ 16,4%, ou 19.723 de 120.033 linhas. Ser metade do real é coincidência desta massa. O formato do
  plano é o mesmo da forma com `DATETIME2(0)` (mesmos operadores e índices), e a concessão de memória é menor
  (77.512 contra 88.192 KB). O tempo fica dentro do ruído.
- `DATETIME2(7)` contra a coluna `DATETIME2(0)` gera a busca dinâmica (`GetRangeWithMismatchedTypes`, com
  `Nested Loops`/`Merge Interval` sobre `Constant Scan`, em row mode). O literal em texto também gera. Continua um
  `Index Seek` *SARGable*, com a mesma estimativa.
- `DATETIME2(0)` faz a busca direta, com o plano todo em batch mode. As faixas de tempo das duas se sobrepõem:
  **nenhuma diferença mensurável**, então a API não mudou.
- O SQL da API agrega por `p.ClienteId`, ordena o top 5 e só então busca os 5 clientes (`Clientes`: 10 leituras
  contra 17). Usa um `Adaptive Join` que, nesta massa, executou como hash join. O custo fica na faixa da V2.
- As variantes V2 ficam entre 54 e 61 ms de CPU, com faixas sobrepostas; nenhuma é declarada mais rápida.

### Planos de execução (operadores principais)

- **V0** (paralelo, DOP 12, row mode; memória concedida 56.640 KB, usada no máximo 17.280 KB):
  - `Index Scan` em `IX_Pedidos_ClienteId_DataPedido` lendo os **120.033** pedidos. É o índice coberto mais estreito;
    `IX_Pedidos_DataPedido` não serve para `YEAR()`.
  - `Hash Match (Left Outer Join)` com `Clientes`: 120.083 linhas, porque os 50 clientes sem pedido ainda estão lá.
  - `Compute Scalar` de `YEAR` → `Filter [Expr1005] = 2026` (est. 40.011 / real 39.983) → `Bitmap`.
  - `Hash Match (Left Outer Join)` com `Index Scan` em `UX_ItensPedido_PedidoId_ProdutoId`.
  - Agregação parcial → `Sort` + `Stream Aggregate` por `Nome` (est. 335,6 / real 604 grupos) → `Sort` pelo total.
- **V0 serial**: `Merge Join (Left Outer Join)` entre `Clientes` e `IX_Pedidos_ClienteId_DataPedido` (300 páginas,
  contra 895 leituras no paralelo) → `Filter` → `Hash Match (Left Outer Join)` com os itens → `Hash Match (Aggregate)`
  por nome. Continua `Left Outer Join` + `Filter`, também sem paralelismo.
- **V1**: muda só o acesso a `Pedidos`, que vira `Index Seek` em `IX_Pedidos_DataPedido` (39.983 linhas lidas), e a
  primeira junção passa a `Inner Join`. O resto segue a V0 (row mode, agrupamento por nome).
- **V2** (serial; junções, agregação, `Sort` e `Window Aggregate` em batch mode; o seek e a busca dinâmica em row
  mode):
  - `Index Seek` em `IX_Pedidos_DataPedido` (39.982).
  - `Hash Match (Inner Join)` com `Index Scan` em `UX_ItensPedido_PedidoId_ProdutoId`: lê as 360.052 linhas, e o
    filtro *bitmap* reduz a saída a 119.924.
  - `Hash Match (Aggregate)` por cliente (1.160) → `Hash Match (Inner Join)` com `Clustered Index Scan` em
    `PK_Clientes` → `Sort` → `Window Aggregate` (`ROW_NUMBER`).
  - Em `Pedidos`, nas junções e na agregação, a estimativa fica a menos de 0,1% do real. A exceção é a varredura de
    `ItensPedido`: estima 360.052 linhas e entrega 119.924, porque o *bitmap* é aplicado durante a leitura.
- **V2 sem `IX_Pedidos_DataPedido`**: `Clustered Index Scan` em `PK_Pedidos` (120.033 lidas, 39.982 passam pelo
  predicado) → `Merge Join` com `Index Scan` em `UX_ItensPedido_PedidoId_ProdutoId` (360.052; os dois lados já vêm
  em ordem de `PedidoId`). O agregado e a ordenação são os mesmos.
- **Concessão de memória excessiva na V2** (`MemoryGrantWarning`, `Excessive Grant`): todas as formas da V2 e o SQL
  da API pedem de 71 a 88 MB e usam no máximo 1,6 a 3,1 MB. A V0 serial pede 15,6 MB e usa 3,7 MB, sem aviso. A
  causa exata não foi isolada. Com as configurações padrão do SQL Server 2022 (feedback ligado), o próprio servidor
  corrige essa concessão: na rodada inicial, a V2 recebia 6.304 KB (`IsMemoryGrantFeedbackAdjusted="Yes: Stable"`).
  Com várias execuções simultâneas antes desse ajuste, a reserva excessiva pode gerar espera por memória
  (`RESOURCE_SEMAPHORE`). Concorrência não foi medida; nada foi alterado na API.
- Nenhum plano teve `Key Lookup` nem *spill*. A única conversão implícita é a de `Quantidade` (`int`) para `decimal`
  na multiplicação, inevitável e sem efeito no acesso.

### O que os números mostram

O critério para tempo: uma diferença só é afirmada quando as faixas entre passadas não se sobrepõem, ou quando a
ordem se repete em todas as passadas. Leituras são determinísticas.

1. **D6 confirmada sem paralelismo.** Comparando V0 e V1 com `MAXDOP 1`:
   - Com `YEAR()`, `Pedidos` é lida inteira (`Index Scan` + `Filter`, 300 páginas). Trocando **só** o predicado,
     vira `Index Seek` com 102 leituras (−66% em `Pedidos`, −12% no total).
   - A CPU cai de 247 para 157 ms (−36%), com faixas que não se sobrepõem (223–339 contra 148–194).
   - Nas versões paralelas, as 895 leituras de `Pedidos` incluem o custo do próprio paralelismo: elas não medem a D6.
2. **V0 → V2, como o servidor executa cada uma**: leituras −37% (2.416 → 1.517), CPU 963 → 54 ms, decorrido
   198 → 60 ms, sem sobreposição. Grande parte da diferença de CPU é **paralelismo**: a V0 roda com DOP 12 e gasta
   ~4× mais CPU que ela mesma em série (963 contra 247 ms), terminando só um pouco antes (198 contra 248 ms).
3. **V0 → V2, ambas em série**: leituras −12% (1.715 → 1.517), CPU 247 → 54 ms (~4,6×), decorrido 248 → 60 ms (~4×).
   A V1 serial já tem as mesmas leituras da V2 (1.517) e ainda gasta 157 ms. O restante vem das diferenças de plano
   (**hipóteses**, não isoladas uma a uma): row mode contra batch mode, agrupamento por `NVARCHAR` (`Nome`) contra
   `Id` inteiro, e a junção externa com `ItensPedido`.
4. **`ItensPedido` domina o custo da V2**: 1.398 de 1.517 leituras (92%). O otimizador preferiu varrer o índice com
   filtro *bitmap* a buscar os itens pedido a pedido (seriam ~40 mil buscas; essa alternativa não foi medida).
5. **`IX_Pedidos_DataPedido`** reduz as leituras de `Pedidos` de 344 para 102 (−70%; −14% no total da V2). No tempo:
   - mediana de CPU 86 → 54 ms e decorrido 90 → 60 ms (cerca de −1/3);
   - o decorrido foi menor com o índice em todas as 5 passadas, e a CPU em 4 de 5;
   - na passada B, mais lenta para todas as V2, a faixa da V2 (50–89) encosta na da V2 sem índice (83–88).

   Ganho modesto e provável, coerente com as leituras. Com parâmetros `DATETIME2(7)`, o mesmo: CPU 99 → 60 ms.
6. Com 2026 = 1/3 dos pedidos, o otimizador ainda prefere o `Index Seek`. Com uma fração maior do período, a
   varredura pode ser legitimamente melhor (RSK-08): seek não é superior a scan por princípio.

### Decisão sobre índices: nenhuma mudança no esquema

| Índice | Evidência | Decisão |
|---|---|---|
| `IX_Pedidos_DataPedido (DataPedido) INCLUDE (ClienteId)` | Seek em V1, V2, nas formas parametrizadas e no SQL da API. Coberto, sem `Key Lookup`. −70% de leituras em `Pedidos` e tempo da V2 cerca de 1/3 menor (item 5). Custo: 300 páginas (2,3 MB por 120 mil pedidos) e 1 inserção a mais por pedido criado (`POST /pedidos` grava 1 linha em `Pedidos`). Pela spec (§8.5), também serve ao filtro de período de `GET /pedidos`, não medido aqui. | **Mantido.** |
| `IX_Pedidos_ClienteId_DataPedido (ClienteId, DataPedido)` | Não aparece em nenhum plano da V2 na massa. A V0 o varre por ser o índice coberto mais estreito. No seed pequeno, o plano da V2 (via MCP) faz seek nele por cliente. Existe para o filtro por cliente de `GET /pedidos` (§8.5), fora desta medição. | **Mantido** (não medido para a listagem). |
| `UX_ItensPedido_PedidoId_ProdutoId (PedidoId, ProdutoId) INCLUDE (Quantidade, PrecoUnitario)` | Garante a RN-04. O `INCLUDE` cobre a soma: em todas as variantes ele é lido no lugar do índice clusterizado (1.410 contra 1.547 páginas), sem `Key Lookup`. | **Mantido.** |

Nenhum índice foi criado ou alterado no esquema versionado, e o `IT-21` continua valendo sem mudança. A remoção de
`IX_Pedidos_DataPedido` foi feita só no banco isolado, e o índice foi recriado com a mesma definição. Reduzir a
varredura de `ItensPedido` exigiria outro desenho (por exemplo, columnstore ou agregados pré-calculados), sem medição
nem necessidade neste escopo.

### Limitações

- **Ambiente**: um único notebook (i7-1255U, núcleos híbridos, Docker Desktop/WSL 2), com outros processos. Tempos
  de 50 a 350 ms variam entre passadas (a V0 paralela, de 806 a 1.346 ms de CPU). As conclusões de tempo seguem o
  critério acima.
- **Cache**: só cache de dados aquecido. Os dados (~31 MB) cabem inteiros na memória. Não houve medição com cache
  frio.
- **Condições controladas**: Query Store e feedbacks desligados para medir. Com as configurações padrão, a
  concessão de memória da V2 é corrigida automaticamente (ver acima) e os planos podem mudar entre execuções.
- **Massa**: sintética e uniforme (2026 = 1/3 dos pedidos). Outras distribuições e volumes podem mudar as escolhas
  do otimizador.
- **Batch mode sobre rowstore**: depende do nível de compatibilidade (150+) e da edição (Developer aqui). Em outro
  ambiente, o plano da V2 pode ser diferente.
- **Planos reais**: capturados numa execução separada, porque `STATISTICS XML` tem custo próprio.
- **MCP**: aponta para o banco de desenvolvimento (seed de 33 pedidos). As medições da massa foram feitas por
  `sqlcmd` no banco isolado, e os números do seed nunca foram misturados aos da massa.

### Como o MCP foi usado na investigação

- **Na E7**, pelo MCP: o DBHub repassa as mensagens de `STATISTICS IO`; o plano em cache é legível sem `SHOWPLAN`;
  `dm_exec_sql_text.dbid` vem nulo em consultas ad hoc (filtrar por `dm_exec_plan_attributes`); variáveis locais
  distorcem a estimativa. Esses achados definiram o protocolo: a forma parametrizada e a de variáveis locais
  entraram como variantes medidas.
- **Na E8**, com `execute_sql` no banco de desenvolvimento:
  - V0 e V2 executadas com `SET STATISTICS IO ON`, com as leituras devolvidas em `messages`. Os defeitos aparecem
    também no seed: "Ana Souza" com 1.952,35 na V0, e Elisa com 509,67 na V0 contra 159,77 na V2.
  - Operadores do plano em cache lidos por XQuery sobre `sys.dm_exec_query_plan`, cruzando
    `sys.dm_exec_query_stats` e `sys.dm_exec_plan_attributes` (`dbid`).
  - Estatísticas por `sys.stats` com `sys.dm_db_stats_properties`, e índices por `sys.indexes`.
- **Achados do MCP**:
  - No seed, a V2 usa outro plano (`Nested Loops` com seek em `IX_Pedidos_ClienteId_DataPedido` por cliente). Os
    planos do banco pequeno não representam o volume, o que justificou a massa.
  - A V0 também mantém `Left Outer Join` + `Filter` no seed, o mesmo que se viu na massa.

## Limitações conhecidas

_A preencher na entrega E13._
