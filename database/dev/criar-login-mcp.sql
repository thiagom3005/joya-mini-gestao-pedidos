-- dev/criar-login-mcp.sql — login de privilégio mínimo para o MCP do Claude Code (spec §2.3.1).
-- NUNCA roda automaticamente (fica fora da raiz de database/). Execute manualmente, com o banco já criado:
--
--   PowerShell / Linux / macOS (a senha vem do ambiente; só o NOME da variável aparece na linha de comando):
--     docker compose run --rm --no-deps -e JOYA_MCP_SENHA db-init /opt/mssql-tools18/bin/sqlcmd -C -b -f 65001 \
--       -S sqlserver -U sa -i /database/dev/criar-login-mcp.sql
--   Git Bash (Windows): prefixe MSYS_NO_PATHCONV=1, senão o caminho /opt/... é convertido para C:/Program Files/Git/...
--   --no-deps: não recria o sqlserver (o que removeria a porta publicada pelo docker-compose.dev.yml).
--
-- O sqlcmd lê a variável de ambiente JOYA_MCP_SENHA como variável de script $(JOYA_MCP_SENHA).
-- Requisitos da senha: política do SQL Server (8+ caracteres, maiúsculas, minúsculas e dígitos) e SOMENTE letras,
-- dígitos, "_" ou "-", porque ela é inserida sem codificação na DSN do .mcp.json e entre aspas simples aqui.
-- Idempotente: cria o login e o usuário se não existirem; caso contrário, apenas redefine a senha.
--
-- Permissões (spec §2.3.1): db_datareader no JoyaPedidos (inclui visibilidade dos metadados dos objetos lidos) e
-- VIEW SERVER PERFORMANCE STATE (sys.dm_exec_query_stats, SQL Server 2022). Nada administrativo, nenhuma escrita.

:on error exit
SET NOCOUNT ON;

-- COLLATE binária: com a collation padrão (CI_AS), [A-Za-z] aceitaria letras acentuadas.
IF LEN(N'$(JOYA_MCP_SENHA)') < 8 OR N'$(JOYA_MCP_SENHA)' COLLATE Latin1_General_BIN2 LIKE N'%[^A-Za-z0-9_-]%'
    THROW 50001, N'JOYA_MCP_SENHA ausente ou inválida: use 8+ caracteres, apenas letras, dígitos, "_" ou "-".', 1;
GO

USE master;
GO

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'joya_mcp')
BEGIN
    CREATE LOGIN joya_mcp WITH PASSWORD = N'$(JOYA_MCP_SENHA)', DEFAULT_DATABASE = JoyaPedidos, CHECK_POLICY = ON;
    PRINT N'Login joya_mcp criado.';
END
ELSE
BEGIN
    ALTER LOGIN joya_mcp WITH PASSWORD = N'$(JOYA_MCP_SENHA)';
    PRINT N'Senha do login joya_mcp redefinida.';
END

GRANT VIEW SERVER PERFORMANCE STATE TO joya_mcp;
GO

USE JoyaPedidos;
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'joya_mcp')
BEGIN
    CREATE USER joya_mcp FOR LOGIN joya_mcp;
    PRINT N'Usuário joya_mcp criado no JoyaPedidos.';
END

IF IS_ROLEMEMBER(N'db_datareader', N'joya_mcp') = 0
    ALTER ROLE db_datareader ADD MEMBER joya_mcp;
GO

PRINT N'Permissões do joya_mcp: db_datareader (JoyaPedidos) e VIEW SERVER PERFORMANCE STATE.';
GO
