namespace JoyaPedidos.Dominio;

/// <summary>
/// Produto lido do banco sob bloqueio, dentro da transação de criação (spec §7.2). <see cref="Preco"/> é o preço vigente,
/// que o pedido congela (RN-08); <see cref="Estoque"/> é o saldo disponível naquele instante.
/// </summary>
public sealed record Produto(int Id, string Nome, decimal Preco, int Estoque);
