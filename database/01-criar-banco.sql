-- 01-criar-banco.sql — cria o banco JoyaPedidos (spec §4.6). Idempotente: não faz nada se o banco existir.
-- Executado automaticamente pelo db-init (sqlcmd -C -b) e pelos testes de integração.
-- READ_COMMITTED_SNAPSHOT fica desligado (padrão de bancos novos), como exige a spec §7.2.

SET NOCOUNT ON;

IF DB_ID(N'JoyaPedidos') IS NULL
BEGIN
    CREATE DATABASE JoyaPedidos;
    PRINT N'Banco JoyaPedidos criado.';
END
ELSE
BEGIN
    PRINT N'Banco JoyaPedidos já existe.';
END
GO
