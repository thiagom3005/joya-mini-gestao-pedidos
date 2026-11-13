# Parte 4: consultas feitas pelo MCP (banco de desenvolvimento)

Entrega E8, 13/11/2026. Servidor MCP `sqlserver-joya` (DBHub 1.6.1, stdio), login `joya_mcp` (somente leitura),
banco `JoyaPedidos` do projeto `joya-pedidos`, com o **seed** (10 clientes, 12 produtos, 33 pedidos, 52 itens).
Nenhum número daqui se mistura às medições da massa, feitas por `sqlcmd` no projeto isolado `joya-perf`.

Antes e depois da E8, o retrato do banco pelo MCP foi o mesmo da E7: contagens 10/12/33/52;
`CHECKSUM_AGG(BINARY_CHECKSUM(*))` de `Clientes` −568960660, `Produtos` −804364437, `Pedidos` −725309831,
`ItensPedido` −1078983109; 18 objetos de usuário, checksum 2071488277.

## 1. V2 corrigida, com `SET STATISTICS IO ON` (`execute_sql`)

Texto de `database/consultas/ranking-2026-corrigida.sql`. Resultado (10 linhas):

| Posição | ClienteId | Nome | Total |
|---|---|---|---|
| 1 | 9 | Heitor Mendes | 1110.00 |
| 2 | 1 | Ana Souza | 993.59 |
| 3 | 2 | Ana Souza | 958.76 |
| 4 | 3 | Bruno Lima | 579.40 |
| 5 | 5 | Diego Alves | 578.89 |
| 6 | 8 | Gabriela Torres | 415.70 |
| 7 | 4 | Carla Dias | 259.66 |
| 8 | 10 | Isabela Castro | 258.00 |
| 9 | 7 | Fábio Nunes | 199.80 |
| 10 | 6 | Elisa Rocha | 159.77 |

Mensagens 3615 devolvidas pelo DBHub: `ItensPedido` scan count 18, logical reads 36; `Pedidos` 10 / 20;
`Clientes` 1 / 2; `Worktable` 0.

## 2. V0 original, com `SET STATISTICS IO ON` (`execute_sql`)

Texto de `database/consultas/ranking-2026-original.sql`. Resultado (9 linhas, em ordem crescente):
Fábio Nunes 199.80; Isabela Castro 258.00; Carla Dias 259.66; Gabriela Torres 415.70; **Elisa Rocha 509.67**;
Diego Alves 578.89; Bruno Lima 579.40; Heitor Mendes 1110.00; **Ana Souza 1952.35**.

- D2: as duas "Ana Souza" (993,59 + 958,76) viram uma linha de 1.952,35.
- D4: Elisa aparece com 509,67 na V0 e 159,77 na V2. A diferença é o pedido 15, de 349,90, em
  `2026-01-01T02:59:59Z` = 31/12/2025 23:59:59 em São Paulo: é 2026 em UTC (a V0 conta) e 2025 no ano civil local
  (a V2 não conta). Conferido pelo MCP, consultando os pedidos da Elisa a partir de 31/12/2025.
- D1: ordem crescente.

Mensagens 3615: `ItensPedido` 19 / 38; `Pedidos` 10 / 20; `Clientes` 1 / 2.

## 3. Plano em cache das duas consultas (`execute_sql`)

`sys.dm_exec_query_stats` × `sys.dm_exec_sql_text` × `sys.dm_exec_plan_attributes` (`attribute = 'dbid'`, porque
`dm_exec_sql_text.dbid` é nulo em ad hoc) × `sys.dm_exec_query_plan`, com XQuery em `//RelOp`
(`@PhysicalOp`, `@LogicalOp`, `Object/@Index`, `@EstimateRows`):

- **V0** (1 execução, 60 leituras, 1.367 µs de CPU): `Clustered Index Scan` em `PK_Clientes` → `Sort` →
  `Nested Loops (Left Outer Join)` com `Index Seek` em `IX_Pedidos_ClienteId_DataPedido` → `Compute Scalar` →
  **`Filter`** → `Nested Loops (Left Outer Join)` com `Index Seek` em `UX_ItensPedido_PedidoId_ProdutoId` →
  `Stream Aggregate` → `Sort`. A junção com `Pedidos` continua externa e o filtro de `YEAR()` vem depois dela, como
  na massa.
- **V2** (1 execução, 58 leituras, 3.021 µs de CPU): `Clustered Index Scan` em `PK_Clientes` →
  `Nested Loops (Inner Join)` com busca dinâmica (`Merge Interval` sobre `Constant Scan`) e `Index Seek` em
  **`IX_Pedidos_ClienteId_DataPedido`** por cliente → `Nested Loops` com `Index Seek` em
  `UX_ItensPedido_PedidoId_ProdutoId` → `Stream Aggregate` → `Sort` → `Segment`/`Sequence Project` (`ROW_NUMBER`).

No seed, a V2 usa **outro plano** que na massa (lá: `Index Seek` em `IX_Pedidos_DataPedido`, hash joins em batch
mode). Planos do banco pequeno não representam o volume; por isso a investigação usou a massa.

## 4. Estatísticas (`execute_sql`)

`sys.stats` × `sys.dm_db_stats_properties` em `Pedidos` e `ItensPedido`: `IX_Pedidos_DataPedido` com 33 linhas,
33 amostradas, 24 degraus, 0 modificações; `IX_Pedidos_ClienteId_DataPedido` 33/33, 6 degraus;
`UX_ItensPedido_PedidoId_ProdutoId` 52/52, 23 degraus; estatísticas automáticas em `ItensPedido.ProdutoId` e
`ItensPedido.PrecoUnitario`. Estatísticas completas (amostra = linhas), sem modificações pendentes.

## O que não foi feito pelo MCP

O plano previa também V2 sem `IX_Pedidos_DataPedido` via MCP. Não foi possível nem desejável: o MCP aponta para o
banco de desenvolvimento, que não tem a massa, e o `joya_mcp` não pode (nem deve) remover índices. Essa variante foi
medida por `sqlcmd` no `joya-perf`.
