namespace JoyaPedidos.Dominio;

/// <summary>Linha pedida pelo cliente HTTP, já validada no formato (RN-02 a RN-04). Nunca traz preço (RN-08).</summary>
public sealed record ItemSolicitado(int ProdutoId, int Quantidade);
