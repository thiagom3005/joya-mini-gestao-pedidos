using JoyaPedidos.Dominio;

namespace JoyaPedidos.TestesUnitarios.Dominio;

/// <summary>Erros de negócio com lista: cópia somente leitura e igualdade pelos elementos.</summary>
public sealed class ErroNegocioTestes
{
    [Fact]
    public void ProdutosNaoEncontrados_MesmosIdsEmListasDiferentes_SaoIguais()
    {
        Assert.Equal(new ProdutosNaoEncontrados([77, 78]), new ProdutosNaoEncontrados(new List<int> { 77, 78 }));
        Assert.NotEqual(new ProdutosNaoEncontrados([77, 78]), new ProdutosNaoEncontrados([78, 77]));
    }

    [Fact]
    public void ProdutosNaoEncontrados_ListaOriginalAlteradaDepois_ErroNaoMuda()
    {
        int[] ids = [77];
        var erro = new ProdutosNaoEncontrados(ids);

        ids[0] = 1;

        Assert.Equal([77], erro.ProdutoIds);
    }

    [Fact]
    public void EstoqueInsuficiente_MesmosItensEmListasDiferentes_SaoIguais()
    {
        ItemSemEstoque Garrafa() => new(5, "Garrafa térmica", 4, 2);

        Assert.Equal(new EstoqueInsuficiente([Garrafa()]), new EstoqueInsuficiente(new List<ItemSemEstoque> { Garrafa() }));
        Assert.NotEqual(new EstoqueInsuficiente([Garrafa()]), new EstoqueInsuficiente([Garrafa() with { EstoqueDisponivel = 3 }]));
    }

    [Fact]
    public void EstoqueInsuficiente_ListaOriginalAlteradaDepois_ErroNaoMuda()
    {
        var itens = new List<ItemSemEstoque> { new(5, "Garrafa térmica", 4, 2) };
        var erro = new EstoqueInsuficiente(itens);

        itens.Clear();

        Assert.Single(erro.Itens);
        Assert.True(Assert.IsAssignableFrom<ICollection<ItemSemEstoque>>(erro.Itens).IsReadOnly);
    }
}
