-- V2 parametrizada (sp_executesql) com limites DATETIME2(7): o tipo que o SqlClient envia para DbType.DateTime2
-- sem escala explícita, como faz a API (Dapper). A coluna DataPedido é DATETIME2(0).
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
    N'@InicioUtc DATETIME2(7), @FimExclusivoUtc DATETIME2(7)',
    @InicioUtc = '2026-01-01T03:00:00',
    @FimExclusivoUtc = '2027-01-01T03:00:00';
