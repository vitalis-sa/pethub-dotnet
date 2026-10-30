using Microsoft.Extensions.DependencyInjection;
using Vitalis.Application.Mensageria;
using Vitalis.Application.Servicos;

namespace Vitalis.Application;

public static class DependencyInjection
{
    /// <summary>Registra os casos de uso. As portas que eles usam são registradas pela infraestrutura.</summary>
    public static IServiceCollection AddAplicacao(this IServiceCollection servicos)
    {
        servicos.AddScoped<IServicoDeResponsaveis, ServicoDeResponsaveis>();
        servicos.AddScoped<IServicoDeEnderecos, ServicoDeEnderecos>();
        servicos.AddScoped<IServicoDeContatos, ServicoDeContatos>();
        servicos.AddScoped<IServicoDeLembretes, ServicoDeLembretes>();
        servicos.AddScoped<IProcessadorDeLembrete, ProcessadorDeLembrete>();
        return servicos;
    }
}
