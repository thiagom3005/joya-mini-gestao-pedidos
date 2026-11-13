namespace JoyaPedidos.TestesIntegracao.Infraestrutura;

/// <summary>
/// Arranjo e verificação direta no banco de teste (spec §10.2): os testes inserem os próprios clientes e produtos e
/// usam os ids devolvidos, nunca ids fixos. Todo SQL é parametrizado.
/// </summary>
public sealed class DadosDeTeste(string stringDeConexao)
{
    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    public Task<int> InserirClienteAsync(string nome) => ConsultaSql.EscalarAsync<int>(
        stringDeConexao,
        "INSERT INTO dbo.Clientes (Nome, Email) OUTPUT INSERTED.Id VALUES (@Nome, @Email);",
        Cancelamento,
        ("@Nome", nome),
        ("@Email", $"{Guid.NewGuid():N}@teste.exemplo"));

    public Task<int> InserirProdutoAsync(string nome, decimal preco, int estoque) => ConsultaSql.EscalarAsync<int>(
        stringDeConexao,
        "INSERT INTO dbo.Produtos (Nome, Preco, Estoque) OUTPUT INSERTED.Id VALUES (@Nome, @Preco, @Estoque);",
        Cancelamento,
        ("@Nome", nome),
        ("@Preco", preco),
        ("@Estoque", estoque));

    public Task<int> EstoqueAsync(int produtoId) => ConsultaSql.EscalarAsync<int>(
        stringDeConexao, "SELECT Estoque FROM dbo.Produtos WHERE Id = @Id;", Cancelamento, ("@Id", produtoId));

    /// <summary>
    /// Leitura suja (<c>NOLOCK</c>): enxerga o estoque mesmo com baixa ainda não confirmada por outra transação. Usada
    /// só para provar se uma transação em andamento já baixou o estoque.
    /// </summary>
    public Task<int> EstoqueSemRespeitarLocksAsync(int produtoId) => ConsultaSql.EscalarAsync<int>(
        stringDeConexao, "SELECT Estoque FROM dbo.Produtos WITH (NOLOCK) WHERE Id = @Id;", Cancelamento, ("@Id", produtoId));

    public Task<int> ContarPedidosAsync() =>
        ConsultaSql.EscalarAsync<int>(stringDeConexao, "SELECT COUNT(*) FROM dbo.Pedidos;", Cancelamento);

    public Task<int> ContarItensAsync() =>
        ConsultaSql.EscalarAsync<int>(stringDeConexao, "SELECT COUNT(*) FROM dbo.ItensPedido;", Cancelamento);

    /// <summary>Itens gravados do pedido como <c>produtoId:quantidade:precoUnitario</c>, em ordem de produto.</summary>
    public Task<string> ItensGravadosAsync(int pedidoId) => ConsultaSql.EscalarAsync<string>(
        stringDeConexao,
        """
        SELECT STRING_AGG(CONCAT(ProdutoId, ':', Quantidade, ':', PrecoUnitario), ';') WITHIN GROUP (ORDER BY ProdutoId)
        FROM dbo.ItensPedido WHERE PedidoId = @PedidoId;
        """,
        Cancelamento,
        ("@PedidoId", pedidoId));

    /// <summary><c>DataPedido</c> gravada, no formato ODBC canônico (<c>yyyy-MM-dd HH:mm:ss</c>), sem conversão de fuso.</summary>
    public Task<string> DataPedidoGravadaAsync(int pedidoId) => ConsultaSql.EscalarAsync<string>(
        stringDeConexao, "SELECT CONVERT(varchar(19), DataPedido, 120) FROM dbo.Pedidos WHERE Id = @Id;", Cancelamento, ("@Id", pedidoId));

    /// <summary>Executa um comando de arranjo sem resultado (ex.: alterar o preço de um produto).</summary>
    public Task ExecutarAsync(string sql, params (string Nome, object Valor)[] parametros) => ConsultaSql.EscalarAsync<int>(
        stringDeConexao, sql + Environment.NewLine + "SELECT 0;", Cancelamento, parametros);

    /// <summary>
    /// Cria um trigger de teste. O nome precisa do prefixo <see cref="SqlServerDeTeste.PrefixoDeTriggerDeTeste"/>, que a
    /// limpeza antes de cada teste remove: o mecanismo de falha nunca sobrevive ao teste que o criou.
    /// </summary>
    public Task CriarTriggerDeTesteAsync(string nome, string definicao)
    {
        Assert.StartsWith(SqlServerDeTeste.PrefixoDeTriggerDeTeste, nome);
        return ConsultaSql.EscalarAsync<int>(
            stringDeConexao, "EXEC sys.sp_executesql @Definicao; SELECT 0;", Cancelamento, ("@Definicao", definicao));
    }

    public Task<int> ContarTriggersDeTesteAsync() => ConsultaSql.EscalarAsync<int>(
        stringDeConexao, "SELECT COUNT(*) FROM sys.triggers WHERE name LIKE N'TR[_]Teste[_]%';", Cancelamento);
}
