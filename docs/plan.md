# Plano de implementação — Mini Gestão de Pedidos (Joya Solutions)

> **Versão 1.0 — Aprovada** (12/11/2026). Corresponde à rev. 3 do plano elaborado em plan mode: revisão crítica
> independente por agente + ajustes da revisão externa do candidato.
> Base exclusiva: `docs/spec.md` aprovada (commit `f8e7549`, `main`). Este plano **não** altera a spec.

## 1. Contexto

- Repositório `thiagom3005/joya-mini-gestao-pedidos`, `main` sincronizada com `origin/main`, 1 commit (`f8e7549`:
  `docs/spec.md` + `.gitattributes`). Nenhum código ainda.
- Ambiente: Windows 11, .NET SDK 10.0.401, Node 24.14 / npm 11.9, Docker 28 + Compose v2.34, Git Bash.
  **Docker Desktop está parado.** `core.autocrlf=true`, neutralizado pelo `.gitattributes`. O shell do Claude nesta
  máquina inclui a ferramenta **PowerShell**, além de Bash.
- Prazo duro: **15/11/2026 16h (Brasília)**. Diretriz: **terminar o quanto antes**. Commits **direto na `main`**.
- Esforço realista: **≈ 28 h para P0/P1** e ≈ 2,5 h para P2. Do P2, só o GitHub Actions é provável.

## 2. Ajustes à ordem sugerida

| Sugestão original | Ajuste | Justificativa |
|---|---|---|
| (1) Configurar todo o Claude Code primeiro | Dividir: base (CLAUDE.md, rules, hook de proteção, agentes) na E1; hook de build na E2; prova de conectividade do MCP na E3; skill na E6; validação completa e uso do MCP na E7. | Cada ferramenta entra quando tem alvo real. A prova curta do MCP logo que o banco sobe revela cedo problemas de compatibilidade. |
| (4) Docker depois do banco | Dividir: `sqlserver` + `db-init` na E3; `api` na E9; `web` na E10. | O banco no Docker é o maior risco de infraestrutura e pré-requisito dos testes de integração. |
| (8) Testes como etapa própria | Dissolvido: cada commit de funcionalidade traz seus testes. | Nenhuma entrega fecha vermelha. |
| (10) Query depois do frontend | MCP (E7) + Parte 4 (E8) antes do frontend (E10). | Peso 10% contra 5%. Depende só do banco e do ranking, e o MCP tem aqui seu primeiro uso real. |
| (12) README no fim | README incremental + clipes gravados ao fim de cada entrega. | A E13 vira consolidação e montagem, não reencenação. |

## 3. Visão geral

| Entrega | Nome | Prio | Estimativa | Depende de |
|---|---|---|---|---|
| E0 | Registrar o plano + preparar ambiente | P0 | 0,5 h | — |
| E1 | Fundação AI First | P0 | 1,25 h | E0 |
| E2 | Esqueleto .NET 10 + hook de build | P0 | 1,25 h | E1 |
| E3 | Banco, Docker do banco, harness de integração e prova do MCP | P0 | 4 h | E2, imagens baixadas |
| E4 | Domínio e validador de criação | P0 | 1,25 h | E2 |
| E5 | Criação de pedido ponta a ponta | P0 | 4 h | E3, E4 |
| E6 | Skill + endpoints de leitura | P0 | 3,5 h | E5 |
| E7 | MCP do SQL Server: validação completa e uso | P0 | 0,75 h | E3 |
| E8 | Parte 4: query de ranking | P0 | 2 h | E6, E7 |
| E9 | API no Docker | P0 | 1 h | E6 |
| E10 | Frontend + `web` no Docker | P0 | 4 h | E6, E9 |
| E11 | Checkpoint de integração e revisão | P0 | 1,5 h | E1..E10 |
| E12 | Diferenciais (Actions, Playwright) | P2 | 2,5 h | E11 verde |
| E13 | README final, plano, vídeo e entrega | P0 | 3 h | E11 (E12, se feita) |

Execução **sequencial** (um diretório de trabalho, commits na `main`). Caminho crítico: imagens Docker → E2 → E3 →
E4 → E5 → E6 → E9 → E10 → E11 → E13 (≈ 24 h). E7 e E8 não bloqueiam o frontend, mas disputam o mesmo tempo.

## 4. Entregas detalhadas

Cada entrega lista escopo, requisitos, arquivos, implementação, testes, validação, commits e gate. Commits seguem o
padrão do primeiro: Conventional Commits em pt-BR, sem acentos. **Cada commit compila e mantém os testes verdes.**

### E0 — Registrar o plano e preparar o ambiente
- **Escopo**: gravar este plano em `docs/plan.md` (com a seção 13); ligar o Docker Desktop e iniciar em segundo plano
  o `docker pull` de `mssql/server:2022-CU<n>-ubuntu-22.04` (tag fixada), `dotnet/sdk:10.0`, `dotnet/aspnet:10.0`,
  `node:24-alpine` e `nginx:alpine`.
- **Requisitos**: RNF-07. **Arquivos**: `docs/plan.md`.
- **Validação**: `git diff f8e7549 -- docs/spec.md` vazio; `docker images` mostra as imagens.
- **Commit**: `docs: adiciona plano de implementacao`.

### E1 — Fundação AI First
- **Requisitos**: RNF-08, RNF-09 (proteção), RNF-14, RNF-23, RNF-24; §2.3.
- **Arquivos**: `CLAUDE.md`; `.claude/rules/{backend,database,frontend,testes}.md` (frontmatter `paths`);
  `.claude/settings.json`; `.claude/hooks/proteger-historico.mjs`;
  `.claude/agents/{revisor-spec,revisor-sql-concorrencia}.md`; `.gitignore`; `README.md` (esqueleto com as seções do CA-23).
- **Implementação**:
  - `CLAUDE.md` inclui a regra **"commits direto na `main`, sem criar branch"** e aponta para spec e plano.
  - **Antes de escrever o hook**, confirmar os nomes de ferramentas efetivamente disponíveis nesta instalação
    (na sessão de planejamento existiam `Bash` e `PowerShell`, além de `Edit`, `Write` e `MultiEdit`) e capturar uma entrada real:
    com `JOYA_HOOK_DEPURAR=1`, o script grava o JSON recebido em `.claude/hooks/ultima-entrada.json` (no `.gitignore`).
    As fixtures são derivadas dessas capturas.
  - Hook `PreToolUse` com matcher `Bash|PowerShell` (comandos) e `Edit|Write|MultiEdit` (arquivos), em Node.
    Analisa os argumentos após `git <subcomando>` (não busca texto solto, para não barrar mensagens de commit).
    Bloqueia: `push --force`, `-f`, `--force-with-lease` e refspec `+`; `rebase` e `pull --rebase`;
    `commit --amend`; `reset --hard`. Também bloqueia edição de arquivo cujo `basename` seja exatamente `.env`
    (`.env.example` é permitido). Bloqueio = saída 2 + motivo em stderr, **antes** de a operação acontecer.
  - Fixtures em `.claude/hooks/fixtures/` (entradas permitidas e bloqueadas para Bash, PowerShell e edição) e um
    verificador `.claude/hooks/verificar-hooks.mjs` que alimenta cada fixture no hook e confere a saída esperada.
  - `.gitignore`: `bin/`, `obj/`, `node_modules/`, `dist/`, `.env`, `TestResults/`, `playwright-report/`,
    `test-results/`, `.claude/settings.local.json`, `CLAUDE.local.md`, `.claude/hooks/ultima-entrada.json`.
- **Testes/validação**:
  - Operações perigosas são testadas **só simulando a entrada do hook**: `node .claude/hooks/verificar-hooks.mjs` no
    Windows e em `docker run --rm -v "$PWD":/w -w /w node:24-alpine node .claude/hooks/verificar-hooks.mjs`
    (portabilidade, CA-20). Nenhum comando Git destrutivo é executado.
  - **Reiniciar a sessão do Claude Code** (hooks são lidos no início) e conferir uma captura real com
    `JOYA_HOOK_DEPURAR=1`. Demonstração ao vivo para o vídeo apenas com operação sem efeito mesmo se o hook falhar:
    pedir a edição do `.env` (ignorado pelo Git). Gravar clipe.
- **Commits**: `chore: adiciona gitignore e esqueleto do README` · `docs: adiciona CLAUDE.md e regras por area` ·
  `chore: adiciona hook de protecao do historico git` · `chore: adiciona agentes de revisao independente`.
- **Gate**: `verificar-hooks.mjs` verde no Windows e no container Linux; captura real confirma os nomes de ferramenta.

### E2 — Esqueleto .NET 10 e hook de build
- **Requisitos**: RNF-01, RNF-21 (base), RNF-25, RF-11, RNF-09 (pós-edição).
- **Arquivos**: `backend/JoyaPedidos.slnx`; `backend/Directory.Build.props`; `backend/Directory.Packages.props`;
  `backend/src/JoyaPedidos.Api/` (`Program.cs`, `Compartilhado/`); `backend/src/JoyaPedidos.Dominio/`;
  `backend/tests/JoyaPedidos.TestesUnitarios/`; `backend/tests/JoyaPedidos.TestesIntegracao/`;
  `.claude/hooks/pos-edicao.mjs`; `.claude/settings.json` (`PostToolUse`).
- **Implementação**:
  - `Directory.Build.props`: net10.0, `Nullable`, `TreatWarningsAsErrors`, `NuGetAudit=true` e `NuGetAuditMode=all`
    (inclui dependências transitivas). **Nenhuma** supressão genérica de NU1901–NU1904: avisos de vulnerabilidade
    continuam visíveis e quebram o build.
  - `Directory.Packages.props`: gerenciamento central de versões com `CentralPackageTransitivePinningEnabled`, para
    corrigir uma dependência transitiva vulnerável fixando versão segura.
  - Política de vulnerabilidades: (1) corrigir atualizando ou fixando a versão; (2) só se não houver versão corrigida,
    suprimir **aquele** advisory com `NuGetAuditSuppress` (URL do GHSA), com justificativa no README e no
    `docs/plan.md`. Alta ou crítica sem correção bloqueia a entrega até decisão do candidato.
  - `Program.cs`: `AddProblemDetails` + `CustomizeProblemDetails` (código genérico por status, `traceId`),
    `UseExceptionHandler`, `UseStatusCodePages`, `TimeProvider.System`, `GET /health`, verificação do fuso
    `America/Sao_Paulo` na inicialização (falha rápida).
  - Testes: **xUnit v3** (assembly fixture, `TestContext.Current.CancellationToken` para evitar o xUnit1051);
    `[assembly: CollectionBehavior(DisableTestParallelization = true)]` no projeto de integração (RSK-12).
  - Hook `PostToolUse` (matcher `Edit|Write|MultiEdit`, `timeout: 120`) em Node. Roda **depois** da edição: não a
    desfaz; em falha devolve as linhas de erro ao Claude (saída 2 + stderr) para que ele corrija em seguida.
    - **Sem builds redundantes**: só reage a `.cs`, `.csproj` e `.props`; compila **apenas o projeto mais próximo**
      do arquivo editado (`dotnet build <projeto>.csproj -nologo -v q`, incremental, com restore), não a solução;
      se já houver build do mesmo projeto em andamento (arquivo de trava temporário), pula; `.md`, `.json`, `.sql`
      e demais arquivos não disparam nada. O build completo da solução fica para o checkpoint Q1, antes do commit.
    - `.ts`/`.tsx` dentro de `frontend/` → `npm run typecheck` (`tsc -b`, incremental) via
      `spawnSync(..., { shell: process.platform === 'win32' })`; não faz nada sem `frontend/node_modules`; ignora `e2e/`.
    - Trava de DLL no Windows (MSB3026/MSB3027) → aviso com saída 0.
    - Fixtures do `PostToolUse` também entram no `verificar-hooks.mjs` (Windows e container Linux).
- **Testes**: IT de fumaça `GET /health` → 200 (valida o harness).
- **Validação**: `dotnet build backend/JoyaPedidos.slnx`; `dotnet test backend/JoyaPedidos.slnx`;
  `dotnet list backend/JoyaPedidos.slnx package --vulnerable --include-transitive` sem achados. Reiniciar a sessão,
  provocar um erro de compilação via Claude e ver o hook devolver a mensagem com a edição preservada; corrigir.
  Gravar clipe (CA-20). Atualizar os comandos do `CLAUDE.md`.
- **Commits**: `chore: cria solucao .NET 10 com projetos de api, dominio e testes` ·
  `feat(api): configura problem details, health check e fuso do negocio` ·
  `chore: adiciona hook pos-edicao de build e verificacao de tipos`.
- **Gate**: build sem avisos, teste de fumaça verde, hook demonstrado.

### E3 — Banco, Docker do banco e harness de integração
- **Requisitos**: RF-01, RF-02, RNF-03, RNF-04 (parcial), RNF-06 (base), RNF-24; §4, §11.
- **Arquivos**: `database/01-criar-banco.sql`, `02-criar-tabelas.sql`, `03-dados-exemplo.sql`;
  `docker-compose.yml` (`sqlserver` + `db-init`); `docker-compose.dev.yml`; `.env.example`;
  `backend/tests/JoyaPedidos.TestesIntegracao/Infraestrutura/` (container, scripts, limpeza, fábrica da API).
- **Implementação**:
  - Tag do SQL Server fixada e igual no compose e no Testcontainers. Healthcheck e `db-init` com
    `/opt/mssql-tools18/bin/sqlcmd -C -b`. `db-init` usa `command` (não `entrypoint`), para permitir
    `docker compose run --rm db-init <sqlcmd ...>` na E8.
  - Senha `${MSSQL_SA_PASSWORD:-<dev inseguro>}`. `.env.example` com `MSSQL_SA_PASSWORD`, `WEB_PORTA`, `API_PORTA` e `SQL_PORTA`.
  - Seed da §4.7: homônimos, estoque 0, estoque ≤ 2, dois produtos com estoque ≥ 1.000, fronteiras
    `2026-01-01T02:59:59Z` e `03:00:00Z`, preço alterado depois do pedido, nenhum pedido após 31/10/2026 local.
  - Testcontainers: copia `database/` para o container e aplica com `ExecAsync` + `sqlcmd -C -b`. Antes de cada teste,
    a limpeza apaga dados **e** triggers de teste.
  - **IT-21 isolado**: roda em um **container próprio**, separado do container compartilhado pelas demais
    integrações. Banco novo, scripts 01–03 aplicados duas vezes, verificações de seed, constraints e índices. Nenhum
    `DROP DATABASE` sobre banco compartilhado ou com conexões ativas. Custo aceito: ~20–30 s a mais na suíte.
  - **Prova curta do MCP (≤ 20 min, assim que o banco sobe)**: criar o login `joya_mcp` com
    `database/dev/criar-login-mcp.sql` (fora da execução automática; senha via variável `sqlcmd`), criar `.mcp.json`
    (configuração da E7, abaixo), definir `JOYA_MCP_SENHA` com `setx`, reiniciar o app e executar `SELECT @@VERSION`
    via MCP. Falha de compatibilidade aqui antecipa a decisão da contingência.
- **Testes**: IT-21.
- **Validação**: `docker compose up -d sqlserver db-init` → `docker compose ps -a` mostra `db-init` `Exited (0)`;
  repetir → contagens iguais (CA-02); `docker compose ps` sem porta do SQL; com `-f docker-compose.dev.yml` →
  `127.0.0.1:1433` (CA-03); `dotnet test`. Atualizar `CLAUDE.md` com os comandos do Docker.
- **Commits**: `feat(banco): cria esquema com chaves, restricoes e indices` ·
  `feat(banco): adiciona carga de exemplo idempotente` · `chore(docker): sobe sql server e aplica scripts automaticamente` ·
  `test: adiciona infraestrutura de integracao com testcontainers` ·
  `chore: adiciona configuracao do mcp do sql server e login de privilegio minimo`.
- **Gate**: IT-21 verde sem afetar as demais integrações; CA-02, CA-03, CA-04, CA-05; MCP responde a `SELECT @@VERSION`.

### E4 — Domínio e validador de criação
- **Requisitos**: RN-01..RN-12, RN-15; RNF-05, RNF-19.
- **Arquivos**: `Dominio/` (`Cliente`, `Produto`, `Pedido`, `ItemPedido`, `ItemSolicitado`, `ErroNegocio`,
  `Resultado`, `RegrasPedido`); `Api/Modulos/Pedidos/ValidadorCriarPedido.cs`;
  `Api/Compartilhado/PeriodoNegocio.cs` (conversão de períodos, conforme DEC-01); `Api/Compartilhado/MapeadorErros.cs`
  (recebe o **número** do erro SQL, função pura).
- **Testes**: UT-01..UT-13, UT-16..UT-18. Os três casos do enunciado são UT-01, UT-06 e UT-02. UT-14 e UT-15 ficam
  para a E6, geradas pela skill.
- **Validação**: `dotnet test backend/tests/JoyaPedidos.TestesUnitarios`.
- **Commits** (código e testes juntos): `feat(dominio): regras de criacao de pedido com testes unitarios` ·
  `feat(api): validador de criacao de pedido com testes unitarios` ·
  `feat(api): conversao de periodo no fuso do negocio com testes` ·
  `feat(api): mapeamento de erros para problem details com testes`.
- **Gate**: UTs verdes.

### E5 — Criação de pedido ponta a ponta (núcleo crítico)
- **Requisitos**: RF-03, RF-10, RN-01..RN-12, RNF-20, RNF-21; §5.3, §5.5, §7.
- **Arquivos**: `Api/Modulos/Pedidos/{PedidosEndpoints,ServicoPedidos,RepositorioPedidos}.cs`, `Contratos/`;
  `Api/Compartilhado/{FabricaConexao,TratadorExcecoes}.cs`; `TestesIntegracao/Pedidos/`.
- **Implementação**: validador → transação → cliente (rollback imediato se ausente) → `SELECT ... WITH (UPDLOCK, ROWLOCK)`
  um a um em ordem crescente → `RegrasPedido.CriarPedido` → `UPDATE` condicional com verificação de 1 linha → inserts →
  commit. `DataPedido` truncado, marcado como UTC, parâmetros `DbType.DateTime2`. Erro 1205 → 503.
- **Testes**: IT-01..IT-06, IT-07 (só `GET /pedidos/{id}`), IT-08..IT-10, IT-17, IT-25, IT-26.
- **Revisão independente** (Q7): agente `revisor-sql-concorrencia` sobre `ServicoPedidos` e `RepositorioPedidos`.
  Achados aceitos viram commits `fix:`; rejeitados vão para o registro do `docs/plan.md`.
- **Commits**: `feat(pedidos): cria pedido com transacao e bloqueio ordenado de estoque` (com IT-01..05 e IT-08) ·
  `feat(pedidos): consulta pedido por id` (IT-06, IT-07 parcial, IT-17) ·
  `test(pedidos): cobre atomicidade apos escrita parcial e precedencia de erros` (IT-25, IT-26) ·
  `test(pedidos): cobre concorrencia e pedidos cruzados` (IT-09, IT-10).
- **Gate (Q3)**: CA-06..CA-09 verdes, zero 500, objetivo de zero 503 atingido, revisão tratada. Gravar clipe.

### E6 — Skill e endpoints de leitura
- **Requisitos**: RNF-10, RF-04, RF-05, RF-09, RN-14, RN-15; §5.4, §5.6–5.8, §8.
- **Arquivos**: `.claude/skills/novo-endpoint/SKILL.md`; `Api/Modulos/Catalogo/`; listagem em `Api/Modulos/Pedidos/`
  (com `ValidadorListarPedidos`); `Api/Modulos/Relatorios/` (com `ValidadorTopClientes`); fixture DR-RANK em
  `TestesIntegracao/Relatorios/`.
- **Implementação**: skill extraída do padrão da E5 (passos, checklist, arquivos de referência, termina com build e
  testes). Usada para gerar os três módulos, **incluindo validadores e UT-14/15**. Listagem com WHERE de fragmentos
  constantes, `OFFSET` em `long` e `COUNT` separado. Ranking com `TOP (5)` e `ORDER BY Total DESC, c.Id`.
- **Testes**: UT-14, UT-15, IT-07 (parte da listagem), IT-11..IT-16, IT-18..IT-20, IT-22.
- **Commits**: `chore: adiciona skill novo-endpoint` · `feat(catalogo): lista clientes e produtos` ·
  `feat(pedidos): lista pedidos com filtros e paginacao` · `feat(relatorios): ranking dos cinco maiores clientes por periodo`.
- **Gate**: CA-10, CA-11, CA-14 verdes; skill usada (CA-21). Gravar clipe da skill em uso.

### E7 — MCP do SQL Server: validação completa e uso
- **Requisitos**: RNF-11, RNF-12 (insumo), RNF-24; §2.3, §2.3.1, §11.6.
- **Configuração** (criada na E3, consolidada aqui):
  - DBHub **`@bytebase/dbhub@1.6.1`**, versão fixada. É a mais recente no npm em 12/11/2026, posterior às correções
    dos advisories GHSA-mwwr-p57h-56pf (modo somente leitura que não protegia), GHSA-fm8p-53ww-hf6w (DNS rebinding no
    transporte HTTP) e GHSA-j656-3hf2-fvjc (MySQL/MariaDB). Os advisories são reverificados na E3 antes de fixar.
  - Só transporte **stdio** (nunca HTTP). `.mcp.json` sem nenhum segredo e **sem senha nos argumentos do processo**:
    `"args": ["-y", "@bytebase/dbhub@1.6.1", "--transport", "stdio"]` e conexão por variáveis de ambiente do processo
    em `"env"`: `DB_TYPE=sqlserver`, `DB_HOST=localhost`, `DB_PORT=${SQL_PORTA:-1433}`, `DB_USER=joya_mcp`,
    `DB_NAME=JoyaPedidos` e `DB_PASSWORD=${JOYA_MCP_SENHA}`. Se o certificado autoassinado exigir `sslmode=require`
    (disponível só na DSN), usar `"DSN": "sqlserver://joya_mcp:${JOYA_MCP_SENHA}@localhost:${SQL_PORTA:-1433}/JoyaPedidos?sslmode=require"`
    também em `env`, nunca em `args`.
  - `JOYA_MCP_SENHA` definida localmente (`setx`) e reinício do app. O Claude Code expande `${VAR}` do ambiente do
    processo, não do `.env`. Se `npx` direto falhar no Windows, usar `cmd /c npx` e documentar a variante Linux.
  - Login `joya_mcp`: `db_datareader` + `VIEW SERVER PERFORMANCE STATE`, nada administrativo. Recriar após
    `docker compose down -v`. Se a versão oferecer modo somente leitura configurável sem expor segredo, ativá-lo como
    camada extra. A garantia é o banco, não a ferramenta.
- **Validação**: `docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d`; no Claude, `/mcp` conectado;
  `fn_my_permissions` confere; **escrita negada pelo banco**: `UPDATE`, `INSERT`, `DELETE` e `CREATE TABLE` via MCP
  falham com erro de permissão do SQL Server; `sys.dm_exec_query_stats` e `sys.indexes` respondem; conferir o seed via
  MCP. Gravar clipe.
- **Evidências no README**: versão do DBHub, advisories verificados, permissões do login, saída das tentativas de
  escrita negadas e consultas feitas.
- **Timebox**: 30 min. Contingência: `sa` documentado como limitação no README, como a §2.3.1 prevê.
- **Commit**: `docs: registra validacao de permissoes do mcp` (e `fix:` na configuração, se necessário).
- **Gate**: CA-22; DUV-04 e DUV-08 resolvidas e registradas.

### E8 — Parte 4: query de ranking
- **Requisitos**: RF-08; §9.
- **Arquivos**: `database/consultas/ranking-2026-original.sql` (literal), `ranking-2026-corrigida.sql` (batch único);
  `database/performance/gerar-massa.sql` (geração em lote com tally, não `WHILE`); IT-23/24; seção "Parte 4" do README.
- **Implementação**: gerar massa com `docker compose run --rm db-init <sqlcmd ...>`; medir V0, V2 e V2 sem
  `IX_Pedidos_DataPedido` via `sqlcmd` (`STATISTICS IO, TIME`; plano com `-y 0`) e via MCP (DMVs, `sys.indexes`);
  mediana de 3 execuções; decidir sobre o índice (se mudar, atualizar o IT-21 no mesmo commit); `down -v` e recriar o
  login do MCP.
- **Testes**: IT-23, IT-24.
- **Commits**: `feat(banco): adiciona query de ranking original e corrigida` (com IT-23/24) ·
  `feat(banco): adiciona script de massa para medicao de desempenho` ·
  `docs: documenta investigacao e medicoes da query de ranking`.
- **Gate**: CA-12 e CA-15 verdes; CA-13 com números reais. Gravar clipe da investigação.

### E9 — API no Docker
- **Arquivos**: `backend/Dockerfile` (SDK 10 → `aspnet:10.0` não chiseled, com tzdata, usuário não root),
  `backend/.dockerignore`, serviço `api` (`${API_PORTA:-5080}:8080`, `depends_on: db-init: service_completed_successfully`).
- **Validação**: `docker compose up --build -d`; `curl http://localhost:5080/health`; CA-18 concreto:
  `GET /pedidos?dataInicio=2026-01-01&dataFim=2026-01-01` no container traz o pedido de `03:00:00Z` e não o de `02:59:59Z`.
- **Commit**: `chore(docker): empacota api .net e integra ao compose`.
- **Gate**: CA-18.

### E10 — Frontend e `web` no Docker
- **Requisitos**: RF-06, RF-07, RNF-02; §6.
- **Arquivos**: `frontend/` (Vite + React + TS strict, ESLint, script `typecheck` = `tsc -b`); `src/api/clienteHttp.ts`,
  `src/tipos/contratos.ts`, `src/utilitarios/formatacao.ts`, `src/componentes/`; `frontend/Dockerfile` (Node → nginx);
  `frontend/nginx.conf`; serviço `web` (`${WEB_PORTA:-3000}:80`).
- **Validação**: `npm --prefix frontend run lint`, `typecheck` e `build`; navegação em `http://localhost:3000` cobrindo
  §6.2–6.4 (201, 400, 409, 422). Hook de typecheck já ativo. Atualizar `CLAUDE.md` com os comandos do frontend.
- **Commits**: `feat(frontend): cria projeto react com cliente http tipado` ·
  `feat(frontend): listagem de pedidos com filtros e paginacao` ·
  `feat(frontend): modal de criacao de pedido com tratamento de erros` ·
  `chore(docker): serve frontend com nginx e proxy para a api`.
- **Gate**: CA-01, CA-16, CA-17. Gravar clipe do fluxo.

### E11 — Checkpoint de integração e revisão independente
- **Passos**: clone limpo em pasta temporária → `docker compose up` sem `.env` (CA-01) → `down` + `up` (CA-02) →
  `docker compose ps` (CA-03); `dotnet test`; lint, typecheck e build do front; varredura de segredos (`git grep`,
  `.env` fora do índice; CA-28); `dotnet list ... package --vulnerable --include-transitive` e `npm audit --omit=dev`
  sem alta ou crítica; identificadores sem acento (CA-27); comandos do `CLAUDE.md` funcionando (CA-19);
  `verificar-hooks.mjs` no Windows e no container Linux; agentes `revisor-spec` e `revisor-sql-concorrencia` sobre o
  código final.
- **Commits**: apenas `fix:` reais.
- **Gate (decisão P2)**: todos os CAs P0/P1 verdes, exceto README final, vídeo e entrega.

### E12 — Diferenciais (somente com E11 verde)
- **12a GitHub Actions** (≈ 45 min): `.github/workflows/ci.yml` com jobs `backend` (setup-dotnet 10, build, testes
  com Testcontainers no `ubuntu-latest`; o audit do NuGet roda junto e quebra o job com vulnerabilidade),
  `frontend` (`npm ci`, lint, typecheck, build) e um passo `node .claude/hooks/verificar-hooks.mjs` (portabilidade
  dos hooks em Linux). Commit
  `ci: executa build e testes a cada push e pull request`. CA-30.
- **12b Playwright** (≈ 1,5 h): `e2e/` com E2E-01..03 contra o compose recém-criado. Commit
  `test(e2e): cobre criacao e listagem de pedidos com playwright`. CA-29.

### E13 — README final, plano, vídeo e entrega
- **README** (CA-23): Docker, testes, decisões, uso do Claude (o que funcionou e o que foi corrigido), MCP, Parte 4,
  limitações.
- **`docs/plan.md`**: consolidar o "Registro de execução".
- **Vídeo** (≤ 10 min, roteiro da §12.2): montagem dos clipes gravados nas entregas + narração do código; upload com
  acesso por link.
- **Véspera do envio**: repetir Q4 e Q6 (clone limpo + `docker compose up`, verificação de vulnerabilidades).
- **Entrega**: confirmar repositório público. O e-mail à recrutadora é enviado **pelo candidato**.
- **Commits**: `docs: completa README com execucao, testes, decisoes e uso do Claude` ·
  `docs: atualiza plano com o registro da execucao`.

## 5. Rastreabilidade (spec → entrega)

| Requisito | Entrega | Requisito | Entrega |
|---|---|---|---|
| RF-01, RF-02 | E3 | RNF-07 | E0, E13 |
| RF-03, RF-10 | E5 | RNF-08 | E1 (+ atualizações E2, E3, E10, E11) |
| RF-04, RF-05, RF-09 | E6 | RNF-09 | E1 (proteção), E2 (build) |
| RF-06, RF-07 | E10 | RNF-10 | E6 |
| RF-08 | E8 | RNF-11, RNF-12 | E7 (+ README na E13) |
| RF-11 | E2 | RNF-13, RNF-15, RNF-26 | E13 (clipes em todas as entregas) |
| RN-01..RN-12 | E4, E5 | RNF-14 | Todas (política de Git) |
| RN-13 | E5/E6 (nenhum endpoint de alteração) | RNF-16, RNF-17 | E12 |
| RN-14, RN-15 | E4 (período), E6 | RNF-18 | Fora |
| RNF-01, RNF-21, RNF-25 | E2, E5 | RNF-19, RNF-20 | E4, E5 |
| RNF-02 | E10 | RNF-22, RNF-23 | Todas (rules + revisão na E11) |
| RNF-03, RNF-04 | E3, E9, E10 | RNF-24 | E1, E3, E7, E11 |
| RNF-05 | E4, E6 | RNF-06 | E3..E8 |

| CA | Entrega | CA | Entrega |
|---|---|---|---|
| CA-01 | E10, E11 | CA-16, CA-17 | E10 |
| CA-02..CA-05 | E3 | CA-18 | E9 |
| CA-06..CA-09 | E5 | CA-19 | E1 → E11 |
| CA-10, CA-11 | E6 | CA-20 | E1, E2 |
| CA-12, CA-13 | E8 | CA-21 | E6 |
| CA-14 | E5, E6 | CA-22 | E3 (prova), E7 |
| CA-15 | E8 (último IT) | CA-23..CA-26 | E13 |
| CA-27, CA-28 | E11 | CA-29, CA-30 | E12 |

## 6. Ferramentas AI First

| Ferramenta | Configuração | Uso | Evidência |
|---|---|---|---|
| `CLAUDE.md` | ≈ 100 linhas: arquitetura, convenções, comandos, fluxo spec → plano → entrega → gate → commit, commits na `main`, proibições. Aponta para spec e plano. | Todas as sessões. | Arquivo + comandos funcionando (CA-19). |
| Rules | `.claude/rules/*.md` com `paths`: backend, database (idempotência, `GO`, `sqlcmd -C -y 0`, query original intocável, IT-21 ao mudar índice), frontend, testes. | Carregadas ao tocar cada área. | Arquivos + correções guiadas por regra no registro. |
| Hook `PreToolUse` | Node; `Bash\|PowerShell` + `Edit\|Write\|MultiEdit`; impede a operação antes de executar. Fixtures + `verificar-hooks.mjs`. | Toda a execução. | Verificador verde em Windows e Linux; clipe do bloqueio de `.env` (E1). |
| Hook `PostToolUse` | Node; build incremental só do projeto afetado ou typecheck; não desfaz a edição; saída 2 devolve o erro; timeout 120 s. | Toda edição de código. | Clipe de erro detectado e corrigido (E2). |
| Skill `novo-endpoint` | Padrão da E5 com checklist. | Gera 3 módulos com validadores e testes (E6). | Commits da E6 + clipe. |
| MCP SQL Server | DBHub 1.6.1 fixado, stdio, conexão por variáveis de ambiente (senha nunca em `args` nem no repositório), login `joya_mcp` de privilégio mínimo. | E3 (prova), E7 (permissões e seed), E8 (DMVs, índices), E11. | README: versão, advisories, permissões, escritas negadas, consultas; clipe. |
| Agentes | `revisor-sql-concorrencia` (E5) e ambos (E11). Outros pontos só se sobrar tempo. | Revisões com contexto limpo. | Achados aceitos/rejeitados no `docs/plan.md`. |
| Plan mode | Spec e este plano. | Início e mudanças de rumo. | `docs/plan.md` + transcrição guardada. |

Registro contínuo: ao fechar cada entrega, 2 a 5 linhas em `docs/plan.md` ("Registro de execução") e um clipe curto
(Win+G ou OBS). Isso alimenta README e vídeo sem retrabalho.

## 7. Checkpoints de qualidade

| ID | Checkpoint | Quando | Como |
|---|---|---|---|
| Q1 | Build sem erros nem avisos | Cada edição (projeto afetado) e antes de cada commit (solução) | Hook pós-edição; `dotnet build backend/JoyaPedidos.slnx`; `npm run build` |
| Q2 | Testes aprovados | Fim de cada entrega | `dotnet test backend/JoyaPedidos.slnx` |
| Q3 | Integridade transacional e concorrência | E5, E11 | IT-05, IT-09, IT-10, IT-25 + `revisor-sql-concorrencia` |
| Q4 | Segurança e segredos | E2, E3, E7, E11, véspera da entrega | `git grep`, `.env` ignorado, permissões do MCP com escrita negada, porta do SQL fechada, audit NuGet e npm sem alta/crítica |
| Q5 | Contratos HTTP | E5, E6, E11 | ITs de status e `problem+json` + `revisor-spec` (E11) |
| Q6 | Docker a partir de clone limpo | E11 e véspera da entrega | Clone em pasta temporária + `docker compose up` |
| Q7 | Revisão independente | E5, E11 | Agentes com contexto limpo |

Nenhum push sem build da solução e testes aprovados (Q1 e Q2 verdes).

## 8. Prazo

- Sequência contínua E0 → E13, sem esperar datas. Marcos: **M1** fim da E5 (núcleo); **M2** fim da E10 (aplicação
  completa no Docker); **M3** fim da E11 (decisão sobre P2).
- Limites duros: código congelado até **15/11 10h**; README, vídeo e envio até **15/11 14h** (2 h de folga).
- **Cortes, em ordem, sem tocar CAs**: Playwright → GitHub Actions → revisões por agente fora de E5/E11 → variantes
  opcionais da §9.6 → polimento do vídeo. Cortar qualquer IT reprovaria CA-10, CA-11 ou CA-15: só como desvio
  explícito registrado no `docs/plan.md` e no README.

## 9. Política de Git

- Commits pequenos direto na `main`; push ao fim de cada entrega, **somente com build e testes aprovados**.
- Registro contínuo de decisões e correções humanas no `docs/plan.md` ("Registro de execução").
- Proibido squash, `--amend`, `push --force`, `rebase`, `reset --hard` (o hook reforça).
- Correções viram commits `fix:` novos. Nenhum commit artificial.

## 10. Riscos de execução

| Risco | Mitigação |
|---|---|
| Pull da imagem mssql (~1,5 GB) e Docker Desktop parado | Ligar e baixar na E0, em segundo plano |
| Hooks só valem após reinício da sessão | Reiniciar após E1 e E2; validar antes de confiar |
| Hook pós-edição lento, redundante ou travado por DLL em uso | Só o projeto afetado, incremental, pula build já em andamento, timeout 120 s, trava = aviso |
| Testes de concorrência intermitentes | Barreira de largada, execução sequencial, investigação de qualquer 503 (§7.5) |
| Testcontainers no Windows | Validado já na E3, com o teste de fumaça do container |
| Vulnerabilidade NuGet (inclusive transitiva) quebra o build | Corrigir atualizando/fixando versão; exceção só por advisory específico, justificada e documentada. Risco residual aceito: um advisory publicado **após** a entrega pode quebrar o build do avaliador; registrado no README com a forma de verificar. |
| MCP incompatível ou sem conectar | Prova curta já na E3; timebox de 30 min na E7; contingência `sa` documentada |
| DBHub com novo advisory | Versão fixada, reverificação de advisories na E3 e na véspera da entrega; só stdio; garantia no login do banco |
| Estimativa estourar | Cortes da seção 8, sem tocar CAs |

## 11. Decisões confirmadas × pendências

| Confirmadas | Pendentes (resolvidas na execução, com registro) |
|---|---|
| Todas as decisões da spec aprovada | DUV-08 e DUV-04: DBHub com login dedicado ou contingência `sa` (E7) |
| Commits direto na `main`, terminar o quanto antes | Tag exata do CU do SQL Server 2022 (E0/E3) |
| Ordem ajustada da seção 2 | Manter ou não `IX_Pedidos_DataPedido` (evidência da E8) |
| xUnit v3 + `Assert` nativo, execução sequencial na integração | P2 feito ou não (M3) |
| DBHub 1.6.1 (stdio) como candidato principal do MCP | Plataforma do vídeo (E13) |
| Auditoria NuGet ativa, sem supressão genérica | Exceções pontuais de advisory, se surgirem (decisão do candidato quando alta/crítica) |
| IT-21 em container isolado | Uso de `DB_*` ou `DSN` em `env` para o DBHub (definido pela prova da E3) |

Divergência com a spec encontrada na implementação: registrar no `docs/plan.md` e pedir aprovação do candidato antes de
alterar `docs/spec.md`.

## 12. Verificação de ponta a ponta

1. `dotnet test backend/JoyaPedidos.slnx` → 0 falhas (UT + IT com SQL real).
2. Clone limpo + `docker compose up` → `http://localhost:3000` lista pedidos; criar pedido pelo modal; ver no topo.
3. `curl` dos endpoints da §5 via `localhost:5080` e via `localhost:3000/api`.
4. MCP com o login de privilégio mínimo: índices e DMVs.
5. (P2) `npx playwright test` e workflow do Actions verde.

## 13. Decisões humanas e discordâncias

Fase de spec:
- Rev. 1 → rev. 2, por exigência do candidato: ordem de locks não garantida por `ORDER BY`; datas em UTC;
  desempenho tratado como hipótese medida; SQL sem porta publicada; nenhuma ferramenta de memória.
- Ajustes finais: deadlock "reduzido, não eliminado"; cliente validado antes dos locks; rastreabilidade da Parte 2.
- Revisão independente da spec: 22 achados aceitos, 2 rejeitados (porta do SQL no compose padrão; remover matrizes 15.2/15.3).
- Aprovação condicionada: semântica de `PostToolUse` × `PreToolUse`; permissões do MCP; zero 503 como objetivo.

Fase de plano:
- Ordem original ajustada (seção 2), com justificativa.
- Candidato: commits direto na `main`; terminar o quanto antes.
- Revisão crítica independente do plano (agente): 25 achados, todos incorporados na rev. 2. Os de maior impacto: hook
  sem cobertura da ferramenta PowerShell; estimativa de 22 h → ≈ 28 h; MCP em entrega própria; validadores de leitura
  gerados pela skill; cortes que não reprovam CAs. Três propostas do agente foram depois **substituídas por decisão
  humana** (abaixo): supressão genérica de NU1901–NU1904, senha da DSN nos argumentos do DBHub e `DROP DATABASE` no
  IT-21. A demonstração do hook com `git reset --hard HEAD` também foi trocada por simulação de entrada.
- Revisão externa do candidato (decisões humanas após a revisão do agente):
  1. **Segurança NuGet**: rejeitada a sugestão do agente de suprimir NU1901–NU1904 de forma genérica. Avisos ficam
     visíveis; correção por atualização/fixação; exceção só por advisory específico, justificada e documentada; alta ou
     crítica nunca ignorada em silêncio.
  2. **MCP**: DBHub mantido, versão fixada com correções de segurança verificadas (1.6.1); senha fora de `args` e do
     repositório (variáveis de ambiente); login de privilégio mínimo; escrita negada pelo banco comprovada; versão e
     evidências no README.
  3. **Isolamento de testes**: rejeitado o `DROP DATABASE` proposto para o IT-21; idempotência validada em container
     isolado, sem afetar as demais integrações.
  4. **Hooks**: nomes de ferramenta confirmados na instalação real; fixtures representativas; perigos testados só por
     simulação de entrada (sem Git destrutivo); `PostToolUse` não desfaz edições; build só do projeto afetado;
     validação em Windows e Linux.
  5. **MCP antecipado**: prova curta de conectividade na E3; validação completa continua na E7.
  6. **Histórico e entrega**: mantidos commits pequenos na `main`, sem squash/amend/rebase/force push, push só com
     build e testes verdes, registro contínuo, P0/P1 antes de P2, prazo e reserva para README, vídeo e envio.

## 14. Registro de execução

Atualizado ao fechar cada entrega, com o que foi feito, o que o Claude propôs, o que foi aceito ou corrigido pelo
candidato e as evidências reais (commits, saídas de testes, clipes). Só entra aqui o que de fato foi executado.

| Entrega | Situação | Registro |
|---|---|---|
| E0 | Concluída (parcial no Docker) | Plano versionado no commit `bcd6696`. Docker Desktop ativo. Imagens já presentes: `dotnet/sdk:10.0`, `mssql/server:2022-latest` (tag CU ainda não fixada) e `node:24-alpine` (baixada na E1). Faltam `dotnet/aspnet:10.0` e `nginx:alpine`, a baixar antes da E9/E10. |
| E1 | Concluída (commits `cf6b896`, `a2ba5bf`, `c139b46`, `9a2cb9c`) | `CLAUDE.md`, 4 rules com `paths`, hook `PreToolUse` em Node (forma exec, `onFailure: block`), 4 arquivos de fixtures (135 casos) + `verificar-hooks.mjs` (142 verificações), 2 agentes revisores, `.gitignore`, esqueleto do README. Formatos confirmados no Claude Code 2.1.321 instalado e por captura real (`Bash`, `PowerShell`, `Write`, `Edit`). Verificador aprovado no Windows (Node 24.14) e em Linux (`node:24-alpine`, via Git Bash e PowerShell). Bloqueio ao vivo confirmado com `git rebase --abort` (inofensivo) pelas duas ferramentas. Revisão independente (agente): 13 achados; 11 corrigidos com novas fixtures (continuação de linha, guarda de execução que desligava o hook via link, atribuição no PowerShell, palavras-chave de shell, `reset --soft`, `config pull.rebase`, `clean -x`/`stash --all`, comando Docker do `CLAUDE.md`, `$( )` em aspas, heredoc, `rebase --abort`, leitura do `.env`). Aceito como limitação: agentes revisores são somente leitura por instrução (item 12). **Desvios do plano**: depuração por arquivo `.claude/hooks/.depurar` em vez da variável `JOYA_HOOK_DEPURAR`, porque o ambiente do hook não muda sem reiniciar o app; `settings.json` também nega `Read`/`Edit` do `.env` (camada extra); hook cobre mais casos que a lista original, por achados da revisão. |
| E2 | Concluída (commits `40d6b50`, `8915dbf` e o `docs` seguinte) | Solução `backend/JoyaPedidos.slnx` com Api, Dominio, TestesUnitarios e TestesIntegracao (net10.0, Nullable, warnings como erros). CPM com fixação transitiva; `nuget.config` só com nuget.org. Pipeline HTTP: Problem Details com `codigo` e `traceId` (também no log de erro), título pt-BR igual em qualquer ambiente, `TratadorExcecoes` (500 sem detalhes internos, inclusive com `Accept: text/html`), `TimeProvider` e fuso `America/Sao_Paulo` por DI com falha rápida, `GET/HEAD /health`. Testes: 15 (10 UT + 5 IT) aprovados no Windows e no Linux (`sdk:10.0`), sem SQL Server. Auditoria NuGet (transitivos): nenhum pacote vulnerável; política provada com pacote vulnerável descartável (NU1903 quebrou o restore). Hook `PostToolUse` (build incremental só do projeto afetado; `.props` → solução; typecheck do frontend quando existir; trava única com PID), 17 fixtures; verificador com 165 verificações no Windows e no Linux (Node + .NET). Erro de compilação devolvido ao Claude ao vivo (CS0029 na fixture proposital, edição mantida). Revisão independente (agente): 13 achados; corrigidos 1–10 e 13 (`nuget.config`, NU1900 como aviso, CS2012 e trava global, `traceId` no log, 500 para `text/html`, `/health` só GET/HEAD, títulos pt-BR, trava órfã por PID, código morto, `LangVersion`/SDK fixados no `global.json`); item 12 já coberto; item 11 (runtime local 10.0.12 desatualizado) fica como pendência do ambiente. **Desvios do plano**: xUnit v3 roda no Microsoft.Testing.Platform (`global.json`), sem VSTest, porque o `xunit.v3` 4.x exige; `Parallelization(Mode = None)` substitui `DisableTestParallelization`, obsoleto no xUnit 4; NU1900 é a única exceção da auditoria, por não ser vulnerabilidade; trava de build única para todos os projetos (compartilham `obj/`) em vez de uma por projeto. **Agrupamento de commits**: os commits 1 e 2 previstos foram unidos em `40d6b50`, porque os testes dependem de `Program` e do pipeline e a solução só compila com os quatro projetos; separar exigiria versões intermediárias artificiais. Cada commit foi validado isoladamente (`git archive` + build/testes/verificador). Pendência: demonstração do hook em clipe para o vídeo (CA-20). |
| E3 | Concluída (commits `f59a58a`, `057ddf4`, `22dbcc4`, `889f28b`, `2e0df0b` e o `docs` seguinte) | **Imagem**: `mcr.microsoft.com/mssql/server:2022-CU28-ubuntu-22.04` (SQL Server 16.0.4325.4, CU28, Developer; publicada em 05/11/2026, mais nova que a CU27-GDR2), igual no compose e no Testcontainers (teste de paridade). **Scripts** `database/01..03` idempotentes (§4.2–4.3; `IDENTITY_CACHE = OFF`), seed determinístico (10 clientes com 2 homônimos que somam 1.952,35 em 2026 contra 1.110,00 do maior individual; 12 produtos com estoques 0/2/1.500/2.000; 33 pedidos e 52 itens; fronteiras `02:59:59Z`/`03:00:00Z`; último em `2026-11-01T02:59:59Z`; preços congelados), aplicados por `database/aplicar-scripts.sh` (`sqlcmd -C -b -f 65001`, senha via `SQLCMDPASSWORD`). **Compose** (`sqlserver` + `db-init`): healthcheck autenticado que também abre o banco; `db-init` com exit 0, idempotente em reexecução e em `down`/`up` (3×), e exit 1 com script de SQL inválido (os seguintes não rodam); nenhuma porta no padrão; `docker-compose.dev.yml` só em `127.0.0.1`; validado no Git Bash e no PowerShell. **Testes**: 36/36 (10 UT + 26 IT) duas vezes seguidas e em ordem aleatória; IT-21 completo em container próprio (idempotência, PK/UQ/FK/CHECK, índices exatos, tipos, violações rejeitadas pelo banco, mínimos do seed, IDENTITY). **Auditoria**: `Microsoft.Data.SqlClient` 7.1.1 trazia `System.Security.Cryptography.Pkcs` 9.0.18 (GHSA-846f-f764-76mx alta, GHSA-2fmv-rv2c-59x7 moderada); corrigido fixando 10.0.15 por fixação transitiva; base de vulnerabilidades conferida contra o índice atual do nuget.org; 0 NU1900–NU1904. **Prova MCP**: DBHub 1.6.1 por stdio, `execute_sql` com `SELECT @@VERSION` → SQL Server 2022 CU28, como `joya_mcp` (db_datareader + VIEW SERVER PERFORMANCE STATE), com criptografia (`encrypt_option = TRUE`); feita por um cliente MCP mínimo (o `.mcp.json` no Claude Code exige reiniciar o app e fica para a E7). Problema do primeiro tempo esgotado: download inicial do `npx`. **Revisão independente (agente)**: 0 ALTA, 2 MÉDIA, 6 BAIXA, todos tratados — `docker stop` virava SIGKILL e o IDENTITY saltava para 1001 (agora `sqlservr` como processo principal: parada limpa em ~1,5 s, exit 0; IDENTITY estável até após `docker kill`); comando do login falhava no Git Bash (`MSYS_NO_PATHCONV=1` documentado); healthcheck sem mensagem de erro (`-r 1`); `stdin` descartado pelo entrypoint da imagem no `db-init` (`entrypoint: []`); validação da senha aceitava acentos (`COLLATE Latin1_General_BIN2`); porta do MCP pelo ambiente do Claude e riscos do `npx`/`.env` documentados; textos e uma asserção fraca corrigidos. Achado meu: erro 904 no `db-init` após `down`/`up` (healthcheck só provava o `master`; corrigido). **Desvios do plano**: `RelogioDeTeste` próprio em vez de `FakeTimeProvider`, porque este não volta no tempo e IT-13/IT-19 precisam disso (pacote removido); `database/aplicar-scripts.sh` acrescentado para o compose e os testes usarem o mesmo mecanismo; DSN do DBHub em `env` com `sslmode=require`, porque as variáveis `DB_*` do DBHub não aceitam `sslmode`; `.mcp.json` com `cmd /c npx` (Windows). **Ressalvas e publicação**: `IDENTITY_CACHE = OFF` evita o salto de 1.000 após parada abrupta, mas não garante sequência sem lacunas (inserts que falham ou são desfeitos consomem valores); nenhuma regra depende de Ids contíguos. A senha previsível que entrou por engano numa rodada da prova pelo PowerShell (gerador indisponível no PowerShell 5.1) foi rotacionada para uma aleatória e confirmada como recusada (`Login failed`), também antes da publicação; a prova com `SELECT @@VERSION` foi repetida com nova rotação. Na publicação, o cache de vulnerabilidades do NuGet estava desatualizado e os servidores de borda do índice respondiam versões diferentes; por isso, além do audit do NuGet (0 NU1900–NU1904), foi feita auditoria manual dos 79 pacotes resolvidos contra a base mais nova (2026.11.12.23.26.09, 8.010 advisories) e as atualizações, sem achados, com controle positivo. **Pendências**: o login `joya_mcp` está com senha aleatória descartada: recriar com `JOYA_MCP_SENHA` do candidato e reiniciar o Claude Code antes da E7; o comando de massa previsto na spec §11.5 precisa de `--no-deps` (E8). |
| E4 | Concluída (commits da E4, incluindo o `docs` com este registro) | **Domínio** (`JoyaPedidos.Dominio`, sem dependências): `Cliente`, `Produto`, `ItemSolicitado`, `ItemPedido` (subtotal derivado), `Pedido` (itens somente leitura ordenados por produto, total derivado), `ErroNegocio` (`ClienteNaoEncontrado`, `ProdutosNaoEncontrados`, `EstoqueInsuficiente` + `ItemSemEstoque`; só dados, listas copiadas e comparadas por elementos), `Resultado<T>`, `RegrasPedido` (`VerificarCliente`, `CriarPedido` com precedência RN-05 > RN-06 listando todas as ocorrências em ordem crescente, preço congelado, `decimal`, `DataPedido` em UTC truncado a segundos). **API**: `ValidadorCriarPedido` (RN-02..04, DEC-15: 50 itens e 10.000 unidades; chaves `clienteId`, `itens`, `itens[i].produtoId`, `itens[i].quantidade`; duplicata apontada na 2ª ocorrência e seguintes) e o contrato `CriarPedidoRequisicao`; `PeriodoNegocio` (datas civis de São Paulo → intervalo UTC semiaberto, deslocamento por data); `MapeadorErros` (`ErroHttp` → Problem Details; 400/422/409; erro SQL pelo número: 1205 → 503 `CONFLITO_CONCORRENCIA`, demais → 500 sem detalhes); códigos novos em `CodigosErroHttp`. **Testes**: UT-01..13 e UT-16..18 (UT-14/15 ficam para a E6), mais casos de horário de verão histórico (04/11/2018, 15/01/2019, dia de 25 h em 16/02/2019), meia-noite ambígua em fuso sintético, limites e imutabilidade. **Resultados**: 103/103 (77 UT + 26 IT; os 36 anteriores preservados); 77/77 UT também no Linux (`sdk:10.0`); build 0 avisos; auditoria NuGet sem pacotes vulneráveis (restore com base de vulnerabilidades atualizada, 0 NU1900–NU1904, e conferência manual dos 79 pacotes contra a base 2026.11.12.23.26.09); verificador de hooks 165/165; `git diff --check` limpo. **Revisão independente (agente)**: 0 ALTA, 2 MÉDIA, 7 BAIXA, todos tratados — (1) validador percorria todas as linhas acima de 50 itens (900 mil linhas → 69 MB de resposta): agora só o erro em `itens`; (2) `dataFim = 9999-12-31` lançava exceção (viraria 500 na E6): agora é “sem limite superior”; (3) guarda de pré-condições no domínio repetia RN-02..04 contra a §3.1: removida; (4) pré-condição de `produtosBloqueados` documentada e produtos extras cobertos por teste; (5) ramo de meia-noite inexistente era redundante (o `GetUtcOffset` da §8.2 já resulta no instante do salto): removido; ramo de meia-noite ambígua mantido e testado; (6) divergência Windows × Linux antes de 2004 registrada abaixo; (7) erros com lista guardavam a lista do chamador e não tinham igualdade de valor: corrigido; (8) UT-11 com contraprova em `double` e igualdade de `decimal` que ignora escala: agora compara o texto invariante (`"0.30"`, `"159.87"`); (9) mensagem com “10.000” fixo mantida de propósito (independe da cultura da máquina; teste amarra à constante) e chave `itens[i]` registrada como desvio. Cada correção foi provada por mutação: com as cinco revertidas numa cópia (validador, `dataFim` máxima, meia-noite ambígua e cópia defensiva dos dois erros), 6 testes falharam, ao menos um por correção. **Desvios do plano/spec (sem alterar a spec)**: `VerificarCliente(clienteId, cliente?)` em vez de `VerificarCliente(cliente?)`, para o erro levar o Id; `CriarPedido` recebe `DateTimeOffset agora` e converte para UTC; `Pedido` sem Id (gerado pelo banco na E5); meia-noite ambígua começa na primeira ocorrência, além da fórmula da §8.2 (só difere em 16/04/1950, no tzdata do Linux); item nulo na lista gera a chave `itens[i]`, caso não detalhado na tabela da §5.3. **Decisão humana (revisão externa da E4)**: o candidato aprovou a chave `itens[i]` para elemento nulo da lista; `{"clienteId":1,"itens":[null]}` resulta em 400 `VALIDACAO` com `errors` contendo `itens[0]` (coberto no UT-13). Registrado aqui, sem alterar a `docs/spec.md`; contrato `CriarPedidoRequisicao` criado já na E4 porque o validador o recebe. **Risco registrado**: as regras de fuso do Windows aplicam horário de verão sintético a São Paulo de 1914 a 2003 (7.259 dias com início diferente do tzdata); de 2004 em diante Windows e Linux coincidem, e todas as datas dos testes e do seed estão nesse período. |
| E5 | Concluída (commits `9d3184b`, `26c656b`, `1c4264d` e o `docs` seguinte) | **Endpoints**: `POST /pedidos` (201 com `Location: /pedidos/{id}` e o corpo da §5.3) e `GET /pedidos/{id:int}` (200 com o mesmo corpo; 404 `PEDIDO_NAO_ENCONTRADO`; id não numérico → 404 `RECURSO_NAO_ENCONTRADO` pelo framework). **Arquivos**: `Modulos/Pedidos/{PedidosEndpoints,ServicoPedidos,RepositorioPedidos}.cs`, `Contratos/PedidoResposta.cs`; `Compartilhado/FabricaConexao.cs` (string `ConnectionStrings:BancoPedidos`, falha rápida na inicialização); `TratadorExcecoes` (erro SQL pelo número via `MapeadorErros`: 1205 → 503 com log de aviso e `traceId`, demais → 500 sem detalhes); `MapeadorErros.PedidoNaoEncontrado`; `Program.cs`; Dapper 2.1.89 (última estável, sem dependências em net10.0) e `Microsoft.Data.SqlClient` 7.1.1 movido para o grupo de produção. Testes em `TestesIntegracao/Pedidos/` e apoio em `Infraestrutura/` (`DadosDeTeste`, `SessaoDeBloqueio`, `RespostaHttp`; `ConsultaSql` com parâmetros). Esquema, scripts, compose, MCP e spec inalterados. **Estratégia transacional (§7.2)**: validação fora da transação; uma transação `READ COMMITTED` explícita por pedido; cliente lido sem lock retido e, se ausente, retorno com descarte (rollback) antes de qualquer lock de produto; um `SELECT ... WITH (UPDLOCK, ROWLOCK)` por produto, em sequência e em ordem crescente de Id; regras puras do domínio; `UPDATE ... WHERE Estoque >= @Quantidade` na mesma ordem exigindo exatamente 1 linha; `INSERT` do pedido com `OUTPUT INSERTED.Id` (`DbType.DateTime2`, UTC truncado) e dos itens um a um (`DECIMAL(18,2)` declarado); commit. Transação descartada sem commit = rollback em qualquer retorno antecipado, exceção ou cancelamento (o token da requisição chega a todos os comandos); sem chamadas externas nem retry. **Testes**: 30 novos (1 UT + 29 IT): IT-01 a IT-06, IT-07 (só a consulta por id), IT-08 (8 casos, inclusive `itens: [null]` → 400 `VALIDACAO` com `itens[0]`, decisão da E4, e corpo vazio), IT-09, IT-10, IT-17, IT-25 (2 variantes), IT-26 (3 casos), mais barreira defensiva da baixa condicional, deadlock real → 503 e cancelamento durante espera por lock. **Resultados**: 133/133 (78 UT + 55 IT; os 103 anteriores preservados); classe de concorrência 5 vezes seguidas (4/4 em todas); suíte de integração em ordem aleatória com `--seed 20261112` e `20261113` (55/55); build 0 avisos; auditoria NuGet sem pacotes vulneráveis e conferência manual dos 85 pacotes resolvidos contra a base 2026.11.12.23.26.09 (8.010 advisories) e o índice servido pela borda (8.003), sem achados; verificador de hooks 165/165; `git diff --check` limpo; nenhum segredo nos arquivos alterados. **Evidências de concorrência**: IT-09 com largada por barreira: exatamente 5 × 201 e 15 × 409 (todos com disponível 0), estoque final 0, 5 pedidos e 5 itens, zero 500 e zero 503, e aumento de esperas `LCK_M_U` no servidor (disputa real); IT-10: 3 rodadas × 20 pedidos em ordens inversas, todos 201, estoques 940 e 880, zero 503; deadlock provocado com sessão de prioridade alta: contador de deadlocks do servidor incrementado, resposta 503 `CONFLITO_CONCORRENCIA`, nada gravado. **Evidências de rollback**: IT-25 com trigger que só falha depois das duas baixas e do item A (escrita parcial real), inclusive com `XACT_ABORT OFF` (transação ainda ativa, desfeita só pela API): 500 sem detalhes, estoques inalterados, zero pedidos e itens, locks liberados; IT-05 com armadilha que reprovaria qualquer `UPDATE` (rejeição antes da primeira escrita); barreira defensiva: baixa sem linha afetada → 409 e a baixa anterior desfeita; cancelamento: com B ainda bloqueado por outra sessão, A fica livre em até 5 s do cancelamento. **Mutações** (cópia descartável): 7 proteções desfeitas uma a uma (ordem dos locks, `UPDLOCK`, verificação de linhas afetadas, commit antes dos itens, 1205 como 500, `dataPedido` sem `Z`, cliente verificado depois dos produtos), todas reprovadas por ao menos um teste; sem a ordem crescente, o próprio IT-10 reprova com 503. **Revisão independente** (agente com o papel de `revisor-sql-concorrencia`): 0 ALTA, 2 MÉDIA, 2 BAIXA, todos corrigidos e provados por mutação — (1) IT-25 não provava o rollback da aplicação (o `THROW` em trigger condena a transação e o servidor desfaz sozinho): variante com `XACT_ABORT OFF`, que reprova a mutação "commit apesar da falha"; (2) o teste de cancelamento passava mesmo ignorando o cancelamento (o tempo limite de 30 s liberava o lock): prazo de 5 s contado do cancelamento, antes de aguardar o envio (no TestServer, a tarefa cancelada do cliente só termina com o servidor), agora reprova a mutação; (3) o ramo de requisição abortada do `TratadorExcecoes` rebaixava qualquer exceção (inclusive 1205) para 499/Information: removido, pois o `UseExceptionHandler` já responde 499 ao cancelamento comum; (4) IT-17 comparava JSON semanticamente (99.90 = 99.9): agora compara o texto. **Desvios**: o agente de projeto `revisor-sql-concorrencia` não estava disponível nesta sessão; a revisão usou um agente geral instruído a seguir a definição do arquivo; a versão inicial divergia da §7.2, passo 5 (a baixa sem linha afetada devolvia `EstoqueInsuficiente` direto, sem lançar exceção), corrigida no ajuste pós-revisão externa abaixo; o teste de deadlock real, opcional na §7.5, foi incluído; leitura de `GET /pedidos/{id}` em dois result sets (`QueryMultiple`) devolvendo o contrato diretamente do repositório. **Riscos residuais**: deadlocks continuam possíveis fora dos cenários testados (§7.5); verificado só no TestServer, não no Kestrel (E9); `READ_COMMITTED_SNAPSHOT` ligado não foi testado (a baixa condicional continua protegendo o estoque); uma corrida em que o cancelamento chegue como `SqlException` geraria log de erro 500 para um cliente já desconectado (não reproduzida); inserts desfeitos consomem valores de IDENTITY; desempenho não medido. **Ajuste pós-revisão externa (aprovação condicionada do candidato)**: a revisão externa de `ServicoPedidos` e `RepositorioPedidos` confirmou que o rollback já funcionava (o retorno antecipado de `Resultado.Falha` dentro do `await using` descartava a transação sem commit) e apontou só a divergência da §7.2, passo 5. Correção: a baixa sem linha afetada lança a exceção controlada privada `BaixaDeEstoqueRecusadaException` (mensagem genérica, carrega o `EstoqueInsuficiente` com o estoque relido sob lock); `CriarAsync` a captura fora do escopo da transação, portanto depois do rollback, e a converte em `Resultado.Falha` → 409 `ESTOQUE_INSUFICIENTE`; ela nunca chega ao tratador central. `LerEstoqueAsync` passou a devolver `int?` (`QuerySingleOrDefault`), tornando explícito o caso de linha inexistente (disponível 0) em vez de depender da conversão implícita de nulo do Dapper; com o lock U retido a linha não pode sumir. Conferido: contagem de linhas exige exatamente 1; ordem dos locks, ordenação de `Pedido.Itens`, `DECIMAL(18,2)` e propagação do token inalterados; fluxo de sucesso inalterado. Sem teste novo: o teste da barreira defensiva já cobre o contrato (409, baixas anteriores desfeitas, nada gravado). Mutação: com o `catch` desativado, esse teste falha recebendo 500 `ERRO_INTERNO` sem detalhes internos. Resultados após o ajuste: build 0 avisos; 133/133 (78 UT + 55 IT); classe de concorrência (IT-09, IT-10, deadlock 1205 → 503, cancelamento) mais 2 vezes, 4/4; auditoria NuGet sem vulneráveis; hooks 165/165; `git diff --check` limpo; spec inalterada. **Commits**: agrupados como pedido na aprovação (criação com IT-01..06, IT-08 e IT-26; consulta com IT-17, IT-07 parcial e UT-18; concorrência e rollback com IT-09, IT-10, deadlock, cancelamento, IT-25 e barreira defensiva; registro). Cada estado foi exportado do índice para uma pasta limpa e validado antes do commit: build 0 avisos e 120/120, 126/126 e 133/133. **Pendências**: parte de listagem do IT-07 (E6); string de conexão no compose e README de execução da API (E9); clipe para o vídeo. |
| E6 | Concluída (commits `29068b6`, `54489f7`, `6d64220`, `a3772de` e o `docs` seguinte) | **Skill** `.claude/skills/novo-endpoint/SKILL.md` (frontmatter `name`, `description` e `argument-hint`; argumentos via `$ARGUMENTS`): recebe módulo, verbo, rota e seção da spec; manda ler spec, plano, rules e os arquivos de referência da E5; orienta endpoint, contratos, validador, repositório e registro no `Program.cs`; exige UT e IT, build sem avisos, testes, auditoria NuGet, `git diff --check`, spec inalterada e busca de segredos; proíbe mexer em spec, esquema, compose, fluxo da E5 e histórico Git; termina com checklist de aderência. **Evidência de uso (CA-21)**, na transcrição da sessão (`~/.claude/projects/C--Projetos-joya-mini-gestao-pedidos/14f30726-0e03-4313-b49e-2b8a84303fa2.jsonl`, horários UTC de 13/11/2026): às 12:11:25 a primeira invocação foi recusada com `Unknown skill: novo-endpoint`, porque a pasta `.claude/skills/` não existia quando a sessão começou; o candidato reiniciou o app e retomou a sessão; depois houve 4 invocações reais da ferramenta `Skill`, todas com `Launching skill: novo-endpoint`: 12:19:06 `Catalogo GET /clientes §5.7`, 12:25:00 `Catalogo GET /produtos §5.8`, 12:30:26 `Pedidos GET /pedidos §5.4` e 12:40:20 `Relatorios GET /relatorios/top-clientes §5.6`. Cada endpoint seguiu os passos da skill e fechou com a validação dela. **Endpoints**: `GET /clientes` e `GET /produtos` (módulo `Catalogo`, `ORDER BY Nome, Id`, sem filtros nem paginação, produto com estoque 0 incluído, preço `decimal` com duas casas); `GET /pedidos` (listagem no módulo `Pedidos`, contrato da §5.4); `GET /relatorios/top-clientes` (módulo `Relatorios`, contrato da §5.6). Nenhuma escrita nova. O fluxo de criação da E5 não mudou: o diff de `RepositorioPedidos` e `PedidosEndpoints` só acrescenta. **Arquivos**: novos em `Api/Compartilhado/` (`DataCivil`, `ValidacaoDePeriodo`, `FiltroDePeriodoSql`), `Api/Modulos/Catalogo/` (`CatalogoEndpoints`, `RepositorioCatalogo` e 2 contratos), `Api/Modulos/Pedidos/` (`ValidadorListarPedidos`, `Contratos/ListarPedidosParametros`, `Contratos/ListaPedidosResposta`) e `Api/Modulos/Relatorios/` (`RelatoriosEndpoints`, `RepositorioRelatorios`, `ValidadorTopClientes` e 2 contratos); alterados `Program.cs`, `PedidosEndpoints.cs`, `RepositorioPedidos.cs` e `DadosDeTeste.cs` (e-mail opcional). Testes novos: `TestesUnitarios/Pedidos/ValidadorListarPedidosTestes`, `TestesUnitarios/Relatorios/ValidadorTopClientesTestes`, `TestesIntegracao/Catalogo/CatalogoTestes`, `TestesIntegracao/Pedidos/ListarPedidosTestes` e `TestesIntegracao/Relatorios/{DadosDeReferenciaRanking,TopClientesTestes}`. Sem dependência nova; esquema, scripts, compose, `.mcp.json` e spec inalterados. **Validação e filtros**: datas recebidas como texto e lidas em formato exato `yyyy-MM-dd` (`DateOnly.TryParseExact`, cultura invariante, sem espaços); os inteiros (`clienteId`, `pagina`, `tamanhoPagina`) ficam com o binding do framework. Os validadores são puros e devolvem os erros por campo ou o filtro com os padrões (página 1, tamanho 10). Faixas são rejeitadas, nunca ajustadas. `dataInicio > dataFim` vai na chave `dataInicio`; data inválida não entra na comparação; `?dataFim=` (texto vazio) é `VALIDACAO`. **Interpretação registrada** (achado da revisão): número fora do `int` (`99999999999`) ou fracionário (`1.5`) na query falha no binding e vira 400 `REQUISICAO_INVALIDA`, como no corpo JSON da §5.1. A §5.4 só cita "não numérico". **Decisões SQL**: WHERE dinâmico só com fragmentos constantes (`p.ClienteId = @ClienteId`, `p.DataPedido >= @InicioUtc`, `p.DataPedido < @FimExclusivoUtc`) e valores sempre parametrizados (`Int32`, `Int64`, `DateTime2`). O predicado de período fica em `FiltroDePeriodoSql`, comum à listagem e ao ranking, e só inclui os limites presentes; assim `dataFim = 9999-12-31` (sem dia seguinte representável) não restringe, em vez de zerar o ranking. Listagem: `COUNT(*)` e página no mesmo lote (`QueryMultiple`: um round-trip, duas consultas com o mesmo WHERE); CTE com `ORDER BY p.DataPedido DESC, p.Id DESC` e `OFFSET @Deslocamento` em `bigint`, calculado em `long`; totais por `CROSS APPLY` só das linhas da página (`COUNT(*)` de linhas e `SUM(Quantidade * PrecoUnitario)`), sem N+1; `totalPaginas` em aritmética inteira. Ranking: `SELECT TOP (5)`, `INNER JOIN` em clientes e itens, `GROUP BY c.Id, c.Nome`, `ORDER BY ValorTotal DESC, c.Id`, sem `YEAR()`; `posicao` vem da enumeração na aplicação, como a §9.4 permite. Catálogo: `ORDER BY Nome, Id`. **DR-RANK** (`DadosDeReferenciaRanking`, inserido via SQL em banco limpo). Resultado de 2026 pelo endpoint: 1 Bruno Lima 500.00; 2 Elisa Rocha 450.00; 3 Carla Dias 400.00; 4 Diego Alves 400.00 (empate com Carla, que tem o Id menor); 5 Ana Souza (ana1) 300.00, exatamente como na §9.5. Fábio (31/12/2025 23:59:59 local) e o segundo pedido de Bruno (01/01/2027 00:00:00 local) ficam fora; Elisa e Diego ficam dentro; os homônimos ficam separados (no período de março a abril, Ana1 com 300.00 e Ana2 com 250.00, em linhas distintas). **Diferenças em relação à tabela da §9.5, sem mudar nenhum resultado esperado**: um cliente sem pedidos acrescentado por último, para provar que quem não comprou não entra; o pedido de Ana1 em duas linhas (2 × 100,00 + 1 × 100,00), para exercitar `Quantidade × PrecoUnitario`; os produtos custam 1,00 hoje, diferente dos preços gravados nos itens, para provar o preço congelado. O seed compartilhado não foi alterado. **Testes**: 75 novos (31 UT + 44 IT). UT-14: 22 (`ValidadorListarPedidosTestes`). UT-15: 9 (`ValidadorTopClientesTestes`). IT-22: 3 (`CatalogoTestes`). Listagem: 26 (`ListarPedidosTestes`): IT-07 (parte da listagem); IT-11 com envelope, campos e desempate no mesmo segundo, e com lista vazia; IT-12 com cliente existente e inexistente; IT-13 nas 4 fronteiras, com limites unilaterais e a contagem deles, e um dia de horário de verão de 2018 (UTC−2); IT-14; IT-15 com 23 pedidos, desempate entre páginas e `pagina = int.MaxValue`; IT-16 com 10 casos `VALIDACAO` e 4 `REQUISICAO_INVALIDA`. Ranking: 15 (`TopClientesTestes`): IT-18 com posições 1 a 5, homônimos e desempate; IT-19 com as fronteiras e os dois dias vizinhos; IT-20 com 3 compradores, período vazio e 6 entradas inválidas; `dataFim = 9999-12-31`. IT-23 e IT-24 ficam para a E8. **Mutações** (cópia da árvore no scratchpad, 18 mutantes, cada um com a classe de teste alvo): desempate da listagem invertido no CTE; deslocamento em `int`; `COUNT` sem filtros; `quantidadeItens` somando unidades; total da listagem pelo preço atual; limite superior inclusivo; UTC−3 fixo; ranking agrupado por nome; desempate do ranking invertido; ranking crescente; `TOP 6`; ranking pelo preço atual; limite superior nulo sempre aplicado; `tamanhoPagina` até 1000; datas do ranking não obrigatórias; clientes por Id; produtos sem esgotados; `dataInicio > dataFim` aceito. Todos morreram, mas não de primeira: o desempate invertido só no CTE **sobreviveu**, porque nenhum teste tinha pedidos no mesmo segundo divididos entre páginas (o `ORDER BY` externo escondia o erro). O teste `ListarPedidos_PedidosNoMesmoSegundoEntrePaginas_DesempataPeloIdSemRepetirNemPular` foi acrescentado e mata o mutante. O mutante de datas obrigatórias foi reescrito para compilar (`if (false)` gera CS0162, que é erro com warnings como erro) e morreu. **Revisão independente**: agentes de projeto `revisor-spec` e `revisor-sql-concorrencia`, agora carregados, em paralelo e somente leitura. Resultado: 0 ALTA, 0 MÉDIA e 7 BAIXA. Corrigidos: UT-14 sem `Assert.Null(filtro)` e sem conferir as chaves exatas em 3 testes (reforçados); a skill não citava `DataCivil`, `ValidacaoDePeriodo` e `FiltroDePeriodoSql` (incluídos), e `RelatoriosEndpoints` não usava o `Problema(...)` do padrão (alinhado); os limites unilaterais não conferiam `totalItens` (asserção acrescentada). Registrados sem mudança de código: a interpretação de `REQUISICAO_INVALIDA` para número fora do `int` e as diferenças do DR-RANK (ambas acima); parâmetros de período enviados como `datetime2(7)` contra a coluna `datetime2(0)` (o plano estimado mostrou `Index Seek` em `IX_Pedidos_DataPedido` com `GetRangeWithMismatchedTypes`, predicado SARGable; o efeito na estimativa é hipótese, a medir na forma parametrizada na E8, §9.6); um pedido sem itens apareceria com `valorTotal` 0 sem casas decimais (inalcançável pela API, RN-02 e RN-10; hipótese não executada). **Desvio de procedimento do revisor de SQL**: para ver um plano, gravou e apagou um arquivo temporário (`/tmp/plano.sql`) dentro do container de desenvolvimento, sem alterar o banco. Ele também conferiu no banco de dev `is_read_committed_snapshot_on = 0` e os tipos de resultado (`ValorTotal decimal(38,2)`). **Resultados**: restore sem erros; build com 0 avisos e 0 erros; 208/208 (109 UT + 99 IT; os 133 anteriores preservados); UT-14 e UT-15 isolados 31/31; listagem e ranking isolados 41/41; catálogo 3/3; regressão dos ITs da E5 (criação, consulta e concorrência) 29/29; auditoria NuGet com transitivos sem vulneráveis nos 4 projetos; hooks 165/165; `git diff --check` limpo; nenhum segredo no diff nem nos arquivos novos; spec inalterada. **Decisão humana (aprovação)**: o candidato aprovou os quatro endpoints e a skill com o uso registrado, aceitou os achados baixos sem pedir funcionalidade nova e classificou a escrita temporária do revisor de SQL como desvio: o registro fica, e revisões declaradas somente leitura não fazem operações de escrita, nem temporárias. **Commits**: na ordem do plano, com a skill antes dos endpoints: `chore: adiciona skill novo-endpoint`; `feat(catalogo): lista clientes e produtos` (com o e-mail opcional em `DadosDeTeste`); `feat(pedidos): lista pedidos com filtros e paginacao` (com os auxiliares de período, usados primeiro pela listagem); `feat(relatorios): ranking dos cinco maiores clientes por periodo`; e o registro. O `Program.cs` do commit do catálogo ainda não registra o módulo `Relatorios`, que só existe no commit do ranking. Cada estado foi exportado do índice para uma pasta limpa e validado antes do commit: build com 0 avisos e 133/133, 136/136, 184/184 e 208/208. **Riscos residuais**: escolha de índice da listagem e do ranking não medida (hipóteses da §8.5; medição na E8); contagem e página sem snapshot comum (aceito pela §8.3); verificado só no TestServer, não no Kestrel (E9). O `CLAUDE.md` não mudou, porque não surgiu comando novo. **Pendências**: conexão do MCP `sqlserver-joya`, que falhou nesta sessão (tempo de conexão de 30 s esgotado) e não bloqueou a E6, a resolver na E7; IT-23, IT-24 e a investigação de desempenho (E8), incluindo a forma parametrizada do ranking; API no Docker (E9); clipe da skill em uso para o vídeo. |
| E7 | Concluída (commits `83d6761` e o `docs` seguinte) | **Conexão real no Claude Code** (app desktop, Windows, 13/11/2026; transcrição `~/.claude/projects/C--Projetos-joya-mini-gestao-pedidos/e0e76574-78c6-4550-8d7a-6dfecf33369a.jsonl`): servidor `sqlserver-joya` do `.mcp.json`, DBHub 1.6.1 por stdio; todas as consultas da E7 só pelas ferramentas MCP `execute_sql` e `search_objects`. Na E6 a conexão tinha esgotado o tempo de 30 s; nesta sessão conectou (`SELECT @@VERSION` → 16.0.4325.4, CU28, Developer). **Identidade**: `SYSTEM_USER` = `ORIGINAL_LOGIN()` = `joya_mcp`, `DB_NAME()` = `JoyaPedidos`, TCP com `encrypt_option = TRUE`, autenticação SQL. **Permissões** (`fn_my_permissions`): servidor `CONNECT SQL`, `VIEW SERVER PERFORMANCE STATE` e `VIEW ANY DATABASE` (`public`); banco `CONNECT`, `SELECT`, `VIEW DATABASE PERFORMANCE STATE` e as duas `VIEW ANY COLUMN ... KEY DEFINITION` (`public`); papel único `db_datareader`. Zero em: 8 papéis fixos de servidor, `db_owner`, `db_datawriter`, `db_ddladmin`, `db_securityadmin`, `db_accessadmin`, `db_backupoperator`, `CONTROL SERVER`, `VIEW SERVER STATE`, `ALTER ANY LOGIN` e, no banco, `CONTROL`, `ALTER`, `CREATE TABLE`, `INSERT`, `UPDATE`, `DELETE`, `EXECUTE`, `SHOWPLAN`. Nenhuma permissão indevida. **Escrita negada pelo banco** (modo somente leitura do DBHub desligado): `UPDATE`, `INSERT` e `DELETE` → erro 229; `CREATE TABLE` → 262. Cada tentativa com `WHERE 1 = 0`, `XACT_ABORT ON` e `ROLLBACK` no mesmo lote; `@@TRANCOUNT = 0` depois; retrato antes/depois (linhas, `CHECKSUM_AGG(BINARY_CHECKSUM(*))`, `IDENT_CURRENT` das 4 tabelas, contagem e checksum de `sys.objects`) idêntico; `TesteNegativoMcp` inexistente. **Consultas**: estrutura das 4 tabelas, chaves e restrições; índices de `Pedidos` e `ItensPedido` (iguais aos da §4, 2 páginas cada); seed (10/12/33/52, fronteiras, preço congelado, último pedido `2026-11-01T02:59:59Z`), conferindo com a E3; pedidos de 01/01 a 31/10/2026 local (18 pedidos, 10 clientes, 5.513,57); `sys.dm_exec_query_stats` com texto, `dbid` do plano e plano XML em cache; `sys.dm_db_index_usage_stats`; índices ausentes (vazio). **Validação da senha** (`database/dev/criar-login-mcp.sql`, correção já presente no working tree ao iniciar a E7): a regra `[^A-Za-z0-9_-]` rejeitava toda senha com `_` ou `-`, porque, com a `COLLATE Latin1_General_BIN2` do script, o SQL Server lê `_-]` como faixa `_`–`]` (invertida e vazia por código de caractere), o que foi medido via MCP; nas collations não binárias (a do banco e `Latin1_General_CI_AS`), `_` e `-` casam. Correção confirmada: `[^A-Za-z0-9\_\-]` com `ESCAPE N'\'`, política mantida (8+, só ASCII, `_`, `-`; `CHECK_POLICY = ON`). Testes só com valores sintéticos: 28 casos da expressão via `execute_sql` (nova: 6 aceitos e 22 rejeitados, como esperado; antiga: 5 válidos rejeitados) e 11 de ponta a ponta com o `sqlcmd` executando só o primeiro lote do arquivo (sem `ALTER LOGIN`): 4 aceitos (exit 0), 6 rejeitados com `Msg 50001`, apóstrofo com `Msg 102`; HEAD rejeitava `_` e `-`. Senha real não usada, login não alterado. **Configuração revisada**: `.mcp.json` com JSON válido e sem segredo (`${JOYA_MCP_SENHA}` e `${SQL_PORTA:-1433}`); `cmd /c npx -y @bytebase/dbhub@1.6.1 --transport stdio`, sem senha na linha de comando dos processos; DBHub instalado 1.6.1 = mais recente no npm; advisories reconsultados no GitHub (GHSA-fm8p-53ww-hf6w, GHSA-mwwr-p57h-56pf, GHSA-j656-3hf2-fvjc, todos corrigidos até 0.22.6; nenhum novo); porta do SQL só em `127.0.0.1:1433`. **Descobertas para a E8**: o DBHub devolve vários result sets e as mensagens informativas (`STATISTICS IO`, mensagem 3615, em `messages`); plano em cache legível sem `SHOWPLAN`; `dm_exec_sql_text.dbid` é `NULL` em ad hoc (filtrar por `dm_exec_plan_attributes`); com `DECLARE`, a estimativa foi 5,4 linhas para 18 reais (medir na forma parametrizada); planos com 2 páginas por índice não são indicativos (a E8 precisa da massa); `UNION` de catálogos exige `COLLATE DATABASE_DEFAULT`; `joya_mcp` não vê `definition` de `CHECK`. **Desvios do plano**: o modo somente leitura do DBHub não foi ativado, porque a 1.6.1 removeu `--readonly` e só o aceita por arquivo TOML com os dados de conexão; a garantia segue sendo o banco, como o plano prevê. Teste ponta a ponta da validação feito com `docker compose run` + `sqlcmd` (só o lote de validação), fora do MCP, porque o `sqlcmd` é quem substitui `$(JOYA_MCP_SENHA)`. Antes disso, duas tentativas pelo PowerShell 5.1 não executaram nada: o pipe inseriu um BOM e o primeiro lote falhou com `Msg 102`. **Achado de ambiente**: `JOYA_MCP_SENHA` existe no processo do app, mas não no ambiente persistente do usuário nem da máquina; abrir o app por outro caminho pode deixar o MCP sem senha. **Revisão independente** (agente geral, somente leitura, só `SELECT`, catálogos e DMVs pelo MCP; sem tentativas de escrita, conforme a decisão da E6): nenhum privilégio indevido; reconfirmou de forma independente identidade, permissões, índices, seed, período, mensagens de `STATISTICS IO`, plano em cache sem `SHOWPLAN` e a regra nova da senha (34 valores sintéticos, inclusive Unicode, controle e espaços nas pontas: só `[A-Za-z0-9_-]` com 8+ caracteres; nada aceito quebra a DSN nem o literal SQL). 0 ALTA, 0 MÉDIA, 5 BAIXA e 1 informativo, todos tratados no README: (1) a causa do `LIKE` só vale para `BIN2` (conferido por mim via MCP: com a collation do banco e com `CI_AS`, `_` e `-` casam), texto qualificado; (2) `setx` com o valor deixaria a senha no histórico do terminal e nos argumentos do processo: README orienta a tela de variáveis do Windows ou `Read-Host -AsSecureString` (o texto da E3/E7 deste plano não foi alterado); (3) `VIEW SERVER PERFORMANCE STATE` expõe consultas em cache da instância inteira (conferido: 131 planos de 3 bancos), registrado como risco aceito; (4) a transcrição é local e não versionada, e a evidência para o avaliador é o clipe; (5) "estoque até 2" → "entre 1 e 2"; (6, informativo) substituição da senha antes da validação no `sqlcmd`, já listada nos riscos. Não cobertos pela revisão: escritas negadas, retrato antes/depois e testes com `sqlcmd` (fora do escopo somente leitura dela). **Resultados**: `.mcp.json` válido; nenhum segredo no diff; `git diff --check` limpo; spec inalterada; banco inalterado (retrato idêntico). Sem build nem testes .NET: nenhum código ou script da aplicação mudou. **Decisão humana (aprovação)**: o candidato aprovou os resultados e os riscos residuais documentados, com uma ressalva de segurança: o `sqlcmd` substitui `$(JOYA_MCP_SENHA)` no texto do script antes da validação SQL, o que permite injeção se a variável receber conteúdo malicioso. O README registra que o script é exclusivo de desenvolvimento, deve ser executado manualmente com senha gerada por procedimento controlado, não deve receber valores de origem não confiável e não deve servir de provisionamento em produção. Sem nova implementação de gerenciamento de credenciais. **Pendências**: clipe do MCP para o vídeo; persistir `JOYA_MCP_SENHA` no ambiente do usuário, sem `setx` com o valor na linha de comando, se o candidato quiser. **Commits** (após a aprovação, com a regra de senha revalidada e o banco com retrato idêntico): `fix(banco): corrige validacao de senha do login do mcp` (só o script) e `docs: registra validacao de permissoes do mcp` (README e este registro). |
| E8 | Concluída (commits `60d905a`, `fb996c6` e o `docs` seguinte) | **Queries**: `database/consultas/ranking-2026-original.sql` e `ranking-2026-corrigida.sql` extraídas byte a byte dos blocos da spec §9.1 e §9.3 (UTF-8 sem BOM, LF); a corrigida é batch único sem `GO`, ranking completo, `DECIMAL(38,2)` com 2 casas. **Testes** (`TestesIntegracao/Relatorios/RankingSqlTestes`; executam os arquivos sem alteração via SqlClient sobre o DR-RANK da E6, inalterado, em banco de teste limpo): IT-23 em 5 testes (6 posições exatas da §9.5; homônimos separados; empate pelo menor `ClienteId`; fronteiras do ano local; preço histórico, com o preço atual dos 2 produtos trocado para 9.999,99 sem mudar o resultado); IT-24 em 3 testes (5 linhas `Nome`/`Total`; totais 400, 400, 500, 550 e 1.000 em ordem crescente; uma "Ana Souza" com 550,00; Fábio dentro e Elisa fora; o nome das duas primeiras linhas não é verificado); 1 teste do CA-12 que compara o arquivo original com o bloco da spec. **Mutações** (arquivo alterado temporariamente e restaurado, hash conferido): ordem crescente, limites do ano UTC, desempate invertido, soma sem quantidade e `DESC` "corrigido" na original; 5 de 5 mortos, com 2 a 3 falhas cada. **Ambiente isolado** `joya-perf`: `docker-compose.perf.yml` (sobreposição com `name: joya-perf`, `hostname: joya-perf-sqlserver` e `MSSQL_MEMORY_LIMIT_MB=2048`), sempre usado com `-p joya-perf`. Antes de cada carga foram conferidos projeto, volume `joya-perf_dados-sql`, rede, ausência de porta, montagens (o `joya-pedidos-sqlserver-1` intacto) e a identidade da instância. **Massa** (`database/performance/gerar-massa.sql`): set-based sobre tally, sem `WHILE`/`NEWID`/`RAND`, numa transação; índices reconstruídos e `UPDATE STATISTICS ... FULLSCAN`. Trava pela instância (`SERVERPROPERTY('MachineName') = 'joya-perf-sqlserver'`); idempotente (reexecução sem efeito, código 0). Volume: 1.210 clientes (600 nomes homônimos, 50 sem pedidos), 262 produtos, 120.033 pedidos (≈ 40 mil por ano local, 2024–2026), 360.052 itens; 20 s. Determinismo comprovado: duas gerações em instâncias distintas deram `CHECKSUM_AGG` e soma idênticos. **Medição** (`medir-ranking.sql` + `medir-variante.sql`; cada variante é incluída pelo próprio arquivo via `:r`): aquecimento + 3 rodadas com `STATISTICS IO, TIME` + plano real com `STATISTICS XML`. Cache de dados aquecido, sem limpar caches da instância; Query Store e feedbacks de memória e cardinalidade desligados só no banco isolado; 5 passadas independentes, com mediana das medianas e faixa calculadas por `resumir-medicao.ps1` (versionado). Variantes: V0, V1, V0 e V1 com `MAXDOP 1` (diagnóstico), V2, V2 `sp_executesql` com `DATETIME2(0)` e `(7)`, V2 com variáveis locais, SQL da API (`TOP 5`) e, sem `IX_Pedidos_DataPedido` (removido e recriado só no banco isolado), V2, V2 `(7)` e V0. **Resultados** (leituras idênticas nas 5 passadas): V0 2.416 leituras, 963 ms de CPU e 198 ms decorridos (paralela, DOP 12, row mode); V2 1.517, 54 e 60 ms (serial, batch mode). Em série: V0 1.715 leituras e 247 ms de CPU; V1 1.517 e 157 ms. A D6 está confirmada: `Pedidos` 300 → 102 leituras e CPU −36%, faixas sem sobreposição. A maior parte da diferença de CPU da V0 paralela vem do paralelismo. Sem `IX_Pedidos_DataPedido`: `Pedidos` 344 leituras contra 102 (−70%) e CPU/decorrido 86/90 contra 54/60 ms; tempo menor com o índice em 5/5 passadas (decorrido) e 4/5 (CPU); ganho modesto e provável. Literais e parâmetros estimam o intervalo quase exatamente (39.975 / 39.982). Variáveis locais estimam 19.723 (seletividade fixa 0,3 × √0,3), com o mesmo formato de plano. `DATETIME2(7)` e o literal geram busca dinâmica (`GetRangeWithMismatchedTypes`); `DATETIME2(0)` faz a busca direta; nenhuma diferença de tempo mensurável entre essas formas. Sem `Key Lookup` nem *spill*. **Achado novo**: todas as formas da V2 e o SQL da API têm `Excessive Grant` (pedem 71 a 88 MB, usam até 3,1 MB). Com as configurações padrão do SQL Server 2022, o feedback de memória corrige isso (6.304 KB na rodada inicial). Registrado como observação e risco sob concorrência, não medido; nada alterado na API. **Decisão de índices**: nenhuma mudança no esquema (os 3 índices mantidos, com justificativa no README); IT-21 e `02-criar-tabelas.sql` inalterados. **MCP** (banco de desenvolvimento, só leitura; registro em `docs/evidencias/parte4/mcp-banco-desenvolvimento.md`): V0 e V2 com `STATISTICS IO` em `messages`; os defeitos aparecem também no seed ("Ana Souza" 1.952,35; Elisa 509,67 × 159,77, explicado pelo pedido 15 em `2026-01-01T02:59:59Z`); operadores do plano em cache por XQuery; estatísticas; a trava dos scripts recusaria o dev (`MachineName` diferente). Achado: no seed, a V2 usa outro plano (seek em `IX_Pedidos_ClienteId_DataPedido` por cliente), o que justificou a massa. **Evidências**: `docs/evidencias/parte4/` (logs das 5 passadas sem as linhas de resultado, `consolidado.txt`, 12 planos reais da passada A, 2 planos da rodada inicial com feedback e o registro do MCP). **Arquivos**: novos `docker-compose.perf.yml`, `database/consultas/` (2), `database/performance/` (`gerar-massa.sql`, `medir-ranking.sql`, `medir-variante.sql`, `resumir-medicao.ps1`, `variantes/` com 7 arquivos), o teste e as evidências; alterados `README.md` (Parte 4), `CLAUDE.md` (comandos do `joya-perf`) e este registro. API, frontend, fluxo transacional da E5, `.mcp.json`, scripts automáticos e spec inalterados. **Revisão independente** (agentes `revisor-spec` e `revisor-sql-concorrencia`, em paralelo e só leitura; o de SQL consultou o `joya-perf` só com `SELECT`/`SET STATISTICS`): 0 ALTA, 4 MÉDIA e 10 BAIXA. As MÉDIA, todas reproduzidas e corrigidas: (1) as travas não identificavam o ambiente (`CONFIRMAR_MASSA` só confirmava a intenção; `medir-ranking` aceitaria o dev se ele tivesse a massa) → trava pelo hostname da instância, conferida via MCP contra o dev; (2) `medir-ranking` não era reentrante após abortar entre `DROP` e `CREATE` → recria o índice ausente no início e usa `DROP INDEX IF EXISTS`; (3) a redução de leituras atribuída à D6 estava inflada pelo paralelismo (V0 serial lê 300 páginas de `Pedidos`, não 895; reproduzido) → variantes `MAXDOP 1` e conclusões refeitas; (4) feedback de concessão de memória e Query Store ligados ligavam as passadas entre si (planos com `IsMemoryGrantFeedbackAdjusted="Yes"`; reproduzido) → condições controladas e as 5 passadas refeitas do zero, com o ambiente recriado. As BAIXA, corrigidas: conclusões além dos números reescritas como hipóteses e com o critério de faixa; números da memória citados dos planos versionados; comando de medição completo no `CLAUDE.md`; idempotência da massa conforme a §4.6 (a rejeição inicial deste achado foi revista após reler a §4.6); `Assert.Equal(2, ...)` no arranjo do teste de preço histórico; comentário da estimativa por variáveis locais (palpite fixo, não densidade); tally sem `TOP` sem `ORDER BY` e `BIGINT` no cálculo das datas; redirecionamento da saída para fora do repositório; "batch mode" da V2 sem índice qualificado; páginas reportadas após a reconstrução; script de consolidação versionado; evidências do MCP versionadas. **Divergências e decisões**: (1) **decisão humana** desta entrega: a massa não vai para o banco de demonstração. A §9.6 e a §11.5 previam `docker compose run --rm db-init` no projeto padrão seguido de `down -v`, substituídos pelo projeto isolado `joya-perf` (a spec não foi alterada); (2) **divergência com a spec, aceita pelo candidato (decisão humana de 13/11/2026)**: a §9.2 (D3) diz que o otimizador simplifica o `LEFT JOIN Pedidos` em junção interna. Na semântica relacional, a junção externa com `WHERE` sobre `p.DataPedido` equivale à interna, e o resultado é o descrito. Nos planos físicos observados, porém, a simplificação **não** acontece com `YEAR()`: na massa, a V0 paralela usa `Hash Match` e a serial `Merge Join`, e no seed (via MCP) aparece `Nested Loops`; nos três, a junção continua `Left Outer Join` e o `Filter` vem depois. Com o predicado em intervalo (V1), vira `Inner Join`. A justificativa e as evidências (planos `v0-original`, `v0-maxdop-1-diagnostico`, `v1-original-com-intervalo` e o registro do MCP) estão no README. O candidato decidiu não alterar a spec e manter o registro aqui e no README; (3) desvio do plano: a medição via MCP prevista para "V0, V2 e V2 sem IX" foi feita só com V0 e V2 sobre o seed, porque o MCP aponta para o dev, que não tem a massa, e o `joya_mcp` não pode remover índices; a variante sem índice foi medida por `sqlcmd` no `joya-perf`; (4) V1, as variantes `MAXDOP 1` e as 5 passadas foram acrescentadas para separar ruído e paralelismo dos efeitos reais. **Reentrância testada**: com o índice removido de propósito, o `medir-ranking.sql` o recriou e terminou com código 0. **Limpeza**: alvo conferido (projeto `joya-perf`, volume `joya-perf_dados-sql`) antes de cada `down -v`; containers, volume e rede do `joya-perf` removidos; `joya-pedidos-sqlserver-1` no ar e os 11 volumes nomeados da máquina (o do `joya-pedidos` e os de outros projetos) intactos. **Banco de desenvolvimento**: pelo MCP, retrato idêntico ao do início (10/12/33/52, mesmos checksums das 4 tabelas e dos 18 objetos), os 3 índices presentes, nenhum cliente da massa, Query Store intocado. **Validação final**: restore sem erros; build 0 avisos; 217/217 (208 anteriores + 9 novos); `RankingSqlTestes` isolado 9/9; auditoria NuGet com transitivos sem pacotes vulneráveis; hooks 165/165; `git diff --check` limpo e 36 arquivos novos sem CR, BOM ou espaço no fim; nenhum segredo (sem `-P`, sem DSN com senha, senha do MCP ausente, nenhuma linha nova com a senha padrão); spec inalterada; nada alterado em `backend/src`, `.mcp.json`, scripts automáticos ou composes base e dev. **Decisão humana (aprovação)**: o candidato aprovou as queries original e corrigida, os resultados do DR-RANK, IT-23 e IT-24, a massa determinística e o ambiente isolado `joya-perf`, o protocolo controlado de 5 passadas e a conclusão de manter os índices existentes. Aceitou os riscos residuais documentados (medição num único notebook, só com cache aquecido, concorrência não medida, concessão de memória excessiva da V2 corrigida pelo feedback padrão) e a divergência do D3, e não pediu novas otimizações. **Estado final do ambiente**: `joya-pedidos` no ar com o seed original, `joya-perf` removido. **Commits**: `feat(banco): adiciona query de ranking original e corrigida` (consultas e `RankingSqlTestes`), `feat(banco): adiciona script de massa para medicao de desempenho` (`docker-compose.perf.yml`, `database/performance/` e `CLAUDE.md`) e `docs: documenta investigacao e medicoes da query de ranking` (README, este registro e as evidências). **Pendências**: clipe da investigação para o vídeo (gate da E8). |
| E9 | Não iniciada | — |
| E10 | Não iniciada | — |
| E11 | Não iniciada | — |
| E12 | Não iniciada | — |
| E13 | Não iniciada | — |
