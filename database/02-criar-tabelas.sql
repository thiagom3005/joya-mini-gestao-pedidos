-- 02-criar-tabelas.sql — tabelas, chaves, restrições e índices (spec §4.2 e §4.3).
-- Idempotente: cada tabela e cada índice só é criado se ainda não existir. Restrições são declaradas junto com a
-- tabela, para que tabela e regras nasçam juntas.
-- Valores monetários em DECIMAL(18,2); instantes em DATETIME2(0) UTC (spec §4.5).

SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

USE JoyaPedidos;
GO

-- Sem cache de IDENTITY: após uma parada abrupta do servidor, o próximo Id continua do maior valor gravado,
-- em vez de saltar 1.000 posições. Os Ids do seed seguem previsíveis na demonstração. Idempotente.
-- Isso NÃO garante sequência sem lacunas: inserts que falham ou são desfeitos (rollback) consomem valores de
-- IDENTITY, que o SQL Server não devolve. Nenhuma regra do sistema depende de Ids contíguos.
ALTER DATABASE SCOPED CONFIGURATION SET IDENTITY_CACHE = OFF;
GO

IF OBJECT_ID(N'dbo.Clientes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Clientes
    (
        Id    INT IDENTITY(1, 1) NOT NULL,
        Nome  NVARCHAR(150)      NOT NULL,
        Email NVARCHAR(254)      NOT NULL,
        CONSTRAINT PK_Clientes PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT CK_Clientes_Nome CHECK (LEN(Nome) > 0),
        CONSTRAINT UQ_Clientes_Email UNIQUE (Email)
    );
    PRINT N'Tabela Clientes criada.';
END
GO

IF OBJECT_ID(N'dbo.Produtos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Produtos
    (
        Id      INT IDENTITY(1, 1) NOT NULL,
        Nome    NVARCHAR(150)      NOT NULL,
        Preco   DECIMAL(18, 2)     NOT NULL,
        Estoque INT                NOT NULL,
        CONSTRAINT PK_Produtos PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT CK_Produtos_Nome CHECK (LEN(Nome) > 0),
        CONSTRAINT CK_Produtos_Preco CHECK (Preco > 0),
        -- Última barreira contra estoque negativo (spec §7.2).
        CONSTRAINT CK_Produtos_Estoque CHECK (Estoque >= 0)
    );
    PRINT N'Tabela Produtos criada.';
END
GO

IF OBJECT_ID(N'dbo.Pedidos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Pedidos
    (
        Id         INT IDENTITY(1, 1) NOT NULL,
        ClienteId  INT                NOT NULL,
        -- Instante UTC, sem DEFAULT: a API sempre informa o valor (RN-09).
        DataPedido DATETIME2(0)       NOT NULL,
        CONSTRAINT PK_Pedidos PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Pedidos_Clientes FOREIGN KEY (ClienteId)
            REFERENCES dbo.Clientes (Id) ON DELETE NO ACTION
    );
    PRINT N'Tabela Pedidos criada.';
END
GO

IF OBJECT_ID(N'dbo.ItensPedido', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ItensPedido
    (
        Id            INT IDENTITY(1, 1) NOT NULL,
        PedidoId      INT                NOT NULL,
        ProdutoId     INT                NOT NULL,
        Quantidade    INT                NOT NULL,
        -- Preço congelado no momento do pedido (RN-08).
        PrecoUnitario DECIMAL(18, 2)     NOT NULL,
        CONSTRAINT PK_ItensPedido PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_ItensPedido_Pedidos FOREIGN KEY (PedidoId)
            REFERENCES dbo.Pedidos (Id) ON DELETE NO ACTION,
        CONSTRAINT FK_ItensPedido_Produtos FOREIGN KEY (ProdutoId)
            REFERENCES dbo.Produtos (Id) ON DELETE NO ACTION,
        CONSTRAINT CK_ItensPedido_Quantidade CHECK (Quantidade > 0),
        CONSTRAINT CK_ItensPedido_PrecoUnitario CHECK (PrecoUnitario > 0)
    );
    PRINT N'Tabela ItensPedido criada.';
END
GO

-- Índices (spec §4.3). Os de Pedidos e o INCLUDE de ItensPedido são hipóteses a validar na Parte 4 (§9.6).
-- ItensPedido.ProdutoId não tem índice próprio, por decisão documentada na spec §4.3.

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Pedidos_DataPedido' AND object_id = OBJECT_ID(N'dbo.Pedidos'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Pedidos_DataPedido ON dbo.Pedidos (DataPedido) INCLUDE (ClienteId);
    PRINT N'Índice IX_Pedidos_DataPedido criado.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Pedidos_ClienteId_DataPedido' AND object_id = OBJECT_ID(N'dbo.Pedidos'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Pedidos_ClienteId_DataPedido ON dbo.Pedidos (ClienteId, DataPedido);
    PRINT N'Índice IX_Pedidos_ClienteId_DataPedido criado.';
END
GO

-- Garante RN-04 (produto não repetido no mesmo pedido) e cobre a soma dos itens por pedido.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_ItensPedido_PedidoId_ProdutoId' AND object_id = OBJECT_ID(N'dbo.ItensPedido'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UX_ItensPedido_PedidoId_ProdutoId
        ON dbo.ItensPedido (PedidoId, ProdutoId) INCLUDE (Quantidade, PrecoUnitario);
    PRINT N'Índice UX_ItensPedido_PedidoId_ProdutoId criado.';
END
GO
