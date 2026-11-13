namespace JoyaPedidos.Api.Modulos.Catalogo.Contratos;

/// <summary>Elemento de <c>GET /produtos</c> (spec §5.8). <c>Preco</c> em <c>decimal</c>, com as duas casas da coluna.</summary>
public sealed record ProdutoCatalogoResposta(int Id, string Nome, decimal Preco, int Estoque);
