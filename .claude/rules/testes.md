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
- Asserções nunca dependem de `Id` fixo: use os ids retornados ou inseridos. Datas controladas com
  `fabrica.Relogio` (`RelogioDeTeste`), que aceita voltar no tempo; o `FakeTimeProvider` não aceita.
- Triggers criados por teste devem se chamar `TR_Teste_*`: a limpeza remove esses, e só esses.
- Banco de teste: `SqlServerDeTeste` usa a mesma imagem do compose e aplica os scripts com `database/aplicar-scripts.sh`.
- Concorrência (IT-09, IT-10): largada simultânea com barreira; zero 500 é requisito; zero 503 é objetivo
  assertado. Um 503 inesperado é investigado, nunca mascarado (spec §7.5).
- Proibido enfraquecer ou remover asserção para fazer um teste passar, e proibido marcar teste como ignorado
  sem registro no `docs/plan.md`.
- E2E (Playwright, `e2e/`): sempre no projeto isolado `joya-e12` (web 3012, API 5012), nunca na stack principal; a
  suíte recusa porta que não seja publicada por um container do `joya-e12`. Stack recém-criado com `docker compose -p joya-e12 down -v` (nunca `down -v`
  sem `-p`: o compose fixa `name: joya-pedidos`). Dados do seed lidos pela API, sem ids ou totais fixos. O projeto
  `api-real` não intercepta nada; o `simulado` usa `page.route` e não grava. Sem retries: falha intermitente aparece.
