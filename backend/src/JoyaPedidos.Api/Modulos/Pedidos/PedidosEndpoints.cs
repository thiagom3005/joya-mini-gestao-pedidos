using JoyaPedidos.Api.Compartilhado;
using JoyaPedidos.Api.Modulos.Pedidos.Contratos;
using JoyaPedidos.Dominio;

namespace JoyaPedidos.Api.Modulos.Pedidos;

/// <summary>
/// Rotas de pedidos (spec §5.3). Só mapeiam HTTP: validam o formato, chamam serviço ou repositório e convertem
/// o resultado. Erros saem como Problem Details pelo <see cref="MapeadorErros"/>.
/// </summary>
public static class PedidosEndpoints
{
    public static IServiceCollection AddModuloPedidos(this IServiceCollection servicos) => servicos
        .AddSingleton<RepositorioPedidos>()
        .AddSingleton<ServicoPedidos>();

    public static IEndpointRouteBuilder MapPedidosEndpoints(this IEndpointRouteBuilder rotas)
    {
        var pedidos = rotas.MapGroup("/pedidos");
        pedidos.MapPost("", CriarAsync);
        return rotas;
    }

    // Corpo opcional: ausente ou "null" chega como null e vira VALIDACAO, não erro do framework.
    private static async Task<IResult> CriarAsync(CriarPedidoRequisicao? requisicao, ServicoPedidos servico, CancellationToken cancelamento)
    {
        var errosDeFormato = ValidadorCriarPedido.Validar(requisicao);
        if (errosDeFormato.Count > 0)
        {
            return Problema(MapeadorErros.DeValidacao(errosDeFormato));
        }

        var (clienteId, itens) = ParaEntradaValidada(requisicao!);
        var resultado = await servico.CriarAsync(clienteId, itens, cancelamento);
        return resultado.Sucesso
            ? TypedResults.Created($"/pedidos/{resultado.Valor.Id}", resultado.Valor)
            : Problema(MapeadorErros.DeNegocio(resultado.Erro));
    }

    // O validador já garantiu cliente, itens, produtos e quantidades presentes: os valores são lidos sem nova checagem.
    private static (int ClienteId, IReadOnlyCollection<ItemSolicitado> Itens) ParaEntradaValidada(CriarPedidoRequisicao requisicao) =>
        (requisicao.ClienteId!.Value,
         requisicao.Itens!.Select(item => new ItemSolicitado(item!.ProdutoId!.Value, item.Quantidade!.Value)).ToList());

    private static IResult Problema(ErroHttp erro) => TypedResults.Problem(erro.ParaProblemDetails());
}
