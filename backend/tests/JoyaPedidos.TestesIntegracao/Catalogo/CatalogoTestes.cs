using System.Net;
using System.Text.Json;
using JoyaPedidos.TestesIntegracao.Infraestrutura;

namespace JoyaPedidos.TestesIntegracao.Catalogo;

/// <summary>
/// IT-22: <c>GET /clientes</c> e <c>GET /produtos</c> (spec §5.7–5.8). Nomes ASCII e ordem esperada explícita, para não
/// depender de collation; homônimos provam o desempate por id.
/// </summary>
public sealed class CatalogoTestes(FabricaApi fabrica) : IAsyncLifetime
{
    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private readonly DadosDeTeste _dados = new(fabrica.Banco.StringDeConexao);
    private readonly HttpClient _cliente = fabrica.CreateClient();

    public async ValueTask InitializeAsync() => await fabrica.LimparBancoAsync(Cancelamento);

    public ValueTask DisposeAsync()
    {
        _cliente.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task<List<JsonElement>> ListarAsync(string rota)
    {
        var resposta = await _cliente.GetAsync(rota, Cancelamento);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("application/json", resposta.Content.Headers.ContentType?.MediaType);
        return (await RespostaHttp.LerJsonAsync(resposta)).EnumerateArray().ToList();
    }

    private static string[] Propriedades(JsonElement elemento) =>
        elemento.EnumerateObject().Select(propriedade => propriedade.Name).ToArray();

    // IT-22 (clientes)
    [Fact]
    public async Task ListarClientes_ComHomonimos_OrdenaPorNomeEDepoisIdComOsCamposDaSpec()
    {
        var bruno = await _dados.InserirClienteAsync("Bruno Lima", "bruno@teste.exemplo");
        var ana1 = await _dados.InserirClienteAsync("Ana Souza", "ana1@teste.exemplo");
        var carla = await _dados.InserirClienteAsync("Carla Dias", "carla@teste.exemplo");
        var ana2 = await _dados.InserirClienteAsync("Ana Souza", "ana2@teste.exemplo");

        var clientes = await ListarAsync("/clientes");

        Assert.Equal([ana1, ana2, bruno, carla], clientes.Select(cliente => cliente.GetProperty("id").GetInt32()));
        Assert.All(clientes, cliente => Assert.Equal(["id", "nome", "email"], Propriedades(cliente)));
        Assert.Equal("Ana Souza", clientes[1].GetProperty("nome").GetString());
        Assert.Equal("ana2@teste.exemplo", clientes[1].GetProperty("email").GetString());
    }

    // IT-22 (clientes, banco vazio)
    [Fact]
    public async Task ListarClientes_SemClientes_RetornaListaVazia()
    {
        Assert.Empty(await ListarAsync("/clientes"));
    }

    // IT-22 (produtos)
    [Fact]
    public async Task ListarProdutos_ComHomonimosEEsgotado_OrdenaPorNomeEDepoisIdIncluindoEstoqueZero()
    {
        var mesa = await _dados.InserirProdutoAsync("Luminaria de mesa", 149.00m, 0);
        var caneca1 = await _dados.InserirProdutoAsync("Caneca de ceramica", 19.99m, 12);
        var garrafa = await _dados.InserirProdutoAsync("Garrafa termica", 99.90m, 5);
        var caneca2 = await _dados.InserirProdutoAsync("Caneca de ceramica", 24.50m, 3);

        var produtos = await ListarAsync("/produtos");

        Assert.Equal([caneca1, caneca2, garrafa, mesa], produtos.Select(produto => produto.GetProperty("id").GetInt32()));
        Assert.All(produtos, produto => Assert.Equal(["id", "nome", "preco", "estoque"], Propriedades(produto)));
        // Preço com as duas casas da coluna DECIMAL(18,2), como número JSON (§5.1).
        Assert.Equal("149.00", produtos[3].GetProperty("preco").GetRawText());
        Assert.Equal(0, produtos[3].GetProperty("estoque").GetInt32());
        Assert.Equal("99.90", produtos[2].GetProperty("preco").GetRawText());
    }
}
