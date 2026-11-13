namespace JoyaPedidos.Api.Compartilhado;

/// <summary>
/// Leitura de <c>dataInicio</c> e <c>dataFim</c> comum à listagem de pedidos e ao ranking (RN-15): formato exato,
/// obrigatoriedade conforme o endpoint e <c>dataInicio ≤ dataFim</c> quando ambas são válidas. Erros vão para
/// <paramref name="erros"/> nas chaves <c>dataInicio</c> e <c>dataFim</c>.
/// </summary>
public static class ValidacaoDePeriodo
{
    public const string ChaveInicio = "dataInicio";
    public const string ChaveFim = "dataFim";

    public static (DateOnly? Inicio, DateOnly? Fim) Ler(
        string? dataInicio, string? dataFim, bool obrigatorio, Dictionary<string, string[]> erros)
    {
        ArgumentNullException.ThrowIfNull(erros);
        var inicio = LerData(dataInicio, ChaveInicio, "inicial", obrigatorio, erros);
        var fim = LerData(dataFim, ChaveFim, "final", obrigatorio, erros);
        if (inicio > fim)
        {
            erros[ChaveInicio] = ["A data inicial não pode ser posterior à data final."];
        }
        return (inicio, fim);
    }

    private static DateOnly? LerData(
        string? texto, string chave, string descricao, bool obrigatorio, Dictionary<string, string[]> erros)
    {
        if (texto is null)
        {
            if (obrigatorio)
            {
                erros[chave] = [$"A data {descricao} é obrigatória."];
            }
            return null;
        }
        if (!DataCivil.TentarLer(texto, out var data))
        {
            erros[chave] = [$"A data {descricao} deve estar no formato {DataCivil.Formato}."];
            return null;
        }
        return data;
    }
}
