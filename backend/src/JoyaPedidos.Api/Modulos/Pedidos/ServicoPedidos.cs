using System.Data;
using JoyaPedidos.Api.Compartilhado;
using JoyaPedidos.Api.Modulos.Pedidos.Contratos;
using JoyaPedidos.Dominio;
using Microsoft.Data.SqlClient;

namespace JoyaPedidos.Api.Modulos.Pedidos;

/// <summary>
/// Orquestração transacional da criação de pedido (spec §3.1 e §7.2). As decisões são das regras puras do domínio;
/// aqui ficam a transação, a ordem dos locks e a gravação.
/// </summary>
public sealed class ServicoPedidos(FabricaConexao fabricaConexao, RepositorioPedidos repositorio, TimeProvider relogio)
{
    /// <summary>
    /// Cria o pedido numa única transação READ COMMITTED: cliente, produtos bloqueados em ordem crescente de Id, regras
    /// de domínio, baixas condicionais na mesma ordem, cabeçalho e itens, commit. Recebe itens já validados (RN-02..04).
    /// A transação é descartada sem commit em qualquer retorno antecipado, exceção ou cancelamento, o que a desfaz por
    /// completo (RN-10). Não há retry: a vítima de deadlock (erro SQL 1205) chega ao tratador central como 503 (§7.5).
    /// </summary>
    public async Task<Resultado<PedidoResposta>> CriarAsync(
        int clienteId,
        IReadOnlyCollection<ItemSolicitado> itensSolicitados,
        CancellationToken cancelamento)
    {
        try
        {
            return await CriarNaTransacaoAsync(clienteId, itensSolicitados, cancelamento);
        }
        catch (BaixaDeEstoqueRecusadaException recusa)
        {
            // Aqui a transação já foi descartada sem commit: a conversão para 409 acontece depois do rollback.
            return Resultado<PedidoResposta>.Falha(recusa.Erro);
        }
    }

    private async Task<Resultado<PedidoResposta>> CriarNaTransacaoAsync(
        int clienteId,
        IReadOnlyCollection<ItemSolicitado> itensSolicitados,
        CancellationToken cancelamento)
    {
        await using var conexao = await fabricaConexao.AbrirAsync(cancelamento);
        await using var transacao = (SqlTransaction)await conexao.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancelamento);

        var cliente = RegrasPedido.VerificarCliente(
            clienteId, await repositorio.ObterClienteAsync(clienteId, transacao, cancelamento));
        if (!cliente.Sucesso)
        {
            return Resultado<PedidoResposta>.Falha(cliente.Erro);
        }

        var produtosBloqueados = await BloquearProdutosEmOrdemAsync(itensSolicitados, transacao, cancelamento);
        var criacao = RegrasPedido.CriarPedido(cliente.Valor, itensSolicitados, produtosBloqueados, relogio.GetUtcNow());
        if (!criacao.Sucesso)
        {
            return Resultado<PedidoResposta>.Falha(criacao.Erro);
        }

        var pedido = criacao.Valor;
        await BaixarEstoquesAsync(pedido, transacao, cancelamento);

        var dataPedidoUtc = pedido.DataPedido.UtcDateTime;
        var pedidoId = await repositorio.InserirPedidoAsync(pedido.Cliente.Id, dataPedidoUtc, transacao, cancelamento);
        await repositorio.InserirItensAsync(pedidoId, pedido.Itens, transacao, cancelamento);
        await transacao.CommitAsync(cancelamento);

        return Resultado<PedidoResposta>.Ok(PedidoResposta.De(pedidoId, pedido.Cliente, dataPedidoUtc, pedido.Itens));
    }

    // Um SELECT ... WITH (UPDLOCK, ROWLOCK) por produto, em sequência e em ordem crescente de Id (§7.2, passo 3).
    // Produtos inexistentes simplesmente não entram na lista; a regra de domínio os reporta (RN-05).
    private async Task<List<Produto>> BloquearProdutosEmOrdemAsync(
        IEnumerable<ItemSolicitado> itensSolicitados, SqlTransaction transacao, CancellationToken cancelamento)
    {
        var produtos = new List<Produto>();
        foreach (var produtoId in itensSolicitados.Select(item => item.ProdutoId).Order())
        {
            if (await repositorio.BloquearProdutoAsync(produtoId, transacao, cancelamento) is { } produto)
            {
                produtos.Add(produto);
            }
        }
        return produtos;
    }

    // Baixas na mesma ordem crescente dos locks (os itens do pedido já vêm ordenados por produto). Uma baixa que não
    // afeta linha (barreira defensiva, §7.2, passo 5) lança exceção: a transação é desfeita, inclusive as baixas
    // anteriores, e só então o erro vira ESTOQUE_INSUFICIENTE.
    private async Task BaixarEstoquesAsync(Pedido pedido, SqlTransaction transacao, CancellationToken cancelamento)
    {
        foreach (var item in pedido.Itens)
        {
            if (!await repositorio.BaixarEstoqueAsync(item.ProdutoId, item.Quantidade, transacao, cancelamento))
            {
                var disponivel = await repositorio.LerEstoqueAsync(item.ProdutoId, transacao, cancelamento) ?? 0;
                throw new BaixaDeEstoqueRecusadaException(
                    new EstoqueInsuficiente([new ItemSemEstoque(item.ProdutoId, item.NomeProduto, item.Quantidade, disponivel)]));
            }
        }
    }

    // Exceção controlada da §7.2, passo 5. Não sai do serviço: CriarAsync a converte em Resultado.Falha (409), e o
    // tratador central nunca a vê como erro interno.
    private sealed class BaixaDeEstoqueRecusadaException(EstoqueInsuficiente erro)
        : Exception("A baixa condicional de estoque não afetou nenhuma linha.")
    {
        public EstoqueInsuficiente Erro { get; } = erro;
    }
}
