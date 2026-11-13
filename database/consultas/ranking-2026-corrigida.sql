-- Ranking de clientes por valor comprado em 2026 (ano civil em America/Sao_Paulo).
-- DataPedido é armazenado em UTC. São Paulo = UTC-3 (sem horário de verão desde 2019):
--   01/01/2026 00:00 local = 2026-01-01 03:00 UTC (início, inclusivo)
--   01/01/2027 00:00 local = 2027-01-01 03:00 UTC (fim, exclusivo)
SELECT
    ROW_NUMBER() OVER (ORDER BY SUM(i.Quantidade * i.PrecoUnitario) DESC, c.Id) AS Posicao,
    c.Id   AS ClienteId,
    c.Nome,
    SUM(i.Quantidade * i.PrecoUnitario) AS Total
FROM Pedidos p
INNER JOIN Clientes c    ON c.Id = p.ClienteId
INNER JOIN ItensPedido i ON i.PedidoId = p.Id
WHERE p.DataPedido >= '2026-01-01T03:00:00'
  AND p.DataPedido <  '2027-01-01T03:00:00'
GROUP BY c.Id, c.Nome
ORDER BY Total DESC, c.Id;
