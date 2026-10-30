using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;
using Vitalis.Historico;
using Vitalis.Integracao;
using Vitalis.Mensageria;
using Vitalis.Models;
using Vitalis.Repositories;
using Vitalis.Seguranca;

namespace Vitalis.Tests.Integration.Fixtures;

public class VitalisWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string SegredoJwt = "segredo-jwt-de-integracao-com-mais-de-32-bytes";

    public JavaFalso Java { get; } = new();

    /// <summary>Histórico de mensagens no lugar do MongoDB.</summary>
    public HistoricoEmMemoria Historico { get; } = new();

    private readonly string _nomeDoBanco = $"vitalis-integracao-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting("ServicoAuth:Segredo", ChavesDeTeste.Segredo);
        builder.UseSetting("Jwt:Segredo", SegredoJwt);
        builder.UseSetting("Jwt:Emissor", "vitalis-integracao");
        builder.UseSetting("ServicosExternos:PethubJava", "http://127.0.0.1:9/health");

        builder.ConfigureServices(services =>
        {
            RemoverRegistroDoOracle(services);
            services.AddSingleton<IPethubJavaClient>(Java);
            services.AddSingleton<IHistoricoDeMensagens>(Historico);

            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_nomeDoBanco));
        });
    }

    private static void RemoverRegistroDoOracle(IServiceCollection services)
    {
        var registros = services
            .Where(descriptor =>
                descriptor.ServiceType == typeof(AppDbContext) ||
                descriptor.ServiceType == typeof(DbContextOptions) ||
                descriptor.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                EhConfiguracaoDeOpcoesDoContexto(descriptor.ServiceType))
            .ToList();

        foreach (var registro in registros)
            services.Remove(registro);
    }

    private static bool EhConfiguracaoDeOpcoesDoContexto(Type serviceType)
        => serviceType.IsGenericType
           && serviceType.GetGenericTypeDefinition().Name == "IDbContextOptionsConfiguration`1"
           && serviceType.GenericTypeArguments[0] == typeof(AppDbContext);

    public void LimparBanco()
    {
        Java.Reiniciar();
        Historico.Limpar();

        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        context.Lembretes.RemoveRange(context.Lembretes);
        context.Contatos.RemoveRange(context.Contatos);
        context.Enderecos.RemoveRange(context.Enderecos);
        context.Responsavels.RemoveRange(context.Responsavels);
        context.SaveChanges();
        context.ChangeTracker.Clear();
    }

    /// <summary>Cliente já autenticado como o responsável, sem passar pelo login.</summary>
    public HttpClient CreateClientAutenticadoComo(long responsavelId)
    {
        using var scope = Services.CreateScope();
        var token = scope.ServiceProvider.GetRequiredService<IEmissorDeToken>().Emitir(
            new Responsavel { Id = responsavelId, Nome = "Teste", Email = "teste@pethub.com", Cpf = "0", Senha = "x" });

        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Grava um lembrete como se tivesse chegado pela fila e devolve o id.</summary>
    public long CriarLembrete(long responsavelId, TipoLembrete tipo = TipoLembrete.VACINA, long referenciaId = 99)
    {
        using var scope = Services.CreateScope();
        var lembrete = new Lembrete
        {
            ResponsavelId = responsavelId, PetId = 7, Tipo = tipo,
            DataAgendada = new DateOnly(2026, 9, 15), Mensagem = "Vacina antirrábica agendada",
            ReferenciaId = referenciaId, ReferenciaTipo = "VacinaTratamento"
        };
        scope.ServiceProvider.GetRequiredService<ILembreteRepository>().Add(lembrete);
        return lembrete.Id;
    }

    /// <summary>Entrega uma mensagem ao processador, exatamente como o consumidor da fila faz.</summary>
    public ResultadoDaMensagem ReceberMensagem(string json)
    {
        using var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IProcessadorDeLembrete>()
            .Processar(System.Text.Encoding.UTF8.GetBytes(json));
    }

    /// <summary>Cliente que fala como o Java: JWT de serviço assinado com o segredo de serviço.</summary>
    public HttpClient CreateClientComServiceToken(string? token = null)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(AutenticadorDeServico.Cabecalho, "Bearer " + (token ?? ChavesDeTeste.TokenDoJava()));
        return client;
    }
}

[CollectionDefinition(Name)]
public class VitalisApiCollection : ICollectionFixture<VitalisWebApplicationFactory>
{
    public const string Name = "API Vitalis em memória";
}
