using JoyaPedidos.TestesIntegracao.Infraestrutura;
using Xunit.Sdk;
using Xunit.v3;

// Banco compartilhado entre as classes de teste (a partir da E3): execução sequencial (spec §10.2, RSK-12).
// No xUnit 4.x, CollectionBehavior.DisableTestParallelization é obsoleto; a forma atual é Parallelization.Mode.
[assembly: Parallelization(Mode = ParallelMode.None)]

// Uma única instância da API (e, a partir da E3, do container SQL Server) para toda a suíte.
[assembly: AssemblyFixture(typeof(FabricaApi))]
