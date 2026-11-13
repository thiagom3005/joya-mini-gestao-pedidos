namespace JoyaPedidos.TestesIntegracao.Infraestrutura;

/// <summary>Garantias da própria infraestrutura de integração (plano E3): paridade com o compose e limpeza segura.</summary>
public sealed class InfraestruturaDeTesteTestes(FabricaApi fabrica)
{
    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    [Fact]
    public void ImagemDoSqlServer_TestesECompose_SaoAMesma()
    {
        var raiz = Directory.GetParent(SqlServerDeTeste.LocalizarPastaDatabase())!.FullName;
        var compose = File.ReadAllText(Path.Combine(raiz, "docker-compose.yml"));

        Assert.Contains(SqlServerDeTeste.Imagem, compose);
    }

    [Fact]
    public async Task LimparBanco_ComSeedETriggerDeTeste_RemoveDadosETriggerEPreservaOEsquema()
    {
        var conexao = fabrica.Banco.StringDeConexao;
        await ConsultaSql.EscalarAsync<int>(conexao, """
            EXEC (N'CREATE TRIGGER dbo.TR_Teste_Limpeza ON dbo.Produtos AFTER UPDATE AS BEGIN SET NOCOUNT ON; END');
            SELECT 1;
            """, Cancelamento);
        await ConsultaSql.EscalarAsync<int>(conexao, """
            IF NOT EXISTS (SELECT 1 FROM dbo.Clientes) INSERT INTO dbo.Clientes (Nome, Email) VALUES (N'Arranjo', N'arranjo@exemplo.com');
            SELECT 1;
            """, Cancelamento);
        fabrica.Relogio.Avancar(TimeSpan.FromDays(3));

        await fabrica.LimparBancoAsync(Cancelamento);

        Assert.Equal("0/0/0/0", await ConsultaSql.EscalarAsync<string>(conexao, """
            SELECT CONCAT((SELECT COUNT(*) FROM dbo.Clientes), '/', (SELECT COUNT(*) FROM dbo.Produtos), '/',
                          (SELECT COUNT(*) FROM dbo.Pedidos), '/', (SELECT COUNT(*) FROM dbo.ItensPedido))
            """, Cancelamento));
        Assert.Equal(0, await ConsultaSql.EscalarAsync<int>(conexao, "SELECT COUNT(*) FROM sys.triggers WHERE name LIKE N'TR[_]Teste[_]%'", Cancelamento));
        Assert.Equal(4, await ConsultaSql.EscalarAsync<int>(conexao, """
            SELECT COUNT(*) FROM sys.tables WHERE name IN (N'Clientes', N'Produtos', N'Pedidos', N'ItensPedido')
            """, Cancelamento));
        Assert.Equal(FabricaApi.InstanteInicial, fabrica.Relogio.GetUtcNow());
    }

    [Fact]
    public void Relogio_DaApi_EORelogioControladoDaFabrica()
    {
        var relogioDaApi = fabrica.Services.GetService(typeof(TimeProvider));

        Assert.Same(fabrica.Relogio, relogioDaApi);
    }

    [Fact]
    public void Relogio_VoltarNoTempo_EPermitido()
    {
        var relogio = new RelogioDeTeste(new DateTimeOffset(2027, 1, 1, 3, 0, 0, TimeSpan.Zero));

        relogio.Agora = new DateTimeOffset(2026, 1, 1, 2, 59, 59, TimeSpan.Zero);

        Assert.Equal(new DateTimeOffset(2026, 1, 1, 2, 59, 59, TimeSpan.Zero), relogio.GetUtcNow());
    }
}
