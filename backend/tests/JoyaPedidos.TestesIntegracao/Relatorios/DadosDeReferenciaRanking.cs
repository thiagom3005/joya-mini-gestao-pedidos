using JoyaPedidos.TestesIntegracao.Infraestrutura;

namespace JoyaPedidos.TestesIntegracao.Relatorios;

/// <summary>
/// DR-RANK (spec §9.5): dados de referência do ranking, inseridos via SQL em banco limpo (§10.2), com os clientes na
/// ordem da tabela, para que os ids sejam crescentes (Carla &lt; Diego decide o empate de 400,00). Os itens gravam o
/// preço histórico; o preço atual dos produtos (1,00) é diferente de propósito, para provar que o ranking usa o preço
/// congelado em <c>ItensPedido</c>. Acréscimo à tabela da spec: um cliente sem pedidos, inserido por último, que nunca
/// pode aparecer no ranking.
/// </summary>
public sealed record DadosDeReferenciaRanking(
    int Ana1, int Ana2, int Bruno, int Carla, int Diego, int Elisa, int Fabio, int SemPedidos)
{
    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    public static async Task<DadosDeReferenciaRanking> InserirAsync(string stringDeConexao)
    {
        var dados = new DadosDeTeste(stringDeConexao);
        var ana1 = await dados.InserirClienteAsync("Ana Souza", "ana1@teste.exemplo");
        var ana2 = await dados.InserirClienteAsync("Ana Souza", "ana2@teste.exemplo");
        var bruno = await dados.InserirClienteAsync("Bruno Lima");
        var carla = await dados.InserirClienteAsync("Carla Dias");
        var diego = await dados.InserirClienteAsync("Diego Alves");
        var elisa = await dados.InserirClienteAsync("Elisa Rocha");
        var fabio = await dados.InserirClienteAsync("Fabio Nunes");
        var semPedidos = await dados.InserirClienteAsync("Gabriel Sem Pedidos");
        var produtoA = await dados.InserirProdutoAsync("Produto A", 1.00m, 100);
        var produtoB = await dados.InserirProdutoAsync("Produto B", 1.00m, 100);

        // Hora local de São Paulo → UTC (UTC-3). Ana 1 soma duas linhas: 2 × 100,00 + 1 × 100,00 = 300,00.
        await InserirPedidoAsync(stringDeConexao, ana1, "2026-03-15T13:00:00", (produtoA, 2, 100.00m), (produtoB, 1, 100.00m));
        await InserirPedidoAsync(stringDeConexao, ana2, "2026-04-20T13:00:00", (produtoA, 1, 250.00m));
        await InserirPedidoAsync(stringDeConexao, bruno, "2026-06-10T13:00:00", (produtoA, 1, 500.00m));
        await InserirPedidoAsync(stringDeConexao, bruno, "2027-01-01T03:00:00", (produtoA, 1, 999.00m)); // 01/01/2027 00:00:00
        await InserirPedidoAsync(stringDeConexao, carla, "2026-05-05T13:00:00", (produtoA, 1, 400.00m));
        await InserirPedidoAsync(stringDeConexao, diego, "2026-01-01T03:00:00", (produtoA, 1, 400.00m)); // 01/01/2026 00:00:00
        await InserirPedidoAsync(stringDeConexao, elisa, "2027-01-01T02:59:59", (produtoA, 1, 450.00m)); // 31/12/2026 23:59:59
        await InserirPedidoAsync(stringDeConexao, fabio, "2026-01-01T02:59:59", (produtoA, 1, 1000.00m)); // 31/12/2025 23:59:59

        return new DadosDeReferenciaRanking(ana1, ana2, bruno, carla, diego, elisa, fabio, semPedidos);
    }

    private static async Task InserirPedidoAsync(
        string stringDeConexao, int clienteId, string dataPedidoUtc, params (int ProdutoId, int Quantidade, decimal PrecoUnitario)[] itens)
    {
        var pedidoId = await ConsultaSql.EscalarAsync<int>(
            stringDeConexao,
            "INSERT INTO dbo.Pedidos (ClienteId, DataPedido) OUTPUT INSERTED.Id VALUES (@ClienteId, CONVERT(datetime2(0), @DataPedido, 126));",
            Cancelamento,
            ("@ClienteId", clienteId),
            ("@DataPedido", dataPedidoUtc));
        foreach (var item in itens)
        {
            await ConsultaSql.EscalarAsync<int>(
                stringDeConexao,
                """
                INSERT INTO dbo.ItensPedido (PedidoId, ProdutoId, Quantidade, PrecoUnitario)
                VALUES (@PedidoId, @ProdutoId, @Quantidade, @PrecoUnitario);
                SELECT 0;
                """,
                Cancelamento,
                ("@PedidoId", pedidoId),
                ("@ProdutoId", item.ProdutoId),
                ("@Quantidade", item.Quantidade),
                ("@PrecoUnitario", item.PrecoUnitario));
        }
    }
}
