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
| Testes unitários e de integração | `dotnet test backend/JoyaPedidos.slnx` (ou `cd backend && dotnet test`) |
| Só um projeto de teste | `dotnet test --project backend/tests/JoyaPedidos.TestesUnitarios` |
| Pacotes vulneráveis, inclusive transitivos | `dotnet list backend/JoyaPedidos.slnx package --vulnerable --include-transitive` |
| SDK e runtimes desatualizados | `dotnet sdk check` |
| Verificar os hooks do Claude (qualquer SO com Node) | `node .claude/hooks/verificar-hooks.mjs` |
| Mesma verificação em Linux via Docker, no Git Bash | `MSYS_NO_PATHCONV=1 docker run --rm -v "$PWD":/w:ro -w /w node:24-alpine node .claude/hooks/verificar-hooks.mjs` |
| Mesma verificação em Linux via Docker, no PowerShell | `docker run --rm -v "${PWD}:/w:ro" -w /w node:24-alpine node .claude/hooks/verificar-hooks.mjs` |

Comandos de Docker e frontend serão acrescentados quando existirem (entregas E3 e E10). Não invente comandos.

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
