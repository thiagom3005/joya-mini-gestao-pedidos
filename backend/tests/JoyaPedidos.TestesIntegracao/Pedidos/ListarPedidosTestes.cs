using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JoyaPedidos.TestesIntegracao.Infraestrutura;

namespace JoyaPedidos.TestesIntegracao.Pedidos;

/// <summary>
/// <c>GET /pedidos</c> (spec §5.4, §8): IT-07 (parte da listagem), IT-11 a IT-16. Os pedidos são criados pela API com o
/// relógio controlado nos instantes de cada cenário; as asserções são feitas pela listagem.
/// </summary>
public sealed class ListarPedidosTestes(FabricaApi fabrica) : IAsyncLifetime
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

    private static DateTimeOffset Utc(string instante) => DateTimeOffset.Parse(instante, System.Globalization.CultureInfo.InvariantCulture);

    private async Task<int> CriarPedidoEmAsync(string instanteUtc, int clienteId, params (int ProdutoId, int Quantidade)[] itens)
    {
        fabrica.Relogio.Agora = Utc(instanteUtc);
        var resposta = await _cliente.PostAsJsonAsync(
            "/pedidos",
            new { clienteId, itens = itens.Select(item => new { produtoId = item.ProdutoId, quantidade = item.Quantidade }) },
            Cancelamento);
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        return (await RespostaHttp.LerJsonAsync(resposta)).GetProperty("id").GetInt32();
    }

    private async Task<JsonElement> ListarAsync(string consulta = "")
    {
        var resposta = await _cliente.GetAsync($"/pedidos{consulta}", Cancelamento);
        var corpo = await RespostaHttp.LerJsonAsync(resposta);
        Assert.True(resposta.StatusCode == HttpStatusCode.OK, $"Esperado 200, obtido {(int)resposta.StatusCode}: {corpo}");
        Assert.Equal("application/json", resposta.Content.Headers.ContentType?.MediaType);
        return corpo;
    }

    private static int[] Ids(JsonElement lista) =>
        lista.GetProperty("itens").EnumerateArray().Select(item => item.GetProperty("id").GetInt32()).ToArray();

    private static (int Pagina, int TamanhoPagina, int TotalItens, int TotalPaginas) Envelope(JsonElement lista) => (
        lista.GetProperty("pagina").GetInt32(),
        lista.GetProperty("tamanhoPagina").GetInt32(),
        lista.GetProperty("totalItens").GetInt32(),
        lista.GetProperty("totalPaginas").GetInt32());

    private static string[] Propriedades(JsonElement elemento) =>
        elemento.EnumerateObject().Select(propriedade => propriedade.Name).ToArray();

    // IT-11
    [Fact]
    public async Task ListarPedidos_SemFiltros_OrdenaPorDataEIdDecrescentesComEnvelopeETotaisCorretos()
    {
        var bruno = await _dados.InserirClienteAsync("Bruno Lima");
        var caneca = await _dados.InserirProdutoAsync("Caneca de ceramica", 19.99m, 100);
        var garrafa = await _dados.InserirProdutoAsync("Garrafa termica", 99.90m, 100);
        var antigo = await CriarPedidoEmAsync("2026-03-01T12:00:00Z", bruno, (caneca, 3), (garrafa, 1));
        var mesmoSegundo1 = await CriarPedidoEmAsync("2026-05-01T12:00:00Z", bruno, (caneca, 1));
        var mesmoSegundo2 = await CriarPedidoEmAsync("2026-05-01T12:00:00Z", bruno, (garrafa, 2));

        var lista = await ListarAsync();

        Assert.Equal(["itens", "pagina", "tamanhoPagina", "totalItens", "totalPaginas"], Propriedades(lista));
        Assert.Equal((1, 10, 3, 1), Envelope(lista));
        // Mesmo segundo: o Id desempata em ordem decrescente (§8.3).
        Assert.Equal([mesmoSegundo2, mesmoSegundo1, antigo], Ids(lista));

        var itens = lista.GetProperty("itens").EnumerateArray().ToList();
        Assert.All(itens, item => Assert.Equal(["id", "cliente", "dataPedido", "quantidadeItens", "valorTotal"], Propriedades(item)));
        Assert.Equal(["id", "nome"], Propriedades(itens[0].GetProperty("cliente")));
        Assert.Equal(bruno, itens[0].GetProperty("cliente").GetProperty("id").GetInt32());
        Assert.Equal("Bruno Lima", itens[0].GetProperty("cliente").GetProperty("nome").GetString());
        Assert.Equal("2026-05-01T12:00:00Z", itens[0].GetProperty("dataPedido").GetString());
        Assert.Equal("199.80", itens[0].GetProperty("valorTotal").GetRawText());
        // quantidadeItens conta linhas de item (2 produtos), não unidades (3 + 1).
        Assert.Equal(2, itens[2].GetProperty("quantidadeItens").GetInt32());
        Assert.Equal("159.87", itens[2].GetProperty("valorTotal").GetRawText());
        Assert.Equal(1, itens[1].GetProperty("quantidadeItens").GetInt32());
        Assert.Equal("19.99", itens[1].GetProperty("valorTotal").GetRawText());
    }

    // IT-11 (sem pedidos)
    [Fact]
    public async Task ListarPedidos_SemPedidos_RetornaListaVaziaComTotaisZero()
    {
        var lista = await ListarAsync();

        Assert.Empty(Ids(lista));
        Assert.Equal((1, 10, 0, 0), Envelope(lista));
    }

    // IT-12
    [Fact]
    public async Task ListarPedidos_FiltroPorCliente_RetornaSoOsPedidosDoCliente()
    {
        var ana = await _dados.InserirClienteAsync("Ana Souza");
        var bruno = await _dados.InserirClienteAsync("Bruno Lima");
        var caneca = await _dados.InserirProdutoAsync("Caneca de ceramica", 19.99m, 100);
        var daAna1 = await CriarPedidoEmAsync("2026-03-01T12:00:00Z", ana, (caneca, 1));
        await CriarPedidoEmAsync("2026-03-02T12:00:00Z", bruno, (caneca, 1));
        var daAna2 = await CriarPedidoEmAsync("2026-03-03T12:00:00Z", ana, (caneca, 2));

        var lista = await ListarAsync($"?clienteId={ana}");

        Assert.Equal([daAna2, daAna1], Ids(lista));
        Assert.Equal((1, 10, 2, 1), Envelope(lista));
        Assert.All(lista.GetProperty("itens").EnumerateArray(),
            item => Assert.Equal(ana, item.GetProperty("cliente").GetProperty("id").GetInt32()));
    }

    // IT-12 (cliente inexistente): é filtro, não referência; 200 com lista vazia.
    [Fact]
    public async Task ListarPedidos_ClienteInexistente_Retorna200ComListaVazia()
    {
        var bruno = await _dados.InserirClienteAsync("Bruno Lima");
        var caneca = await _dados.InserirProdutoAsync("Caneca de ceramica", 19.99m, 100);
        await CriarPedidoEmAsync("2026-03-01T12:00:00Z", bruno, (caneca, 1));

        var lista = await ListarAsync($"?clienteId={int.MaxValue}");

        Assert.Empty(Ids(lista));
        Assert.Equal((1, 10, 0, 0), Envelope(lista));
    }

    // Os quatro instantes de fronteira da §8.2 (hora local de São Paulo entre parênteses).
    private async Task<(int ForaAntes, int DentroInicio, int DentroFim, int ForaDepois)> CriarPedidosNasFronteirasDe2026Async(
        int clienteId, int produtoId) => (
        await CriarPedidoEmAsync("2026-01-01T02:59:59Z", clienteId, (produtoId, 1)), // 31/12/2025 23:59:59
        await CriarPedidoEmAsync("2026-01-01T03:00:00Z", clienteId, (produtoId, 1)), // 01/01/2026 00:00:00
        await CriarPedidoEmAsync("2027-01-01T02:59:59Z", clienteId, (produtoId, 1)), // 31/12/2026 23:59:59
        await CriarPedidoEmAsync("2027-01-01T03:00:00Z", clienteId, (produtoId, 1))); // 01/01/2027 00:00:00

    // IT-13
    [Fact]
    public async Task ListarPedidos_Periodo2026EmSaoPaulo_RetornaExatamenteOsDoisPedidosDentroDasFronteiras()
    {
        var bruno = await _dados.InserirClienteAsync("Bruno Lima");
        var caneca = await _dados.InserirProdutoAsync("Caneca de ceramica", 19.99m, 100);
        var pedidos = await CriarPedidosNasFronteirasDe2026Async(bruno, caneca);

        var lista = await ListarAsync("?dataInicio=2026-01-01&dataFim=2026-12-31");

        Assert.Equal([pedidos.DentroFim, pedidos.DentroInicio], Ids(lista));
        Assert.Equal(2, lista.GetProperty("totalItens").GetInt32());
    }

    // IT-13 (limites independentes, §8.2): só dataInicio aplica o limite inferior; só dataFim, o superior.
    [Fact]
    public async Task ListarPedidos_SoDataInicioOuSoDataFim_AplicaApenasOLimiteInformado()
    {
        var bruno = await _dados.InserirClienteAsync("Bruno Lima");
        var caneca = await _dados.InserirProdutoAsync("Caneca de ceramica", 19.99m, 100);
        var pedidos = await CriarPedidosNasFronteirasDe2026Async(bruno, caneca);

        var aPartirDe2026 = await ListarAsync("?dataInicio=2026-01-01");
        var ate2026 = await ListarAsync("?dataFim=2026-12-31");

        Assert.Equal([pedidos.ForaDepois, pedidos.DentroFim, pedidos.DentroInicio], Ids(aPartirDe2026));
        Assert.Equal([pedidos.DentroFim, pedidos.DentroInicio, pedidos.ForaAntes], Ids(ate2026));
        // A contagem usa o mesmo filtro unilateral da página.
        Assert.Equal((1, 10, 3, 1), Envelope(aPartirDe2026));
        Assert.Equal((1, 10, 3, 1), Envelope(ate2026));
    }

    // Sem UTC-3 fixo: em 01/12/2018 São Paulo estava em horário de verão (UTC-2), e o dia começou às 02:00 UTC.
    [Fact]
    public async Task ListarPedidos_DiaEmHorarioDeVeraoHistorico_UsaODeslocamentoDaquelaData()
    {
        var bruno = await _dados.InserirClienteAsync("Bruno Lima");
        var caneca = await _dados.InserirProdutoAsync("Caneca de ceramica", 19.99m, 100);
        await CriarPedidoEmAsync("2018-12-01T01:59:59Z", bruno, (caneca, 1)); // 30/11/2018 23:59:59 (UTC-2)
        var inicioDoDia = await CriarPedidoEmAsync("2018-12-01T02:00:00Z", bruno, (caneca, 1)); // 01/12/2018 00:00:00
        var fimDoDia = await CriarPedidoEmAsync("2018-12-02T01:59:59Z", bruno, (caneca, 1)); // 01/12/2018 23:59:59
        await CriarPedidoEmAsync("2018-12-02T02:00:00Z", bruno, (caneca, 1)); // 02/12/2018 00:00:00

        var lista = await ListarAsync("?dataInicio=2018-12-01&dataFim=2018-12-01");

        Assert.Equal([fimDoDia, inicioDoDia], Ids(lista));
    }

    // IT-14
    [Fact]
    public async Task ListarPedidos_ClienteEPeriodoCombinados_RetornaAIntersecao()
    {
        var ana = await _dados.InserirClienteAsync("Ana Souza");
        var bruno = await _dados.InserirClienteAsync("Bruno Lima");
        var caneca = await _dados.InserirProdutoAsync("Caneca de ceramica", 19.99m, 100);
        await CriarPedidoEmAsync("2026-01-01T02:59:59Z", ana, (caneca, 1)); // da Ana, mas em 2025 local
        var anaEm2026 = await CriarPedidoEmAsync("2026-06-10T13:00:00Z", ana, (caneca, 1));
        await CriarPedidoEmAsync("2026-06-11T13:00:00Z", bruno, (caneca, 1)); // em 2026, mas do Bruno

        var lista = await ListarAsync($"?clienteId={ana}&dataInicio=2026-01-01&dataFim=2026-12-31");

        Assert.Equal([anaEm2026], Ids(lista));
        Assert.Equal((1, 10, 1, 1), Envelope(lista));
    }

    // IT-15
    [Fact]
    public async Task ListarPedidos_23PedidosComTamanho10_Paginas10E10E3EQuartaVazia()
    {
        var bruno = await _dados.InserirClienteAsync("Bruno Lima");
        var caneca = await _dados.InserirProdutoAsync("Caneca de ceramica", 19.99m, 100);
        var criados = new List<int>();
        for (var minuto = 0; minuto < 23; minuto++)
        {
            criados.Add(await CriarPedidoEmAsync($"2026-04-01T12:{minuto:00}:00Z", bruno, (caneca, 1)));
        }

        var paginas = new List<JsonElement>();
        for (var pagina = 1; pagina <= 4; pagina++)
        {
            paginas.Add(await ListarAsync($"?pagina={pagina}&tamanhoPagina=10"));
        }

        Assert.Equal([10, 10, 3, 0], paginas.Select(pagina => Ids(pagina).Length));
        Assert.All(paginas.Select((pagina, indice) => (pagina, indice)),
            par => Assert.Equal((par.indice + 1, 10, 23, 3), Envelope(par.pagina)));
        // As três páginas juntas, sem repetição nem lacuna, na ordem decrescente de criação.
        Assert.Equal(Enumerable.Reverse(criados), paginas.SelectMany(Ids));
    }

    // IT-15 (ordem total entre páginas, §8.3): pedidos no mesmo segundo divididos entre duas páginas. O Id desempata
    // a escolha das linhas de cada página, não só a ordem de exibição: nada se repete nem fica de fora.
    [Fact]
    public async Task ListarPedidos_PedidosNoMesmoSegundoEntrePaginas_DesempataPeloIdSemRepetirNemPular()
    {
        var bruno = await _dados.InserirClienteAsync("Bruno Lima");
        var caneca = await _dados.InserirProdutoAsync("Caneca de ceramica", 19.99m, 100);
        var primeiro = await CriarPedidoEmAsync("2026-04-01T12:00:00Z", bruno, (caneca, 1));
        var segundo = await CriarPedidoEmAsync("2026-04-01T12:00:00Z", bruno, (caneca, 1));
        var terceiro = await CriarPedidoEmAsync("2026-04-01T12:00:00Z", bruno, (caneca, 1));

        var pagina1 = await ListarAsync("?pagina=1&tamanhoPagina=2");
        var pagina2 = await ListarAsync("?pagina=2&tamanhoPagina=2");

        Assert.Equal([terceiro, segundo], Ids(pagina1));
        Assert.Equal([primeiro], Ids(pagina2));
    }

    // IT-15 (deslocamento em 64 bits, §8.3): a maior página possível devolve lista vazia, sem erro.
    [Fact]
    public async Task ListarPedidos_PaginaMaximaDoInteiro_Retorna200VazioComTotaisCorretos()
    {
        var bruno = await _dados.InserirClienteAsync("Bruno Lima");
        var caneca = await _dados.InserirProdutoAsync("Caneca de ceramica", 19.99m, 100);
        await CriarPedidoEmAsync("2026-04-01T12:00:00Z", bruno, (caneca, 1));

        var lista = await ListarAsync($"?pagina={int.MaxValue}&tamanhoPagina=100");

        Assert.Empty(Ids(lista));
        Assert.Equal((int.MaxValue, 100, 1, 1), Envelope(lista));
    }

    // IT-16 (VALIDACAO): faixas, ordem e formato de data.
    [Theory]
    [InlineData("?pagina=0", "pagina")]
    [InlineData("?tamanhoPagina=0", "tamanhoPagina")]
    [InlineData("?tamanhoPagina=101", "tamanhoPagina")]
    [InlineData("?clienteId=0", "clienteId")]
    [InlineData("?clienteId=-1", "clienteId")]
    [InlineData("?dataInicio=2026-02-01&dataFim=2026-01-31", "dataInicio")]
    [InlineData("?dataInicio=2026-13-01", "dataInicio")]
    [InlineData("?dataInicio=01/02/2026", "dataInicio")]
    [InlineData("?dataFim=2026-02-30", "dataFim")]
    [InlineData("?dataFim=", "dataFim")]
    public async Task ListarPedidos_ParametroInvalido_Retorna400ValidacaoNaChaveDoCampo(string consulta, string chave)
    {
        var resposta = await _cliente.GetAsync($"/pedidos{consulta}", Cancelamento);

        var problema = await RespostaHttp.LerProblemaAsync(resposta, HttpStatusCode.BadRequest, "VALIDACAO");
        Assert.Equal([chave], problema.GetProperty("errors").EnumerateObject().Select(erro => erro.Name));
    }

    // IT-16 (REQUISICAO_INVALIDA): inteiro não numérico ou fora do int falha no binding do framework.
    [Theory]
    [InlineData("?clienteId=abc")]
    [InlineData("?clienteId=99999999999")]
    [InlineData("?pagina=1.5")]
    [InlineData("?tamanhoPagina=dez")]
    public async Task ListarPedidos_InteiroNaoNumerico_Retorna400RequisicaoInvalida(string consulta)
    {
        var resposta = await _cliente.GetAsync($"/pedidos{consulta}", Cancelamento);

        var problema = await RespostaHttp.LerProblemaAsync(resposta, HttpStatusCode.BadRequest, "REQUISICAO_INVALIDA");
        Assert.False(problema.TryGetProperty("errors", out _));
    }

    // IT-07 (parte da listagem): mudar o preço do produto depois não altera o total do pedido listado.
    [Fact]
    public async Task ListarPedidos_PrecoDoProdutoAlteradoDepois_MantemOTotalOriginal()
    {
        var bruno = await _dados.InserirClienteAsync("Bruno Lima");
        var caneca = await _dados.InserirProdutoAsync("Caneca de ceramica", 19.99m, 10);
        var pedido = await CriarPedidoEmAsync("2026-04-01T12:00:00Z", bruno, (caneca, 3));
        await _dados.ExecutarAsync("UPDATE dbo.Produtos SET Preco = 25.00 WHERE Id = @Id;", ("@Id", caneca));

        var lista = await ListarAsync();

        var item = Assert.Single(lista.GetProperty("itens").EnumerateArray().ToList());
        Assert.Equal(pedido, item.GetProperty("id").GetInt32());
        Assert.Equal("59.97", item.GetProperty("valorTotal").GetRawText());
    }
}
