using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace JoyaPedidos.TestesIntegracao.Infraestrutura;

/// <summary>
/// Hospeda a API em memória para os testes HTTP. Na E3 passa a iniciar o SQL Server via Testcontainers,
/// aplicar os scripts de database/ e trocar o <see cref="TimeProvider"/> por um relógio controlado.
/// </summary>
public sealed class FabricaApi : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testes");
    }
}
