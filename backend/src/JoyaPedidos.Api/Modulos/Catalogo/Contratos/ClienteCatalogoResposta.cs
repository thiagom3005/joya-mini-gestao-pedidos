namespace JoyaPedidos.Api.Modulos.Catalogo.Contratos;

/// <summary>Elemento de <c>GET /clientes</c> (spec §5.7).</summary>
public sealed record ClienteCatalogoResposta(int Id, string Nome, string Email);
