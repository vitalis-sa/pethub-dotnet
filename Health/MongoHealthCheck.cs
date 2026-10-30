using System.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Vitalis.Configuracao;

namespace Vitalis.Health;

/// <summary>
/// Envia um <c>ping</c> ao MongoDB. Sem MongoDB configurado o resultado é <c>Degraded</c>: a API funciona,
/// só o histórico de mensagens fica desligado.
/// </summary>
public class MongoHealthCheck(IServiceProvider servicos, IOptions<MongoOpcoes> opcoes) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var cliente = servicos.GetService<IMongoClient>();
        if (cliente is null)
            return HealthCheckResult.Degraded(
                "MongoDB não configurado (Mongo__ConnectionString): histórico de mensagens desligado.");

        var cronometro = Stopwatch.StartNew();
        try
        {
            await cliente.GetDatabase(opcoes.Value.Banco)
                .RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: cancellationToken);
            cronometro.Stop();

            return HealthCheckResult.Healthy("Conexão com o MongoDB estabelecida com sucesso.",
                new Dictionary<string, object>
                {
                    { "LatenciaMs", cronometro.ElapsedMilliseconds },
                    { "Banco", opcoes.Value.Banco }
                });
        }
        catch (Exception ex)
        {
            cronometro.Stop();
            return HealthCheckResult.Unhealthy("Falha ao conectar no MongoDB.", ex,
                new Dictionary<string, object> { { "LatenciaMs", cronometro.ElapsedMilliseconds } });
        }
    }
}
