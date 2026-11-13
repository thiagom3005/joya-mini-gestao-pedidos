-- V1 (spec §9.6, opcional): a query original com UMA mudança, o predicado YEAR(p.DataPedido) = 2026 trocado pelo
-- intervalo semiaberto equivalente do ano UTC. Mesmo resultado (errado) da V0; isola o efeito da SARGabilidade (D6).
SELECT c.Nome, SUM(i.Quantidade * i.PrecoUnitario) AS Total
FROM Clientes c
LEFT JOIN Pedidos p ON p.ClienteId = c.Id
LEFT JOIN ItensPedido i ON i.PedidoId = p.Id
WHERE p.DataPedido >= '2026-01-01T00:00:00' AND p.DataPedido < '2027-01-01T00:00:00'
GROUP BY c.Nome
ORDER BY Total
