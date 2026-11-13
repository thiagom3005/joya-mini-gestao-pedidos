namespace JoyaPedidos.Dominio;

/// <summary>Item de um pedido com o preço congelado no momento da criação (RN-08).</summary>
public sealed record ItemPedido(int ProdutoId, string NomeProduto, int Quantidade, decimal PrecoUnitario)
{
    /// <summary>Quantidade × preço unitário, em <c>decimal</c> exato, sem arredondamento (RN-11).</summary>
    public decimal Subtotal => Quantidade * PrecoUnitario;
}
