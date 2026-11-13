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
   minúsculas e dígitos). Ela nunca vai para o repositório.
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

Riscos aceitos (ambiente de desenvolvimento): o `npx` fixa a versão do DBHub, mas não as dependências transitivas
dele (sem lockfile publicado); e o DBHub também carrega um `.env` da pasta atual, o que coloca a senha do `sa` de
desenvolvimento no ambiente do processo do MCP, mesmo sem usá-la. A proteção efetiva é o login `joya_mcp`, que não
escreve nem administra.

Prova de conectividade (entrega E3, 12/11/2026): handshake MCP por stdio com o DBHub 1.6.1 e chamada da ferramenta
`execute_sql` com `SELECT @@VERSION`, que devolveu *Microsoft SQL Server 2022 (RTM-CU28) 16.0.4325.4, Developer
Edition*, conectado como `joya_mcp` ao `JoyaPedidos` com criptografia ativa. A validação completa (permissões
efetivas, escrita negada, DMVs) e o uso pelo Claude Code ficam para a entrega E7.

## Parte 4: debug da query de ranking

_A preencher na entrega E8: o que estava errado, como foi investigado (plano de execução, índices), a query
corrigida e as medições obtidas._

## Limitações conhecidas

_A preencher na entrega E13._
