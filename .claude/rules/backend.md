---
paths:
  - "backend/**"
---

# Regras do backend (.NET 10)

Referências: spec §3 (regras), §5 (contratos), §7 (concorrência), §13 (decisões).

## Estrutura
- Minimal APIs agrupadas por módulo (`MapGroup`). Endpoints só mapeiam HTTP: validam, chamam serviço ou
  repositório e convertem o resultado. Nada de regra de negócio no endpoint.
- `JoyaPedidos.Dominio` não referencia ASP.NET, Dapper, SqlClient nem `TimeProvider`: recebe dados prontos
  (inclusive `agoraUtc`) e devolve resultado.
- Sem interfaces de repositório. Use classes concretas; o domínio puro garante a testabilidade.
- Validadores escritos à mão (sem FluentValidation), funções puras que devolvem erros por campo com chaves
  no caminho JSON (`itens[0].quantidade`, índice começando em 0).

## Dados e SQL
- Dapper com SQL explícito e **sempre parametrizado**. WHERE dinâmico só com fragmentos constantes (§8.4).
- Dinheiro: `decimal`, nunca `float`/`double`. Datas: instantes UTC (`DateTime` com `Kind = Utc` ou
  `DateTimeOffset`), parâmetros `DbType.DateTime2`, valores truncados a segundos antes de gravar.
- Criação de pedido segue exatamente a §7.2: cliente primeiro (rollback imediato se ausente), um
  `SELECT ... WITH (UPDLOCK, ROWLOCK)` por produto em ordem crescente de `ProdutoId`, regra pura, `UPDATE`
  condicional exigindo 1 linha afetada, inserts, commit. Não "otimize" juntando os selects num `IN (...)`.
- Transação descartada sem commit = rollback. Nada de chamadas externas dentro da transação.

## HTTP e erros
- Erros sempre em Problem Details com `codigo` estável (§5.2). Códigos da aplicação são explícitos; erros do
  framework recebem código genérico por status via regra central.
- 400 `VALIDACAO`, 422 `CLIENTE_NAO_ENCONTRADO`/`PRODUTO_NAO_ENCONTRADO`, 409 `ESTOQUE_INSUFICIENTE`,
  404 `PEDIDO_NAO_ENCONTRADO`, 503 `CONFLITO_CONCORRENCIA` (erro SQL 1205), 500 `ERRO_INTERNO` sem detalhes.
- JSON camelCase pt-BR; instantes com sufixo `Z`; datas civis `yyyy-MM-dd` com parsing estrito.

## Dependências
- Versões centralizadas em `Directory.Packages.props`. Auditoria NuGet ligada; vulnerabilidade se corrige
  atualizando ou fixando versão. Nunca suprimir NU1901–NU1904 de forma genérica (plano, E2).
