---
paths:
  - "database/**"
  - "docker-compose*.yml"
---

# Regras do banco (SQL Server 2022)

Referências: spec §4 (modelo), §9 (ranking), §11 (Docker).

## Scripts
- `database/*.sql` (raiz) rodam automaticamente no `db-init`, em ordem lexical, com `sqlcmd -C -b`. Subpastas
  (`consultas/`, `performance/`, `dev/`) **nunca** rodam automaticamente.
- Todo script automático é **idempotente**: cria objeto só se não existir; seed só se `Clientes` estiver vazia.
  Executar duas vezes não pode falhar nem duplicar dados (IT-21).
- Scripts podem usar `GO` (execução via `sqlcmd`, também nos testes). Exceção: `consultas/ranking-2026-corrigida.sql`
  é um único batch sem `GO`, porque o IT-23 o executa via SqlClient.
- Nomes de objetos conforme §4.2–4.3 (`PK_`, `FK_`, `CK_`, `UQ_`, `UX_`, `IX_`), sem acento.
- `DataPedido` é `DATETIME2(0)` em UTC. Comentários com o equivalente em horário de São Paulo ajudam a revisão.

## Query da Parte 4
- `consultas/ranking-2026-original.sql` é cópia **literal** do enunciado (spec §9.1). Nunca corrigir esse arquivo.
- Desempenho é hipótese: só afirmar ganho com medição (`SET STATISTICS IO, TIME ON`, plano real; `sqlcmd -y 0`
  para não truncar XML). Registrar resultados neutros também.
- Mudou um índice? Atualize o IT-21 no mesmo commit.

## Segurança
- Senha só por variável de ambiente (`MSSQL_SA_PASSWORD`, `JOYA_MCP_SENHA`); nunca literal em script versionado.
  Logins criados por script recebem a senha como variável `sqlcmd`.
- SQL Server sem porta publicada no compose padrão. Exposição só via `docker-compose.dev.yml`, em `127.0.0.1`.
