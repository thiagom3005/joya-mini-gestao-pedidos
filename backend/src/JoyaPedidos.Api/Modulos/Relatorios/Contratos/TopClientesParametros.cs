namespace JoyaPedidos.Api.Modulos.Relatorios.Contratos;

/// <summary>Query string de <c>GET /relatorios/top-clientes</c> (spec §5.6), como chegou: datas em texto.</summary>
public sealed record TopClientesParametros(string? DataInicio, string? DataFim);

/// <summary>Período validado: ambas as datas presentes e <c>DataInicio ≤ DataFim</c>.</summary>
public sealed record PeriodoTopClientes(DateOnly DataInicio, DateOnly DataFim);
