using JoyaPedidos.Api.Modulos.Catalogo.Contratos;
using Microsoft.AspNetCore.Http.HttpResults;

namespace JoyaPedidos.Api.Modulos.Catalogo;

/// <summary>Rotas auxiliares de leitura do catálogo (spec §5.7–5.8, §5.10): sem parâmetros, sem paginação, sem escrita.</summary>
public static class CatalogoEndpoints
{
    public static IServiceCollection AddModuloCatalogo(this IServiceCollection servicos) =>
        servicos.AddSingleton<RepositorioCatalogo>();

    public static IEndpointRouteBuilder MapCatalogoEndpoints(this IEndpointRouteBuilder rotas)
    {
        rotas.MapGet("/clientes", ListarClientesAsync);
        rotas.MapGet("/produtos", ListarProdutosAsync);
        return rotas;
    }

    private static async Task<Ok<IReadOnlyList<ClienteCatalogoResposta>>> ListarClientesAsync(
        RepositorioCatalogo repositorio, CancellationToken cancelamento) =>
        TypedResults.Ok(await repositorio.ListarClientesAsync(cancelamento));

    private static async Task<Ok<IReadOnlyList<ProdutoCatalogoResposta>>> ListarProdutosAsync(
        RepositorioCatalogo repositorio, CancellationToken cancelamento) =>
        TypedResults.Ok(await repositorio.ListarProdutosAsync(cancelamento));
}
