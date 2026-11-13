using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JoyaPedidos.TestesIntegracao.Infraestrutura;

namespace JoyaPedidos.TestesIntegracao.Pedidos;

/// <summary>
/// <c>GET /pedidos/{id}</c> (spec §5.5): IT-17 e a parte do IT-07 que cabe à E5 (a listagem fica para a E6).
/// </summary>
public sealed class ConsultarPedidoTestes(FabricaApi fabrica) : IAsyncLifetime
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

    private async Task<JsonElement> CriarPedidoAsync(int clienteId, params (int ProdutoId, int Quantidade)[] itens)
    {
        var resposta = await _cliente.PostAsJsonAsync(
            "/pedidos",
            new { clienteId, itens = itens.Select(item => new { produtoId = item.ProdutoId, quantidade = item.Quantidade }) },
            Cancelamento);
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        return await RespostaHttp.LerJsonAsync(resposta);
    }

    // IT-17 (existente)
    [Fact]
    public async Task ObterPedido_IdExistente_Retorna200ComOMesmoCorpoDoCreated()
    {
        var bruno = await _dados.InserirClienteAsync("Bruno Lima");
        var caneca = await _dados.InserirProdutoAsync("Caneca de cerâmica", 19.99m, 10);
        var garrafa = await _dados.InserirProdutoAsync("Garrafa térmica", 99.90m, 5);
        var criado = await CriarPedidoAsync(bruno, (garrafa, 1), (caneca, 3));

        var resposta = await _cliente.GetAsync($"/pedidos/{criado.GetProperty("id").GetInt32()}", Cancelamento);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("application/json", resposta.Content.Headers.ContentType?.MediaType);
        // Texto idêntico, não só JSON equivalente: 99.90 e 99.9 seriam iguais numa comparação semântica (revisão da E5).
        var consultado = await RespostaHttp.LerJsonAsync(resposta);
        Assert.Equal(criado.GetRawText(), consultado.GetRawText());
        Assert.EndsWith("Z", consultado.GetProperty("dataPedido").GetString(), StringComparison.Ordinal);
        Assert.Equal("99.90", consultado.GetProperty("itens")[1].GetProperty("precoUnitario").GetRawText());
    }

    // IT-07 (parte da E5: consulta por id)
    [Fact]
    public async Task ObterPedido_PrecoDoProdutoAlteradoDepois_MantemPrecoETotalOriginais()
    {
        var bruno = await _dados.InserirClienteAsync("Bruno Lima");
        var caneca = await _dados.InserirProdutoAsync("Caneca de cerâmica", 19.99m, 10);
        var criado = await CriarPedidoAsync(bruno, (caneca, 3));
        await _dados.ExecutarAsync("UPDATE dbo.Produtos SET Preco = 25.00 WHERE Id = @Id;", ("@Id", caneca));

        var resposta = await _cliente.GetAsync($"/pedidos/{criado.GetProperty("id").GetInt32()}", Cancelamento);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var consultado = await RespostaHttp.LerJsonAsync(resposta);
        var item = Assert.Single(consultado.GetProperty("itens").EnumerateArray().ToList());
        Assert.Equal("19.99", item.GetProperty("precoUnitario").GetRawText());
        Assert.Equal("59.97", item.GetProperty("subtotal").GetRawText());
        Assert.Equal("59.97", consultado.GetProperty("valorTotal").GetRawText());
    }

    // IT-17 (inexistente)
    [Fact]
    public async Task ObterPedido_IdNumericoInexistente_Retorna404PedidoNaoEncontrado()
    {
        var resposta = await _cliente.GetAsync($"/pedidos/{int.MaxValue}", Cancelamento);

        var problema = await RespostaHttp.LerProblemaAsync(resposta, HttpStatusCode.NotFound, "PEDIDO_NAO_ENCONTRADO");
        Assert.Equal("Pedido não encontrado", problema.GetProperty("title").GetString());
        Assert.Equal($"O pedido {int.MaxValue} não existe.", problema.GetProperty("detail").GetString());
    }

    // IT-17 (não numérico): a restrição de rota não casa e o framework responde com o código genérico.
    [Theory]
    [InlineData("abc")]
    [InlineData("99999999999")] // além do int
    public async Task ObterPedido_IdNaoNumerico_Retorna404RecursoNaoEncontrado(string id)
    {
        var resposta = await _cliente.GetAsync($"/pedidos/{id}", Cancelamento);

        await RespostaHttp.LerProblemaAsync(resposta, HttpStatusCode.NotFound, "RECURSO_NAO_ENCONTRADO");
    }
}
