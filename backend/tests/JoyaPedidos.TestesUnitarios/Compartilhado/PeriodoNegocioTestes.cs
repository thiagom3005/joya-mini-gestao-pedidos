using JoyaPedidos.Api.Compartilhado;

namespace JoyaPedidos.TestesUnitarios.Compartilhado;

/// <summary>Conversão de datas civis de São Paulo em intervalos UTC (spec §8.2; UT-16, UT-17).</summary>
public sealed class PeriodoNegocioTestes
{
    private static readonly TimeZoneInfo SaoPaulo = FusoHorarioNegocio.Carregar();

    private static DateTime Utc(int ano, int mes, int dia, int hora, int minuto = 0) =>
        new(ano, mes, dia, hora, minuto, 0, DateTimeKind.Utc);

    // UT-16
    [Fact]
    public void ParaIntervaloUtc_Ano2026_InicioUtcAs03hEFimExclusivoNoInicioDe2027()
    {
        var intervalo = PeriodoNegocio.ParaIntervaloUtc(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), SaoPaulo);

        Assert.Equal(Utc(2026, 1, 1, 3), intervalo.InicioUtc);
        Assert.Equal(Utc(2027, 1, 1, 3), intervalo.FimExclusivoUtc);
        Assert.Equal(DateTimeKind.Utc, intervalo.InicioUtc!.Value.Kind);
        Assert.Equal(DateTimeKind.Utc, intervalo.FimExclusivoUtc!.Value.Kind);
    }

    // UT-16: fronteiras da tabela da spec §8.2, aplicadas ao intervalo semiaberto
    [Theory]
    [InlineData("2026-01-01T02:59:59Z", false)] // 31/12/2025 23:59:59 em São Paulo
    [InlineData("2026-01-01T03:00:00Z", true)]  // 01/01/2026 00:00:00
    [InlineData("2027-01-01T02:59:59Z", true)]  // 31/12/2026 23:59:59
    [InlineData("2027-01-01T03:00:00Z", false)] // 01/01/2027 00:00:00
    public void ParaIntervaloUtc_Ano2026_FronteirasDaSpecFicamDoLadoCerto(string instanteUtc, bool dentro)
    {
        var intervalo = PeriodoNegocio.ParaIntervaloUtc(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), SaoPaulo);
        var instante = DateTimeOffset.Parse(instanteUtc, System.Globalization.CultureInfo.InvariantCulture).UtcDateTime;

        Assert.Equal(dentro, instante >= intervalo.InicioUtc && instante < intervalo.FimExclusivoUtc);
    }

    // UT-17: 04/11/2018 começou o horário de verão; o relógio saltou de 00:00 para 01:00 (meia-noite inexistente)
    [Fact]
    public void InicioDoDiaEmUtc_MeiaNoiteInexistenteDoHorarioDeVerao_NaoLancaEComecaNoPrimeiroInstanteValido()
    {
        var inicio = PeriodoNegocio.InicioDoDiaEmUtc(new DateOnly(2018, 11, 4), SaoPaulo);

        Assert.Equal(Utc(2018, 11, 4, 3), inicio);
        // O primeiro instante válido do dia é 01:00 local, já no horário de verão (UTC-2).
        Assert.Equal(new DateTime(2018, 11, 4, 1, 0, 0), TimeZoneInfo.ConvertTimeFromUtc(inicio, SaoPaulo));
    }

    // Não presumir UTC-3: em pleno horário de verão de 2018/2019, a meia-noite local era 02:00 UTC.
    [Fact]
    public void InicioDoDiaEmUtc_DiaEmHorarioDeVeraoHistorico_UsaDeslocamentoDeMenosDuasHoras()
    {
        var inicio = PeriodoNegocio.InicioDoDiaEmUtc(new DateOnly(2019, 1, 15), SaoPaulo);

        Assert.Equal(Utc(2019, 1, 15, 2), inicio);
    }

    // 17/02/2019 terminou o horário de verão: à meia-noite o relógio voltou para 23:00 do dia 16.
    [Fact]
    public void ParaIntervaloUtc_DiaDoFimDoHorarioDeVerao_IntervaloTem25Horas()
    {
        var intervalo = PeriodoNegocio.ParaIntervaloUtc(new DateOnly(2019, 2, 16), new DateOnly(2019, 2, 16), SaoPaulo);

        Assert.Equal(Utc(2019, 2, 16, 2), intervalo.InicioUtc);        // 00:00 do dia 16 em UTC-2
        Assert.Equal(Utc(2019, 2, 17, 3), intervalo.FimExclusivoUtc);  // 00:00 do dia 17 em UTC-3
        Assert.Equal(TimeSpan.FromHours(25), intervalo.FimExclusivoUtc - intervalo.InicioUtc);
    }

    [Fact]
    public void ParaIntervaloUtc_SoDataInicio_AplicaApenasOLimiteInferior()
    {
        var intervalo = PeriodoNegocio.ParaIntervaloUtc(new DateOnly(2026, 3, 9), null, SaoPaulo);

        Assert.Equal(Utc(2026, 3, 9, 3), intervalo.InicioUtc);
        Assert.Null(intervalo.FimExclusivoUtc);
    }

    [Fact]
    public void ParaIntervaloUtc_SoDataFim_AplicaApenasOLimiteSuperiorNoDiaSeguinte()
    {
        var intervalo = PeriodoNegocio.ParaIntervaloUtc(null, new DateOnly(2026, 3, 9), SaoPaulo);

        Assert.Null(intervalo.InicioUtc);
        Assert.Equal(Utc(2026, 3, 10, 3), intervalo.FimExclusivoUtc);
    }

    [Fact]
    public void ParaIntervaloUtc_DataFimNoUltimoDiaRepresentavel_NaoLancaENaoLimitaOFim()
    {
        var intervalo = PeriodoNegocio.ParaIntervaloUtc(new DateOnly(2026, 1, 1), DateOnly.MaxValue, SaoPaulo);

        Assert.Equal(Utc(2026, 1, 1, 3), intervalo.InicioUtc);
        Assert.Null(intervalo.FimExclusivoUtc);
    }

    // Fuso sintético (UTC-3, verão UTC-2 terminando à 01:00 de 01/03/2030): a meia-noite desse dia ocorre duas vezes.
    // Em São Paulo isso só aconteceu em 16/04/1950, e só o tzdata do Linux registra; o fuso sintético torna o teste
    // igual em qualquer sistema operacional.
    private static readonly TimeZoneInfo FusoComMeiaNoiteAmbigua = TimeZoneInfo.CreateCustomTimeZone(
        "Teste/MeiaNoiteAmbigua",
        TimeSpan.FromHours(-3),
        "Teste",
        "Teste padrão",
        "Teste verão",
        [
            TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
                new DateTime(2029, 1, 1),
                new DateTime(2030, 12, 31),
                TimeSpan.FromHours(1),
                TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 0, 0, 0), 11, 1),
                TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 1, 0, 0), 3, 1)),
        ]);

    [Fact]
    public void InicioDoDiaEmUtc_MeiaNoiteAmbigua_ComecaNaPrimeiraOcorrencia()
    {
        var meiaNoite = new DateTime(2030, 3, 1, 0, 0, 0);
        Assert.True(FusoComMeiaNoiteAmbigua.IsAmbiguousTime(meiaNoite)); // pré-condição do cenário

        var inicio = PeriodoNegocio.InicioDoDiaEmUtc(DateOnly.FromDateTime(meiaNoite), FusoComMeiaNoiteAmbigua);

        Assert.Equal(Utc(2030, 3, 1, 2), inicio); // 00:00 ainda no verão (UTC-2), não a segunda, às 03:00Z
    }

    [Fact]
    public void ParaIntervaloUtc_DiaAntesDaMeiaNoiteAmbigua_IntervaloTem24HorasSemSobreporODiaSeguinte()
    {
        var intervalo = PeriodoNegocio.ParaIntervaloUtc(new DateOnly(2030, 2, 28), new DateOnly(2030, 2, 28), FusoComMeiaNoiteAmbigua);

        Assert.Equal(Utc(2030, 2, 28, 2), intervalo.InicioUtc);
        Assert.Equal(Utc(2030, 3, 1, 2), intervalo.FimExclusivoUtc);
    }

    [Fact]
    public void ParaIntervaloUtc_MesmoDia_CobreAs24HorasLocais()
    {
        var intervalo = PeriodoNegocio.ParaIntervaloUtc(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), SaoPaulo);

        Assert.Equal(TimeSpan.FromHours(24), intervalo.FimExclusivoUtc - intervalo.InicioUtc);
    }
}
