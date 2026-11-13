using JoyaPedidos.Api.Compartilhado;
using JoyaPedidos.Api.Modulos.Relatorios.Contratos;

namespace JoyaPedidos.Api.Modulos.Relatorios;

/// <summary>
/// Validação de <c>GET /relatorios/top-clientes</c> (spec §5.6; RN-15). Função pura: <c>dataInicio</c> e <c>dataFim</c>
/// obrigatórias, no formato exato <c>yyyy-MM-dd</c>, com <c>dataInicio ≤ dataFim</c> (o mesmo dia é válido).
/// </summary>
public static class ValidadorTopClientes
{
    public static (IReadOnlyDictionary<string, string[]> Erros, PeriodoTopClientes? Periodo) Validar(TopClientesParametros parametros)
    {
        ArgumentNullException.ThrowIfNull(parametros);
        var erros = new Dictionary<string, string[]>();
        var (inicio, fim) = ValidacaoDePeriodo.Ler(parametros.DataInicio, parametros.DataFim, obrigatorio: true, erros);
        return erros.Count == 0 && inicio is { } dataInicio && fim is { } dataFim
            ? (erros, new PeriodoTopClientes(dataInicio, dataFim))
            : (erros, null);
    }
}
