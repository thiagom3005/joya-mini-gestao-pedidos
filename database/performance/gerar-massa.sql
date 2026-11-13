-- performance/gerar-massa.sql — massa de desempenho da Parte 4 (spec §9.6). NUNCA roda automaticamente.
-- Execute SOMENTE no ambiente isolado joya-perf (docker-compose.perf.yml), nunca no banco de desenvolvimento:
--
--   docker compose -p joya-perf -f docker-compose.yml -f docker-compose.perf.yml run --rm --no-deps -T db-init \
--     /opt/mssql-tools18/bin/sqlcmd -C -b -f 65001 -S sqlserver -U sa -i /database/performance/gerar-massa.sql
--   (Git Bash: prefixe MSYS_NO_PATHCONV=1.)
--
-- Trava: recusa (erro 50010) qualquer instância cujo SERVERPROPERTY('MachineName') não seja joya-perf-sqlserver,
-- o hostname que só o docker-compose.perf.yml define. Idempotente (spec §4.6): se a massa já existe, não faz nada.
--
-- Determinística: todos os valores vêm de aritmética modular sobre uma tabela de números (tally) gerada em memória;
-- sem NEWID(), RAND() nem WHILE. Inserções em lote (INSERT ... SELECT), numa única transação.
-- Volume, somado ao seed de 03-dados-exemplo.sql:
--   - 1.200 clientes: 600 nomes distintos, cada um usado por 2 clientes (homônimos); os 50 últimos sem pedidos;
--   - 250 produtos;
--   - 120.000 pedidos de 01/01/2024 00:00 a 31/12/2026 23:59 em São Paulo (UTC-3), em ordem cronológica do Id;
--   - 1 a 5 itens por pedido (média 3: 360.000 itens), produtos distintos no pedido, preço histórico entre 90% e
--     110% do preço atual do produto.
-- Respeita PKs, FKs, UNIQUEs e CHECKs da spec §4.2.

:on error exit
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF ISNULL(CAST(SERVERPROPERTY('MachineName') AS NVARCHAR(128)), N'') <> N'joya-perf-sqlserver'
    THROW 50010, N'Instância errada: a massa só pode ir para o ambiente isolado joya-perf (docker-compose.perf.yml).', 1;
GO

USE JoyaPedidos;
GO

-- Reexecução sem efeito: SET NOEXEC ON compila, mas não executa, os batches seguintes (o último, SET NOEXEC OFF,
-- sempre é processado). O código de saída continua 0.
IF EXISTS (SELECT 1 FROM dbo.Clientes WHERE Email LIKE N'%@massa.exemplo')
BEGIN
    PRINT N'A massa já existe neste banco: nada a fazer.';
    SET NOEXEC ON;
END
GO

DECLARE @Clientes INT = 1200;
DECLARE @ClientesComPedidos INT = 1150;
DECLARE @Produtos INT = 250;
DECLARE @Pedidos INT = 120000;
-- 01/01/2024 00:00 e 01/01/2027 00:00 em São Paulo (UTC-3), em UTC; o fim é exclusivo.
DECLARE @InicioUtc DATETIME2(0) = '2024-01-01T03:00:00';
DECLARE @FimUtc DATETIME2(0) = '2027-01-01T03:00:00';
DECLARE @Segundos BIGINT = DATEDIFF_BIG(SECOND, @InicioUtc, @FimUtc);

BEGIN TRANSACTION;

-- Números 0..@Pedidos-1: ROW_NUMBER sobre 10^6 linhas devolve sempre exatamente 1..10^6 (qualquer que seja a ordem
-- interna), e o filtro mantém 0..@Pedidos-1. Sem TOP sem ORDER BY.
WITH Digitos AS (SELECT d FROM (VALUES (0), (1), (2), (3), (4), (5), (6), (7), (8), (9)) AS v (d)),
Milhao AS (
    SELECT CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS INT) AS n
    FROM Digitos AS a CROSS JOIN Digitos AS b CROSS JOIN Digitos AS c
    CROSS JOIN Digitos AS d CROSS JOIN Digitos AS e CROSS JOIN Digitos AS f
)
SELECT n
INTO #Numeros
FROM Milhao
WHERE n < @Pedidos;
CREATE UNIQUE CLUSTERED INDEX UX_Temp ON #Numeros (n);

-- Clientes: nome = primeiro nome (n % 20) + sobrenome ((n / 20) % 30). Os clientes n e n + 600 são homônimos.
INSERT INTO dbo.Clientes (Nome, Email)
SELECT pn.Nome + N' ' + sn.Sobrenome,
       N'cliente' + RIGHT(N'0000' + CAST(x.n AS NVARCHAR(4)), 4) + N'@massa.exemplo'
FROM #Numeros AS x
INNER JOIN (VALUES (0, N'Ana'), (1, N'Bruno'), (2, N'Carla'), (3, N'Diego'), (4, N'Elisa'), (5, N'Fabio'),
                   (6, N'Gabriel'), (7, N'Helena'), (8, N'Igor'), (9, N'Julia'), (10, N'Karina'), (11, N'Lucas'),
                   (12, N'Marina'), (13, N'Nicolas'), (14, N'Olivia'), (15, N'Paulo'), (16, N'Rafaela'),
                   (17, N'Sergio'), (18, N'Tatiana'), (19, N'Vitor')) AS pn (i, Nome) ON pn.i = x.n % 20
INNER JOIN (VALUES (0, N'Souza'), (1, N'Lima'), (2, N'Dias'), (3, N'Alves'), (4, N'Rocha'), (5, N'Nunes'),
                   (6, N'Silva'), (7, N'Santos'), (8, N'Oliveira'), (9, N'Pereira'), (10, N'Costa'),
                   (11, N'Ribeiro'), (12, N'Martins'), (13, N'Carvalho'), (14, N'Gomes'), (15, N'Lopes'),
                   (16, N'Mendes'), (17, N'Barros'), (18, N'Freitas'), (19, N'Cardoso'), (20, N'Teixeira'),
                   (21, N'Moreira'), (22, N'Campos'), (23, N'Vieira'), (24, N'Monteiro'), (25, N'Ramos'),
                   (26, N'Batista'), (27, N'Farias'), (28, N'Pinto'), (29, N'Castro')) AS sn (i, Sobrenome)
    ON sn.i = (x.n / 20) % 30
WHERE x.n < @Clientes
ORDER BY x.n;

-- Índice de geração (n) de cada cliente, lido do próprio e-mail.
SELECT CAST(SUBSTRING(Email, 8, 4) AS INT) AS Indice, Id
INTO #MapaClientes
FROM dbo.Clientes
WHERE Email LIKE N'%@massa.exemplo';
CREATE UNIQUE CLUSTERED INDEX UX_Temp ON #MapaClientes (Indice);

-- Produtos: preço atual entre 5,00 e 317,00; estoque alto (a massa não exercita estoque).
INSERT INTO dbo.Produtos (Nome, Preco, Estoque)
SELECT N'Produto massa ' + RIGHT(N'000' + CAST(x.n AS NVARCHAR(3)), 3),
       CAST(5.00 + (x.n % 97) * 3.25 AS DECIMAL(18, 2)),
       1000 + x.n
FROM #Numeros AS x
WHERE x.n < @Produtos
ORDER BY x.n;

SELECT CAST(RIGHT(Nome, 3) AS INT) AS Indice, Id, Preco
INTO #MapaProdutos
FROM dbo.Produtos
WHERE Nome LIKE N'Produto massa %';
CREATE UNIQUE CLUSTERED INDEX UX_Temp ON #MapaProdutos (Indice);

-- Pedidos: datas espalhadas uniformemente pelos 3 anos locais, crescentes com n (+ até 599 s de deslocamento),
-- sempre dentro de [@InicioUtc, @FimUtc). Cliente = (13n + n / 997) mod 1.150: os 50 últimos clientes não compram.
-- INSERT ... SELECT com ORDER BY garante que os valores de IDENTITY seguem a ordem de n.
DECLARE @UltimoPedidoAntes INT = (SELECT ISNULL(MAX(Id), 0) FROM dbo.Pedidos);

INSERT INTO dbo.Pedidos (ClienteId, DataPedido)
SELECT mc.Id,
       DATEADD(SECOND, CAST(CAST(x.n AS BIGINT) * @Segundos / @Pedidos + CAST(x.n AS BIGINT) * 7919 % 600 AS INT), @InicioUtc)
FROM #Numeros AS x
INNER JOIN #MapaClientes AS mc ON mc.Indice = (x.n * 13 + x.n / 997) % @ClientesComPedidos
ORDER BY x.n;

SELECT CAST(ROW_NUMBER() OVER (ORDER BY Id) - 1 AS INT) AS Indice, Id
INTO #MapaPedidos
FROM dbo.Pedidos
WHERE Id > @UltimoPedidoAntes;
CREATE UNIQUE CLUSTERED INDEX UX_Temp ON #MapaPedidos (Indice);

-- Itens: o pedido n tem 1 + n mod 5 itens (j = 0..4). Produto (7n + 37j) mod 250: deslocamentos 0, 37, 74, 111 e
-- 148 são distintos, então não há produto repetido no pedido (UX_ItensPedido_PedidoId_ProdutoId). Quantidade de 1 a
-- 4; preço histórico = preço atual × (90% a 110%), arredondado a 2 casas.
INSERT INTO dbo.ItensPedido (PedidoId, ProdutoId, Quantidade, PrecoUnitario)
SELECT mp.Id,
       pr.Id,
       1 + (mp.Indice + j.j) % 4,
       CAST(pr.Preco * (90 + (mp.Indice + 3 * j.j) % 21) / 100.0 AS DECIMAL(18, 2))
FROM #MapaPedidos AS mp
CROSS JOIN (VALUES (0), (1), (2), (3), (4)) AS j (j)
INNER JOIN #MapaProdutos AS pr ON pr.Indice = (mp.Indice * 7 + j.j * 37) % @Produtos
WHERE j.j < 1 + mp.Indice % 5
ORDER BY mp.Id, j.j;

COMMIT TRANSACTION;
GO

-- Índices reconstruídos depois da carga: toda medição parte do mesmo estado físico (o medir-ranking.sql recria
-- IX_Pedidos_DataPedido, e um índice recém-criado é mais compacto que um preenchido por inserções).
ALTER INDEX ALL ON dbo.Clientes REBUILD;
ALTER INDEX ALL ON dbo.Produtos REBUILD;
ALTER INDEX ALL ON dbo.Pedidos REBUILD;
ALTER INDEX ALL ON dbo.ItensPedido REBUILD;
GO

-- Estatísticas completas antes de medir (spec §9.6, passo 4).
UPDATE STATISTICS dbo.Clientes WITH FULLSCAN;
UPDATE STATISTICS dbo.Produtos WITH FULLSCAN;
UPDATE STATISTICS dbo.Pedidos WITH FULLSCAN;
UPDATE STATISTICS dbo.ItensPedido WITH FULLSCAN;
GO

DECLARE @Resumo NVARCHAR(400) = CONCAT(
    N'Massa gerada. Totais (com o seed): ',
    (SELECT COUNT(*) FROM dbo.Clientes), N' clientes, ',
    (SELECT COUNT(*) FROM dbo.Produtos), N' produtos, ',
    (SELECT COUNT(*) FROM dbo.Pedidos), N' pedidos, ',
    (SELECT COUNT(*) FROM dbo.ItensPedido), N' itens.');
PRINT @Resumo;
GO

SET NOEXEC OFF;
GO
