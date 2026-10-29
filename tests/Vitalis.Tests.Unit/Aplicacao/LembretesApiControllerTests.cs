using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using FluentAssertions;
using Moq;
using Vitalis.Models;
using Vitalis.Repositories;
using Vitalis.Tests.Unit.Fixtures;
using Xunit;

namespace Vitalis.Tests.Unit.Aplicacao;

public class LembretesApiControllerTests
{
    private readonly Mock<ILembreteRepository> _repositorioMock = new();
    private readonly Mock<ILogger<LembretesApiController>> _loggerMock = new();

    private LembretesApiController CriarController(long? responsavelLogado = null)
        => ConfiguracaoFixture.ComHttpContext(
            new LembretesApiController(_repositorioMock.Object, _loggerMock.Object),
            responsavelLogado: responsavelLogado);

    [Fact]
    public void GetByResponsavel_ResponsavelComLembretes_DeveRetornarApenasOsSeusLembretes()
    {
        // Arrange
        _repositorioMock.Setup(r => r.GetByResponsavelId(1)).Returns([NovoLembrete(id: 1, responsavelId: 1)]);
        var controller = CriarController(responsavelLogado: 1);

        // Act
        var resultado = controller.GetByResponsavel(1);

        // Assert
        var ok = resultado.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeAssignableTo<IEnumerable<Lembrete>>()
            .Which.Should().OnlyContain(l => l.ResponsavelId == 1);
    }

    [Fact]
    public void GetDosesFuturas_ResponsavelLogado_DeveDevolverAsProximasDosesDoRepositorio()
    {
        // Arrange
        _repositorioMock.Setup(r => r.GetDosesFuturas(1, It.IsAny<DateTime>(), It.IsAny<int>()))
            .Returns([NovoLembrete(id: 7, responsavelId: 1, tipo: TipoLembrete.MEDICAMENTO)]);
        var controller = CriarController(responsavelLogado: 1);

        // Act
        var resultado = controller.GetDosesFuturas(1);

        // Assert
        var ok = resultado.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeAssignableTo<IEnumerable<Lembrete>>().Which.Should().ContainSingle(l => l.Id == 7);
    }

    [Fact]
    public void GetByResponsavelETipo_TipoInformado_DeveDelegarOFiltroParaORepositorio()
    {
        // Arrange
        _repositorioMock.Setup(r => r.GetByResponsavelIdETipo(1, TipoLembrete.VACINA))
            .Returns([NovoLembrete(tipo: TipoLembrete.VACINA)]);
        var controller = CriarController(responsavelLogado: 1);

        // Act
        var resultado = controller.GetByResponsavelETipo(1, TipoLembrete.VACINA);

        // Assert
        resultado.Should().BeOfType<OkObjectResult>();
        _repositorioMock.Verify(r => r.GetByResponsavelIdETipo(1, TipoLembrete.VACINA), Times.Once);
    }

    [Fact]
    public void AtualizarStatus_LembreteDoProprioResponsavel_DeveAtualizarERetornarNoContent()
    {
        // Arrange
        _repositorioMock.Setup(r => r.GetById(1)).Returns(NovoLembrete(id: 1, responsavelId: 1));
        var controller = CriarController(responsavelLogado: 1);

        // Act
        var resultado = controller.AtualizarStatus(1, new AtualizarStatusDto { Status = StatusLembrete.ENVIADO });

        // Assert
        resultado.Should().BeOfType<NoContentResult>();
        _repositorioMock.Verify(r => r.AtualizarStatus(1, StatusLembrete.ENVIADO), Times.Once);
    }

    [Fact]
    public void AtualizarStatus_LembreteDeOutroResponsavel_DeveRetornarForbidSemAtualizar()
    {
        // Arrange
        _repositorioMock.Setup(r => r.GetById(1)).Returns(NovoLembrete(id: 1, responsavelId: 1));
        var controller = CriarController(responsavelLogado: 2);

        // Act
        var resultado = controller.AtualizarStatus(1, new AtualizarStatusDto { Status = StatusLembrete.ENVIADO });

        // Assert
        resultado.Should().BeOfType<ForbidResult>();
        _repositorioMock.Verify(r => r.AtualizarStatus(It.IsAny<long>(), It.IsAny<StatusLembrete>()), Times.Never);
    }

    [Fact]
    public void AtualizarStatus_LembreteInexistente_DeveRetornarNotFoundSemAtualizar()
    {
        // Arrange
        _repositorioMock.Setup(r => r.GetById(999)).Returns((Lembrete?)null);
        var controller = CriarController(responsavelLogado: 1);

        // Act
        var resultado = controller.AtualizarStatus(999, new AtualizarStatusDto { Status = StatusLembrete.FALHOU });

        // Assert
        resultado.Should().BeOfType<NotFoundObjectResult>();
        _repositorioMock.Verify(r => r.AtualizarStatus(It.IsAny<long>(), It.IsAny<StatusLembrete>()), Times.Never);
    }

    [Fact]
    public void Delete_LembreteDoProprioResponsavel_DeveRemoverERetornarNoContent()
    {
        // Arrange
        _repositorioMock.Setup(r => r.GetById(1)).Returns(NovoLembrete(id: 1, responsavelId: 1));
        var controller = CriarController(responsavelLogado: 1);

        // Act
        var resultado = controller.Delete(1);

        // Assert
        resultado.Should().BeOfType<NoContentResult>();
        _repositorioMock.Verify(r => r.Delete(1), Times.Once);
    }

    [Fact]
    public void Delete_LembreteDeOutroResponsavel_DeveRetornarForbidSemRemover()
    {
        // Arrange
        _repositorioMock.Setup(r => r.GetById(1)).Returns(NovoLembrete(id: 1, responsavelId: 1));
        var controller = CriarController(responsavelLogado: 2);

        // Act
        var resultado = controller.Delete(1);

        // Assert
        resultado.Should().BeOfType<ForbidResult>();
        _repositorioMock.Verify(r => r.Delete(It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public void Delete_LembreteInexistente_DeveRetornarNotFoundSemRemover()
    {
        // Arrange
        _repositorioMock.Setup(r => r.GetById(999)).Returns((Lembrete?)null);
        var controller = CriarController(responsavelLogado: 1);

        // Act
        var resultado = controller.Delete(999);

        // Assert
        resultado.Should().BeOfType<NotFoundObjectResult>();
        _repositorioMock.Verify(r => r.Delete(It.IsAny<long>()), Times.Never);
    }

    private static Lembrete NovoLembrete(
        long id = 1, long responsavelId = 1, TipoLembrete tipo = TipoLembrete.VACINA) => new()
    {
        Id = id, ResponsavelId = responsavelId, PetId = 7, Tipo = tipo,
        DataAgendada = new DateOnly(2026, 9, 15), Mensagem = "Vacina antirrábica agendada"
    };
}
