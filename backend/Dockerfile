# API .NET 10 (spec §11.1): build multi-stage, SDK → runtime ASP.NET com tzdata, usuário não root, porta 8080.
# Contexto de build: a pasta backend/ (docker-compose.yml: build.context ./backend).
#
# Imagens oficiais com versão e distribuição fixadas (spec §11.7). Ubuntu 24.04 (noble), não chiseled: o runtime
# precisa de /usr/share/zoneinfo para America/Sao_Paulo (RSK-02) e a API falha na inicialização sem ele.
# O global.json da raiz fica fora deste contexto: pede SDK 10.0.100 com rollForward latestFeature, e o 10.0.404
# atende; a seção "test" dele só vale para o dotnet test.

FROM mcr.microsoft.com/dotnet/sdk:10.0.404-noble AS build
WORKDIR /src

# Restore em camada própria: só muda quando projetos, versões centrais ou fontes de pacotes mudam.
COPY nuget.config Directory.Build.props Directory.Packages.props ./
COPY src/JoyaPedidos.Dominio/JoyaPedidos.Dominio.csproj src/JoyaPedidos.Dominio/
COPY src/JoyaPedidos.Api/JoyaPedidos.Api.csproj src/JoyaPedidos.Api/
RUN dotnet restore src/JoyaPedidos.Api/JoyaPedidos.Api.csproj

COPY src/ src/
RUN dotnet publish src/JoyaPedidos.Api/JoyaPedidos.Api.csproj \
        --configuration Release \
        --no-restore \
        --output /app \
        -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.15-noble AS runtime
WORKDIR /app

# Só a saída do publish: sem SDK, código-fonte, testes ou credenciais. A string de conexão chega pelo ambiente
# (ConnectionStrings__BancoPedidos, definida no docker-compose.yml).
COPY --from=build /app ./

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# Usuário sem privilégios que a imagem oficial já traz (app, UID 1654).
USER $APP_UID

ENTRYPOINT ["dotnet", "JoyaPedidos.Api.dll"]
