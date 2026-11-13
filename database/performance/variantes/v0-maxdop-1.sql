-- Diagnóstico: a query original com OPTION (MAXDOP 1), para comparar leituras e CPU com a V2 (serial) sem o efeito
-- do paralelismo. NÃO é a V0 literal: a única mudança é a dica de execução no fim.
SELECT c.Nome, SUM(i.Quantidade * i.PrecoUnitario) AS Total
FROM Clientes c
LEFT JOIN Pedidos p ON p.ClienteId = c.Id
LEFT JOIN ItensPedido i ON i.PedidoId = p.Id
WHERE YEAR(p.DataPedido) = 2026
GROUP BY c.Nome
ORDER BY Total
OPTION (MAXDOP 1)
