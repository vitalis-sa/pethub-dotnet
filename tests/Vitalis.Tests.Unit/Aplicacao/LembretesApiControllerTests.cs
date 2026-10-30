using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using FluentAssertions;
using Moq;
using Vitalis.Consultas;
using Vitalis.Dto;
using Vitalis.Historico;
using Vitalis.Models;
using Vitalis.Repositories;
using Vitalis.Tests.Unit.Fixtures;
using Xunit;

namespace Vitalis.Tests.Unit.Aplicacao;

public class LembretesApiControllerTests
{
    private readonly Mock<ILembreteRepository> _repositorioMock = new();
    private readonly Mock<IHistoricoDeMensagens> _historicoMock = new();
    private readonly Mock<ILogger<LembretesApiController>> _loggerMock = new();

    private LembretesApiController CriarController(long? responsavelLogado = null)
        => ConfiguracaoFixture.ComHttpContext(
            new LembretesApiController(_repositorioMock.Object, _historicoMock.Object, _loggerMock.Object),
            responsavelLogado: responsavelLogado);

    [Fact]
    public void GetByResponsavel_ResponsavelComLembretes_DeveRetornarPaginaComOsSeusLembretesELinks()
    {
        // Arrange
        _repositorioMock.Setup(r => r.Consultar(1, It.IsAny<FiltroDeLembretes>(), It.IsAny<ConsultaPaginada>()))
            .Returns(([NovoLembrete(id: 1, responsavelId: 1)], 1));
        var controller = CriarController(responsavelLogado: 1);

        // Act
        var resultado = controller.GetByResponsavel(1, new FiltroDeLembretes(), new ConsultaPaginada());

        // Assert
        var pagina = resultado.Should().BeOfType<OkObjectResult>().Which.Value
            .Should().BeOfType<PaginaDeResultados<LembreteResposta>>().Subject;
        pagina.Itens.Should().ContainSingle().Which.ResponsavelId.Should().Be(1);
        pagina.TotalDeItens.Should().Be(1);
        pagina.Links.Select(l => l.Rel).Should().Contain(["self", "first", "last"]);
        pagina.Itens[0].Links.Should().Contain(l => l.Rel == "atualizar-status" && l.Metodo == "PATCH");
    }

    [Fact]
    public void GetByResponsavel_SegundaDeTresPaginas_DeveCalcularOTotalEOferecerAnteriorEProxima()
    {
        // Arrange
        _repositorioMock.Setup(r => r.Consultar(1, It.IsAny<FiltroDeLembretes>(), It.IsAny<ConsultaPaginada>()))
            .Returns(([NovoLembrete()], 25));
        var controller = CriarController(responsavelLogado: 1);

        // Act
        var resultado = controller.GetByResponsavel(1, new FiltroDeLembretes(), new ConsultaPaginada { Pagina = 2, Tamanho = 10 });

        // Assert
        var pagina = resultado.Should().BeOfType<OkObjectResult>().Which.Value
            .Should().BeOfType<PaginaDeResultados<LembreteResposta>>().Subject;
        pagina.TotalDePaginas.Should().Be(3);
        pagina.Links.Should().Contain(l => l.Rel == "prev" && l.Href.Contains("pagina=1"));
        pagina.Links.Should().Contain(l => l.Rel == "next" && l.Href.Contains("pagina=3"));
    }

    [Fact]
    public void GetByResponsavel_FiltrosEOrdenacao_DeveRepassarTudoAoRepositorio()
    {
        // Arrange
        var filtro = new FiltroDeLembretes { Tipo = TipoLembrete.CONSULTA, Status = StatusLembrete.PENDENTE, PetId = 5 };
        var consulta = new ConsultaPaginada { OrdenarPor = "tipo", Direcao = "asc" };
        _repositorioMock.Setup(r => r.Consultar(1, filtro, consulta)).Returns(([], 0));
        var controller = CriarController(responsavelLogado: 1);

        // Act
        controller.GetByResponsavel(1, filtro, consulta);

        // Assert
        _repositorioMock.Verify(r => r.Consultar(1, filtro, consulta), Times.Once);
    }

    [Fact]
    public void GetByResponsavel_OrdenacaoPorCampoDesconhecido_DeveRetornar400SemConsultar()
    {
        // Arrange
        var controller = CriarController(responsavelLogado: 1);

        // Act
        var resultado = controller.GetByResponsavel(1, new FiltroDeLembretes(), new ConsultaPaginada { OrdenarPor = "senha" });

        // Assert
        var erro = resultado.Should().BeOfType<BadRequestObjectResult>().Which.Value
            .Should().BeOfType<ValidationProblemDetails>().Subject;
        erro.Errors.Should().ContainKey("ordenarPor");
        _repositorioMock.Verify(r => r.Consultar(It.IsAny<long>(), It.IsAny<FiltroDeLembretes>(), It.IsAny<ConsultaPaginada>()), Times.Never);
    }

    [Fact]
    public void GetByResponsavel_PeriodoComInicioDepoisDoFim_DeveRetornar400()
    {
        // Arrange
        var filtro = new FiltroDeLembretes { De = new DateOnly(2026, 12, 1), Ate = new DateOnly(2026, 11, 1) };
        var controller = CriarController(responsavelLogado: 1);

        // Act
        var resultado = controller.GetByResponsavel(1, filtro, new ConsultaPaginada());

        // Assert
        resultado.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void GetById_LembreteDoProprioResponsavel_DeveRetornarOkComLinkSelf()
    {
        // Arrange
        _repositorioMock.Setup(r => r.GetById(3)).Returns(NovoLembrete(id: 3, responsavelId: 1));
        var controller = CriarController(responsavelLogado: 1);

        // Act
        var resultado = controller.GetById(3);

        // Assert
        var lembrete = resultado.Should().BeOfType<OkObjectResult>().Which.Value
            .Should().BeOfType<LembreteResposta>().Subject;
        lembrete.Links.Should().Contain(l => l.Rel == "self" && l.Href == "/api/lembretes/3");
    }

    [Fact]
    public void GetById_LembreteDeOutroResponsavel_DeveRetornar403()
    {
        // Arrange
        _repositorioMock.Setup(r => r.GetById(3)).Returns(NovoLembrete(id: 3, responsavelId: 1));
        var controller = CriarController(responsavelLogado: 2);

        // Act
        var resultado = controller.GetById(3);

        // Assert
        resultado.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(403);
    }

    [Fact]
    public void GetById_LembreteInexistente_DeveRetornarNotFoundComProblemDetails()
    {
        // Arrange
        _repositorioMock.Setup(r => r.GetById(404)).Returns((Lembrete?)null);
        var controller = CriarController(responsavelLogado: 1);

        // Act
        var resultado = controller.GetById(404);

        // Assert
        var problema = resultado.Should().BeOfType<NotFoundObjectResult>().Which.Value
            .Should().BeOfType<ProblemDetails>().Subject;
        problema.Status.Should().Be(404);
        problema.Detail.Should().Be("Lembrete não encontrado");
        problema.Extensions["erro"].Should().Be("Lembrete não encontrado");
    }

    [Fact]
    public async Task GetHistorico_MongoNaoConfigurado_DeveRetornar503()
    {
        // Arrange
        _historicoMock.SetupGet(h => h.Habilitado).Returns(false);
        var controller = CriarController(responsavelLogado: 1);

        // Act
        var resultado = await controller.GetHistorico(1, new FiltroDeHistorico(), new ConsultaPaginada());

        // Assert
        resultado.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(503);
    }

    [Fact]
    public async Task GetHistorico_MongoConfigurado_DeveRetornarPaginaDoHistorico()
    {
        // Arrange
        _historicoMock.SetupGet(h => h.Habilitado).Returns(true);
        _historicoMock.Setup(h => h.ConsultarAsync(1, It.IsAny<FiltroDeHistorico>(), It.IsAny<ConsultaPaginada>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(([new RegistroDeMensagem { Id = "a1", ResponsavelId = 1, Resultado = "Confirmar", Motivo = "lembrete criado" }], 1L));
        var controller = CriarController(responsavelLogado: 1);

        // Act
        var resultado = await controller.GetHistorico(1, new FiltroDeHistorico(), new ConsultaPaginada());

        // Assert
        var pagina = resultado.Should().BeOfType<OkObjectResult>().Which.Value
            .Should().BeOfType<PaginaDeResultados<RegistroDeMensagemResposta>>().Subject;
        pagina.Itens.Should().ContainSingle().Which.Motivo.Should().Be("lembrete criado");
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
        ok.Value.Should().BeAssignableTo<IEnumerable<LembreteResposta>>().Which.Should().ContainSingle(l => l.Id == 7);
    }

    [Fact]
    public void GetByResponsavelETipo_TipoInformado_DeveDelegarOFiltroParaORepositorio()
    {
        // Arrange
        _repositorioMock.Setup(r => r.Consultar(1, It.IsAny<FiltroDeLembretes>(), It.IsAny<ConsultaPaginada>()))
            .Returns(([NovoLembrete(tipo: TipoLembrete.VACINA)], 1));
        var controller = CriarController(responsavelLogado: 1);

        // Act
        var resultado = controller.GetByResponsavelETipo(1, TipoLembrete.VACINA, new ConsultaPaginada());

        // Assert
        resultado.Should().BeOfType<OkObjectResult>();
        _repositorioMock.Verify(r => r.Consultar(1, It.Is<FiltroDeLembretes>(f => f.Tipo == TipoLembrete.VACINA),
            It.IsAny<ConsultaPaginada>()), Times.Once);
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
    public void AtualizarStatus_LembreteDeOutroResponsavel_DeveRetornar403SemAtualizar()
    {
        // Arrange
        _repositorioMock.Setup(r => r.GetById(1)).Returns(NovoLembrete(id: 1, responsavelId: 1));
        var controller = CriarController(responsavelLogado: 2);

        // Act
        var resultado = controller.AtualizarStatus(1, new AtualizarStatusDto { Status = StatusLembrete.ENVIADO });

        // Assert
        resultado.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(403);
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
    public void Delete_LembreteDeOutroResponsavel_DeveRetornar403SemRemover()
    {
        // Arrange
        _repositorioMock.Setup(r => r.GetById(1)).Returns(NovoLembrete(id: 1, responsavelId: 1));
        var controller = CriarController(responsavelLogado: 2);

        // Act
        var resultado = controller.Delete(1);

        // Assert
        resultado.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(403);
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
