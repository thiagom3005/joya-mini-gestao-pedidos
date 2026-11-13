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

Requisitos: Docker com Compose v2 e containers Linux x64. Nenhum `.env` é necessário.

> Estado atual (entrega E3): o compose sobe **apenas o banco**. A API e o frontend entram nas entregas E9 e E10.

```bash
docker compose up -d sqlserver db-init
```

- `sqlserver`: SQL Server 2022 CU28 (`mcr.microsoft.com/mssql/server:2022-CU28-ubuntu-22.04`), edição Developer,
  volume `joya-pedidos_dados-sql`, **sem porta publicada no host**.
- `db-init`: espera o banco ficar saudável, aplica `database/01..03*.sql` em ordem e encerra com código 0. Rodar de
  novo não duplica nada.
- Confira com `docker compose ps -a`: `db-init` deve estar `Exited (0)`.

A senha padrão do `sa` (`Joya_Dev_2026_Inseguro`) é **pública, insegura e exclusiva de desenvolvimento local**. Para
trocá-la, copie `.env.example` para `.env` e defina `MSSQL_SA_PASSWORD`.

Para acessar o banco pelo host (MCP, SSMS, Azure Data Studio), use o arquivo opt-in que publica a porta só em
`127.0.0.1`:

```bash
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d sqlserver db-init
```

Reset completo (apaga apenas o volume deste projeto e recarrega os dados de exemplo):

```bash
docker compose down -v
```

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

_A preencher na entrega E8: o que estava errado, como foi investigado (plano de execução, índices), a query
corrigida e as medições obtidas._

## Limitações conhecidas

_A preencher na entrega E13._
