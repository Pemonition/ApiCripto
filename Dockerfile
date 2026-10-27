# ---------- Etapa 1: build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Copia só os .csproj primeiro: o restore fica em cache enquanto as dependências não mudarem
COPY global.json ./
COPY ApiCripto/ApiCripto.csproj ApiCripto/
RUN dotnet restore ApiCripto/ApiCripto.csproj
COPY ApiCripto/ ApiCripto/
RUN dotnet publish ApiCripto/ApiCripto.csproj -c Release -o /app --no-restore

# ---------- Etapa 2: runtime (imagem enxuta, sem SDK) ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .

# O SQLite fica numa pasta própria; num plano pago dá para montar um disco persistente aqui.
RUN mkdir -p /app/data && chown -R $APP_UID /app/data
USER $APP_UID

ENV ASPNETCORE_ENVIRONMENT=Production \
    ConnectionStrings__Default="Data Source=/app/data/apicripto.db" \
    PORT=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "ApiCripto.dll"]
