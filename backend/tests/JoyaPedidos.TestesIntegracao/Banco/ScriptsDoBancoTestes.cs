using JoyaPedidos.TestesIntegracao.Infraestrutura;

namespace JoyaPedidos.TestesIntegracao.Banco;

/// <summary>
/// IT-21 (spec §10.4): scripts 01–03 aplicados duas vezes em banco novo, num container próprio. Sem erro, sem
/// duplicação; esquema conforme §4.2–4.3; seed conforme os mínimos da §4.7, inclusive o limite de DataPedido.
/// </summary>
public sealed class ScriptsDoBancoTestes(BancoIsoladoFixture banco) : IClassFixture<BancoIsoladoFixture>
{
    // Limites do ano civil em São Paulo (UTC-3), em UTC (spec §8.2).
    private const string Inicio2025Utc = "2025-01-01T03:00:00";
    private const string Inicio2026Utc = "2026-01-01T03:00:00";
    private const string Inicio2027Utc = "2027-01-01T03:00:00";
    private const string LimiteSuperiorDoSeedUtc = "2026-11-01T02:59:59"; // 31/10/2026 23:59:59 em São Paulo

    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private Task<int> ContarAsync(string sql) => ConsultaSql.EscalarAsync<int>(banco.StringDeConexao, sql, Cancelamento);

    private Task<IReadOnlyList<string>> ListarAsync(string sql) => ConsultaSql.ListaAsync(banco.StringDeConexao, sql, Cancelamento);

    [Fact]
    public void AplicarScripts_SegundaVez_NaoFalhaNemDuplicaDados()
    {
        Assert.NotEmpty(banco.ContagensAposPrimeiraExecucao);
        Assert.Equal(banco.ContagensAposPrimeiraExecucao, banco.ContagensAposSegundaExecucao);
        Assert.Contains("Banco JoyaPedidos já existe.", banco.SaidaDaSegundaExecucao);
        Assert.Contains("carga ignorada", banco.SaidaDaSegundaExecucao);
        Assert.DoesNotContain(" criad", banco.SaidaDaSegundaExecucao); // nenhum "criado." nem "criada."
    }

    [Fact]
    public async Task Identity_AposOSeed_ContinuaDoMaiorIdESemCache()
    {
        // IDENTITY_CACHE desligado: após parada abrupta o próximo Id não salta 1.000 posições (revisão da E3).
        Assert.Equal(0, await ContarAsync("SELECT CAST(value AS int) FROM sys.database_scoped_configurations WHERE name = N'IDENTITY_CACHE'"));

        // Medido pela fixture antes de qualquer teste da classe: independe da ordem de execução.
        Assert.Empty(banco.IdentidadesDesalinhadasAposScripts);
    }

    [Fact]
    public async Task Esquema_ChavesPrimariasEUnicas_ExistemComOsNomesDaSpec()
    {
        var chaves = await ListarAsync("""
            SELECT CONCAT(name COLLATE DATABASE_DEFAULT, ':', type COLLATE DATABASE_DEFAULT) FROM sys.key_constraints
            WHERE parent_object_id IN (OBJECT_ID(N'dbo.Clientes'), OBJECT_ID(N'dbo.Produtos'),
                                       OBJECT_ID(N'dbo.Pedidos'), OBJECT_ID(N'dbo.ItensPedido'))
            ORDER BY name
            """);

        Assert.Equal(["PK_Clientes:PK", "PK_ItensPedido:PK", "PK_Pedidos:PK", "PK_Produtos:PK", "UQ_Clientes_Email:UQ"], chaves);
    }

    [Fact]
    public async Task Esquema_ChavesEstrangeiras_ApontamParaAsTabelasCertasSemCascataEConfiaveis()
    {
        var estrangeiras = await ListarAsync("""
            SELECT CONCAT(name COLLATE DATABASE_DEFAULT, ':', OBJECT_NAME(parent_object_id), '->', OBJECT_NAME(referenced_object_id), ':',
                          delete_referential_action_desc COLLATE DATABASE_DEFAULT, ':ativa=', 1 - is_disabled, ':confiavel=', 1 - is_not_trusted)
            FROM sys.foreign_keys
            ORDER BY name
            """);

        Assert.Equal(
            [
                "FK_ItensPedido_Pedidos:ItensPedido->Pedidos:NO_ACTION:ativa=1:confiavel=1",
                "FK_ItensPedido_Produtos:ItensPedido->Produtos:NO_ACTION:ativa=1:confiavel=1",
                "FK_Pedidos_Clientes:Pedidos->Clientes:NO_ACTION:ativa=1:confiavel=1",
            ],
            estrangeiras);
    }

    [Fact]
    public async Task Esquema_RestricoesCheck_ExistemAtivasEConfiaveis()
    {
        var restricoes = await ListarAsync("""
            SELECT CONCAT(name, ':ativa=', 1 - is_disabled, ':confiavel=', 1 - is_not_trusted)
            FROM sys.check_constraints
            ORDER BY name
            """);

        Assert.Equal(
            [
                "CK_Clientes_Nome:ativa=1:confiavel=1",
                "CK_ItensPedido_PrecoUnitario:ativa=1:confiavel=1",
                "CK_ItensPedido_Quantidade:ativa=1:confiavel=1",
                "CK_Produtos_Estoque:ativa=1:confiavel=1",
                "CK_Produtos_Nome:ativa=1:confiavel=1",
                "CK_Produtos_Preco:ativa=1:confiavel=1",
            ],
            restricoes);
    }

    [Fact]
    public async Task Esquema_Indices_SaoExatamenteOsDaSpecComChavesEIncludes()
    {
        // Formato: nome|unicidade|colunas-chave em ordem|colunas incluídas (ordem alfabética).
        var indices = await ListarAsync("""
            SELECT CONCAT(i.name, '|', IIF(i.is_unique = 1, 'unico', 'nao-unico'), '|',
                   STRING_AGG(IIF(ic.is_included_column = 0, c.name, NULL), ',') WITHIN GROUP (ORDER BY ic.key_ordinal, c.name), '|',
                   STRING_AGG(IIF(ic.is_included_column = 1, c.name, NULL), ',') WITHIN GROUP (ORDER BY ic.key_ordinal, c.name))
            FROM sys.indexes AS i
            JOIN sys.index_columns AS ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            JOIN sys.columns AS c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE i.object_id IN (OBJECT_ID(N'dbo.Clientes'), OBJECT_ID(N'dbo.Produtos'),
                                  OBJECT_ID(N'dbo.Pedidos'), OBJECT_ID(N'dbo.ItensPedido'))
              AND i.is_primary_key = 0 AND i.is_unique_constraint = 0
            GROUP BY i.name, i.is_unique
            ORDER BY i.name
            """);

        // ItensPedido.ProdutoId não tem índice próprio, por decisão da spec §4.3.
        Assert.Equal(
            [
                "IX_Pedidos_ClienteId_DataPedido|nao-unico|ClienteId,DataPedido|",
                "IX_Pedidos_DataPedido|nao-unico|DataPedido|ClienteId",
                "UX_ItensPedido_PedidoId_ProdutoId|unico|PedidoId,ProdutoId|PrecoUnitario,Quantidade",
            ],
            indices);
    }

    [Fact]
    public async Task Esquema_TiposDeDinheiroEDeData_SaoDecimal18Com2EDatetime2Com0()
    {
        var colunas = await ListarAsync("""
            SELECT CONCAT(OBJECT_NAME(c.object_id), '.', c.name, ':', TYPE_NAME(c.user_type_id), '(',
                          IIF(TYPE_NAME(c.user_type_id) = 'decimal', CONCAT(c.precision, ','), ''), c.scale, '):nulo=', c.is_nullable)
            FROM sys.columns AS c
            WHERE c.name IN (N'Preco', N'PrecoUnitario', N'DataPedido')
              AND c.object_id IN (OBJECT_ID(N'dbo.Produtos'), OBJECT_ID(N'dbo.Pedidos'), OBJECT_ID(N'dbo.ItensPedido'))
            ORDER BY 1
            """);

        Assert.Equal(
            ["ItensPedido.PrecoUnitario:decimal(18,2):nulo=0", "Pedidos.DataPedido:datetime2(0):nulo=0", "Produtos.Preco:decimal(18,2):nulo=0"],
            colunas);
    }

    [Theory]
    [InlineData("estoque negativo", "UPDATE dbo.Produtos SET Estoque = -1 WHERE Id = (SELECT MIN(Id) FROM dbo.Produtos)", 547)]
    [InlineData("quantidade zero", "UPDATE dbo.ItensPedido SET Quantidade = 0 WHERE Id = (SELECT MIN(Id) FROM dbo.ItensPedido)", 547)]
    [InlineData("preço zero", "UPDATE dbo.Produtos SET Preco = 0 WHERE Id = (SELECT MIN(Id) FROM dbo.Produtos)", 547)]
    [InlineData("produto repetido no pedido", "INSERT INTO dbo.ItensPedido (PedidoId, ProdutoId, Quantidade, PrecoUnitario) SELECT TOP (1) PedidoId, ProdutoId, 1, 1.00 FROM dbo.ItensPedido", 2601)]
    [InlineData("cliente inexistente", "INSERT INTO dbo.Pedidos (ClienteId, DataPedido) VALUES (-1, '2026-01-10T12:00:00')", 547)]
    [InlineData("e-mail repetido", "INSERT INTO dbo.Clientes (Nome, Email) SELECT TOP (1) N'Outro', Email FROM dbo.Clientes", 2627)]
    public async Task Restricoes_ViolacaoNoBanco_SaoRejeitadas(string regra, string sql, int numeroDoErroEsperado)
    {
        var erro = await ConsultaSql.TentarEmTransacaoDesfeitaAsync(banco.StringDeConexao, sql, Cancelamento);

        Assert.True(erro is not null, $"O banco deveria rejeitar: {regra}.");
        Assert.Equal(numeroDoErroEsperado, erro.Number);
    }

    [Fact]
    public async Task Seed_ClientesEHomonimos_AtendemOsMinimosDaSpec()
    {
        Assert.True(await ContarAsync("SELECT COUNT(*) FROM dbo.Clientes") >= 8);

        // Dois clientes com o mesmo nome e e-mails diferentes, cuja soma em 2026 (ano civil de São Paulo) supera o
        // maior total entre os demais clientes: demonstra o defeito do GROUP BY c.Nome (spec §4.7, §9.2 D2).
        var homonimosQueSuperamOMaior = await ContarAsync($"""
            WITH Totais AS (
                SELECT c.Id, c.Nome, SUM(i.Quantidade * i.PrecoUnitario) AS Total
                FROM dbo.Pedidos AS p
                JOIN dbo.Clientes AS c ON c.Id = p.ClienteId
                JOIN dbo.ItensPedido AS i ON i.PedidoId = p.Id
                WHERE p.DataPedido >= '{Inicio2026Utc}' AND p.DataPedido < '{Inicio2027Utc}'
                GROUP BY c.Id, c.Nome)
            SELECT COUNT(*)
            FROM Totais AS a
            JOIN Totais AS b ON b.Nome = a.Nome AND b.Id > a.Id
            WHERE a.Total + b.Total > (SELECT MAX(t.Total) FROM Totais AS t WHERE t.Nome <> a.Nome)
            """);
        Assert.True(homonimosQueSuperamOMaior >= 1, "Faltam homônimos cuja soma em 2026 supere o maior cliente individual.");
    }

    [Fact]
    public async Task Seed_Produtos_AtendemOsMinimosDeEstoqueEPreco()
    {
        Assert.True(await ContarAsync("SELECT COUNT(*) FROM dbo.Produtos") >= 10);
        Assert.True(await ContarAsync("SELECT COUNT(*) FROM dbo.Produtos WHERE Estoque = 0") >= 1, "Falta produto sem estoque.");
        Assert.True(await ContarAsync("SELECT COUNT(*) FROM dbo.Produtos WHERE Estoque BETWEEN 1 AND 2") >= 1, "Falta produto com estoque baixo (1 a 2).");
        Assert.True(await ContarAsync("SELECT COUNT(*) FROM dbo.Produtos WHERE Estoque >= 1000") >= 2, "Faltam dois produtos com estoque alto (E2E).");
        Assert.True(await ContarAsync("SELECT COUNT(*) FROM dbo.Produtos WHERE Preco <> FLOOR(Preco)") >= 1, "Falta preço com centavos.");
    }

    [Fact]
    public async Task Seed_Pedidos_TemDe1A4ItensEPrecoCongeladoDiferenteDoAtual()
    {
        Assert.True(await ContarAsync("SELECT COUNT(*) FROM dbo.Pedidos") >= 30);
        Assert.Equal(0, await ContarAsync("""
            SELECT COUNT(*) FROM dbo.Pedidos AS p
            WHERE (SELECT COUNT(*) FROM dbo.ItensPedido AS i WHERE i.PedidoId = p.Id) NOT BETWEEN 1 AND 4
            """));
        Assert.True(await ContarAsync("""
            SELECT COUNT(*) FROM dbo.ItensPedido AS i JOIN dbo.Produtos AS pr ON pr.Id = i.ProdutoId
            WHERE i.PrecoUnitario <> pr.Preco
            """) >= 1, "Falta item com preço congelado diferente do preço atual.");
    }

    [Fact]
    public async Task Seed_DatasDosPedidos_CobremFronteirasE2025E2026SemPassarDoLimite()
    {
        Assert.True(await ContarAsync($"SELECT COUNT(*) FROM dbo.Pedidos WHERE DataPedido >= '{Inicio2025Utc}' AND DataPedido < '{Inicio2026Utc}'") >= 1);
        Assert.True(await ContarAsync($"SELECT COUNT(*) FROM dbo.Pedidos WHERE DataPedido >= '{Inicio2026Utc}' AND DataPedido < '{Inicio2027Utc}'") >= 1);

        // Fronteira do ano civil local: 31/12/2025 23:59:59 e 01/01/2026 00:00:00 em São Paulo (spec §4.7).
        Assert.Equal(1, await ContarAsync("SELECT COUNT(*) FROM dbo.Pedidos WHERE DataPedido = '2026-01-01T02:59:59'"));
        Assert.Equal(1, await ContarAsync("SELECT COUNT(*) FROM dbo.Pedidos WHERE DataPedido = '2026-01-01T03:00:00'"));

        // Nenhum pedido depois de 31/10/2026 23:59:59 em São Paulo.
        Assert.Equal(0, await ContarAsync($"SELECT COUNT(*) FROM dbo.Pedidos WHERE DataPedido > '{LimiteSuperiorDoSeedUtc}'"));
    }
}
