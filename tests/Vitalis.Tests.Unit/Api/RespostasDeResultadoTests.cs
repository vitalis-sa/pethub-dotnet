using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Vitalis.Tests.Unit.Fixtures;
using Xunit;

namespace Vitalis.Tests.Unit.Api;

/// <summary>Cada tipo de erro de um caso de uso vira o status HTTP certo, sempre em ProblemDetails.</summary>
public class RespostasDeResultadoTests
{
    private sealed class ControllerDeTeste : ControllerBase;

    private static ControllerBase Controller() => ConfiguracaoFixture.ComHttpContext(new ControllerDeTeste());

    [Theory]
    [InlineData(TipoDeErro.NaoAutorizado, 401)]
    [InlineData(TipoDeErro.Proibido, 403)]
    [InlineData(TipoDeErro.NaoEncontrado, 404)]
    [InlineData(TipoDeErro.Conflito, 409)]
    [InlineData(TipoDeErro.Indisponivel, 503)]
    public void Problema_CadaTipoDeErro_DeveVirarOStatusCertoComProblemDetails(TipoDeErro tipo, int status)
    {
        // Arrange
        var erro = new Erro(tipo, "mensagem de teste");

        // Act
        var resposta = Controller().Problema(erro);

        // Assert
        var objeto = resposta.Should().BeAssignableTo<ObjectResult>().Subject;
        objeto.StatusCode.Should().Be(status);
        var problema = objeto.Value.Should().BeOfType<ProblemDetails>().Subject;
        problema.Detail.Should().Be("mensagem de teste");
        problema.Extensions["erro"].Should().Be("mensagem de teste");
    }

    [Fact]
    public void Problema_InvalidoComCampo_DeveVirarValidationProblemDetailsComOCampo()
    {
        // Act
        var resposta = Controller().Problema(Erro.Invalido("ordenarPor", "campo errado"));

        // Assert
        var problema = resposta.Should().BeOfType<BadRequestObjectResult>().Which.Value
            .Should().BeOfType<ValidationProblemDetails>().Subject;
        problema.Errors["ordenarPor"].Should().Equal("campo errado");
    }

    [Fact]
    public void Responder_Sucesso_DeveUsarARespostaDeSucesso()
    {
        // Act
        var resposta = Controller().Responder(Resultado<int>.Ok(42), valor => new OkObjectResult(valor));

        // Assert
        resposta.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(42);
    }

    [Fact]
    public void Responder_Erro_NaoDeveChamarARespostaDeSucesso()
    {
        // Arrange
        Resultado resultado = Erro.NaoEncontrado("sumiu");
        var chamou = false;

        // Act
        var resposta = Controller().Responder(resultado, () => { chamou = true; return new NoContentResult(); });

        // Assert
        chamou.Should().BeFalse();
        resposta.Should().BeOfType<NotFoundObjectResult>();
    }
}
