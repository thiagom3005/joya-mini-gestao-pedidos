-- performance/medir-ranking.sql — medição da Parte 4 (spec §9.6). NUNCA roda automaticamente.
-- Execute SOMENTE no ambiente isolado joya-perf, depois de gerar-massa.sql:
--
--   docker compose -p joya-perf -f docker-compose.yml -f docker-compose.perf.yml run --rm --no-deps -T db-init \
--     /opt/mssql-tools18/bin/sqlcmd -C -b -f 65001 -y 0 -S sqlserver -U sa -d JoyaPedidos \
--     -i /database/performance/medir-ranking.sql > /caminho/fora/do/repositorio/medicao.txt
--   (Git Bash: prefixe MSYS_NO_PATHCONV=1. -y 0 não trunca o XML do plano.)
--
-- Trava: recusa (erro 50020) instância que não seja joya-perf-sqlserver (hostname do docker-compose.perf.yml) ou
-- banco sem a massa. Pode ser executado de novo, inclusive depois de abortar no meio (recria o índice ausente).
--
-- Condições controladas, aplicadas SÓ neste banco isolado: Query Store desligado e feedbacks de concessão de memória
-- e de cardinalidade desligados, para que nenhuma execução ajuste o plano ou a memória da seguinte e cada passada
-- seja independente. Sem limpar caches da instância (DBCC DROPCLEANBUFFERS/FREEPROCCACHE): a medição é com cache de
-- dados aquecido, depois de uma execução descartada. As estatísticas vêm do gerar-massa.sql (FULLSCAN).
-- Variantes: V0 (original literal), V1 (V0 só com o predicado em intervalo), V0 e V1 com MAXDOP 1 (diagnóstico),
-- V2 (corrigida, limites literais), V2 parametrizada com DATETIME2(0) e DATETIME2(7), V2 com variáveis locais, SQL da
-- API (TOP 5, DATETIME2(7)) e, sem IX_Pedidos_DataPedido, V2, V2 DATETIME2(7) e V0. O índice é removido e recriado
-- (mesma definição de 02-criar-tabelas.sql) NESTE banco isolado; o esquema versionado não muda.

:on error exit
SET NOCOUNT ON;
GO

IF ISNULL(CAST(SERVERPROPERTY('MachineName') AS NVARCHAR(128)), N'') <> N'joya-perf-sqlserver'
   OR DB_NAME() <> N'JoyaPedidos'
   OR NOT EXISTS (SELECT 1 FROM dbo.Clientes WHERE Email LIKE N'%@massa.exemplo')
    THROW 50020, N'Execute só no ambiente isolado joya-perf (docker-compose.perf.yml), no JoyaPedidos com a massa.', 1;
GO

-- Reentrância: uma execução abortada entre o DROP e o CREATE deixaria o índice ausente.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Pedidos_DataPedido' AND object_id = OBJECT_ID(N'dbo.Pedidos'))
BEGIN
    PRINT N'IX_Pedidos_DataPedido ausente (execução anterior interrompida): recriando antes de medir.';
    CREATE NONCLUSTERED INDEX IX_Pedidos_DataPedido ON dbo.Pedidos (DataPedido) INCLUDE (ClienteId);
END
GO

-- Condições controladas (só neste banco). Alterar estas opções descarta os planos em cache deste banco.
IF EXISTS (SELECT 1 FROM sys.database_query_store_options WHERE actual_state <> 0)
    ALTER DATABASE CURRENT SET QUERY_STORE CLEAR;
ALTER DATABASE CURRENT SET QUERY_STORE = OFF;
ALTER DATABASE SCOPED CONFIGURATION SET MEMORY_GRANT_FEEDBACK_PERSISTENCE = OFF;
ALTER DATABASE SCOPED CONFIGURATION SET MEMORY_GRANT_FEEDBACK_PERCENTILE_GRANT = OFF;
ALTER DATABASE SCOPED CONFIGURATION SET ROW_MODE_MEMORY_GRANT_FEEDBACK = OFF;
ALTER DATABASE SCOPED CONFIGURATION SET BATCH_MODE_MEMORY_GRANT_FEEDBACK = OFF;
ALTER DATABASE SCOPED CONFIGURATION SET CE_FEEDBACK = OFF;
GO

PRINT N'### AMBIENTE';
SELECT CAST(SERVERPROPERTY('MachineName') AS NVARCHAR(128)) AS Instancia,
       CAST(SERVERPROPERTY('ProductVersion') AS NVARCHAR(30)) AS Versao,
       CAST(SERVERPROPERTY('ProductUpdateLevel') AS NVARCHAR(10)) AS Cu,
       si.cpu_count AS Cpus,
       si.physical_memory_kb / 1024 AS MemoriaVisivelMb,
       (SELECT CAST(value_in_use AS INT) FROM sys.configurations WHERE name = N'max degree of parallelism') AS MaxdopServidor,
       (SELECT CAST(value AS INT) FROM sys.database_scoped_configurations WHERE name = N'MAXDOP') AS MaxdopBanco,
       (SELECT CAST(value_in_use AS INT) FROM sys.configurations WHERE name = N'cost threshold for parallelism') AS LimiarParalelismo,
       (SELECT compatibility_level FROM sys.databases WHERE name = DB_NAME()) AS NivelCompatibilidade,
       (SELECT actual_state_desc FROM sys.database_query_store_options) AS QueryStore,
       (SELECT STRING_AGG(CONCAT(name, N'=', CAST(value AS NVARCHAR(10))), N' ') WITHIN GROUP (ORDER BY name)
        FROM sys.database_scoped_configurations
        WHERE name IN (N'MEMORY_GRANT_FEEDBACK_PERSISTENCE', N'MEMORY_GRANT_FEEDBACK_PERCENTILE_GRANT',
                       N'ROW_MODE_MEMORY_GRANT_FEEDBACK', N'BATCH_MODE_MEMORY_GRANT_FEEDBACK', N'CE_FEEDBACK',
                       N'DOP_FEEDBACK', N'BATCH_MODE_ON_ROWSTORE')) AS Configuracoes
FROM sys.dm_os_sys_info AS si;
GO

PRINT N'### MASSA';
SELECT (SELECT COUNT(*) FROM dbo.Clientes) AS Clientes,
       (SELECT COUNT(*) FROM (SELECT Nome FROM dbo.Clientes GROUP BY Nome HAVING COUNT(*) > 1) AS h) AS NomesHomonimos,
       (SELECT COUNT(*) FROM dbo.Clientes AS c WHERE NOT EXISTS (SELECT 1 FROM dbo.Pedidos AS p WHERE p.ClienteId = c.Id)) AS ClientesSemPedidos,
       (SELECT COUNT(*) FROM dbo.Produtos) AS Produtos,
       (SELECT COUNT(*) FROM dbo.Pedidos) AS Pedidos,
       (SELECT COUNT(*) FROM dbo.ItensPedido) AS Itens,
       (SELECT CAST(COUNT(*) AS DECIMAL(9, 2)) / (SELECT COUNT(*) FROM dbo.Pedidos) FROM dbo.ItensPedido) AS ItensPorPedido,
       (SELECT MIN(DataPedido) FROM dbo.Pedidos) AS PrimeiroPedidoUtc,
       (SELECT MAX(DataPedido) FROM dbo.Pedidos) AS UltimoPedidoUtc;

PRINT N'### PEDIDOS POR ANO (local = UTC-3; UTC = YEAR da coluna)';
SELECT Ano,
       SUM(CASE WHEN Base = 'local' THEN Pedidos END) AS PedidosAnoLocal,
       SUM(CASE WHEN Base = 'utc' THEN Pedidos END) AS PedidosAnoUtc
FROM (SELECT 'local' AS Base, YEAR(DATEADD(HOUR, -3, DataPedido)) AS Ano, COUNT(*) AS Pedidos FROM dbo.Pedidos GROUP BY YEAR(DATEADD(HOUR, -3, DataPedido))
      UNION ALL
      SELECT 'utc', YEAR(DataPedido), COUNT(*) FROM dbo.Pedidos GROUP BY YEAR(DataPedido)) AS a
GROUP BY Ano
ORDER BY Ano;

PRINT N'### TAMANHO DOS INDICES';
SELECT OBJECT_NAME(i.object_id) AS Tabela, i.name AS Indice, i.type_desc AS Tipo,
       SUM(ps.row_count) AS Linhas, SUM(ps.used_page_count) AS PaginasUsadas,
       SUM(ps.used_page_count) * 8 / 1024.0 AS UsadoMb
FROM sys.indexes AS i
INNER JOIN sys.dm_db_partition_stats AS ps ON ps.object_id = i.object_id AND ps.index_id = i.index_id
WHERE OBJECTPROPERTY(i.object_id, 'IsUserTable') = 1
GROUP BY i.object_id, i.name, i.type_desc, i.index_id
ORDER BY Tabela, i.index_id;
GO

:setvar ROTULO "V0 original"
:setvar ARQUIVO /database/consultas/ranking-2026-original.sql
:r /database/performance/medir-variante.sql

:setvar ROTULO "V1 original com intervalo"
:setvar ARQUIVO /database/performance/variantes/v1-original-com-intervalo.sql
:r /database/performance/medir-variante.sql

:setvar ROTULO "V0 MAXDOP 1 (diagnostico)"
:setvar ARQUIVO /database/performance/variantes/v0-maxdop-1.sql
:r /database/performance/medir-variante.sql

:setvar ROTULO "V1 MAXDOP 1 (diagnostico)"
:setvar ARQUIVO /database/performance/variantes/v1-maxdop-1.sql
:r /database/performance/medir-variante.sql

:setvar ROTULO "V2 corrigida"
:setvar ARQUIVO /database/consultas/ranking-2026-corrigida.sql
:r /database/performance/medir-variante.sql

:setvar ROTULO "V2 parametrizada datetime2(0)"
:setvar ARQUIVO /database/performance/variantes/v2-parametrizada-datetime2-0.sql
:r /database/performance/medir-variante.sql

:setvar ROTULO "V2 parametrizada datetime2(7)"
:setvar ARQUIVO /database/performance/variantes/v2-parametrizada-datetime2-7.sql
:r /database/performance/medir-variante.sql

:setvar ROTULO "V2 variaveis locais"
:setvar ARQUIVO /database/performance/variantes/v2-variaveis-locais.sql
:r /database/performance/medir-variante.sql

:setvar ROTULO "API top 5 datetime2(7)"
:setvar ARQUIVO /database/performance/variantes/api-top5-parametrizada.sql
:r /database/performance/medir-variante.sql

PRINT N'### Removendo IX_Pedidos_DataPedido (somente neste banco isolado)';
DROP INDEX IF EXISTS IX_Pedidos_DataPedido ON dbo.Pedidos;
GO

:setvar ROTULO "V2 corrigida SEM IX_Pedidos_DataPedido"
:setvar ARQUIVO /database/consultas/ranking-2026-corrigida.sql
:r /database/performance/medir-variante.sql

:setvar ROTULO "V2 parametrizada datetime2(7) SEM IX_Pedidos_DataPedido"
:setvar ARQUIVO /database/performance/variantes/v2-parametrizada-datetime2-7.sql
:r /database/performance/medir-variante.sql

:setvar ROTULO "V0 original SEM IX_Pedidos_DataPedido"
:setvar ARQUIVO /database/consultas/ranking-2026-original.sql
:r /database/performance/medir-variante.sql

PRINT N'### Recriando IX_Pedidos_DataPedido (mesma definição de 02-criar-tabelas.sql)';
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Pedidos_DataPedido' AND object_id = OBJECT_ID(N'dbo.Pedidos'))
    CREATE NONCLUSTERED INDEX IX_Pedidos_DataPedido ON dbo.Pedidos (DataPedido) INCLUDE (ClienteId);
GO

PRINT N'### FIM';
GO
