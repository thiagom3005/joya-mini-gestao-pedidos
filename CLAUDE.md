# CLAUDE.md — Mini Gestão de Pedidos (Joya Solutions)

Teste técnico Full Stack: cadastro e consulta de pedidos de clientes (SQL Server, API .NET 10, React + TypeScript),
desenvolvido com Claude Code em Spec-Driven Development. O processo de engenharia é parte da avaliação.

## Fontes de verdade

- `docs/spec.md` — especificação **aprovada**. Regras, contratos HTTP, modelo de dados, testes e critérios de aceite.
  Não altere sem aprovação explícita do candidato. Divergência encontrada → registrar e perguntar.
- `docs/plan.md` — plano **aprovado**, com entregas E0–E13. Execute **somente a entrega pedida**; não avance sozinho.
  Ao fechar uma entrega, atualize a §14 "Registro de execução" apenas com o que de fato foi feito e verificado.
- Em conflito entre este arquivo e a spec, a spec vence.

## Arquitetura (spec §13)

- Monólito modular. Produção: `backend/src/JoyaPedidos.Api` (Minimal APIs) e `backend/src/JoyaPedidos.Dominio`
  (regras puras, sem dependências externas). Testes: `backend/tests/JoyaPedidos.TestesUnitarios` e
  `backend/tests/JoyaPedidos.TestesIntegracao`.
- Módulos da API: `Pedidos`, `Catalogo`, `Relatorios` e `Compartilhado`. Camada de serviço só em `Pedidos`.
- Banco: scripts em `database/` são a única fonte do esquema. Dapper com SQL explícito; sem EF.
- Frontend: `frontend/` (Vite + React + TS strict), página única, sem router e sem biblioteca de estado.
- Sem microsserviços, mensageria, CQRS, interfaces de repositório ou abstrações sem uso real.

## Convenções

- **Português do Brasil** em código autoral, nomes, testes, mensagens, documentação e commits. Exceções: termos
  técnicos, bibliotecas e contratos externos.
- **Identificadores sem acento** (`PrecoUnitario`, `Catalogo`, `Relatorios`), inclusive em SQL e TypeScript.
- Sufixos: `<Modulo>Endpoints`, `Validador<Operacao>`, `Servico<Modulo>`, `Repositorio<Modulo>`, `<Alvo>Testes`.
- SOLID e Clean Code com bom senso: funções pequenas, nomes que dizem a intenção, sem código morto, sem comentários
  que repetem o código. Simplicidade vence generalidade.
- Detalhes por área ficam em `.claude/rules/` (backend, database, frontend, testes) e são carregados ao tocar cada pasta.

## Segurança

- Nunca versionar segredos. `.env` é ignorado; só `.env.example` é versionado. A senha padrão do compose é
  **insegura e exclusiva de desenvolvimento** (spec §11.4).
- Não editar `.env`, não exibir senhas em saídas, logs, commits ou argumentos de processo.
- SQL sempre parametrizado. Erros 500 nunca expõem stack trace, SQL ou detalhes internos.
- O hook `.claude/hooks/proteger-historico.mjs` é uma rede de proteção, **não** uma fronteira de segurança: siga as
  regras mesmo quando o hook não as detectar (aliases do Git, scripts e APIs de arquivo escapam da análise).
- Os agentes de `.claude/agents/` são somente leitura **por instrução**; o Bash deles não tem bloqueio técnico de
  escrita. Revise o que fizerem.

## Testes

- Toda funcionalidade vem com seus testes no mesmo commit (spec §10, plano §4).
- Unitários: regras de domínio e validadores, sem banco. Integração: endpoints contra SQL Server real (Testcontainers).
- Nunca afirmar que algo foi testado sem ter executado. Nunca remover ou enfraquecer asserção para "passar".

## Comandos disponíveis

| Objetivo | Comando |
|---|---|
| Restaurar pacotes do backend | `dotnet restore backend/JoyaPedidos.slnx` |
| Compilar o backend (0 avisos exigidos) | `dotnet build backend/JoyaPedidos.slnx` |
| Testes unitários e de integração (integração exige Docker) | `dotnet test backend/JoyaPedidos.slnx` (ou `cd backend && dotnet test`) |
| Só um projeto de teste | `dotnet test --project backend/tests/JoyaPedidos.TestesUnitarios` |
| Integração em ordem aleatória (repetibilidade) | `dotnet test --project backend/tests/JoyaPedidos.TestesIntegracao -- --seed 20261112` |
| Subir banco e API (clone limpo; API em http://localhost:5080) | `docker compose up --build -d` |
| Subir banco e API mantendo a porta do SQL no loopback (MCP) | `docker compose -f docker-compose.yml -f docker-compose.dev.yml up --build -d` |
| Saúde da API (200, `Healthy`; no PowerShell 5.1, `curl.exe`) | `curl http://localhost:5080/health` |
| Logs da API / do frontend | `docker compose logs api` / `docker compose logs web` |
| Reconstruir só a imagem / subir só a API reconstruída | `docker compose build api` / `docker compose up --build -d --no-deps api` |
| Reconstruir e subir só o frontend (com o banco no ar pelo `dev.yml`, use os dois `-f`) | `docker compose up --build -d --no-deps web` |
| Frontend no navegador / proxy da API pelo nginx | http://localhost:3000 / `curl http://localhost:3000/api/health` |
| Frontend: instalar dependências (exatas do lock) | `npm --prefix frontend ci` |
| Frontend: lint, tipos e build de produção | `npm --prefix frontend run lint` / `npm --prefix frontend run typecheck` / `npm --prefix frontend run build` |
| Frontend: servidor de desenvolvimento (http://localhost:5173, proxy `/api` → `localhost:5080`) | `npm --prefix frontend run dev` |
| Frontend: dependências de produção vulneráveis | `npm --prefix frontend audit --omit=dev` |
| Subir só o banco e aplicar os scripts | `docker compose up -d sqlserver db-init` |
| Conferir (db-init deve estar `Exited (0)`) | `docker compose ps -a` |
| Banco acessível no host (só 127.0.0.1) | `docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d sqlserver db-init` |
| Reaplicar os scripts (idempotente) | `docker compose run --rm --no-deps db-init` |
| Script manual / login do MCP (Git Bash: prefixe `MSYS_NO_PATHCONV=1`) | `docker compose run --rm --no-deps -e JOYA_MCP_SENHA db-init /opt/mssql-tools18/bin/sqlcmd -C -b -f 65001 -S sqlserver -U sa -i /database/dev/criar-login-mcp.sql` |
| Parar mantendo os dados | `docker compose down` |
| Reset completo do banco deste projeto (apaga só `joya-pedidos_dados-sql`) | `docker compose down -v` |
| Parte 4: subir o ambiente ISOLADO de desempenho (projeto `joya-perf`) | `docker compose -p joya-perf -f docker-compose.yml -f docker-compose.perf.yml up -d sqlserver db-init` |
| Parte 4: gerar a massa (só no `joya-perf`; Git Bash: `MSYS_NO_PATHCONV=1`) | `docker compose -p joya-perf -f docker-compose.yml -f docker-compose.perf.yml run --rm --no-deps -T db-init /opt/mssql-tools18/bin/sqlcmd -C -b -f 65001 -S sqlserver -U sa -i /database/performance/gerar-massa.sql` |
| Parte 4: medir o ranking (só no `joya-perf`; saída grande, redirecione para fora do repositório) | `docker compose -p joya-perf -f docker-compose.yml -f docker-compose.perf.yml run --rm --no-deps -T db-init /opt/mssql-tools18/bin/sqlcmd -C -b -f 65001 -y 0 -S sqlserver -U sa -d JoyaPedidos -i /database/performance/medir-ranking.sql` |
| Parte 4: remover o ambiente de desempenho (apaga só `joya-perf_dados-sql`) | `docker compose -p joya-perf -f docker-compose.yml -f docker-compose.perf.yml down -v` |
| Pacotes vulneráveis, inclusive transitivos | `dotnet list backend/JoyaPedidos.slnx package --vulnerable --include-transitive` |
| SDK e runtimes desatualizados | `dotnet sdk check` |
| Verificar os hooks do Claude (qualquer SO com Node) | `node .claude/hooks/verificar-hooks.mjs` |
| Mesma verificação em Linux via Docker, no Git Bash | `MSYS_NO_PATHCONV=1 docker run --rm -v "$PWD":/w:ro -w /w node:24-alpine node .claude/hooks/verificar-hooks.mjs` |
| Mesma verificação em Linux via Docker, no PowerShell | `docker run --rm -v "${PWD}:/w:ro" -w /w node:24-alpine node .claude/hooks/verificar-hooks.mjs` |

Não invente comandos: acrescente aqui só os que foram executados.

Notas do Docker e do banco:
- Compose com nome de projeto `joya-pedidos`: nunca use `down -v` em outros projetos da máquina.
- A massa de desempenho (`database/performance/`) só roda no projeto isolado `joya-perf`, nunca no `joya-pedidos`.
- O `sqlserver` roda o `sqlservr` como processo principal (parada limpa) e o banco tem `IDENTITY_CACHE = OFF`.
- O banco nunca publica porta no compose padrão. MCP e ferramentas usam o `docker-compose.dev.yml` (loopback).
  Com o banco no ar por esse arquivo, um `docker compose up` só com o `docker-compose.yml` recria o `sqlserver` sem a
  porta (os dados ficam no volume, mas o MCP perde a conexão): use os dois arquivos.
- A API (`backend/Dockerfile`) roda como usuário não root, em `Production`, com a string de conexão só no ambiente
  (`ConnectionStrings__BancoPedidos`, montada no compose). Sem healthcheck HTTP: a imagem não tem `curl`.
- Senha padrão do `sa` (`Joya_Dev_2026_Inseguro`): pública, insegura e só de desenvolvimento local.

Notas do ambiente .NET:
- `global.json` (raiz) liga o `dotnet test` ao Microsoft.Testing.Platform (xUnit v3, pacote `xunit.v3` 4.x) e
  exige um SDK .NET 10. Testes são executáveis (`OutputType Exe`); não há VSTest.
- `backend/nuget.config` fixa o nuget.org como única fonte. Auditoria NuGet ligada: vulnerabilidade quebra o build.
  Só NU1900 (falha ao consultar a base, ex.: sem rede) fica como aviso.
- O hook `PostToolUse` compila só o projeto do arquivo editado (alguns segundos). Antes de commit, compile e teste a
  solução inteira.
- `.claude/hooks/fixtures/projeto-com-erro/` tem um erro de compilação **proposital** (fixture do hook). Não corrija.

## Git

- Commits pequenos e frequentes **direto na `main`**. Não crie branches.
- Conventional Commits em pt-BR, sem acentos (ex.: `feat(pedidos): cria pedido com transacao`).
- Cada commit compila e mantém os testes verdes. Push só com build e testes aprovados.
- **Proibido** reescrever histórico: `--amend`, squash, `rebase`, `pull --rebase` (ou `pull.rebase=true`),
  `push --force` (e variantes), `reset --hard`/`--soft`/`--keep`/`--merge`. Correções são commits novos (`fix:`).
- Não use `git clean -x/-X` nem `git stash --all`: apagariam o `.env` local.
- Não faça commit nem push sem pedido explícito do candidato.
