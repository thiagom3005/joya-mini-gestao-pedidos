using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace JoyaPedidos.Api.Compartilhado;

/// <summary>Padroniza todas as respostas de erro como Problem Details com <c>codigo</c> e <c>traceId</c> (spec §5.2).</summary>
public static class ConfiguracaoProblemDetails
{
    public const string ExtensaoCodigo = "codigo";
    public const string ExtensaoTraceId = "traceId";

    public static IServiceCollection AddProblemDetailsPadronizados(this IServiceCollection servicos)
    {
        servicos.AddProblemDetails(opcoes => opcoes.CustomizeProblemDetails = contexto =>
            Padronizar(contexto.ProblemDetails, contexto.HttpContext));

        servicos.AddExceptionHandler<TratadorExcecoes>();
        return servicos;
    }

    /// <summary>
    /// Sem <c>codigo</c> = erro genérico (framework ou exceção não tratada): título e código padronizados em pt-BR,
    /// iguais em qualquer ambiente. Erros da aplicação sempre trazem o próprio <c>codigo</c> e título.
    /// </summary>
    public static void Padronizar(ProblemDetails problema, HttpContext contexto)
    {
        var status = problema.Status ?? contexto.Response.StatusCode;
        if (!problema.Extensions.ContainsKey(ExtensaoCodigo))
        {
            problema.Title = CodigosErroHttp.TituloGenericoParaStatus(status);
            problema.Extensions[ExtensaoCodigo] = CodigosErroHttp.GenericoParaStatus(status);
        }
        problema.Extensions.TryAdd(ExtensaoTraceId, IdentificadorDeRastreio(contexto));
    }

    /// <summary>Mesmo valor devolvido em <c>traceId</c> e registrado nos logs, para correlação.</summary>
    public static string IdentificadorDeRastreio(HttpContext contexto) => Activity.Current?.Id ?? contexto.TraceIdentifier;
}
