using JoyaPedidos.Dominio;

namespace JoyaPedidos.Api.Modulos.Pedidos.Contratos;

/// <summary>
/// Corpo do 201 de <c>POST /pedidos</c> e do 200 de <c>GET /pedidos/{id}</c> (spec §5.3, §5.5). Itens em ordem crescente
/// de produto; subtotais e total derivados em <c>decimal</c> (RN-11); <c>dataPedido</c> em UTC, serializado com <c>Z</c>.
/// </summary>
public sealed record PedidoResposta(
    int Id,
    ClienteResposta Cliente,
    DateTime DataPedido,
    IReadOnlyList<ItemPedidoResposta> Itens,
    decimal ValorTotal)
{
    public static PedidoResposta De(int id, Cliente cliente, DateTime dataPedidoUtc, IEnumerable<ItemPedido> itens)
    {
        var itensDaResposta = itens
            .OrderBy(item => item.ProdutoId)
            .Select(item => new ItemPedidoResposta(item.ProdutoId, item.NomeProduto, item.Quantidade, item.PrecoUnitario, item.Subtotal))
            .ToList();

        return new PedidoResposta(
            id,
            new ClienteResposta(cliente.Id, cliente.Nome),
            // O SqlClient devolve datetime2 sem fuso; sem o Kind Utc, o JSON sairia sem o sufixo Z (spec §4.5).
            DateTime.SpecifyKind(dataPedidoUtc, DateTimeKind.Utc),
            itensDaResposta,
            itensDaResposta.Sum(item => item.Subtotal));
    }
}

public sealed record ClienteResposta(int Id, string Nome);

public sealed record ItemPedidoResposta(int ProdutoId, string NomeProduto, int Quantidade, decimal PrecoUnitario, decimal Subtotal);
