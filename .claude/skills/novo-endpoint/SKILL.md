---
name: novo-endpoint
description: Cria um endpoint HTTP da API de pedidos (.NET 10, Minimal APIs, Dapper) a partir da spec aprovada, no padrão da E5, com contratos, validador, repositório, testes unitários e de integração, validação de build/testes/segurança e checklist final de aderência. Use ao implementar um endpoint previsto em docs/spec.md §5 (ex.: GET /clientes, GET /pedidos, GET /relatorios/top-clientes).
argument-hint: <Modulo> <VERBO> <rota> <secao-da-spec> [observacoes]
---

# Skill `novo-endpoint`

Argumentos recebidos: `$ARGUMENTS`

Formato esperado: `<Modulo> <VERBO> <rota> <secao-da-spec> [observacoes]`.
Exemplo: `Catalogo GET /clientes §5.7`.

Se faltar módulo, verbo, rota ou seção, **pare e pergunte**. Não invente contrato: o contrato é o da seção indicada.

## 1. Ler antes de escrever (obrigatório)

1. `docs/spec.md`: a seção do contrato indicada, §5.1 (convenções), §5.2 (erros) e as seções de regra que ela cita
   (ex.: §8 para listagem, §9.4–9.5 para ranking). Anote: parâmetros, tipos, padrões, faixas, ordenação, envelope,
   status e códigos de erro, e os IDs de teste da §10 que cobrem o endpoint.
2. `docs/plan.md`: a entrega em execução. Implemente **só** o que ela pede.
3. `CLAUDE.md` e `.claude/rules/{backend,testes}.md`.
4. Arquivos de referência da E5 (copie o estilo, não o conteúdo):
   - `backend/src/JoyaPedidos.Api/Modulos/Pedidos/PedidosEndpoints.cs`: `Add<Modulo>()` + `Map<Modulo>Endpoints()`,
     `MapGroup`, handlers privados estáticos, `TypedResults`, erros via `MapeadorErros` → `Problema(...)`.
   - `backend/src/JoyaPedidos.Api/Modulos/Pedidos/RepositorioPedidos.cs`: classe concreta `sealed`, `FabricaConexao`
     no construtor primário, `CommandDefinition` com parâmetros e `cancellationToken`, SQL em literal constante.
   - `backend/src/JoyaPedidos.Api/Modulos/Pedidos/ValidadorCriarPedido.cs`: validador estático puro que devolve
     `IReadOnlyDictionary<string, string[]>` com chaves no caminho JSON.
   - `backend/src/JoyaPedidos.Api/Modulos/Pedidos/Contratos/`: `sealed record`, um arquivo por contrato.
   - `backend/src/JoyaPedidos.Api/Compartilhado/`: `MapeadorErros`, `CodigosErroHttp`, `PeriodoNegocio`
     (datas civis de São Paulo → intervalo UTC semiaberto), `FusoHorarioNegocio`, `FabricaConexao`.
   - Endpoint com período (`dataInicio`/`dataFim`): **reutilize**, não duplique. `ValidacaoDePeriodo.Ler` (formato
     exato via `DataCivil`, obrigatoriedade e `dataInicio ≤ dataFim`, chaves `dataInicio`/`dataFim`),
     `PeriodoNegocio.ParaIntervaloUtc` e `FiltroDePeriodoSql` (fragmentos constantes sobre `p.DataPedido`, só os
     limites presentes). Exemplos: `ValidadorListarPedidos`, `RepositorioPedidos.ListarAsync`, `RepositorioRelatorios`.
   - Testes: `backend/tests/JoyaPedidos.TestesUnitarios/Pedidos/ValidadorCriarPedidoTestes.cs`,
     `backend/tests/JoyaPedidos.TestesIntegracao/Pedidos/ConsultarPedidoTestes.cs` e `Infraestrutura/`
     (`FabricaApi`, `DadosDeTeste`, `RespostaHttp`).

## 2. Arquivos a produzir (só os aplicáveis)

| Arquivo | Quando | Padrão |
|---|---|---|
| `Api/Modulos/<Modulo>/<Modulo>Endpoints.cs` | Sempre (novo módulo) ou edição do existente | Só HTTP: valida, chama repositório/serviço, converte. Sem regra de negócio. |
| `Api/Modulos/<Modulo>/Contratos/<Nome>.cs` | Resposta ou parâmetros com forma própria | `sealed record`, propriedades em PascalCase (JSON camelCase pelo padrão Web). Dinheiro `decimal`; instantes `DateTime` com `Kind = Utc`; datas civis `string yyyy-MM-dd` no eco. |
| `Api/Modulos/<Modulo>/Validador<Operacao>.cs` | Há parâmetros de entrada com regra | Estático e puro. Recebe texto bruto para datas (parsing estrito `yyyy-MM-dd`, `CultureInfo.InvariantCulture`). Devolve erros por campo **e** a entrada normalizada (padrões aplicados). Nunca ajusta faixas silenciosamente. |
| `Api/Modulos/<Modulo>/Repositorio<Modulo>.cs` | Há acesso a dados | Dapper, SQL explícito e parametrizado. WHERE dinâmico só com fragmentos **constantes** (§8.4). `DbType.DateTime2` para instantes. Sem N+1. Sem interface. |
| `Program.cs` | Módulo novo | `builder.Services.Add<Modulo>()` e `app.Map<Modulo>Endpoints()`. |
| `TestesUnitarios/<Modulo>/Validador<Operacao>Testes.cs` | Há validador | Cenários UT da §10.3, um comportamento por teste, `[Theory]` para variações. |
| `TestesIntegracao/<Modulo>/<Alvo>Testes.cs` | Sempre | Cenários IT da §10.4. `IAsyncLifetime` que chama `fabrica.LimparBancoAsync` no início; dados arranjados no teste via `DadosDeTeste`/SQL; asserções pelo endpoint; nunca Id fixo; `RespostaHttp.LerProblemaAsync` para erros. |

Regras de código (CLAUDE.md, rules): pt-BR, identificadores sem acento, sufixos do projeto, funções pequenas, sem
código morto, sem comentário que repete o código, sem abstração sem uso real, sem dependência nova sem justificativa.

## 3. Regras de contrato que costumam falhar

- Erro de validação: 400 `VALIDACAO` via `MapeadorErros.DeValidacao`, chaves camelCase no caminho JSON.
- Inteiro não numérico na query string: deixar o binding do framework falhar (`int?` no handler) → 400
  `REQUISICAO_INVALIDA` pela regra central. Não capturar nem reinterpretar.
- Datas na query: recebidas como `string?` e validadas pelo validador (formato exato), nunca `DateOnly` no binding.
- Coleções sempre com ordem total determinística (inclua o `Id` como desempate).
- `OFFSET` calculado em `long`. `COUNT(*)` separado com os mesmos filtros.
- Total monetário derivado de `ItensPedido` (`Quantidade * PrecoUnitario`), nunca do preço atual do produto.
- Período: `PeriodoNegocio.ParaIntervaloUtc(dataInicio, dataFim, fuso)` com o `TimeZoneInfo` injetado; predicado
  `>= @InicioUtc AND < @FimExclusivoUtc`; nunca `YEAR(coluna)` nem deslocamento fixo de UTC−3.

## 4. Validação obrigatória (execute de verdade)

```bash
dotnet build backend/JoyaPedidos.slnx
dotnet test --project backend/tests/JoyaPedidos.TestesUnitarios
dotnet test --project backend/tests/JoyaPedidos.TestesIntegracao -- --filter-class "*<Alvo>Testes"
dotnet test backend/JoyaPedidos.slnx
dotnet list backend/JoyaPedidos.slnx package --vulnerable --include-transitive
git diff --check
git diff --quiet -- docs/spec.md && echo "spec inalterada"
```

Build com **0 avisos**. Nenhum teste existente removido, ignorado ou enfraquecido. Busque segredos no diff
(`git diff | grep -iE "password|senha|pwd="`): só a senha pública de desenvolvimento documentada pode aparecer.
Nunca afirme que algo passou sem ter executado.

## 5. Proibições

- Não alterar `docs/spec.md`. Divergência encontrada → registrar e perguntar.
- Não alterar esquema, scripts de `database/` nem o Docker Compose.
- Não fazer commit, push, branch, amend, rebase, squash, reset ou force push. O commit é decisão do candidato.
- Não mexer no fluxo transacional de criação de pedidos (E5) ao criar endpoints de leitura.
- Não adicionar autenticação, escrita ou parâmetros que a spec não pede.

## 6. Checklist final (responda item a item, com a evidência)

```
[ ] Contrato idêntico à §<x> (campos, tipos, ordenação, envelope, status, códigos)
[ ] Validador puro com chaves camelCase e padrões aplicados (se houver parâmetros)
[ ] SQL explícito, parametrizado, fragmentos constantes, sem N+1, ordem total determinística
[ ] decimal para dinheiro; instantes UTC com Z; datas civis yyyy-MM-dd
[ ] Endpoint só mapeia HTTP; módulo registrado no Program.cs
[ ] UT do validador e IT do endpoint com os IDs da §10 citados
[ ] Build 0 avisos; testes do alvo e suíte completa aprovados (números reais)
[ ] Auditoria NuGet sem vulneráveis; git diff --check limpo; spec inalterada; sem segredos
[ ] Nenhum commit/push realizado
```
