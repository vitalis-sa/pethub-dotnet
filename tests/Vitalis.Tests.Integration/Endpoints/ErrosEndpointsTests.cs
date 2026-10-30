using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Vitalis.Middlewares;
using Vitalis.Tests.Integration.Fixtures;
using Xunit;

namespace Vitalis.Tests.Integration.Endpoints;

/// <summary>Todo erro sai em application/problem+json, venha do controller, do ASP.NET ou de uma exceção.</summary>
[Collection(VitalisApiCollection.Name)]
public class ErrosEndpointsTests : IDisposable
{
    private readonly VitalisWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ErrosEndpointsTests(VitalisWebApplicationFactory factory)
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
    public async Task RotaInexistente_DeveResponder404EmProblemDetails()
    {
        // Act
        var resposta = await _client.GetAsync("/api/rota-que-nao-existe");
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        corpo.GetProperty("status").GetInt32().Should().Be(404);
    }

    [Fact]
    public async Task RotaProtegida_SemToken_DeveResponder401EmProblemDetails()
    {
        // Act
        var resposta = await _client.GetAsync("/api/lembretes/responsavel/1");

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task ErroDoController_DeveTrazerTraceIdEOMesmoCorrelationIdDoCabecalho()
    {
        // Arrange
        var responsavelId = await DadosDeIntegracao.CadastrarResponsavelAsync(_client);
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, $"/api/responsavel/{responsavelId}/enderecos/999");
        requisicao.Headers.Add(CorrelationIdMiddleware.CorrelationIdHeader, "erro-rastreavel-7");

        // Act
        var resposta = await _client.SendAsync(requisicao);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        corpo.GetProperty("detail").GetString().Should().Be("Endereço não encontrado");
        corpo.GetProperty("erro").GetString().Should().Be("Endereço não encontrado");
        corpo.GetProperty("correlationId").GetString().Should().Be("erro-rastreavel-7");
        corpo.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ExcecaoNaoTratada_DeveVirar500EmProblemDetailsSemVazarDetalhesInternos()
    {
        // Arrange
        var responsavelId = await DadosDeIntegracao.CadastrarResponsavelAsync(_client);
        _factory.Java.Explodir = true;

        // Act
        var resposta = await _client.DeleteAsync($"/api/responsavel/{responsavelId}");
        var texto = await resposta.Content.ReadAsStringAsync();

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        texto.Should().Contain("correlationId");
        texto.Should().NotContain("sigiloso").And.NotContain("InvalidOperationException");
    }

    [Fact]
    public async Task ExcecaoNaoTratada_NaoDeveApagarOResponsavel()
    {
        // Arrange
        var responsavelId = await DadosDeIntegracao.CadastrarResponsavelAsync(_client);
        _factory.Java.Explodir = true;

        // Act
        await _client.DeleteAsync($"/api/responsavel/{responsavelId}");
        _factory.Java.Explodir = false;
        var depois = await _client.GetAsync($"/api/responsavel/{responsavelId}");

        // Assert
        depois.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
