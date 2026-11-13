using JoyaPedidos.Api.Modulos.Relatorios;
using JoyaPedidos.Api.Modulos.Relatorios.Contratos;

namespace JoyaPedidos.TestesUnitarios.Relatorios;

/// <summary>UT-15: validação de GET /relatorios/top-clientes (spec §5.6; RN-15).</summary>
public sealed class ValidadorTopClientesTestes
{
    // UT-15 (datas ausentes)
    [Fact]
    public void Validar_SemNenhumaData_RetornaErroNasDuasChaves()
    {
        var (erros, periodo) = ValidadorTopClientes.Validar(new TopClientesParametros(null, null));

        Assert.Equal(["A data inicial é obrigatória."], erros["dataInicio"]);
        Assert.Equal(["A data final é obrigatória."], erros["dataFim"]);
        Assert.Equal(2, erros.Count);
        Assert.Null(periodo);
    }

    [Theory]
    [InlineData(null, "2026-12-31", "dataInicio")]
    [InlineData("2026-01-01", null, "dataFim")]
    public void Validar_UmaDataAusente_RetornaErroSoNaChaveAusente(string? dataInicio, string? dataFim, string chave)
    {
        var (erros, periodo) = ValidadorTopClientes.Validar(new TopClientesParametros(dataInicio, dataFim));

        Assert.Equal([chave], erros.Keys);
        Assert.Null(periodo);
    }

    // UT-15 (dataInicio > dataFim)
    [Fact]
    public void Validar_DataInicioPosteriorADataFim_RetornaErroNaChaveDataInicio()
    {
        var (erros, periodo) = ValidadorTopClientes.Validar(new TopClientesParametros("2026-12-31", "2026-01-01"));

        Assert.Equal(["dataInicio"], erros.Keys);
        Assert.Null(periodo);
    }

    // UT-15 (mesmo dia)
    [Fact]
    public void Validar_MesmoDia_EhValido()
    {
        var (erros, periodo) = ValidadorTopClientes.Validar(new TopClientesParametros("2026-07-01", "2026-07-01"));

        Assert.Empty(erros);
        Assert.Equal(new PeriodoTopClientes(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 1)), periodo);
    }

    [Fact]
    public void Validar_Ano2026_DevolveOPeriodoConvertido()
    {
        var (erros, periodo) = ValidadorTopClientes.Validar(new TopClientesParametros("2026-01-01", "2026-12-31"));

        Assert.Empty(erros);
        Assert.Equal(new PeriodoTopClientes(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)), periodo);
    }

    [Theory]
    [InlineData("2026-13-01")]
    [InlineData("01/01/2026")]
    [InlineData("")]
    public void Validar_DataForaDoFormato_RetornaErroDeFormato(string data)
    {
        var (erros, periodo) = ValidadorTopClientes.Validar(new TopClientesParametros(data, "2026-12-31"));

        Assert.Equal(["A data inicial deve estar no formato yyyy-MM-dd."], erros["dataInicio"]);
        Assert.Null(periodo);
    }
}
