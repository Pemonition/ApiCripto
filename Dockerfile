# =============================================================================
# Build multi-stage.
#
# O primeiro estágio tem o SDK inteiro (~800 MB) para compilar. O segundo tem
# só o runtime ASP.NET (~220 MB) e recebe apenas os binários publicados.
#
# A imagem final não carrega compilador, código-fonte nem pacotes NuGet: além
# de menor e mais rápida de distribuir, ela reduz a superfície de ataque —
# quem entrar no container não encontra ferramenta de build para usar.
# =============================================================================

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia só os .csproj primeiro e restaura.
#
# Isso existe por causa do cache de camadas do Docker: enquanto nenhuma
# dependência mudar, o "dotnet restore" é reaproveitado do cache mesmo que
# todo o código tenha mudado. Copiando tudo de uma vez, cada alteração de uma
# linha em um controller obrigaria a baixar os pacotes de novo.
COPY Carteira.sln ./
COPY src/Carteira.Domain/Carteira.Domain.csproj                 src/Carteira.Domain/
COPY src/Carteira.Application/Carteira.Application.csproj       src/Carteira.Application/
COPY src/Carteira.Infrastructure/Carteira.Infrastructure.csproj src/Carteira.Infrastructure/
COPY src/Carteira.Api/Carteira.Api.csproj                       src/Carteira.Api/
COPY tests/Carteira.Tests/Carteira.Tests.csproj                 tests/Carteira.Tests/
RUN dotnet restore

# Agora sim o código.
COPY . .
RUN dotnet publish src/Carteira.Api/Carteira.Api.csproj -c Release -o /app/publish --no-restore

# -----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Usuário sem privilégios: se a aplicação for comprometida, o atacante não
# começa como root dentro do container.
RUN adduser --disabled-password --gecos "" carteira && chown -R carteira /app
USER carteira

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Carteira.Api.dll"]
