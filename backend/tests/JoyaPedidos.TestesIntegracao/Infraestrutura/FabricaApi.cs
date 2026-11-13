using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JoyaPedidos.TestesIntegracao.Infraestrutura;

/// <summary>
/// API em memória ligada a um SQL Server real compartilhado pela suíte (spec §10.2): um container por execução,
/// com os scripts de database/ aplicados. Os testes rodam em sequência e chamam <see cref="LimparBancoAsync"/>
/// antes de arranjar os próprios dados; nenhuma asserção depende de Id fixo.
/// O relógio da API é um <see cref="RelogioDeTeste"/>, para datas determinísticas (spec RN-09, IT-13).
/// </summary>
public sealed class FabricaApi : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>Instante inicial do relógio controlado (UTC).</summary>
    public static readonly DateTimeOffset InstanteInicial = new(2026, 11, 12, 15, 0, 0, TimeSpan.Zero);

    private SqlServerDeTeste? _banco;

    public RelogioDeTeste Relogio { get; } = new(InstanteInicial);

    public SqlServerDeTeste Banco => _banco ?? throw new InvalidOperationException("Banco de teste ainda não iniciado.");

    public async ValueTask InitializeAsync()
    {
        _banco = await SqlServerDeTeste.IniciarAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Banco vazio (sem dados nem triggers de teste) e relógio de volta ao instante inicial.</summary>
    public async Task LimparBancoAsync(CancellationToken cancelamento)
    {
        await Banco.LimparDadosAsync(cancelamento);
        Relogio.Agora = InstanteInicial;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testes");
        builder.UseSetting("ConnectionStrings:BancoPedidos", Banco.StringDeConexao);
        builder.ConfigureServices(servicos =>
        {
            servicos.RemoveAll<TimeProvider>();
            servicos.AddSingleton<TimeProvider>(Relogio);
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (_banco is not null)
        {
            await _banco.DisposeAsync();
        }
    }
}
