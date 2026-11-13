using JoyaPedidos.TestesIntegracao.Infraestrutura;

namespace JoyaPedidos.TestesIntegracao.Banco;

/// <summary>
/// Container SQL Server EXCLUSIVO do IT-21, separado do container compartilhado pela suíte (plano E3): banco novo,
/// scripts 01–03 aplicados duas vezes, contagens registradas após cada execução. Descartado ao fim da classe.
/// Nenhum DROP DATABASE é necessário.
/// </summary>
public sealed class BancoIsoladoFixture : IAsyncLifetime
{
    private SqlServerDeTeste? _banco;

    public string StringDeConexao => _banco!.StringDeConexao;

    public string SaidaDaSegundaExecucao { get; private set; } = string.Empty;

    public string ContagensAposPrimeiraExecucao { get; private set; } = string.Empty;

    public string ContagensAposSegundaExecucao { get; private set; } = string.Empty;

    /// <summary>
    /// Tabelas cujo IDENTITY atual difere do maior Id, medido logo após os scripts e antes de qualquer teste da classe
    /// (testes de restrição consomem valores de IDENTITY em inserts que falham, e o SQL Server não os devolve).
    /// </summary>
    public IReadOnlyList<string> IdentidadesDesalinhadasAposScripts { get; private set; } = [];

    public async ValueTask InitializeAsync()
    {
        var cancelamento = TestContext.Current.CancellationToken;

        // IniciarAsync já aplica os scripts uma vez em um banco novo.
        _banco = await SqlServerDeTeste.IniciarAsync(cancelamento);
        ContagensAposPrimeiraExecucao = await ContarAsync(cancelamento);

        SaidaDaSegundaExecucao = await _banco.AplicarScriptsAsync(cancelamento);
        ContagensAposSegundaExecucao = await ContarAsync(cancelamento);
        IdentidadesDesalinhadasAposScripts = await ConsultaSql.ListaAsync(StringDeConexao, """
            SELECT t.Tabela FROM (VALUES
                (N'Clientes',    IDENT_CURRENT(N'dbo.Clientes'),    (SELECT MAX(Id) FROM dbo.Clientes)),
                (N'Produtos',    IDENT_CURRENT(N'dbo.Produtos'),    (SELECT MAX(Id) FROM dbo.Produtos)),
                (N'Pedidos',     IDENT_CURRENT(N'dbo.Pedidos'),     (SELECT MAX(Id) FROM dbo.Pedidos)),
                (N'ItensPedido', IDENT_CURRENT(N'dbo.ItensPedido'), (SELECT MAX(Id) FROM dbo.ItensPedido))
            ) AS t (Tabela, Atual, Maior)
            WHERE t.Atual <> t.Maior
            """, cancelamento);
    }

    private Task<string> ContarAsync(CancellationToken cancelamento) => ConsultaSql.EscalarAsync<string>(
        StringDeConexao,
        """
        SELECT CONCAT(
            (SELECT COUNT(*) FROM dbo.Clientes), '/', (SELECT COUNT(*) FROM dbo.Produtos), '/',
            (SELECT COUNT(*) FROM dbo.Pedidos), '/', (SELECT COUNT(*) FROM dbo.ItensPedido))
        """,
        cancelamento);

    public async ValueTask DisposeAsync()
    {
        if (_banco is not null)
        {
            await _banco.DisposeAsync();
        }
    }
}
