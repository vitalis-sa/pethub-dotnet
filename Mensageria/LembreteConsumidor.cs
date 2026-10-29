using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Vitalis.Configuracao;

namespace Vitalis.Mensageria;

/// <summary>
/// Consome a fila de lembretes publicada pelo pethub-java. Só o .NET consome: o lembrete é do responsável.
/// Declara exchange, fila e ligação (operação idempotente, igual à do Java), então a ordem de subida dos
/// serviços não importa. Só confirma a mensagem depois de gravada.
/// </summary>
public class LembreteConsumidor(
    IServiceScopeFactory escopos,
    IOptions<RabbitOpcoes> opcoes,
    ILogger<LembreteConsumidor> logger) : BackgroundService
{
    private static readonly TimeSpan EsperaParaReconectar = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan EsperaParaRepetir = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken parada)
    {
        var rabbit = opcoes.Value;
        if (!rabbit.Configurado)
        {
            logger.LogWarning("RabbitMq:Host não configurado: o consumidor de lembretes ficou desligado.");
            return;
        }

        while (!parada.IsCancellationRequested)
        {
            try
            {
                await ConsumirAsync(rabbit, parada);
            }
            catch (OperationCanceledException) when (parada.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Consumidor de lembretes sem conexão com o RabbitMQ; nova tentativa em {Segundos}s.",
                    EsperaParaReconectar.TotalSeconds);
                await EsperarAsync(EsperaParaReconectar, parada);
            }
        }
    }

    private async Task ConsumirAsync(RabbitOpcoes rabbit, CancellationToken parada)
    {
        var fabrica = new ConnectionFactory
        {
            HostName = rabbit.Host,
            Port = rabbit.Porta,
            UserName = rabbit.Usuario,
            Password = rabbit.Senha,
            VirtualHost = rabbit.VirtualHost,
            Ssl = new SslOption(rabbit.Host, enabled: rabbit.Ssl)
        };

        await using var conexao = await fabrica.CreateConnectionAsync(parada);
        await using var canal = await conexao.CreateChannelAsync(cancellationToken: parada);

        await canal.ExchangeDeclareAsync(rabbit.Exchange, ExchangeType.Direct, durable: true, cancellationToken: parada);
        await canal.QueueDeclareAsync(rabbit.Fila, durable: true, exclusive: false, autoDelete: false, cancellationToken: parada);
        await canal.QueueBindAsync(rabbit.Fila, rabbit.Exchange, rabbit.RoutingKey, cancellationToken: parada);
        await canal.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: parada);

        var consumidor = new AsyncEventingBasicConsumer(canal);
        consumidor.ReceivedAsync += (_, entrega) => TratarAsync(canal, entrega, parada);
        await canal.BasicConsumeAsync(rabbit.Fila, autoAck: false, consumer: consumidor, cancellationToken: parada);

        logger.LogInformation("Consumindo a fila {Fila}.", rabbit.Fila);
        await Task.Delay(Timeout.Infinite, parada);
    }

    private async Task TratarAsync(IChannel canal, BasicDeliverEventArgs entrega, CancellationToken parada)
    {
        ResultadoDaMensagem resultado;
        using (var escopo = escopos.CreateScope())
        {
            resultado = escopo.ServiceProvider.GetRequiredService<IProcessadorDeLembrete>().Processar(entrega.Body.Span);
        }

        switch (resultado)
        {
            case ResultadoDaMensagem.Confirmar:
                await canal.BasicAckAsync(entrega.DeliveryTag, multiple: false, parada);
                break;
            case ResultadoDaMensagem.Descartar:
                await canal.BasicRejectAsync(entrega.DeliveryTag, requeue: false, parada);
                break;
            default:
                await EsperarAsync(EsperaParaRepetir, parada);
                await canal.BasicNackAsync(entrega.DeliveryTag, multiple: false, requeue: true, parada);
                break;
        }
    }

    private static async Task EsperarAsync(TimeSpan tempo, CancellationToken parada)
    {
        try { await Task.Delay(tempo, parada); }
        catch (OperationCanceledException) { /* encerrando */ }
    }
}
