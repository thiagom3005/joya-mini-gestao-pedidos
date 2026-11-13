namespace JoyaPedidos.Api.Modulos.Pedidos.Contratos;

/// <summary>
/// Corpo de <c>POST /pedidos</c> (spec §5.3). Tipos anuláveis distinguem campo ausente de valor 0 (spec §5.1).
/// Não há campo de preço: qualquer preço enviado é ignorado (RN-08).
/// </summary>
public sealed record CriarPedidoRequisicao(int? ClienteId, IReadOnlyList<ItemPedidoRequisicao?>? Itens);

/// <summary>Linha de <see cref="CriarPedidoRequisicao.Itens"/>.</summary>
public sealed record ItemPedidoRequisicao(int? ProdutoId, int? Quantidade);
