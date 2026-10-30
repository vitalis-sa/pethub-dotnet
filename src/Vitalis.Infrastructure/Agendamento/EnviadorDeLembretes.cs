using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vitalis.Application.Servicos;

namespace Vitalis.Infrastructure.Agendamento;

/// <summary>
/// Motor dos lembretes de medicamento: de hora em hora libera as doses cujo horário já chegou
/// (PENDENTE vira ENVIADO), e só então elas aparecem para o responsável. Se o serviço ficou parado,
/// a próxima rodada libera tudo o que venceu nesse intervalo.
/// </summary>
public class EnviadorDeLembretes(IServiceScopeFactory escopos, ILogger<EnviadorDeLembretes> logger) : BackgroundService
{
    public static readonly TimeSpan Intervalo = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken parada)
    {
        using var relogio = new PeriodicTimer(Intervalo);
        do
        {
            LiberarVencidos();
        }
        while (await ProximaRodadaAsync(relogio, parada));
    }

    private void LiberarVencidos()
    {
        try
        {
            using var escopo = escopos.CreateScope();
            escopo.ServiceProvider.GetRequiredService<IServicoDeLembretes>().LiberarDosesVencidas();
        }
        catch (Exception ex)
        {
            // Banco fora do ar: a próxima rodada tenta de novo.
            logger.LogError(ex, "Falha ao liberar os lembretes vencidos; nova tentativa na próxima hora.");
        }
    }

    private static async Task<bool> ProximaRodadaAsync(PeriodicTimer relogio, CancellationToken parada)
    {
        try { return await relogio.WaitForNextTickAsync(parada); }
        catch (OperationCanceledException) { return false; }
    }
}
