-- V2 com variáveis locais (DECLARE): o otimizador não conhece os valores ao compilar e usa uma seletividade fixa
-- para as duas desigualdades (30% com recuo exponencial: 0,3 × √0,3 ≈ 16,4% das linhas), não o histograma.
-- Forma comum em scripts manuais; a API NÃO usa esta forma (usa sp_executesql).
DECLARE @InicioUtc DATETIME2(0) = '2026-01-01T03:00:00';
DECLARE @FimExclusivoUtc DATETIME2(0) = '2027-01-01T03:00:00';
SELECT
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
ORDER BY Total DESC, c.Id;
