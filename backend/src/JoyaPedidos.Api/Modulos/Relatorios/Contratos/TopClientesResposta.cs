namespace JoyaPedidos.Api.Modulos.Relatorios.Contratos;

/// <summary>
/// Resposta de <c>GET /relatorios/top-clientes</c> (spec §5.6): eco do período em <c>yyyy-MM-dd</c> e até 5 clientes,
/// com <c>posicao</c> de 1 a N sem lacunas.
/// </summary>
public sealed record TopClientesResposta(string DataInicio, string DataFim, IReadOnlyList<ClienteRankingResposta> Clientes);

public sealed record ClienteRankingResposta(int Posicao, int ClienteId, string Nome, decimal ValorTotal);
