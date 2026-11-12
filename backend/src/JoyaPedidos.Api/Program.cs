using JoyaPedidos.Api.Compartilhado;

var builder = WebApplication.CreateBuilder(args);

// Falha rápida: sem o fuso do negócio, filtros por período ficariam errados (spec RSK-02).
builder.Services.AddSingleton(FusoHorarioNegocio.Carregar());
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddProblemDetailsPadronizados();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

// Spec §5.9: GET /health (HEAD também, por ser equivalente a GET sem corpo).
app.MapHealthChecks("/health").WithMetadata(new HttpMethodMetadata([HttpMethods.Get, HttpMethods.Head]));

app.Run();
