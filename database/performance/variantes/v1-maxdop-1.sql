-- Diagnóstico: a V1 (original com o predicado em intervalo do ano UTC) com OPTION (MAXDOP 1). Comparada com a V0
-- MAXDOP 1, isola o efeito da SARGabilidade (D6) sem paralelismo.
SELECT c.Nome, SUM(i.Quantidade * i.PrecoUnitario) AS Total
FROM Clientes c
LEFT JOIN Pedidos p ON p.ClienteId = c.Id
LEFT JOIN ItensPedido i ON i.PedidoId = p.Id
WHERE p.DataPedido >= '2026-01-01T00:00:00' AND p.DataPedido < '2027-01-01T00:00:00'
GROUP BY c.Nome
ORDER BY Total
OPTION (MAXDOP 1)
