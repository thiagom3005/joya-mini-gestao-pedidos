using System.Globalization;
using JoyaPedidos.Dominio;
using Microsoft.AspNetCore.Mvc;

namespace JoyaPedidos.Api.Compartilhado;

/// <summary>
/// Erro pronto para virar resposta HTTP no contrato da spec §5.2: status, <c>codigo</c>, título, detalhe e extensões
/// específicas (<c>errors</c>, <c>produtosNaoEncontrados</c>, <c>itensSemEstoque</c>).
/// </summary>
public sealed record ErroHttp(int Status, string Codigo, string Titulo, string Detalhe, IReadOnlyDictionary<string, object> Extensoes)
{
    /// <summary>Problem Details com <c>codigo</c> e extensões; o <c>traceId</c> é acrescentado pela padronização central.</summary>
    public ProblemDetails ParaProblemDetails()
    {
        var problema = new ProblemDetails { Status = Status, Title = Titulo, Detail = Detalhe };
        problema.Extensions[ConfiguracaoProblemDetails.ExtensaoCodigo] = Codigo;
        foreach (var (chave, valor) in Extensoes)
        {
            problema.Extensions[chave] = valor;
        }
        return problema;
    }
}

/// <summary>
/// Tradução central dos erros da aplicação para o contrato HTTP (spec §5.2, RN-12, §7.5). Funções puras.
/// Textos são fixos e nunca incluem mensagem de exceção, SQL ou detalhe de infraestrutura.
/// </summary>
public static class MapeadorErros
{
    /// <summary>Número do erro do SQL Server quando a transação é escolhida como vítima de deadlock.</summary>
    public const int ErroSqlDeadlock = 1205;

    private static readonly IReadOnlyDictionary<string, object> SemExtensoes = new Dictionary<string, object>();

    /// <summary>400 <c>VALIDACAO</c> com os erros por campo do validador de entrada.</summary>
    public static ErroHttp DeValidacao(IReadOnlyDictionary<string, string[]> erros)
    {
        ArgumentNullException.ThrowIfNull(erros);
        if (erros.Count == 0)
        {
            throw new ArgumentException("Não há erros de validação para mapear.", nameof(erros));
        }
        return new ErroHttp(
            StatusCodes.Status400BadRequest,
            CodigosErroHttp.Validacao,
            "Requisição inválida",
            "Um ou mais campos são inválidos.",
            new Dictionary<string, object> { ["errors"] = erros });
    }

    /// <summary>422 para cliente ou produtos inexistentes; 409 para estoque insuficiente.</summary>
    public static ErroHttp DeNegocio(ErroNegocio erro) => erro switch
    {
        ClienteNaoEncontrado cliente => new ErroHttp(
            StatusCodes.Status422UnprocessableEntity,
            CodigosErroHttp.ClienteNaoEncontrado,
            "Cliente não encontrado",
            string.Create(CultureInfo.InvariantCulture, $"O cliente {cliente.ClienteId} não existe."),
            SemExtensoes),
        ProdutosNaoEncontrados produtos => new ErroHttp(
            StatusCodes.Status422UnprocessableEntity,
            CodigosErroHttp.ProdutoNaoEncontrado,
            "Produto não encontrado",
            string.Create(CultureInfo.InvariantCulture, $"{produtos.ProdutoIds.Count} produto(s) informado(s) não existem."),
            new Dictionary<string, object> { ["produtosNaoEncontrados"] = produtos.ProdutoIds }),
        EstoqueInsuficiente estoque => new ErroHttp(
            StatusCodes.Status409Conflict,
            CodigosErroHttp.EstoqueInsuficiente,
            "Estoque insuficiente",
            string.Create(CultureInfo.InvariantCulture, $"Não há estoque suficiente para {estoque.Itens.Count} produto(s)."),
            new Dictionary<string, object> { ["itensSemEstoque"] = estoque.Itens }),
        null => throw new ArgumentNullException(nameof(erro)),
        _ => throw new ArgumentOutOfRangeException(nameof(erro), erro.GetType().Name, "Erro de negócio sem mapeamento HTTP."),
    };

    /// <summary>404 <c>PEDIDO_NAO_ENCONTRADO</c> para <c>GET /pedidos/{id}</c> com id numérico inexistente (spec §5.5).</summary>
    public static ErroHttp PedidoNaoEncontrado(int pedidoId) => new(
        StatusCodes.Status404NotFound,
        CodigosErroHttp.PedidoNaoEncontrado,
        "Pedido não encontrado",
        string.Create(CultureInfo.InvariantCulture, $"O pedido {pedidoId} não existe."),
        SemExtensoes);

    /// <summary>
    /// Erro do SQL Server pelo número (sem depender de <c>SqlException</c>): 1205 (vítima de deadlock) vira 503
    /// <c>CONFLITO_CONCORRENCIA</c>, sem retry automático (§7.5); qualquer outro vira 500 <c>ERRO_INTERNO</c>.
    /// </summary>
    public static ErroHttp DeErroSql(int numeroDoErro) => numeroDoErro == ErroSqlDeadlock
        ? new ErroHttp(
            StatusCodes.Status503ServiceUnavailable,
            CodigosErroHttp.ConflitoConcorrencia,
            "Conflito de concorrência",
            "Conflito momentâneo de concorrência. Tente novamente.",
            SemExtensoes)
        : ErroInterno();

    /// <summary>500 <c>ERRO_INTERNO</c> genérico.</summary>
    public static ErroHttp ErroInterno() => new(
        StatusCodes.Status500InternalServerError,
        CodigosErroHttp.ErroInterno,
        "Erro interno",
        "Ocorreu um erro inesperado.",
        SemExtensoes);
}
