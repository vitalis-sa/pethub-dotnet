# Build em duas etapas: o SDK compila e publica, o runtime ASP.NET só executa.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restaura primeiro, para o cache do Docker aproveitar as dependências quando só o código muda.
COPY Vitalis.csproj ./
RUN dotnet restore Vitalis.csproj

COPY . ./
RUN dotnet publish Vitalis.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# O Serilog grava em logs/ dentro do contêiner; o usuário "app" precisa poder escrever ali.
RUN mkdir -p /app/logs && chown app:app /app/logs

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
USER app

ENTRYPOINT ["dotnet", "Vitalis.dll"]
