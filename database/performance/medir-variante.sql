-- performance/medir-variante.sql — protocolo de UMA variante (spec §9.6, passos 3 a 5). Incluído por
-- medir-ranking.sql com :r, depois de :setvar ROTULO e :setvar ARQUIVO. Não execute sozinho.
--   1 aquecimento (descartado) + 3 rodadas medidas com SET STATISTICS IO, TIME ON (a mediana vai para o README)
--   + 1 execução separada com SET STATISTICS XML ON (plano real; não entra na medição de tempo).
-- O arquivo da variante é incluído sem alteração, sozinho no próprio batch.

PRINT N'### $(ROTULO) | aquecimento';
GO
SET STATISTICS IO, TIME ON;
GO
:r $(ARQUIVO)
GO
SET STATISTICS IO, TIME OFF;
GO

PRINT N'### $(ROTULO) | rodada 1';
GO
SET STATISTICS IO, TIME ON;
GO
:r $(ARQUIVO)
GO
SET STATISTICS IO, TIME OFF;
GO

PRINT N'### $(ROTULO) | rodada 2';
GO
SET STATISTICS IO, TIME ON;
GO
:r $(ARQUIVO)
GO
SET STATISTICS IO, TIME OFF;
GO

PRINT N'### $(ROTULO) | rodada 3';
GO
SET STATISTICS IO, TIME ON;
GO
:r $(ARQUIVO)
GO
SET STATISTICS IO, TIME OFF;
GO

PRINT N'### $(ROTULO) | plano real';
GO
SET STATISTICS XML ON;
GO
:r $(ARQUIVO)
GO
SET STATISTICS XML OFF;
GO
PRINT N'### $(ROTULO) | fim';
GO
