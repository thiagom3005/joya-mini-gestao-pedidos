using System.Data;
using Dapper;
using JoyaPedidos.Api.Compartilhado;
using JoyaPedidos.Api.Modulos.Pedidos.Contratos;
using JoyaPedidos.Dominio;
using Microsoft.Data.SqlClient;

namespace JoyaPedidos.Api.Modulos.Pedidos;

/// <summary>
/// SQL explícito e parametrizado de pedidos (Dapper). Os comandos da criação recebem a transação do
/// <see cref="ServicoPedidos"/> e rodam na conexão dela, um comando por produto, como exige a spec §7.2.
/// </summary>
public sealed class RepositorioPedidos(FabricaConexao fabricaConexao)
{
    /// <summary>RN-01: leitura simples (lock S liberado ao fim da leitura); nenhum produto foi bloqueado ainda.</summary>
    public Task<Cliente?> ObterClienteAsync(int clienteId, SqlTransaction transacao, CancellationToken cancelamento) =>
        ConexaoDa(transacao).QuerySingleOrDefaultAsync<Cliente>(new CommandDefinition(
            "SELECT Id, Nome FROM dbo.Clientes WHERE Id = @ClienteId;",
            new { ClienteId = clienteId },
            transacao,
            cancellationToken: cancelamento));

    /// <summary>
    /// Lê o produto e retém o lock de atualização (U) até o fim da transação (§7.2, passo 3). Um comando por produto,
    /// chamado em ordem crescente de Id pelo serviço: a ordem de aquisição dos locks é determinística por construção.
    /// </summary>
    public Task<Produto?> BloquearProdutoAsync(int produtoId, SqlTransaction transacao, CancellationToken cancelamento) =>
        ConexaoDa(transacao).QuerySingleOrDefaultAsync<Produto>(new CommandDefinition(
            "SELECT Id, Nome, Preco, Estoque FROM dbo.Produtos WITH (UPDLOCK, ROWLOCK) WHERE Id = @ProdutoId;",
            new { ProdutoId = produtoId },
            transacao,
            cancellationToken: cancelamento));

    /// <summary>
    /// Baixa condicional (§7.2, passo 5): só afeta a linha se ainda houver estoque. Devolve <c>false</c> se nenhuma linha
    /// foi afetada, o que o lock U deveria impedir (barreira defensiva, correta mesmo com outro nível de isolamento).
    /// </summary>
    public async Task<bool> BaixarEstoqueAsync(int produtoId, int quantidade, SqlTransaction transacao, CancellationToken cancelamento)
    {
        var linhasAfetadas = await ConexaoDa(transacao).ExecuteAsync(new CommandDefinition(
            "UPDATE dbo.Produtos SET Estoque = Estoque - @Quantidade WHERE Id = @ProdutoId AND Estoque >= @Quantidade;",
            new { ProdutoId = produtoId, Quantidade = quantidade },
            transacao,
            cancellationToken: cancelamento));
        return linhasAfetadas == 1;
    }

    /// <summary>
    /// Estoque atual, para informar o disponível quando a baixa condicional não afeta nenhuma linha. <c>null</c> se a
    /// linha não existir (o lock U retido impede a exclusão; o caso fica explícito em vez de virar 0 implicitamente).
    /// </summary>
    public Task<int?> LerEstoqueAsync(int produtoId, SqlTransaction transacao, CancellationToken cancelamento) =>
        ConexaoDa(transacao).QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            "SELECT Estoque FROM dbo.Produtos WHERE Id = @ProdutoId;",
            new { ProdutoId = produtoId },
            transacao,
            cancellationToken: cancelamento));

    /// <summary>Grava o cabeçalho e devolve o Id gerado. <c>DataPedido</c> vai como <c>datetime2</c> (spec §4.5).</summary>
    public Task<int> InserirPedidoAsync(int clienteId, DateTime dataPedidoUtc, SqlTransaction transacao, CancellationToken cancelamento)
    {
        var parametros = new DynamicParameters();
        parametros.Add("ClienteId", clienteId, DbType.Int32);
        parametros.Add("DataPedido", dataPedidoUtc, DbType.DateTime2);
        return ConexaoDa(transacao).ExecuteScalarAsync<int>(new CommandDefinition(
            "INSERT INTO dbo.Pedidos (ClienteId, DataPedido) OUTPUT INSERTED.Id VALUES (@ClienteId, @DataPedido);",
            parametros,
            transacao,
            cancellationToken: cancelamento));
    }

    /// <summary>Grava os itens com o preço congelado (RN-08), um comando por item, na transação do pedido.</summary>
    public Task InserirItensAsync(int pedidoId, IEnumerable<ItemPedido> itens, SqlTransaction transacao, CancellationToken cancelamento) =>
        ConexaoDa(transacao).ExecuteAsync(new CommandDefinition(
            "INSERT INTO dbo.ItensPedido (PedidoId, ProdutoId, Quantidade, PrecoUnitario) VALUES (@PedidoId, @ProdutoId, @Quantidade, @PrecoUnitario);",
            itens.Select(item => ParametrosDoItem(pedidoId, item)).ToList(),
            transacao,
            cancellationToken: cancelamento));

    /// <summary>Pedido gravado com os preços congelados e o nome atual de cliente e produtos; <c>null</c> se não existir.</summary>
    public async Task<PedidoResposta?> ObterPedidoAsync(int pedidoId, CancellationToken cancelamento)
    {
        const string sql = """
            SELECT p.Id, p.DataPedido, c.Id AS ClienteId, c.Nome AS NomeCliente
            FROM dbo.Pedidos AS p
            INNER JOIN dbo.Clientes AS c ON c.Id = p.ClienteId
            WHERE p.Id = @PedidoId;

            SELECT i.ProdutoId, pr.Nome AS NomeProduto, i.Quantidade, i.PrecoUnitario
            FROM dbo.ItensPedido AS i
            INNER JOIN dbo.Produtos AS pr ON pr.Id = i.ProdutoId
            WHERE i.PedidoId = @PedidoId
            ORDER BY i.ProdutoId;
            """;

        await using var conexao = await fabricaConexao.AbrirAsync(cancelamento);
        await using var resultados = await conexao.QueryMultipleAsync(new CommandDefinition(
            sql, new { PedidoId = pedidoId }, cancellationToken: cancelamento));

        var cabecalho = await resultados.ReadSingleOrDefaultAsync<CabecalhoPedido>();
        if (cabecalho is null)
        {
            return null;
        }
        var itens = await resultados.ReadAsync<ItemPedido>();
        return PedidoResposta.De(cabecalho.Id, new Cliente(cabecalho.ClienteId, cabecalho.NomeCliente), cabecalho.DataPedido, itens);
    }

    /// <summary>
    /// Página de pedidos e total de pedidos que atendem ao filtro (spec §8.3–8.4), num lote com duas consultas: o
    /// <c>COUNT(*)</c> e a página, com o mesmo WHERE. Ordem total <c>DataPedido DESC, Id DESC</c>. Quantidade de itens e
    /// valor total vêm dos itens da própria página (<c>CROSS APPLY</c>), com o preço congelado, sem consulta por pedido.
    /// </summary>
    public async Task<(IReadOnlyList<PedidoResumoResposta> Itens, int TotalItens)> ListarAsync(
        FiltroListarPedidos filtro, IntervaloUtc intervalo, CancellationToken cancelamento)
    {
        var (filtros, parametros) = FiltrosDaListagem(filtro.ClienteId, intervalo);
        // OFFSET em bigint: página alta não estoura, só devolve lista vazia (§8.3).
        parametros.Add("Deslocamento", (long)(filtro.Pagina - 1) * filtro.TamanhoPagina, DbType.Int64);
        parametros.Add("TamanhoPagina", filtro.TamanhoPagina, DbType.Int32);
        var sql = $"""
            SELECT COUNT(*) FROM dbo.Pedidos AS p {filtros};

            WITH Pagina AS (
                SELECT p.Id, p.ClienteId, p.DataPedido
                FROM dbo.Pedidos AS p
                {filtros}
                ORDER BY p.DataPedido DESC, p.Id DESC
                OFFSET @Deslocamento ROWS FETCH NEXT @TamanhoPagina ROWS ONLY
            )
            SELECT pg.Id, pg.DataPedido, c.Id AS ClienteId, c.Nome AS NomeCliente, t.QuantidadeItens, t.ValorTotal
            FROM Pagina AS pg
            INNER JOIN dbo.Clientes AS c ON c.Id = pg.ClienteId
            CROSS APPLY (
                SELECT COUNT(*) AS QuantidadeItens, SUM(i.Quantidade * i.PrecoUnitario) AS ValorTotal
                FROM dbo.ItensPedido AS i
                WHERE i.PedidoId = pg.Id
            ) AS t
            ORDER BY pg.DataPedido DESC, pg.Id DESC;
            """;

        await using var conexao = await fabricaConexao.AbrirAsync(cancelamento);
        await using var resultados = await conexao.QueryMultipleAsync(new CommandDefinition(
            sql, parametros, cancellationToken: cancelamento));
        var totalItens = await resultados.ReadSingleAsync<int>();
        var linhas = await resultados.ReadAsync<LinhaDaListagem>();
        return (linhas.Select(linha => linha.ParaResposta()).ToList(), totalItens);
    }

    // WHERE dinâmico só com fragmentos constantes (§8.4): os valores vão sempre como parâmetros, nunca no texto do SQL.
    private static (string Where, DynamicParameters Parametros) FiltrosDaListagem(int? clienteId, IntervaloUtc intervalo)
    {
        var condicoes = new List<string>();
        var parametros = new DynamicParameters();
        if (clienteId is { } id)
        {
            condicoes.Add("p.ClienteId = @ClienteId");
            parametros.Add("ClienteId", id, DbType.Int32);
        }
        FiltroDePeriodoSql.Adicionar(intervalo, condicoes, parametros);
        return (FiltroDePeriodoSql.Where(condicoes), parametros);
    }

    private static SqlConnection ConexaoDa(SqlTransaction transacao) =>
        transacao.Connection ?? throw new InvalidOperationException("A transação já foi concluída.");

    // Precisão e escala da coluna (DECIMAL(18,2)) declaradas explicitamente: sem elas, o SqlClient declara o parâmetro
    // com a precisão de cada valor, e cada combinação gera um plano de execução diferente.
    private static DynamicParameters ParametrosDoItem(int pedidoId, ItemPedido item)
    {
        var parametros = new DynamicParameters();
        parametros.Add("PedidoId", pedidoId, DbType.Int32);
        parametros.Add("ProdutoId", item.ProdutoId, DbType.Int32);
        parametros.Add("Quantidade", item.Quantidade, DbType.Int32);
        parametros.Add("PrecoUnitario", item.PrecoUnitario, DbType.Decimal, precision: 18, scale: 2);
        return parametros;
    }

    private sealed record CabecalhoPedido(int Id, DateTime DataPedido, int ClienteId, string NomeCliente);

    private sealed record LinhaDaListagem(
        int Id, DateTime DataPedido, int ClienteId, string NomeCliente, int QuantidadeItens, decimal ValorTotal)
    {
        // datetime2 chega sem Kind; o valor gravado é UTC (§4.5) e sai com "Z".
        public PedidoResumoResposta ParaResposta() => new(
            Id,
            new ClienteResposta(ClienteId, NomeCliente),
            DateTime.SpecifyKind(DataPedido, DateTimeKind.Utc),
            QuantidadeItens,
            ValorTotal);
    }
}
