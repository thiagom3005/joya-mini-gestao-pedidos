namespace JoyaPedidos.Dominio;

/// <summary>Cliente já lido do banco, no formato que as regras de pedido precisam (spec §4.2).</summary>
public sealed record Cliente(int Id, string Nome);
