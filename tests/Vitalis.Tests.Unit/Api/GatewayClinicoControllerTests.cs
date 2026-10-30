using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Vitalis.Tests.Unit.Fixtures;
using Xunit;

namespace Vitalis.Tests.Unit.Api;

public class GatewayClinicoControllerTests
{
    private readonly Mock<IPethubJavaClient> _java = new();

    private GatewayClinicoController CriarController(long? logado = 7)
        => ConfiguracaoFixture.ComHttpContext(
            new GatewayClinicoController(_java.Object, NullLogger<GatewayClinicoController>.Instance),
            responsavelLogado: logado);

    [Fact]
    public async Task Encaminhar_RecursoPermitido_DeveRepassarComOIdDoResponsavelDoToken()
    {
        // Arrange
        _java.Setup(j => j.EncaminharAsync(HttpMethod.Get, "/api/pets", 7, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("[]", Encoding.UTF8, "application/json") });

        // Act
        var resultado = await CriarController().Encaminhar("pets", default);

        // Assert
        var conteudo = resultado.Should().BeOfType<ContentResult>().Subject;
        conteudo.StatusCode.Should().Be(200);
        conteudo.Content.Should().Be("[]");
    }

    [Fact]
    public async Task Encaminhar_RespostaDeErroDoJava_DeveManterOStatusEOCorpo()
    {
        // Arrange
        _java.Setup(j => j.EncaminharAsync(It.IsAny<HttpMethod>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<HttpContent?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.Forbidden)
            { Content = new StringContent("{\"message\":\"negado\"}", Encoding.UTF8, "application/json") });

        // Act
        var resultado = await CriarController().Encaminhar("pets/9", default);

        // Assert
        resultado.Should().BeOfType<ContentResult>().Which.StatusCode.Should().Be(403);
    }

    [Theory]
    [InlineData("actuator/health")]
    [InlineData("auth/login")]
    [InlineData("pets/../actuator")]
    [InlineData("")]
    public async Task Encaminhar_RecursoForaDaLista_DeveRetornarNotFoundSemChamarOJava(string caminho)
    {
        // Act
        var resultado = await CriarController().Encaminhar(caminho, default);

        // Assert
        resultado.Should().BeOfType<NotFoundObjectResult>();
        _java.Verify(j => j.EncaminharAsync(It.IsAny<HttpMethod>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<HttpContent?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Encaminhar_JavaIndisponivel_DeveRetornar503()
    {
        // Arrange
        _java.Setup(j => j.EncaminharAsync(It.IsAny<HttpMethod>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<HttpContent?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ServicoIndisponivelException("fora do ar"));

        // Act
        var resultado = await CriarController().Encaminhar("consultas", default);

        // Assert
        resultado.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(503);
    }

    [Fact]
    public async Task Encaminhar_SemResponsavelNoToken_DeveRetornarUnauthorized()
    {
        // Act
        var resultado = await CriarController(logado: null).Encaminhar("pets", default);

        // Assert
        resultado.Should().BeOfType<UnauthorizedObjectResult>();
    }
}
