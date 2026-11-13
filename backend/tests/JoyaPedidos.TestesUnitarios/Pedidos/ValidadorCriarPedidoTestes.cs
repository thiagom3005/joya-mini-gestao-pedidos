using JoyaPedidos.Api.Modulos.Pedidos;
using JoyaPedidos.Api.Modulos.Pedidos.Contratos;

namespace JoyaPedidos.TestesUnitarios.Pedidos;

/// <summary>Validação de formato de POST /pedidos (spec §5.3; UT-02, UT-03, UT-04 e UT-13).</summary>
public sealed class ValidadorCriarPedidoTestes
{
    private static ItemPedidoRequisicao Item(int? produtoId = 1, int? quantidade = 1) => new(produtoId, quantidade);

    private static CriarPedidoRequisicao Requisicao(int? clienteId = 1, params ItemPedidoRequisicao?[]? itens) =>
        new(clienteId, itens);

    // UT-02
    [Fact]
    public void Validar_ListaDeItensVazia_RetornaErroNaChaveItens()
    {
        var erros = ValidadorCriarPedido.Validar(Requisicao(1, []));

        var mensagens = Assert.Single(erros, par => par.Key == "itens").Value;
        Assert.Equal(["O pedido deve conter ao menos um item."], mensagens);
        Assert.Single(erros);
    }

    // UT-03
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Validar_QuantidadeZeroOuNegativa_RetornaErroNaQuantidadeDaLinha(int quantidade)
    {
        var erros = ValidadorCriarPedido.Validar(Requisicao(1, Item(produtoId: 2, quantidade: quantidade)));

        Assert.Equal(["itens[0].quantidade"], erros.Keys);
    }

    // UT-04
    [Fact]
    public void Validar_MesmoProdutoNasLinhas0E1_RetornaErroSoNaSegundaOcorrencia()
    {
        var erros = ValidadorCriarPedido.Validar(Requisicao(1, Item(produtoId: 7), Item(produtoId: 7)));

        Assert.Equal(["itens[1].produtoId"], erros.Keys);
        Assert.Contains("itens[0]", erros["itens[1].produtoId"][0]);
    }

    [Fact]
    public void Validar_ProdutoRepetidoTresVezes_RetornaErroNaSegundaENaTerceira()
    {
        var erros = ValidadorCriarPedido.Validar(Requisicao(1, Item(7), Item(8), Item(7), Item(7)));

        Assert.Equal(["itens[2].produtoId", "itens[3].produtoId"], erros.Keys.Order());
    }

    // UT-13: requisição válida no limite superior
    [Fact]
    public void Validar_CinquentaItensComQuantidadeDezMil_NaoRetornaErros()
    {
        var itens = Enumerable.Range(1, ValidadorCriarPedido.MaximoDeItens).Select(id => Item(id, 10_000)).ToArray();

        var erros = ValidadorCriarPedido.Validar(Requisicao(1, itens));

        Assert.Empty(erros);
    }

    // UT-13: demais casos, um por vez, com a chave exata da spec §5.3
    [Theory]
    [MemberData(nameof(RequisicoesInvalidas))]
    public void Validar_CampoInvalido_RetornaErroSoNaChaveEsperada(string caso, CriarPedidoRequisicao requisicao, string chaveEsperada)
    {
        var erros = ValidadorCriarPedido.Validar(requisicao);

        Assert.True(erros.Keys.SequenceEqual([chaveEsperada]), $"{caso}: chaves obtidas = [{string.Join(", ", erros.Keys)}]");
        Assert.NotEmpty(erros[chaveEsperada]);
    }

    public static TheoryData<string, CriarPedidoRequisicao, string> RequisicoesInvalidas() => new()
    {
        { "clienteId ausente", Requisicao(null, Item()), "clienteId" },
        { "clienteId zero", Requisicao(0, Item()), "clienteId" },
        { "clienteId negativo", Requisicao(-1, Item()), "clienteId" },
        { "itens nulo", new CriarPedidoRequisicao(1, null), "itens" },
        { "51 itens", Requisicao(1, Enumerable.Range(1, 51).Select(id => Item(id)).ToArray()), "itens" },
        { "produtoId ausente", Requisicao(1, Item(produtoId: null)), "itens[0].produtoId" },
        { "produtoId zero", Requisicao(1, Item(produtoId: 0)), "itens[0].produtoId" },
        { "produtoId negativo", Requisicao(1, Item(produtoId: -3)), "itens[0].produtoId" },
        { "quantidade ausente", Requisicao(1, Item(quantidade: null)), "itens[0].quantidade" },
        { "quantidade 10.001", Requisicao(1, Item(quantidade: 10_001)), "itens[0].quantidade" },
        { "item nulo na lista", Requisicao(1, Item(1), null), "itens[1]" },
        { "único item nulo", new CriarPedidoRequisicao(1, [null]), "itens[0]" }, // decisão do candidato na E4
    };

    [Fact]
    public void Validar_MilLinhasInvalidasERepetidas_RetornaSoOErroDoLimiteSemExaminarAsLinhas()
    {
        var itens = Enumerable.Repeat(Item(produtoId: 1, quantidade: 0), 1_000).ToArray();

        var erros = ValidadorCriarPedido.Validar(Requisicao(1, itens));

        Assert.Equal(["itens"], erros.Keys);
        Assert.Equal(["O pedido pode ter no máximo 50 itens."], erros["itens"]);
    }

    [Fact]
    public void Validar_QuantidadeAcimaDoMaximo_MensagemCitaOLimiteVigente()
    {
        var erros = ValidadorCriarPedido.Validar(Requisicao(1, Item(quantidade: ValidadorCriarPedido.QuantidadeMaxima + 1)));

        // A mensagem é fixa (independe da cultura da máquina); este teste a amarra ao valor da constante.
        Assert.Equal(10_000, ValidadorCriarPedido.QuantidadeMaxima);
        Assert.Equal(["A quantidade deve estar entre 1 e 10.000."], erros["itens[0].quantidade"]);
    }

    [Fact]
    public void Validar_VariosErros_RetornaTodosDeUmaVez()
    {
        var erros = ValidadorCriarPedido.Validar(Requisicao(null, Item(produtoId: 0, quantidade: 0), Item(2, 1)));

        Assert.Equal(["clienteId", "itens[0].produtoId", "itens[0].quantidade"], erros.Keys.Order());
    }

    [Fact]
    public void Validar_CorpoAusente_RetornaErrosDeClienteEItens()
    {
        var erros = ValidadorCriarPedido.Validar(null);

        Assert.Equal(["clienteId", "itens"], erros.Keys.Order());
    }

    [Fact]
    public void Validar_RequisicaoValida_NaoRetornaErros()
    {
        var erros = ValidadorCriarPedido.Validar(Requisicao(3, Item(2, 3), Item(5, 1)));

        Assert.Empty(erros);
    }
}
