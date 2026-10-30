using System.Reflection;
using FluentAssertions;
using Xunit;

namespace Vitalis.Tests.Unit.Arquitetura;

/// <summary>
/// Regra de dependência da Clean Architecture: as camadas internas não conhecem as externas nem os frameworks
/// de banco, mensageria e HTTP. Se alguém usar o EF dentro da Aplicação, por exemplo, estes testes falham.
/// </summary>
public class DependenciasEntreCamadasTests
{
    private static readonly Assembly Dominio = typeof(Responsavel).Assembly;
    private static readonly Assembly Aplicacao = typeof(ServicoDeResponsaveis).Assembly;
    private static readonly Assembly Infraestrutura = typeof(HasherBCrypt).Assembly;

    private static readonly string[] Frameworks =
    [
        "Microsoft.EntityFrameworkCore", "Oracle.", "MongoDB.", "RabbitMQ.", "BCrypt.",
        "Microsoft.AspNetCore", "Microsoft.IdentityModel", "Swashbuckle", "Serilog"
    ];

    private static IEnumerable<string> Referencias(Assembly assembly)
        => assembly.GetReferencedAssemblies().Select(a => a.Name!);

    [Fact]
    public void Dominio_NaoDeveDependerDeNenhumaOutraCamadaNemDeFramework()
    {
        // Act
        var referencias = Referencias(Dominio).ToList();

        // Assert
        referencias.Should().NotContain(r => r.StartsWith("Vitalis."));
        referencias.Should().NotContain(r => Frameworks.Any(r.StartsWith));
    }

    [Fact]
    public void Aplicacao_DeveDependerSoDoDominio()
    {
        // Act
        var camadas = Referencias(Aplicacao).Where(r => r.StartsWith("Vitalis.")).ToList();

        // Assert
        camadas.Should().Equal("Vitalis.Domain");
    }

    [Fact]
    public void Aplicacao_NaoDeveConhecerBancoMensageriaNemHttp()
        => Referencias(Aplicacao).Should().NotContain(r => Frameworks.Any(r.StartsWith));

    [Fact]
    public void Infraestrutura_NaoDeveDependerDaApi()
        => Referencias(Infraestrutura).Should().NotContain("Vitalis.Api");

    [Fact]
    public void Dominio_EntidadesNaoDevemTerAtributosDeMapeamentoDeBanco()
    {
        // Act
        var comMapeamento = Dominio.GetTypes()
            .SelectMany(t => t.GetCustomAttributes(true).Concat(t.GetProperties().SelectMany(p => p.GetCustomAttributes(true))))
            .Where(a => a.GetType().Namespace == "System.ComponentModel.DataAnnotations.Schema"
                        || a.GetType().Name is "KeyAttribute" or "JsonIgnoreAttribute")
            .Select(a => a.GetType().Name)
            .ToList();

        // Assert
        comMapeamento.Should().BeEmpty("o mapeamento das tabelas fica em Infrastructure/Persistencia/Configuracoes");
    }
}
