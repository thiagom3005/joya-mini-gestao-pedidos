namespace JoyaPedidos.Dominio;

/// <summary>
/// Regras puras da criação de pedido (spec §3, §3.1). Recebem dados já lidos (cliente, produtos bloqueados, instante
/// atual) e não acessam banco, relógio ou HTTP. A atomicidade (RN-10) e a baixa de estoque (RN-07) são da orquestração
/// transacional (E5); aqui só se decide se o pedido pode existir e com quais valores.
/// </summary>
public static class RegrasPedido
{
    /// <summary>RN-01: o cliente lido precisa existir. Chamada antes de qualquer bloqueio de produto (spec §3.1).</summary>
    public static Resultado<Cliente> VerificarCliente(int clienteId, Cliente? cliente) =>
        cliente is null
            ? Resultado<Cliente>.Falha(new ClienteNaoEncontrado(clienteId))
            : Resultado<Cliente>.Ok(cliente);

    /// <summary>
    /// Cria o pedido a partir dos itens solicitados e dos produtos lidos sob bloqueio.
    /// Precedência (RN-12): produtos inexistentes (RN-05) vencem estoque insuficiente (RN-06); cada falha lista todas as
    /// suas ocorrências. Em caso de sucesso, o preço unitário é o preço lido (RN-08), a data é <paramref name="agora"/>
    /// em UTC truncado a segundos (RN-09) e os valores são <c>decimal</c> exatos (RN-11).
    /// </summary>
    /// <remarks>
    /// Recebe entrada já validada: as regras de formato (RN-02 a RN-04) ficam só no validador e não se repetem aqui
    /// (spec §3.1). <paramref name="produtosBloqueados"/> traz no máximo um produto por Id (um <c>SELECT</c> por Id na
    /// orquestração); produtos que não foram solicitados são ignorados.
    /// </remarks>
    public static Resultado<Pedido> CriarPedido(
        Cliente cliente,
        IReadOnlyCollection<ItemSolicitado> itensSolicitados,
        IReadOnlyCollection<Produto> produtosBloqueados,
        DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(cliente);
        ArgumentNullException.ThrowIfNull(itensSolicitados);
        ArgumentNullException.ThrowIfNull(produtosBloqueados);

        var produtosPorId = produtosBloqueados.ToDictionary(produto => produto.Id);

        var ausentes = itensSolicitados
            .Where(item => !produtosPorId.ContainsKey(item.ProdutoId))
            .Select(item => item.ProdutoId)
            .Order()
            .ToList();
        if (ausentes.Count > 0)
        {
            return Resultado<Pedido>.Falha(new ProdutosNaoEncontrados(ausentes));
        }

        var semEstoque = itensSolicitados
            .Select(item => (Item: item, Produto: produtosPorId[item.ProdutoId]))
            .Where(par => par.Item.Quantidade > par.Produto.Estoque)
            .Select(par => new ItemSemEstoque(par.Produto.Id, par.Produto.Nome, par.Item.Quantidade, par.Produto.Estoque))
            .OrderBy(item => item.ProdutoId)
            .ToList();
        if (semEstoque.Count > 0)
        {
            return Resultado<Pedido>.Falha(new EstoqueInsuficiente(semEstoque));
        }

        var itens = itensSolicitados.Select(item =>
        {
            var produto = produtosPorId[item.ProdutoId];
            return new ItemPedido(produto.Id, produto.Nome, item.Quantidade, produto.Preco);
        });

        return Resultado<Pedido>.Ok(new Pedido(cliente, TruncarASegundosEmUtc(agora), itens));
    }

    /// <summary>RN-09: instante em UTC (deslocamento zero) sem a fração de segundo.</summary>
    public static DateTimeOffset TruncarASegundosEmUtc(DateTimeOffset instante)
    {
        var utc = instante.ToUniversalTime();
        return new DateTimeOffset(utc.Ticks - utc.Ticks % TimeSpan.TicksPerSecond, TimeSpan.Zero);
    }
}
