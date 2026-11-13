using JoyaPedidos.Api.Compartilhado;
using JoyaPedidos.Api.Modulos.Relatorios.Contratos;
using Microsoft.AspNetCore.Mvc;

namespace JoyaPedidos.Api.Modulos.Relatorios;

/// <summary>Rotas de relatórios (spec §5.6). Só mapeiam HTTP: validam, convertem o período e chamam o repositório.</summary>
public static class RelatoriosEndpoints
{
    public static IServiceCollection AddModuloRelatorios(this IServiceCollection servicos) =>
        servicos.AddSingleton<RepositorioRelatorios>();

    public static IEndpointRouteBuilder MapRelatoriosEndpoints(this IEndpointRouteBuilder rotas)
    {
        rotas.MapGroup("/relatorios").MapGet("/top-clientes", TopClientesAsync);
        return rotas;
    }

    private static async Task<IResult> TopClientesAsync(
        [AsParameters] TopClientesParametros parametros,
        RepositorioRelatorios repositorio,
        [FromServices] TimeZoneInfo fusoDoNegocio,
        CancellationToken cancelamento)
    {
        var (erros, periodo) = ValidadorTopClientes.Validar(parametros);
        if (periodo is null)
        {
            return Problema(MapeadorErros.DeValidacao(erros));
        }

        var intervalo = PeriodoNegocio.ParaIntervaloUtc(periodo.DataInicio, periodo.DataFim, fusoDoNegocio);
        var clientes = await repositorio.TopClientesAsync(intervalo, cancelamento);
        return TypedResults.Ok(new TopClientesResposta(
            DataCivil.Formatar(periodo.DataInicio), DataCivil.Formatar(periodo.DataFim), clientes));
    }

    private static IResult Problema(ErroHttp erro) => TypedResults.Problem(erro.ParaProblemDetails());
}
