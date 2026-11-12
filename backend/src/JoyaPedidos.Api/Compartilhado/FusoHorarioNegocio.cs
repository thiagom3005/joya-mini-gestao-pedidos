namespace JoyaPedidos.Api.Compartilhado;

/// <summary>
/// Fuso fixo do negócio (spec PRE-05, DEC-06): datas civis de filtros e relatórios são interpretadas em
/// America/Sao_Paulo. A API falha na inicialização se o sistema não tiver o fuso (spec RSK-02).
/// </summary>
public static class FusoHorarioNegocio
{
    public const string Identificador = "America/Sao_Paulo";

    public static TimeZoneInfo Carregar()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(Identificador);
        }
        catch (Exception excecao) when (excecao is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new InvalidOperationException(
                $"Fuso horário '{Identificador}' indisponível neste sistema. Instale os dados de fuso (tzdata) " +
                "ou use uma imagem de runtime que os inclua.",
                excecao);
        }
    }
}
