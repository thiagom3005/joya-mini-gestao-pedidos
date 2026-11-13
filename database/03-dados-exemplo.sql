-- 03-dados-exemplo.sql — carga de exemplo determinística (spec §4.7).
-- Idempotente: só carrega se dbo.Clientes estiver vazia, tudo numa única transação (tudo ou nada).
-- IDs explícitos (IDENTITY_INSERT) para que documentação e demonstração possam citá-los; novos registros criados
-- pela API continuam a partir do maior Id (o 02 desliga o IDENTITY_CACHE, então não há salto após reinício).
--
-- O que este seed demonstra:
--   * Homônimos: clientes 1 e 2 se chamam "Ana Souza". Em 2026 (ano civil de São Paulo) somam 1.952,35, mais que o
--     maior cliente individual (Heitor Mendes, 1.110,00), embora cada uma isolada fique abaixo dele. Isso evidencia
--     o defeito do GROUP BY c.Nome da query original (spec §9.2, D2).
--   * Estoques: produto 7 com estoque 0; produto 6 com estoque 2; produtos 4 e 5 com estoque >= 1.000 (E2E).
--   * Fronteiras do ano civil local (UTC-3), com DataPedido em UTC:
--       pedido 15: 2026-01-01T02:59:59Z = 31/12/2025 23:59:59 em São Paulo (conta como 2025)
--       pedido 16: 2026-01-01T03:00:00Z = 01/01/2026 00:00:00 em São Paulo (conta como 2026)
--   * Limite superior: nenhum pedido após 2026-11-01T02:59:59Z (31/10/2026 23:59:59 em São Paulo); o pedido 33 está
--     exatamente nesse instante. Um pedido criado na demonstração aparece sempre no topo da listagem.
--   * Preço congelado: itens dos pedidos 1, 2 e 10 têm PrecoUnitario menor que o Preco atual do produto.
--   * Estoque já representa o saldo após os pedidos históricos (o seed não recalcula estoque).

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

USE JoyaPedidos;
GO

IF EXISTS (SELECT 1 FROM dbo.Clientes)
BEGIN
    PRINT N'Dados de exemplo já presentes; carga ignorada.';
END
ELSE
BEGIN
    BEGIN TRANSACTION;

    SET IDENTITY_INSERT dbo.Clientes ON;
    INSERT INTO dbo.Clientes (Id, Nome, Email) VALUES
        (1,  N'Ana Souza',       N'ana.souza@exemplo.com'),
        (2,  N'Ana Souza',       N'ana.souza2@exemplo.com'),
        (3,  N'Bruno Lima',      N'bruno.lima@exemplo.com'),
        (4,  N'Carla Dias',      N'carla.dias@exemplo.com'),
        (5,  N'Diego Alves',     N'diego.alves@exemplo.com'),
        (6,  N'Elisa Rocha',     N'elisa.rocha@exemplo.com'),
        (7,  N'Fábio Nunes',     N'fabio.nunes@exemplo.com'),
        (8,  N'Gabriela Torres', N'gabriela.torres@exemplo.com'),
        (9,  N'Heitor Mendes',   N'heitor.mendes@exemplo.com'),
        (10, N'Isabela Castro',  N'isabela.castro@exemplo.com');
    SET IDENTITY_INSERT dbo.Clientes OFF;

    SET IDENTITY_INSERT dbo.Produtos ON;
    INSERT INTO dbo.Produtos (Id, Nome, Preco, Estoque) VALUES
        (1,  N'Caneca de cerâmica',            19.99,  120),
        (2,  N'Garrafa térmica',               99.90,   45),
        (3,  N'Camiseta básica',               49.90,  200),
        (4,  N'Caderno universitário',         24.50, 1500),
        (5,  N'Caneta esferográfica (cx. 12)', 18.75, 2000),
        (6,  N'Mochila executiva',            289.00,    2),
        (7,  N'Luminária de mesa',            149.00,    0),
        (8,  N'Fone de ouvido bluetooth',     199.99,   30),
        (9,  N'Mouse sem fio',                 89.90,   60),
        (10, N'Teclado mecânico',             349.90,   15),
        (11, N'Suporte para notebook',        129.00,   25),
        (12, N'Agenda 2026',                   39.90,   80);
    SET IDENTITY_INSERT dbo.Produtos OFF;

    -- DataPedido em UTC; comentário com o horário de São Paulo (UTC-3).
    SET IDENTITY_INSERT dbo.Pedidos ON;
    INSERT INTO dbo.Pedidos (Id, ClienteId, DataPedido) VALUES
        (1,  3,  '2025-01-15T13:20:00'), -- 15/01/2025 10:20
        (2,  1,  '2025-02-03T15:05:00'), -- 03/02/2025 12:05
        (3,  4,  '2025-02-20T18:40:00'), -- 20/02/2025 15:40
        (4,  5,  '2025-03-11T12:00:00'), -- 11/03/2025 09:00
        (5,  6,  '2025-04-02T14:30:00'), -- 02/04/2025 11:30
        (6,  7,  '2025-04-28T20:15:00'), -- 28/04/2025 17:15
        (7,  8,  '2025-05-19T11:45:00'), -- 19/05/2025 08:45
        (8,  9,  '2025-06-07T16:10:00'), -- 07/06/2025 13:10
        (9,  2,  '2025-07-22T13:00:00'), -- 22/07/2025 10:00
        (10, 10, '2025-08-14T19:25:00'), -- 14/08/2025 16:25
        (11, 3,  '2025-09-30T17:50:00'), -- 30/09/2025 14:50
        (12, 4,  '2025-10-18T12:35:00'), -- 18/10/2025 09:35
        (13, 5,  '2025-11-25T21:10:00'), -- 25/11/2025 18:10
        (14, 7,  '2025-12-20T15:00:00'), -- 20/12/2025 12:00
        (15, 6,  '2026-01-01T02:59:59'), -- 31/12/2025 23:59:59 (fronteira: ainda 2025 em São Paulo)
        (16, 8,  '2026-01-01T03:00:00'), -- 01/01/2026 00:00:00 (fronteira: já 2026 em São Paulo)
        (17, 1,  '2026-01-20T14:00:00'), -- 20/01/2026 11:00
        (18, 2,  '2026-02-05T16:30:00'), -- 05/02/2026 13:30
        (19, 3,  '2026-02-18T13:15:00'), -- 18/02/2026 10:15
        (20, 4,  '2026-03-09T18:00:00'), -- 09/03/2026 15:00
        (21, 5,  '2026-03-27T12:45:00'), -- 27/03/2026 09:45
        (22, 9,  '2026-04-14T19:20:00'), -- 14/04/2026 16:20
        (23, 1,  '2026-05-03T15:40:00'), -- 03/05/2026 12:40
        (24, 10, '2026-05-21T11:10:00'), -- 21/05/2026 08:10
        (25, 2,  '2026-06-11T17:25:00'), -- 11/06/2026 14:25
        (26, 6,  '2026-07-02T14:05:00'), -- 02/07/2026 11:05
        (27, 7,  '2026-07-29T20:50:00'), -- 29/07/2026 17:50
        (28, 3,  '2026-08-16T13:35:00'), -- 16/08/2026 10:35
        (29, 8,  '2026-09-04T16:15:00'), -- 04/09/2026 13:15
        (30, 1,  '2026-09-25T12:20:00'), -- 25/09/2026 09:20
        (31, 5,  '2026-10-12T18:45:00'), -- 12/10/2026 15:45
        (32, 2,  '2026-10-31T23:30:00'), -- 31/10/2026 20:30
        (33, 4,  '2026-11-01T02:59:59'); -- 31/10/2026 23:59:59 (limite superior do seed)
    SET IDENTITY_INSERT dbo.Pedidos OFF;

    INSERT INTO dbo.ItensPedido (PedidoId, ProdutoId, Quantidade, PrecoUnitario) VALUES
        (1,  1,  2,  17.99), -- preço antigo (atual 19,99)
        (1,  3,  1,  49.90),
        (2,  2,  1,  89.90), -- preço antigo (atual 99,90)
        (3,  4,  10, 24.50),
        (3,  5,  2,  18.75),
        (4,  9,  1,  89.90),
        (4,  8,  1, 199.99),
        (5,  12, 3,  39.90),
        (6,  10, 1, 349.90),
        (6,  9,  1,  89.90),
        (7,  3,  4,  49.90),
        (7,  1,  2,  19.99),
        (8,  11, 1, 129.00),
        (9,  6,  1, 289.00),
        (10, 2,  2,  89.90), -- preço antigo (atual 99,90)
        (10, 12, 1,  39.90),
        (11, 8,  1, 199.99),
        (12, 4,  5,  24.50),
        (13, 5,  6,  18.75),
        (13, 1,  1,  19.99),
        (14, 7,  1, 149.00),
        (15, 10, 1, 349.90),
        (16, 9,  2,  89.90),
        (17, 10, 1, 349.90),
        (17, 11, 1, 129.00),
        (18, 8,  2, 199.99),
        (19, 3,  3,  49.90),
        (19, 12, 2,  39.90),
        (20, 2,  1,  99.90),
        (20, 1,  4,  19.99),
        (21, 6,  1, 289.00),
        (22, 4,  30, 24.50),
        (22, 5,  20, 18.75),
        (23, 9,  1,  89.90),
        (23, 8,  1, 199.99),
        (24, 11, 2, 129.00),
        (25, 10, 1, 349.90),
        (25, 12, 1,  39.90),
        (26, 3,  2,  49.90),
        (26, 1,  3,  19.99),
        (27, 2,  2,  99.90),
        (28, 10, 1, 349.90),
        (29, 4,  8,  24.50),
        (29, 12, 1,  39.90),
        (30, 2,  1,  99.90),
        (30, 3,  1,  49.90),
        (30, 5,  4,  18.75),
        (31, 8,  1, 199.99),
        (31, 9,  1,  89.90),
        (32, 11, 1, 129.00),
        (32, 1,  2,  19.99),
        (33, 12, 2,  39.90);

    COMMIT TRANSACTION;
    PRINT N'Dados de exemplo carregados: 10 clientes, 12 produtos, 33 pedidos, 52 itens.';
END
GO
