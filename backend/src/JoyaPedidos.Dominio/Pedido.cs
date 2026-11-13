using System.Collections.ObjectModel;

namespace JoyaPedidos.Dominio;

/// <summary>
/// Pedido criado pelas regras de domínio, ainda sem Id (o banco o gera na E5). Imutável (RN-13): os itens ficam numa
/// coleção somente leitura, ordenados por <see cref="ItemPedido.ProdutoId"/> crescente (spec §5.3). O valor total é
/// sempre derivado dos itens, nunca armazenado (RN-11, DEC-04).
/// </summary>
public sealed class Pedido
{
    internal Pedido(Cliente cliente, DateTimeOffset dataPedido, IEnumerable<ItemPedido> itens)
    {
        Cliente = cliente;
        DataPedido = dataPedido;
        Itens = new ReadOnlyCollection<ItemPedido>(itens.OrderBy(item => item.ProdutoId).ToList());
    }

    public Cliente Cliente { get; }

    /// <summary>Instante UTC (deslocamento zero), truncado a segundos (RN-09).</summary>
    public DateTimeOffset DataPedido { get; }

    public IReadOnlyList<ItemPedido> Itens { get; }

    /// <summary>Soma dos subtotais, em <c>decimal</c> exato (RN-11).</summary>
    public decimal ValorTotal => Itens.Sum(item => item.Subtotal);
}
