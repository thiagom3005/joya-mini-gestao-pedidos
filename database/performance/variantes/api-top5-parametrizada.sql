-- SQL de GET /relatorios/top-clientes (RepositorioRelatorios.TopClientesAsync) para 2026, com os parâmetros como a
-- API os envia pelo Dapper: sp_executesql e DATETIME2(7). TOP (5); sem ROW_NUMBER (a posição vem da aplicação).
EXEC sys.sp_executesql
    N'SELECT TOP (5)
    c.Id AS ClienteId,
    c.Nome,
    SUM(i.Quantidade * i.PrecoUnitario) AS ValorTotal
FROM dbo.Pedidos AS p
INNER JOIN dbo.Clientes AS c ON c.Id = p.ClienteId
INNER JOIN dbo.ItensPedido AS i ON i.PedidoId = p.Id
WHERE p.DataPedido >= @InicioUtc AND p.DataPedido < @FimExclusivoUtc
GROUP BY c.Id, c.Nome
ORDER BY ValorTotal DESC, c.Id;',
    N'@InicioUtc DATETIME2(7),@FimExclusivoUtc DATETIME2(7)',
    @InicioUtc = '2026-01-01T03:00:00',
    @FimExclusivoUtc = '2027-01-01T03:00:00';
