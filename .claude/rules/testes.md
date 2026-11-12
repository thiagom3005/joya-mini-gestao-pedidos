---
paths:
  - "backend/tests/**"
  - "e2e/**"
---

# Regras de testes

Referência: spec §10 (catálogo UT, IT, E2E).

- Cada teste implementa um cenário do catálogo; cite o ID no nome da classe ou em comentário curto (`// IT-09`).
- Nome: `Metodo_Cenario_ResultadoEsperado` em pt-BR (ex.: `CriarPedido_SemItens_RetornaErroDeValidacao`).
- Arrange/Act/Assert explícitos, um comportamento por teste, dados visíveis no próprio teste.
- xUnit v3 com `Assert` nativo (sem FluentAssertions). Passe `TestContext.Current.CancellationToken` às chamadas
  assíncronas.
- Integração: SQL Server real via Testcontainers, scripts aplicados com `sqlcmd -C -b` dentro do container.
  Execução sequencial (banco compartilhado). Limpeza antes de cada teste remove dados e triggers de teste.
  O IT-21 usa container próprio; nunca `DROP DATABASE` no banco compartilhado.
- Asserções nunca dependem de `Id` fixo: use os ids retornados ou inseridos. Datas controladas com `FakeTimeProvider`.
- Concorrência (IT-09, IT-10): largada simultânea com barreira; zero 500 é requisito; zero 503 é objetivo
  assertado. Um 503 inesperado é investigado, nunca mascarado (spec §7.5).
- Proibido enfraquecer ou remover asserção para fazer um teste passar, e proibido marcar teste como ignorado
  sem registro no `docs/plan.md`.
- E2E (Playwright): stack recém-criado (`docker compose down -v` antes), dados do seed lidos pela API.
