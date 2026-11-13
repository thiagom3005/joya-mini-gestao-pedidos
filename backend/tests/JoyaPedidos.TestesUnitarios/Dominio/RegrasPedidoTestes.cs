using JoyaPedidos.Dominio;

namespace JoyaPedidos.TestesUnitarios.Dominio;

/// <summary>Regras puras de criação de pedido (spec §3; UT-01, UT-05 a UT-12).</summary>
public sealed class RegrasPedidoTestes
{
    private static readonly Cliente Bruno = new(3, "Bruno Lima");
    private static readonly DateTimeOffset Agora = new(2026, 11, 12, 19, 30, 0, TimeSpan.Zero);

    private static Produto Caneca(int estoque = 10) => new(2, "Caneca de cerâmica", 19.99m, estoque);

    private static Produto Garrafa(int estoque = 10) => new(5, "Garrafa térmica", 99.90m, estoque);

    // UT-01
    [Fact]
    public void VerificarCliente_ClienteInexistente_RetornaClienteNaoEncontradoComOId()
    {
        var resultado = RegrasPedido.VerificarCliente(999, cliente: null);

        Assert.False(resultado.Sucesso);
        Assert.Equal(new ClienteNaoEncontrado(999), resultado.Erro);
    }

    [Fact]
    public void VerificarCliente_ClienteExistente_RetornaOProprioCliente()
    {
        var resultado = RegrasPedido.VerificarCliente(3, Bruno);

        Assert.True(resultado.Sucesso);
        Assert.Same(Bruno, resultado.Valor);
    }

    // UT-05
    [Fact]
    public void CriarPedido_DoisProdutosAusentes_RetornaProdutoNaoEncontradoComOsDoisIdsEmOrdem()
    {
        ItemSolicitado[] itens = [new(78, 1), new(2, 1), new(77, 2)];

        var resultado = RegrasPedido.CriarPedido(Bruno, itens, [Caneca()], Agora);

        var erro = Assert.IsType<ProdutosNaoEncontrados>(resultado.Erro);
        Assert.Equal([77, 78], erro.ProdutoIds);
    }

    // UT-06
    [Fact]
    public void CriarPedido_QuantidadeMaiorQueOEstoque_RetornaEstoqueInsuficienteComSolicitadoEDisponivel()
    {
        var resultado = RegrasPedido.CriarPedido(Bruno, [new ItemSolicitado(5, 4)], [Garrafa(estoque: 2)], Agora);

        var erro = Assert.IsType<EstoqueInsuficiente>(resultado.Erro);
        var item = Assert.Single(erro.Itens);
        Assert.Equal(new ItemSemEstoque(5, "Garrafa térmica", QuantidadeSolicitada: 4, EstoqueDisponivel: 2), item);
    }

    // UT-07
    [Fact]
    public void CriarPedido_DoisItensSemEstoque_ListaAmbosEmOrdemDeProduto()
    {
        ItemSolicitado[] itens = [new(5, 3), new(2, 11)];

        var resultado = RegrasPedido.CriarPedido(Bruno, itens, [Caneca(estoque: 10), Garrafa(estoque: 0)], Agora);

        var erro = Assert.IsType<EstoqueInsuficiente>(resultado.Erro);
        Assert.Equal(
            [new ItemSemEstoque(2, "Caneca de cerâmica", 11, 10), new ItemSemEstoque(5, "Garrafa térmica", 3, 0)],
            erro.Itens);
    }

    // UT-08
    [Fact]
    public void CriarPedido_QuantidadeIgualAoEstoque_CriaOPedido()
    {
        var resultado = RegrasPedido.CriarPedido(Bruno, [new ItemSolicitado(2, 10)], [Caneca(estoque: 10)], Agora);

        Assert.True(resultado.Sucesso);
        Assert.Equal(10, Assert.Single(resultado.Valor.Itens).Quantidade);
    }

    // UT-09
    [Fact]
    public void CriarPedido_ProdutoAusenteEOutroSemEstoque_RetornaSoProdutoNaoEncontrado()
    {
        ItemSolicitado[] itens = [new(2, 50), new(77, 1)];

        var resultado = RegrasPedido.CriarPedido(Bruno, itens, [Caneca(estoque: 1)], Agora);

        var erro = Assert.IsType<ProdutosNaoEncontrados>(resultado.Erro);
        Assert.Equal([77], erro.ProdutoIds);
    }

    // UT-10
    [Fact]
    public void CriarPedido_ProdutoComPreco1999_ItemCongelaOPrecoLido()
    {
        var resultado = RegrasPedido.CriarPedido(Bruno, [new ItemSolicitado(2, 1)], [Caneca()], Agora);

        var item = Assert.Single(resultado.Valor!.Itens);
        Assert.Equal(19.99m, item.PrecoUnitario);
        Assert.Equal("Caneca de cerâmica", item.NomeProduto);
    }

    // UT-11
    [Fact]
    public void CriarPedido_TresCanecasEUmaGarrafa_CalculaSubtotaisETotalExatos()
    {
        ItemSolicitado[] itens = [new(5, 1), new(2, 3)];

        var resultado = RegrasPedido.CriarPedido(Bruno, itens, [Caneca(), Garrafa()], Agora);

        var pedido = resultado.Valor!;
        Assert.Equal([2, 5], pedido.Itens.Select(item => item.ProdutoId)); // ordenados por produto (spec §5.3)
        // Texto invariante: além do valor, confere a escala de duas casas (0.3m == 0.30m em decimal).
        Assert.Equal("59.97", Invariante(pedido.Itens[0].Subtotal));
        Assert.Equal("99.90", Invariante(pedido.Itens[1].Subtotal));
        Assert.Equal("159.87", Invariante(pedido.ValorTotal));
    }

    // UT-11 (decimal exato, sem erro de ponto flutuante)
    [Fact]
    public void CriarPedido_TresUnidadesDe010_SubtotalETotal030Exatos()
    {
        var resultado = RegrasPedido.CriarPedido(Bruno, [new ItemSolicitado(9, 3)], [new Produto(9, "Bala", 0.10m, 100)], Agora);

        var pedido = resultado.Valor!;
        Assert.Equal("0.30", Invariante(pedido.Itens[0].Subtotal));
        Assert.Equal("0.30", Invariante(pedido.ValorTotal));
    }

    private static string Invariante(decimal valor) => valor.ToString(System.Globalization.CultureInfo.InvariantCulture);

    // UT-12
    [Fact]
    public void CriarPedido_AgoraComMilissegundos_DataPedidoEmUtcTruncadaASegundos()
    {
        var agora = new DateTimeOffset(2026, 11, 12, 19, 30, 0, 987, TimeSpan.Zero);

        var resultado = RegrasPedido.CriarPedido(Bruno, [new ItemSolicitado(2, 1)], [Caneca()], agora);

        Assert.Equal(new DateTimeOffset(2026, 11, 12, 19, 30, 0, TimeSpan.Zero), resultado.Valor!.DataPedido);
        Assert.Equal(TimeSpan.Zero, resultado.Valor.DataPedido.Offset);
    }

    // UT-12 (instante informado em outro deslocamento continua o mesmo instante, em UTC)
    [Fact]
    public void CriarPedido_AgoraComDeslocamentoLocal_DataPedidoConvertidaParaUtc()
    {
        var agoraEmSaoPaulo = new DateTimeOffset(2026, 11, 12, 16, 30, 0, 500, TimeSpan.FromHours(-3));

        var resultado = RegrasPedido.CriarPedido(Bruno, [new ItemSolicitado(2, 1)], [Caneca()], agoraEmSaoPaulo);

        Assert.Equal(new DateTimeOffset(2026, 11, 12, 19, 30, 0, TimeSpan.Zero), resultado.Valor!.DataPedido);
        Assert.Equal(TimeSpan.Zero, resultado.Valor.DataPedido.Offset);
    }

    [Fact]
    public void CriarPedido_Sucesso_PreservaClienteEItensSaoSomenteLeitura()
    {
        var resultado = RegrasPedido.CriarPedido(Bruno, [new ItemSolicitado(2, 1)], [Caneca()], Agora);

        Assert.Same(Bruno, resultado.Valor!.Cliente);
        var colecao = Assert.IsAssignableFrom<ICollection<ItemPedido>>(resultado.Valor.Itens);
        Assert.True(colecao.IsReadOnly);
    }

    [Fact]
    public void CriarPedido_ProdutoBloqueadoNaoSolicitado_EIgnorado()
    {
        var resultado = RegrasPedido.CriarPedido(Bruno, [new ItemSolicitado(2, 1)], [Caneca(), Garrafa(estoque: 0)], Agora);

        Assert.True(resultado.Sucesso);
        Assert.Equal(2, Assert.Single(resultado.Valor.Itens).ProdutoId);
    }
}
