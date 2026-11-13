using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace JoyaPedidos.TestesIntegracao.Infraestrutura;

/// <summary>
/// SQL Server real e descartável para os testes (spec §10.1, DEC-12): mesma imagem do docker-compose.yml e os mesmos
/// scripts de database/, aplicados pelo mesmo database/aplicar-scripts.sh (sqlcmd -C -b) usado pelo db-init.
/// </summary>
public sealed class SqlServerDeTeste : IAsyncDisposable
{
    /// <summary>
    /// Deve ser idêntica à do docker-compose.yml (verificado por
    /// <c>InfraestruturaDeTesteTestes.ImagemDoSqlServer_TestesECompose_SaoAMesma</c>).
    /// </summary>
    public const string Imagem = "mcr.microsoft.com/mssql/server:2022-CU28-ubuntu-22.04";

    /// <summary>Banco criado por database/01-criar-banco.sql.</summary>
    public const string NomeDoBanco = "JoyaPedidos";

    /// <summary>Prefixo obrigatório de triggers criados por testes; a limpeza remove todos eles.</summary>
    public const string PrefixoDeTriggerDeTeste = "TR_Teste_";

    // Senha de um container efêmero de teste: não é segredo e nunca sai desta máquina.
    private const string SenhaDoContainer = "Joya_Teste_2026_Inseguro";

    private readonly MsSqlContainer _container;

    private SqlServerDeTeste(MsSqlContainer container) => _container = container;

    /// <summary>Conexão com o banco do projeto (não com o master).</summary>
    public string StringDeConexao => new SqlConnectionStringBuilder(_container.GetConnectionString())
    {
        InitialCatalog = NomeDoBanco,
    }.ConnectionString;

    /// <summary>Inicia um container novo e aplica os scripts de database/ uma vez.</summary>
    public static async Task<SqlServerDeTeste> IniciarAsync(CancellationToken cancelamento)
    {
        var container = new MsSqlBuilder(Imagem)
            .WithPassword(SenhaDoContainer)
            // Lida pelo sqlcmd de aplicar-scripts.sh: a senha não aparece na linha de comando.
            .WithEnvironment("SQLCMDPASSWORD", SenhaDoContainer)
            .WithResourceMapping(new DirectoryInfo(LocalizarPastaDatabase()), new DirectoryInfo("/database/"))
            .Build();

        var banco = new SqlServerDeTeste(container);
        try
        {
            await container.StartAsync(cancelamento);
            await banco.AplicarScriptsAsync(cancelamento);
            return banco;
        }
        catch
        {
            await banco.DisposeAsync();
            throw;
        }
    }

    /// <summary>Executa database/aplicar-scripts.sh no container; falha com a saída completa se algum script falhar.</summary>
    public async Task<string> AplicarScriptsAsync(CancellationToken cancelamento)
    {
        var execucao = await _container.ExecAsync(["/bin/bash", "/database/aplicar-scripts.sh", "localhost"], cancelamento);
        if (execucao.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"aplicar-scripts.sh terminou com código {execucao.ExitCode}.{Environment.NewLine}{execucao.Stdout}{execucao.Stderr}");
        }
        return execucao.Stdout;
    }

    /// <summary>
    /// Deixa o banco vazio para o próximo teste: remove triggers de teste (<see cref="PrefixoDeTriggerDeTeste"/>) e
    /// apaga os dados na ordem das chaves estrangeiras. O esquema e os índices permanecem.
    /// </summary>
    public async Task LimparDadosAsync(CancellationToken cancelamento)
    {
        const string sql = """
            DECLARE @remover NVARCHAR(MAX) = N'';
            SELECT @remover += N'DROP TRIGGER ' + QUOTENAME(OBJECT_SCHEMA_NAME(t.object_id)) + N'.' + QUOTENAME(t.name) + N';'
            FROM sys.triggers AS t
            WHERE t.parent_class = 1 AND t.name LIKE @prefixo + N'%';
            EXEC sys.sp_executesql @remover;

            DELETE FROM dbo.ItensPedido;
            DELETE FROM dbo.Pedidos;
            DELETE FROM dbo.Produtos;
            DELETE FROM dbo.Clientes;
            """;

        await using var conexao = new SqlConnection(StringDeConexao);
        await conexao.OpenAsync(cancelamento);
        await using var comando = new SqlCommand(sql, conexao);
        comando.Parameters.AddWithValue("@prefixo", PrefixoDeTriggerDeTeste.Replace("_", "[_]", StringComparison.Ordinal));
        await comando.ExecuteNonQueryAsync(cancelamento);
    }

    public ValueTask DisposeAsync() => _container.DisposeAsync();

    /// <summary>Sobe a partir da pasta dos binários até achar database/aplicar-scripts.sh na raiz do repositório.</summary>
    public static string LocalizarPastaDatabase()
    {
        for (var pasta = new DirectoryInfo(AppContext.BaseDirectory); pasta is not null; pasta = pasta.Parent)
        {
            var candidata = Path.Combine(pasta.FullName, "database");
            if (File.Exists(Path.Combine(candidata, "aplicar-scripts.sh")))
            {
                return candidata;
            }
        }
        throw new DirectoryNotFoundException("Pasta database/ do repositório não encontrada a partir de " + AppContext.BaseDirectory);
    }
}
