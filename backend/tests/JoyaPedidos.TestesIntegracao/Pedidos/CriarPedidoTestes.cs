using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using JoyaPedidos.TestesIntegracao.Infraestrutura;

namespace JoyaPedidos.TestesIntegracao.Pedidos;

/// <summary>
/// <c>POST /pedidos</c> ponta a ponta contra SQL Server real (spec §3.1, §5.3, §7.2): IT-01 a IT-06, IT-08 e IT-26.
/// </summary>
public sealed class CriarPedidoTestes(FabricaApi fabrica) : IAsyncLifetime
{
    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private readonly DadosDeTeste _dados = new(fabrica.Banco.StringDeConexao);
    private readonly HttpClient _cliente = fabrica.CreateClient();

    public async ValueTask InitializeAsync() => await fabrica.LimparBancoAsync(Cancelamento);

    // Remove também os triggers de falha criados pelos testes, mesmo quando o teste falha.
    public async ValueTask DisposeAsync()
    {
        _cliente.Dispose();
        await fabrica.LimparBancoAsync(CancellationToken.None);
    }

    private Task<HttpResponseMessage> PostarAsync(object corpo) => _cliente.PostAsJsonAsync("/pedidos", corpo, Cancelamento);

    private static object Item(int produtoId, int quantidade) => new { produtoId, quantidade };

    private static string Texto(int valor) => valor.ToString(CultureInfo.InvariantCulture);

    // IT-01
    [Fact]
    public async Task CriarPedido_DoisItensValidos_Retorna201ComLocationECorpoDaSpecEBaixaOsEstoques()
    {
        var bruno = await _dados.InserirClienteAsync("Bruno Lima");
        var caneca = await _dados.InserirProdutoAsync("Caneca de cerâmica", 19.99m, 10);
        var garrafa = await _dados.InserirProdutoAsync("Garrafa térmica", 99.90m, 5);
        fabrica.Relogio.Agora = new DateTimeOffset(2026, 11, 12, 19, 30, 0, 987, TimeSpan.Zero);

        var resposta = await PostarAsync(new { clienteId = bruno, itens = new[] { Item(garrafa, 1), Item(caneca, 3) } });

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var corpo = await RespostaHttp.LerJsonAsync(resposta);
        var pedidoId = corpo.GetProperty("id").GetInt32();
        Assert.Equal($"/pedidos/{pedidoId}", resposta.Headers.Location?.OriginalString);
        Assert.Equal(bruno, corpo.GetProperty("cliente").GetProperty("id").GetInt32());
        Assert.Equal("Bruno Lima", corpo.GetProperty("cliente").GetProperty("nome").GetString());
        Assert.Equal("2026-11-12T19:30:00Z", corpo.GetProperty("dataPedido").GetString());
        var itens = corpo.GetProperty("itens").EnumerateArray().ToList();
        Assert.Equal([caneca, garrafa], itens.Select(item => item.GetProperty("produtoId").GetInt32()));
        Assert.Equal(["Caneca de cerâmica", "Garrafa térmica"], itens.Select(item => item.GetProperty("nomeProduto").GetString()));
        Assert.Equal([3, 1], itens.Select(item => item.GetProperty("quantidade").GetInt32()));
        Assert.Equal(["19.99", "99.90"], itens.Select(item => item.GetProperty("precoUnitario").GetRawText()));
        Assert.Equal(["59.97", "99.90"], itens.Select(item => item.GetProperty("subtotal").GetRawText()));
        Assert.Equal("159.87", corpo.GetProperty("valorTotal").GetRawText());

        Assert.Equal(7, await _dados.EstoqueAsync(caneca));
        Assert.Equal(4, await _dados.EstoqueAsync(garrafa));
        Assert.Equal(1, await _dados.ContarPedidosAsync());
        Assert.Equal(2, await _dados.ContarItensAsync());
        Assert.Equal($"{caneca}:3:19.99;{garrafa}:1:99.90", await _dados.ItensGravadosAsync(pedidoId));
        Assert.Equal("2026-11-12 19:30:00", await _dados.DataPedidoGravadaAsync(pedidoId)); // UTC, truncada a segundos
    }

    // IT-02
    [Fact]
    public async Task CriarPedido_ClienteInexistente_Retorna422SemBloquearProdutosNemGravarNada()
    {
        var caneca = await _dados.InserirProdutoAsync("Caneca de cerâmica", 19.99m, 10);
        // RN-01: o cliente é verificado antes de qualquer lock de produto. Se a API tentasse bloquear a caneca, ficaria
        // esperando esta sessão até o tempo limite do comando e a resposta não seria 422.
        await using var sessao = await SessaoDeBloqueio.AbrirAsync(fabrica.Banco.StringDeConexao);
        await sessao.BloquearProdutoAsync(caneca, Cancelamento);

        var resposta = await PostarAsync(new { clienteId = int.MaxValue, itens = new[] { Item(caneca, 1) } });
        await sessao.DesfazerAsync();

        var problema = await RespostaHttp.LerProblemaAsync(resposta, HttpStatusCode.UnprocessableEntity, "CLIENTE_NAO_ENCONTRADO");
        Assert.Equal("Cliente não encontrado", problema.GetProperty("title").GetString());
        Assert.Equal($"O cliente {Texto(int.MaxValue)} não existe.", problema.GetProperty("detail").GetString());
        Assert.Equal(10, await _dados.EstoqueAsync(caneca));
        Assert.Equal(0, await _dados.ContarPedidosAsync());
        Assert.Equal(0, await _dados.ContarItensAsync());
    }

    // IT-03
    [Fact]
    public async Task CriarPedido_DoisProdutosInexistentes_Retorna422ComTodosOsAusentesSemGravarNada()
    {
        var bruno = await _dados.InserirClienteAsync("Bruno Lima");
        var caneca = await _dados.InserirProdutoAsync("Caneca de cerâmica", 19.99m, 10);
        int[] inexistentes = [int.MaxValue, int.MaxValue - 1];

        var resposta = await PostarAsync(new
        {
            clienteId = bruno,
            itens = new[] { Item(inexistentes[0], 1), Item(caneca, 1), Item(inexistentes[1], 2) },
        });

        var problema = await RespostaHttp.LerProblemaAsync(resposta, HttpStatusCode.UnprocessableEntity, "PRODUTO_NAO_ENCONTRADO");
        Assert.Equal("2 produto(s) informado(s) não existem.", problema.GetProperty("detail").GetString());
        Assert.Equal(
            [int.MaxValue - 1, int.MaxValue],
            problema.GetProperty("produtosNaoEncontrados").EnumerateArray().Select(id => id.GetInt32()));
        Assert.Equal(10, await _dados.EstoqueAsync(caneca));
        Assert.Equal(0, await _dados.ContarPedidosAsync());
        Assert.True(await SessaoDeBloqueio.ProdutoLivreAsync(fabrica.Banco.StringDeConexao, caneca));
    }

    // IT-04
    [Fact]
    public async Task CriarPedido_QuantidadeMaiorQueOEstoque_Retorna409ComSolicitadoEDisponivelSemGravarNada()
    {
        var bruno = await _dados.InserirClienteAsync("Bruno Lima");
        var garrafa = await _dados.InserirProdutoAsync("Garrafa térmica", 99.90m, 2);

        var resposta = await PostarAsync(new { clienteId = bruno, itens = new[] { Item(garrafa, 4) } });

        var problema = await RespostaHttp.LerProblemaAsync(resposta, HttpStatusCode.Conflict, "ESTOQUE_INSUFICIENTE");
        Assert.Equal("Não há estoque suficiente para 1 produto(s).", problema.GetProperty("detail").GetString());
        var item = Assert.Single(problema.GetProperty("itensSemEstoque").EnumerateArray().ToList());
        Assert.Equal(garrafa, item.GetProperty("produtoId").GetInt32());
        Assert.Equal("Garrafa térmica", item.GetProperty("nomeProduto").GetString());
        Assert.Equal(4, item.GetProperty("quantidadeSolicitada").GetInt32());
        Assert.Equal(2, item.GetProperty("estoqueDisponivel").GetInt32());
        Assert.Equal(2, await _dados.EstoqueAsync(garrafa));
        Assert.Equal(0, await _dados.ContarPedidosAsync());
        Assert.True(await SessaoDeBloqueio.ProdutoLivreAsync(fabrica.Banco.StringDeConexao, garrafa)); // lock U liberado no rollback
    }

    // IT-05
    [Fact]
    public async Task CriarPedido_UmItemComEstoqueEOutroSem_Retorna409AntesDeQualquerEscrita()
    {
        var bruno = await _dados.InserirClienteAsync("Bruno Lima");
        var caneca = await _dados.InserirProdutoAsync("Caneca de cerâmica", 19.99m, 10);
        var esgotado = await _dados.InserirProdutoAsync("Produto esgotado", 5.00m, 0);
        // Armadilha: qualquer UPDATE em Produtos falharia (e a resposta seria 500). O 409 prova que a rejeição veio do
        // domínio antes da primeira baixa, e não de um rollback depois dela.
        await _dados.CriarTriggerDeTesteAsync("TR_Teste_NenhumaBaixa", """
            CREATE TRIGGER dbo.TR_Teste_NenhumaBaixa ON dbo.Produtos AFTER UPDATE AS
            BEGIN
                SET NOCOUNT ON;
                THROW 50001, N'Nenhuma baixa deveria ter sido executada.', 1;
            END
            """);

        var resposta = await PostarAsync(new { clienteId = bruno, itens = new[] { Item(caneca, 3), Item(esgotado, 1) } });

        var problema = await RespostaHttp.LerProblemaAsync(resposta, HttpStatusCode.Conflict, "ESTOQUE_INSUFICIENTE");
        var item = Assert.Single(problema.GetProperty("itensSemEstoque").EnumerateArray().ToList());
        Assert.Equal(esgotado, item.GetProperty("produtoId").GetInt32());
        Assert.Equal(10, await _dados.EstoqueAsync(caneca));
        Assert.Equal(0, await _dados.ContarPedidosAsync());
        Assert.True(await SessaoDeBloqueio.ProdutoLivreAsync(fabrica.Banco.StringDeConexao, caneca));
        Assert.True(await SessaoDeBloqueio.ProdutoLivreAsync(fabrica.Banco.StringDeConexao, esgotado));
    }

    // IT-06
    [Fact]
    public async Task CriarPedido_CorpoComPrecoUnitario_IgnoraOPrecoEnviadoEUsaOPrecoDoProduto()
    {
        var bruno = await _dados.InserirClienteAsync("Bruno Lima");
        var caneca = await _dados.InserirProdutoAsync("Caneca de cerâmica", 19.99m, 10);

        var resposta = await PostarAsync(new
        {
            clienteId = bruno,
            itens = new[] { new { produtoId = caneca, quantidade = 2, precoUnitario = 0.01m } },
            valorTotal = 0.02m,
            dataPedido = "2000-01-01T00:00:00Z",
        });

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var corpo = await RespostaHttp.LerJsonAsync(resposta);
        var item = Assert.Single(corpo.GetProperty("itens").EnumerateArray().ToList());
        Assert.Equal("19.99", item.GetProperty("precoUnitario").GetRawText());
        Assert.Equal("39.98", corpo.GetProperty("valorTotal").GetRawText());
        Assert.Equal("2026-11-12T15:00:00Z", corpo.GetProperty("dataPedido").GetString()); // relógio do servidor (RN-09)
        Assert.Equal($"{caneca}:2:19.99", await _dados.ItensGravadosAsync(corpo.GetProperty("id").GetInt32()));
    }

    // IT-08 e a decisão da E4 sobre item nulo (itens[i]).
    [Theory]
    [MemberData(nameof(RequisicoesRejeitadas))]
    public async Task CriarPedido_RequisicaoMalformadaOuInvalida_RetornaProblemDetailsSemGravarNada(
        string caso, string corpoModelo, string tipoDeConteudo, HttpStatusCode status, string codigo, string? chaveDeErro)
    {
        var bruno = await _dados.InserirClienteAsync("Bruno Lima");
        var caneca = await _dados.InserirProdutoAsync("Caneca de cerâmica", 19.99m, 10);
        var corpo = corpoModelo.Replace("{C}", Texto(bruno), StringComparison.Ordinal).Replace("{P}", Texto(caneca), StringComparison.Ordinal);

        var resposta = await _cliente.PostAsync("/pedidos", new StringContent(corpo, Encoding.UTF8, tipoDeConteudo), Cancelamento);

        var problema = await RespostaHttp.LerProblemaAsync(resposta, status, codigo);
        if (chaveDeErro is null)
        {
            Assert.False(problema.TryGetProperty("errors", out _), caso);
        }
        else
        {
            Assert.True(problema.GetProperty("errors").TryGetProperty(chaveDeErro, out _), $"{caso}: errors = {problema.GetProperty("errors")}");
        }
        Assert.Equal(10, await _dados.EstoqueAsync(caneca));
        Assert.Equal(0, await _dados.ContarPedidosAsync());
    }

    public static TheoryData<string, string, string, HttpStatusCode, string, string?> RequisicoesRejeitadas() => new()
    {
        { "sem itens", """{"clienteId":{C},"itens":[]}""", "application/json", HttpStatusCode.BadRequest, "VALIDACAO", "itens" },
        {
            "produto repetido",
            """{"clienteId":{C},"itens":[{"produtoId":{P},"quantidade":1},{"produtoId":{P},"quantidade":2}]}""",
            "application/json", HttpStatusCode.BadRequest, "VALIDACAO", "itens[1].produtoId"
        },
        {
            "quantidade zero", """{"clienteId":{C},"itens":[{"produtoId":{P},"quantidade":0}]}""",
            "application/json", HttpStatusCode.BadRequest, "VALIDACAO", "itens[0].quantidade"
        },
        { "item nulo", """{"clienteId":{C},"itens":[null]}""", "application/json", HttpStatusCode.BadRequest, "VALIDACAO", "itens[0]" },
        { "corpo vazio", "", "application/json", HttpStatusCode.BadRequest, "VALIDACAO", "clienteId" },
        { "JSON malformado", """{"clienteId":{C},"itens":[""", "application/json", HttpStatusCode.BadRequest, "REQUISICAO_INVALIDA", null },
        {
            "quantidade fracionária", """{"clienteId":{C},"itens":[{"produtoId":{P},"quantidade":1.5}]}""",
            "application/json", HttpStatusCode.BadRequest, "REQUISICAO_INVALIDA", null
        },
        {
            "Content-Type text/plain", """{"clienteId":{C},"itens":[{"produtoId":{P},"quantidade":1}]}""",
            "text/plain", HttpStatusCode.UnsupportedMediaType, "REQUISICAO_INVALIDA", null
        },
    };

    // IT-26 (a)
    [Fact]
    public async Task CriarPedido_SemItensEClienteInexistente_RetornaValidacao()
    {
        var resposta = await PostarAsync(new { clienteId = int.MaxValue, itens = Array.Empty<object>() });

        var problema = await RespostaHttp.LerProblemaAsync(resposta, HttpStatusCode.BadRequest, "VALIDACAO");
        Assert.Equal(["itens"], problema.GetProperty("errors").EnumerateObject().Select(campo => campo.Name));
    }

    // IT-26 (b)
    [Fact]
    public async Task CriarPedido_ClienteEProdutoInexistentes_RetornaSoClienteNaoEncontrado()
    {
        var resposta = await PostarAsync(new { clienteId = int.MaxValue, itens = new[] { Item(int.MaxValue, 1) } });

        var problema = await RespostaHttp.LerProblemaAsync(resposta, HttpStatusCode.UnprocessableEntity, "CLIENTE_NAO_ENCONTRADO");
        Assert.False(problema.TryGetProperty("produtosNaoEncontrados", out _));
    }

    // IT-26 (c)
    [Fact]
    public async Task CriarPedido_ProdutoInexistenteEOutroSemEstoque_RetornaSoProdutoNaoEncontrado()
    {
        var bruno = await _dados.InserirClienteAsync("Bruno Lima");
        var esgotado = await _dados.InserirProdutoAsync("Produto esgotado", 5.00m, 0);

        var resposta = await PostarAsync(new { clienteId = bruno, itens = new[] { Item(esgotado, 1), Item(int.MaxValue, 1) } });

        var problema = await RespostaHttp.LerProblemaAsync(resposta, HttpStatusCode.UnprocessableEntity, "PRODUTO_NAO_ENCONTRADO");
        Assert.Equal([int.MaxValue], problema.GetProperty("produtosNaoEncontrados").EnumerateArray().Select(id => id.GetInt32()));
        Assert.False(problema.TryGetProperty("itensSemEstoque", out _));
        Assert.Equal(0, await _dados.ContarPedidosAsync());
    }
}
