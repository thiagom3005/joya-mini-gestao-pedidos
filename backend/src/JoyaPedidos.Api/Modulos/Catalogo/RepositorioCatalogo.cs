using Dapper;
using JoyaPedidos.Api.Compartilhado;
using JoyaPedidos.Api.Modulos.Catalogo.Contratos;

namespace JoyaPedidos.Api.Modulos.Catalogo;

/// <summary>
/// Leitura do catálogo (spec §5.7–5.8, DEC-09): listas completas, sem filtros nem paginação, em ordem total por nome e
/// depois id. Só leitura: o catálogo não tem operação de escrita pela API.
/// </summary>
public sealed class RepositorioCatalogo(FabricaConexao fabricaConexao)
{
    public async Task<IReadOnlyList<ClienteCatalogoResposta>> ListarClientesAsync(CancellationToken cancelamento)
    {
        await using var conexao = await fabricaConexao.AbrirAsync(cancelamento);
        var clientes = await conexao.QueryAsync<ClienteCatalogoResposta>(new CommandDefinition(
            "SELECT Id, Nome, Email FROM dbo.Clientes ORDER BY Nome, Id;",
            cancellationToken: cancelamento));
        return clientes.AsList();
    }

    /// <summary>Inclui produtos com estoque 0: a tela de criação mostra o produto esgotado em vez de escondê-lo.</summary>
    public async Task<IReadOnlyList<ProdutoCatalogoResposta>> ListarProdutosAsync(CancellationToken cancelamento)
    {
        await using var conexao = await fabricaConexao.AbrirAsync(cancelamento);
        var produtos = await conexao.QueryAsync<ProdutoCatalogoResposta>(new CommandDefinition(
            "SELECT Id, Nome, Preco, Estoque FROM dbo.Produtos ORDER BY Nome, Id;",
            cancellationToken: cancelamento));
        return produtos.AsList();
    }
}
