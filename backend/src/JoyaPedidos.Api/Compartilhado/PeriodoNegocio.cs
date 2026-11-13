namespace JoyaPedidos.Api.Compartilhado;

/// <summary>Intervalo UTC semiaberto <c>[InicioUtc, FimExclusivoUtc)</c>; um limite nulo não restringe aquele lado.</summary>
public readonly record struct IntervaloUtc(DateTime? InicioUtc, DateTime? FimExclusivoUtc);

/// <summary>
/// Converte períodos de datas civis do negócio (America/Sao_Paulo) em intervalos de instantes UTC (spec §8.2, RN-15).
/// Funções puras: o fuso vem por parâmetro (na API, o singleton de <see cref="FusoHorarioNegocio"/>).
/// O deslocamento é calculado para cada data, nunca presumido: São Paulo teve horário de verão (UTC-2) até 2019.
/// </summary>
public static class PeriodoNegocio
{
    /// <summary>
    /// Período inclusivo de datas civis → <c>[início de dataInicio, início de (dataFim + 1 dia))</c> em UTC.
    /// Só <paramref name="dataInicio"/>: apenas o limite inferior. Só <paramref name="dataFim"/>: apenas o superior.
    /// A validação <c>dataInicio ≤ dataFim</c> é dos validadores de entrada. <c>dataFim</c> = 31/12/9999 não tem dia
    /// seguinte representável e equivale a não ter limite superior.
    /// </summary>
    public static IntervaloUtc ParaIntervaloUtc(DateOnly? dataInicio, DateOnly? dataFim, TimeZoneInfo fuso)
    {
        ArgumentNullException.ThrowIfNull(fuso);
        return new IntervaloUtc(
            dataInicio is { } inicio ? InicioDoDiaEmUtc(inicio, fuso) : null,
            dataFim is { } fim && fim < DateOnly.MaxValue ? InicioDoDiaEmUtc(fim.AddDays(1), fuso) : null);
    }

    /// <summary>
    /// Primeiro instante do dia civil <paramref name="data"/> no fuso, em UTC (<see cref="DateTimeKind.Utc"/>).
    /// Usa o deslocamento do fuso para a meia-noite daquela data (§8.2). Meia-noite inexistente (o relógio saltou de
    /// 00:00 para 01:00, como em 04/11/2018) recebe o deslocamento padrão e resulta no instante do salto. Meia-noite
    /// ambígua (o relógio voltou de 01:00 para 00:00, como em 16/04/1950 no tzdata) começa na primeira ocorrência,
    /// a de maior deslocamento, e não na segunda, que o deslocamento padrão daria.
    /// </summary>
    public static DateTime InicioDoDiaEmUtc(DateOnly data, TimeZoneInfo fuso)
    {
        ArgumentNullException.ThrowIfNull(fuso);
        var meiaNoite = data.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var deslocamento = fuso.IsAmbiguousTime(meiaNoite)
            ? fuso.GetAmbiguousTimeOffsets(meiaNoite).Max()
            : fuso.GetUtcOffset(meiaNoite);
        return DateTime.SpecifyKind(meiaNoite - deslocamento, DateTimeKind.Utc);
    }
}
