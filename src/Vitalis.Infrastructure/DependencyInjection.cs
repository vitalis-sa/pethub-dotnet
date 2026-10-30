using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Oracle.EntityFrameworkCore.Infrastructure;
using Vitalis.Application.Abstracoes;
using Vitalis.Application.Historico;
using Vitalis.Infrastructure.Agendamento;
using Vitalis.Infrastructure.Configuracao;
using Vitalis.Infrastructure.Health;
using Vitalis.Infrastructure.Historico;
using Vitalis.Infrastructure.Integracao;
using Vitalis.Infrastructure.Mensageria;
using Vitalis.Infrastructure.Persistencia;
using Vitalis.Infrastructure.Persistencia.Repositorios;
using Vitalis.Infrastructure.Seguranca;

namespace Vitalis.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registra as implementações das portas da aplicação: Oracle, MongoDB, RabbitMQ, JWT, pethub-java, relógio e
    /// os health checks. Segredos e endereços vêm da configuração (variáveis de ambiente; veja o README).
    /// </summary>
    public static IServiceCollection AddInfraestrutura(this IServiceCollection servicos, IConfiguration configuracao)
    {
        AdicionarOpcoes(servicos, configuracao);
        AdicionarOracle(servicos, configuracao);
        AdicionarMongo(servicos, configuracao);
        AdicionarSeguranca(servicos);
        AdicionarIntegracoes(servicos);

        servicos.AddSingleton<IRelogio, RelogioDeSaoPaulo>();

        // Trabalhos em segundo plano: fila de lembretes e liberação das doses de hora em hora.
        servicos.AddHostedService<LembreteConsumidor>();
        servicos.AddHostedService<EnviadorDeLembretes>();

        servicos.AddHealthChecks()
            .AddCheck<BancoDadosHealthCheck>("banco_dados")
            .AddCheck<MongoHealthCheck>("mongodb")
            .AddCheck<ServicoExternoHealthCheck>("servico_externo");

        return servicos;
    }

    private static void AdicionarOpcoes(IServiceCollection servicos, IConfiguration configuracao)
    {
        servicos.Configure<JwtOpcoes>(configuracao.GetSection("Jwt"));
        servicos.Configure<RabbitOpcoes>(configuracao.GetSection("RabbitMq"));
        servicos.Configure<PethubJavaOpcoes>(configuracao.GetSection("PethubJava"));
        servicos.Configure<ServicoOpcoes>(configuracao.GetSection("ServicoAuth"));
        servicos.Configure<MongoOpcoes>(configuracao.GetSection("Mongo"));

        // Sem segredo válido a API nem sobe, em vez de falhar só no primeiro login.
        servicos.AddOptions<JwtOpcoes>()
            .Validate(o => ChaveDeAssinatura.SegredoValido(o.Segredo),
                $"Jwt:Segredo (variável Jwt__Segredo) precisa ter ao menos {ChaveDeAssinatura.TamanhoMinimoEmBytes} bytes.")
            .ValidateOnStart();
    }

    private static void AdicionarOracle(IServiceCollection servicos, IConfiguration configuracao)
    {
        servicos.AddDbContext<AppDbContext>(opcoes =>
            // Bancos Oracle anteriores ao 23ai não têm literal booleano: sem isso o EF gera "THEN True ELSE False"
            // em todo Any()/Exists e a consulta falha com ORA-00904: "FALSE": invalid identifier.
            opcoes.UseOracle(configuracao.GetConnectionString("OracleConnection"),
                oracle => oracle.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)));

        servicos.AddScoped<IResponsavelRepository, ResponsavelRepository>();
        servicos.AddScoped<IResponsavelEnderecoRepository, ResponsavelEnderecoRepository>();
        servicos.AddScoped<IResponsavelContatoRepository, ResponsavelContatoRepository>();
        servicos.AddScoped<ILembreteRepository, LembreteRepository>();
    }

    /// <summary>
    /// Histórico das mensagens da fila. Sem Mongo__ConnectionString ele fica desligado e o resto da API funciona
    /// normalmente (o /health mostra "Degraded" para o mongodb).
    /// </summary>
    private static void AdicionarMongo(IServiceCollection servicos, IConfiguration configuracao)
    {
        var mongo = configuracao.GetSection("Mongo").Get<MongoOpcoes>() ?? new MongoOpcoes();
        if (!mongo.Configurado)
        {
            servicos.AddSingleton<IHistoricoDeMensagens, HistoricoDesligado>();
            return;
        }

        servicos.AddSingleton<IMongoClient>(_ =>
        {
            var cliente = MongoClientSettings.FromConnectionString(mongo.ConnectionString);
            cliente.ServerSelectionTimeout = TimeSpan.FromSeconds(mongo.TimeoutSegundos);
            cliente.ConnectTimeout = TimeSpan.FromSeconds(mongo.TimeoutSegundos);
            return new MongoClient(cliente);
        });
        servicos.AddSingleton<IHistoricoDeMensagens, HistoricoDeMensagensMongo>();
        servicos.AddHostedService<PreparadorDoHistorico>();
    }

    private static void AdicionarSeguranca(IServiceCollection servicos)
    {
        servicos.AddSingleton<IEmissorDeToken, EmissorDeToken>();
        // Autenticação entre serviços: JWT HS256 assinado com o segredo de serviço.
        servicos.AddSingleton<IAutenticadorDeServico, AutenticadorDeServico>();
        servicos.AddSingleton<IHasherDeSenha, HasherBCrypt>();
    }

    private static void AdicionarIntegracoes(IServiceCollection servicos)
    {
        servicos.AddHttpClient<IPethubJavaClient, PethubJavaClient>((provedor, http) =>
        {
            var java = provedor.GetRequiredService<IOptions<PethubJavaOpcoes>>().Value;
            http.BaseAddress = new Uri(java.UrlBase);
            http.Timeout = TimeSpan.FromSeconds(java.TimeoutSegundos);
        });

        // HttpClient genérico usado pelo health check do serviço externo.
        servicos.AddHttpClient();
    }
}
