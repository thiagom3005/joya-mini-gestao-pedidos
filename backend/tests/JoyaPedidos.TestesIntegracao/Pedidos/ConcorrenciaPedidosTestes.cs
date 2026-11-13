using System.Net;
using System.Net.Http.Json;
using JoyaPedidos.TestesIntegracao.Infraestrutura;

namespace JoyaPedidos.TestesIntegracao.Pedidos;

/// <summary>
/// Concorrência na criação de pedidos (spec §7): IT-09 e IT-10 com largada simultânea, deadlock real traduzido para
/// 503 (§7.5) e cancelamento da requisição durante a espera por lock (§7.2). Zero 500 é requisito; zero 503 nos
/// cenários controlados é objetivo assertado: um 503 inesperado reprova o teste e é investigado, nunca mascarado.
/// </summary>
public sealed class ConcorrenciaPedidosTestes(FabricaApi fabrica) : IAsyncLifetime
{
    private const int RequisicoesSimultaneas = 20;

    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private readonly DadosDeTeste _dados = new(fabrica.Banco.StringDeConexao);
    private readonly HttpClient _cliente = fabrica.CreateClient();

    public async ValueTask InitializeAsync() => await fabrica.LimparBancoAsync(Cancelamento);

    public ValueTask DisposeAsync()
    {
        _cliente.Dispose();
        return ValueTask.CompletedTask;
    }

    private static object Pedido(int clienteId, params (int ProdutoId, int Quantidade)[] itens) =>
        new { clienteId, itens = itens.Select(item => new { produtoId = item.ProdutoId, quantidade = item.Quantidade }).ToArray() };

    // IT-09
    [Fact]
    public async Task CriarPedido_VinteRequisicoesSimultaneasComEstoque5_Exatamente5CriadosE15Recusados()
    {
        var bruno = await _dados.InserirClienteAsync("Bruno Lima");
        var disputado = await _dados.InserirProdutoAsync("Produto disputado", 10.00m, 5);
        var esperasPorLockAntes = await EsperasPorLockDeAtualizacaoAsync();

        var respostas = await DispararJuntasAsync(Enumerable.Repeat(Pedido(bruno, (disputado, 1)), RequisicoesSimultaneas));

        await AssertarSemErroDeServidorAsync(respostas);
        Assert.Equal(5, respostas.Count(resposta => resposta.StatusCode == HttpStatusCode.Created));
        var recusadas = respostas.Where(resposta => resposta.StatusCode == HttpStatusCode.Conflict).ToList();
        Assert.Equal(15, recusadas.Count);
        foreach (var recusada in recusadas)
        {
            var problema = await RespostaHttp.LerProblemaAsync(recusada, HttpStatusCode.Conflict, "ESTOQUE_INSUFICIENTE");
            var item = Assert.Single(problema.GetProperty("itensSemEstoque").EnumerateArray().ToList());
            Assert.Equal(0, item.GetProperty("estoqueDisponivel").GetInt32()); // lido sob lock, depois das 5 vendas
        }
        Assert.Equal(0, await _dados.EstoqueAsync(disputado));
        Assert.Equal(5, await _dados.ContarPedidosAsync());
        Assert.Equal(5, await _dados.ContarItensAsync());
        // Prova de que houve disputa real: requisições esperaram pelo lock U do produto.
        Assert.True(await EsperasPorLockDeAtualizacaoAsync() > esperasPorLockAntes, "Nenhuma requisição esperou pelo lock U.");
    }

    // IT-10
    [Fact]
    public async Task CriarPedido_PedidosCruzadosEmOrdensInversasPorTresRodadas_TodosCriadosComEstoquesExatos()
    {
        var bruno = await _dados.InserirClienteAsync("Bruno Lima");
        var produto1 = await _dados.InserirProdutoAsync("Produto 1", 10.00m, 1_000);
        var produto2 = await _dados.InserirProdutoAsync("Produto 2", 20.00m, 1_000);
        const int Rodadas = 3;

        for (var rodada = 1; rodada <= Rodadas; rodada++)
        {
            // Metade apresenta [P1, P2] e metade [P2, P1]; o serviço bloqueia sempre em ordem crescente de Id.
            var corpos = Enumerable.Range(0, RequisicoesSimultaneas).Select(indice => indice % 2 == 0
                ? Pedido(bruno, (produto1, 1), (produto2, 2))
                : Pedido(bruno, (produto2, 2), (produto1, 1)));

            var respostas = await DispararJuntasAsync(corpos);

            await AssertarSemErroDeServidorAsync(respostas);
            Assert.All(respostas, resposta => Assert.Equal(HttpStatusCode.Created, resposta.StatusCode));
        }

        const int TotalDePedidos = Rodadas * RequisicoesSimultaneas;
        Assert.Equal(1_000 - TotalDePedidos * 1, await _dados.EstoqueAsync(produto1));
        Assert.Equal(1_000 - TotalDePedidos * 2, await _dados.EstoqueAsync(produto2));
        Assert.Equal(TotalDePedidos, await _dados.ContarPedidosAsync());
        Assert.Equal(TotalDePedidos * 2, await _dados.ContarItensAsync());
    }

    // §7.5: o SQL Server escolhe a API como vítima de um deadlock real; a resposta é 503, sem retry e sem gravar nada.
    [Fact]
    public async Task CriarPedido_EscolhidoComoVitimaDeDeadlock_Retorna503ConflitoConcorrenciaSemGravarNada()
    {
        var bruno = await _dados.InserirClienteAsync("Bruno Lima");
        var produtoA = await _dados.InserirProdutoAsync("Produto A", 10.00m, 10);
        var produtoB = await _dados.InserirProdutoAsync("Produto B", 20.00m, 10);
        await using var sessao = await SessaoDeBloqueio.AbrirAsync(fabrica.Banco.StringDeConexao, prioridadeAltaEmDeadlock: true);
        await sessao.BloquearProdutoAsync(produtoB, Cancelamento);
        var deadlocksAntes = await DeadlocksDoServidorAsync();

        // Pedido apresentado como [B, A]: a API bloqueia A primeiro (menor Id) e fica esperando B, que está com a sessão.
        var envio = _cliente.PostAsJsonAsync("/pedidos", Pedido(bruno, (produtoB, 1), (produtoA, 1)), Cancelamento);
        await sessao.EsperarSessaoBloqueadaPorEstaAsync();
        // A sessão pede A: o ciclo se fecha e o monitor de deadlocks escolhe a API (prioridade normal) como vítima.
        var fechamentoDoCiclo = sessao.BloquearProdutoAsync(produtoA, Cancelamento);

        using var resposta = await envio;
        await fechamentoDoCiclo;
        await sessao.DesfazerAsync();

        var problema = await RespostaHttp.LerProblemaAsync(resposta, HttpStatusCode.ServiceUnavailable, "CONFLITO_CONCORRENCIA");
        Assert.Equal("Conflito momentâneo de concorrência. Tente novamente.", problema.GetProperty("detail").GetString());
        Assert.True(await DeadlocksDoServidorAsync() > deadlocksAntes, "O SQL Server não registrou deadlock.");
        Assert.Equal(10, await _dados.EstoqueAsync(produtoA));
        Assert.Equal(10, await _dados.EstoqueAsync(produtoB));
        Assert.Equal(0, await _dados.ContarPedidosAsync());
    }

    // §7.2: cancelar a requisição enquanto ela espera um lock desfaz a transação e libera o lock que ela já tinha.
    [Fact]
    public async Task CriarPedido_RequisicaoCanceladaEnquantoEsperaLock_DesfazATransacaoELiberaOsLocks()
    {
        var bruno = await _dados.InserirClienteAsync("Bruno Lima");
        var produtoA = await _dados.InserirProdutoAsync("Produto A", 10.00m, 10);
        var produtoB = await _dados.InserirProdutoAsync("Produto B", 20.00m, 10);
        await using var sessao = await SessaoDeBloqueio.AbrirAsync(fabrica.Banco.StringDeConexao);
        await sessao.BloquearProdutoAsync(produtoB, Cancelamento);
        using var cancelamentoDoCliente = CancellationTokenSource.CreateLinkedTokenSource(Cancelamento);

        // Pedido apresentado como [B, A]; a ordem de bloqueio é a dos Ids, não a da requisição.
        var envio = _cliente.PostAsJsonAsync("/pedidos", Pedido(bruno, (produtoB, 1), (produtoA, 1)), cancelamentoDoCliente.Token);
        await sessao.EsperarSessaoBloqueadaPorEstaAsync();
        // Durante a espera: a API já segura A e ainda não baixou nada (a espera é na leitura sob lock de B, §7.2).
        Assert.False(await SessaoDeBloqueio.ProdutoLivreAsync(fabrica.Banco.StringDeConexao, produtoA));
        Assert.Equal(10, await _dados.EstoqueSemRespeitarLocksAsync(produtoA));

        await cancelamentoDoCliente.CancelAsync();

        // A API desiste de esperar B por causa do cancelamento. O prazo curto, contado a partir do cancelamento, exclui o
        // tempo limite de comando (30 s), que também liberaria a espera se o cancelamento fosse ignorado (revisão da E5).
        // A espera vem antes de aguardar o envio: no TestServer, a tarefa cancelada do cliente só termina junto com o
        // processamento no servidor.
        await sessao.EsperarNenhumaSessaoBloqueadaPorEstaAsync(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => envio);
        // B continua com a sessão; A só fica livre se a transação da API foi desfeita.
        Assert.True(await SessaoDeBloqueio.ProdutoLivreAsync(fabrica.Banco.StringDeConexao, produtoA));
        await sessao.DesfazerAsync();
        Assert.Equal(10, await _dados.EstoqueAsync(produtoA));
        Assert.Equal(10, await _dados.EstoqueAsync(produtoB));
        Assert.Equal(0, await _dados.ContarPedidosAsync());
    }

    // Largada simultânea: todas as requisições esperam a mesma barreira e são liberadas juntas no pool de threads.
    private async Task<HttpResponseMessage[]> DispararJuntasAsync(IEnumerable<object> corpos)
    {
        var largada = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var envios = corpos.Select(async corpo =>
        {
            await largada.Task;
            return await _cliente.PostAsJsonAsync("/pedidos", corpo, Cancelamento);
        }).ToList();
        largada.SetResult();
        return await Task.WhenAll(envios);
    }

    // Falha com o corpo (e o traceId) de cada 500 ou 503, para a investigação exigida pela §7.5.
    private static async Task AssertarSemErroDeServidorAsync(IEnumerable<HttpResponseMessage> respostas)
    {
        var errosDeServidor = new List<string>();
        foreach (var resposta in respostas.Where(resposta => (int)resposta.StatusCode >= 500))
        {
            errosDeServidor.Add($"{(int)resposta.StatusCode}: {await RespostaHttp.LerJsonAsync(resposta)}");
        }
        Assert.True(errosDeServidor.Count == 0, string.Join(Environment.NewLine, errosDeServidor));
    }

    // Contador cumulativo do servidor, apesar do nome "/sec".
    private Task<long> DeadlocksDoServidorAsync() => ConsultaSql.EscalarAsync<long>(
        fabrica.Banco.StringDeConexao,
        "SELECT cntr_value FROM sys.dm_os_performance_counters WHERE counter_name = N'Number of Deadlocks/sec' AND instance_name = N'_Total';",
        Cancelamento);

    private Task<long> EsperasPorLockDeAtualizacaoAsync() => ConsultaSql.EscalarAsync<long>(
        fabrica.Banco.StringDeConexao,
        "SELECT waiting_tasks_count FROM sys.dm_os_wait_stats WHERE wait_type = N'LCK_M_U';",
        Cancelamento);
}
