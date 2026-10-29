using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Vitalis.Tests.Integration.Fixtures;
using Xunit;

namespace Vitalis.Tests.Integration.Endpoints;

[Collection(VitalisApiCollection.Name)]
public class GatewayClinicoEndpointsTests : IDisposable
{
    private readonly VitalisWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public GatewayClinicoEndpointsTests(VitalisWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.LimparBanco();
        _client = factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Get_SemToken_DeveRetornar401SemChamarOJava()
    {
        // Act
        var resposta = await _client.GetAsync("/api/clinico/pets");

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _factory.Java.Encaminhados.Should().BeEmpty();
    }

    [Fact]
    public async Task Get_ComToken_DeveRepassarAoJavaComOIdDoResponsavelEAQuery()
    {
        // Arrange
        var id = await DadosDeIntegracao.CadastrarResponsavelAsync(_client);

        // Act
        var resposta = await _client.GetAsync("/api/clinico/pets?page=0&size=5");

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        _factory.Java.Encaminhados.Should().ContainSingle()
            .Which.Should().Be(("GET", "/api/pets?page=0&size=5", id));
    }

    [Fact]
    public async Task Post_AgendarConsulta_DeveRepassarOPostAoJava()
    {
        // Arrange
        var id = await DadosDeIntegracao.CadastrarResponsavelAsync(_client);

        // Act
        var resposta = await _client.PostAsJsonAsync("/api/clinico/consultas", new { petId = 1 });

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        _factory.Java.Encaminhados.Should().ContainSingle()
            .Which.Should().Be(("POST", "/api/consultas", id));
    }

    [Fact]
    public async Task Get_RecursoForaDaLista_DeveRetornar404SemChamarOJava()
    {
        // Arrange
        await DadosDeIntegracao.CadastrarResponsavelAsync(_client);

        // Act
        var resposta = await _client.GetAsync("/api/clinico/actuator/health");

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        _factory.Java.Encaminhados.Should().BeEmpty();
    }

    [Fact]
    public async Task Get_JavaFora_DeveRetornar503()
    {
        // Arrange
        await DadosDeIntegracao.CadastrarResponsavelAsync(_client);
        _factory.Java.Fora = true;

        // Act
        var resposta = await _client.GetAsync("/api/clinico/pets");

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }
}
