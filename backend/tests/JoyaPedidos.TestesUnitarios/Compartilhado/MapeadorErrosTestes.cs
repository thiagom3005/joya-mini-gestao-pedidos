using JoyaPedidos.Api.Compartilhado;
using JoyaPedidos.Dominio;

namespace JoyaPedidos.TestesUnitarios.Compartilhado;

/// <summary>Tradução de erros para o contrato HTTP (spec §5.2, §7.5; UT-18).</summary>
public sealed class MapeadorErrosTestes
{
    // UT-18: validação
    [Fact]
    public void DeValidacao_ErrosPorCampo_Retorna400ValidacaoComErrors()
    {
        var porCampo = new Dictionary<string, string[]> { ["itens"] = ["O pedido deve conter ao menos um item."] };

        var erro = MapeadorErros.DeValidacao(porCampo);

        Assert.Equal((400, "VALIDACAO"), (erro.Status, erro.Codigo));
        Assert.Same(porCampo, erro.Extensoes["errors"]);
    }

    [Fact]
    public void DeValidacao_SemErros_LancaArgumentException()
    {
        Assert.Throws<ArgumentException>(() => MapeadorErros.DeValidacao(new Dictionary<string, string[]>()));
    }

    // UT-18: cada erro de negócio
    [Fact]
    public void DeNegocio_ClienteNaoEncontrado_Retorna422ComOIdNoDetalhe()
    {
        var erro = MapeadorErros.DeNegocio(new ClienteNaoEncontrado(999));

        Assert.Equal((422, "CLIENTE_NAO_ENCONTRADO"), (erro.Status, erro.Codigo));
        Assert.Equal("O cliente 999 não existe.", erro.Detalhe);
        Assert.Empty(erro.Extensoes);
    }

    [Fact]
    public void DeNegocio_ProdutosNaoEncontrados_Retorna422ComALista()
    {
        var erro = MapeadorErros.DeNegocio(new ProdutosNaoEncontrados([77, 78]));

        Assert.Equal((422, "PRODUTO_NAO_ENCONTRADO"), (erro.Status, erro.Codigo));
        Assert.Equal("2 produto(s) informado(s) não existem.", erro.Detalhe);
        Assert.Equal([77, 78], (IReadOnlyList<int>)erro.Extensoes["produtosNaoEncontrados"]);
    }

    [Fact]
    public void DeNegocio_EstoqueInsuficiente_Retorna409ComOsItens()
    {
        ItemSemEstoque[] itens = [new(5, "Garrafa térmica", 4, 2)];

        var erro = MapeadorErros.DeNegocio(new EstoqueInsuficiente(itens));

        Assert.Equal((409, "ESTOQUE_INSUFICIENTE"), (erro.Status, erro.Codigo));
        Assert.Equal("Não há estoque suficiente para 1 produto(s).", erro.Detalhe);
        Assert.Equal(itens, (IReadOnlyList<ItemSemEstoque>)erro.Extensoes["itensSemEstoque"]);
    }

    // UT-18: pedido inexistente na consulta por id (spec §5.5)
    [Fact]
    public void PedidoNaoEncontrado_IdInexistente_Retorna404ComOIdNoDetalhe()
    {
        var erro = MapeadorErros.PedidoNaoEncontrado(1024);

        Assert.Equal((404, "PEDIDO_NAO_ENCONTRADO"), (erro.Status, erro.Codigo));
        Assert.Equal("Pedido não encontrado", erro.Titulo);
        Assert.Equal("O pedido 1024 não existe.", erro.Detalhe);
        Assert.Empty(erro.Extensoes);
    }

    // UT-18: erro SQL pelo número
    [Fact]
    public void DeErroSql_1205VitimaDeDeadlock_Retorna503ConflitoConcorrencia()
    {
        var erro = MapeadorErros.DeErroSql(1205);

        Assert.Equal((503, "CONFLITO_CONCORRENCIA"), (erro.Status, erro.Codigo));
        Assert.Equal("Conflito momentâneo de concorrência. Tente novamente.", erro.Detalhe);
    }

    [Theory]
    [InlineData(547)]   // violação de restrição
    [InlineData(2627)]  // chave duplicada
    [InlineData(-2)]    // tempo esgotado
    [InlineData(1222)]  // tempo de espera por lock
    public void DeErroSql_OutroNumero_Retorna500ErroInternoSemDetalhesDeInfraestrutura(int numero)
    {
        var erro = MapeadorErros.DeErroSql(numero);

        Assert.Equal((500, "ERRO_INTERNO"), (erro.Status, erro.Codigo));
        Assert.Equal("Ocorreu um erro inesperado.", erro.Detalhe);
        Assert.DoesNotContain(numero.ToString(System.Globalization.CultureInfo.InvariantCulture), erro.Detalhe);
        Assert.Empty(erro.Extensoes);
    }

    [Fact]
    public void ParaProblemDetails_ErroDeNegocio_LevaStatusTituloCodigoEExtensoes()
    {
        var problema = MapeadorErros.DeNegocio(new ProdutosNaoEncontrados([77])).ParaProblemDetails();

        Assert.Equal(422, problema.Status);
        Assert.Equal("Produto não encontrado", problema.Title);
        Assert.Equal("PRODUTO_NAO_ENCONTRADO", problema.Extensions["codigo"]);
        Assert.True(problema.Extensions.ContainsKey("produtosNaoEncontrados"));
    }

    private sealed record ErroDesconhecido : ErroNegocio;

    [Fact]
    public void DeNegocio_ErroSemMapeamento_LancaEmVezDeResponderGenericamente()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MapeadorErros.DeNegocio(new ErroDesconhecido()));
    }
}
