-- V2 parametrizada (sp_executesql) com limites DATETIME2(0), o mesmo tipo da coluna DataPedido.
EXEC sys.sp_executesql
    N'SELECT
    ROW_NUMBER() OVER (ORDER BY SUM(i.Quantidade * i.PrecoUnitario) DESC, c.Id) AS Posicao,
    c.Id   AS ClienteId,
    c.Nome,
    SUM(i.Quantidade * i.PrecoUnitario) AS Total
FROM Pedidos p
INNER JOIN Clientes c    ON c.Id = p.ClienteId
INNER JOIN ItensPedido i ON i.PedidoId = p.Id
WHERE p.DataPedido >= @InicioUtc
  AND p.DataPedido <  @FimExclusivoUtc
GROUP BY c.Id, c.Nome
ORDER BY Total DESC, c.Id;',
    N'@InicioUtc DATETIME2(0), @FimExclusivoUtc DATETIME2(0)',
    @InicioUtc = '2026-01-01T03:00:00',
    @FimExclusivoUtc = '2027-01-01T03:00:00';
