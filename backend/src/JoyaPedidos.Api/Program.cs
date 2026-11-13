using JoyaPedidos.Api.Compartilhado;
using JoyaPedidos.Api.Modulos.Catalogo;
using JoyaPedidos.Api.Modulos.Pedidos;

var builder = WebApplication.CreateBuilder(args);

// Falha rápida: sem o fuso do negócio, filtros por período ficariam errados (spec RSK-02).
builder.Services.AddSingleton(FusoHorarioNegocio.Carregar());
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(provedor => FabricaConexao.Criar(provedor.GetRequiredService<IConfiguration>()));
builder.Services.AddProblemDetailsPadronizados();
builder.Services.AddHealthChecks();
builder.Services.AddModuloPedidos();
builder.Services.AddModuloCatalogo();

var app = builder.Build();

// Falha rápida também sem a string de conexão (lida aqui, com a configuração já completa).
app.Services.GetRequiredService<FabricaConexao>();

app.UseExceptionHandler();
app.UseStatusCodePages();

// Spec §5.9: GET /health (HEAD também, por ser equivalente a GET sem corpo).
app.MapHealthChecks("/health").WithMetadata(new HttpMethodMetadata([HttpMethods.Get, HttpMethods.Head]));
app.MapPedidosEndpoints();
app.MapCatalogoEndpoints();

app.Run();
