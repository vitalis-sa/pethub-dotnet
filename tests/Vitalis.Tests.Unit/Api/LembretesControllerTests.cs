using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Vitalis.Tests.Unit.Fixtures;
using Xunit;

namespace Vitalis.Tests.Unit.Api;

/// <summary>A camada HTTP dos lembretes: envelope paginado, links (HATEOAS) e tradução dos erros.</summary>
public class LembretesControllerTests
{
    private readonly Mock<IServicoDeLembretes> _servico = new();

    private LembretesController CriarController(long? responsavelLogado = 1)
        => ConfiguracaoFixture.ComHttpContext(new LembretesController(_servico.Object), responsavelLogado: responsavelLogado);

    [Fact]
    public void GetByResponsavel_PaginaComLembretes_DeveDevolverOEnvelopeComLinksDaPaginaEDosItens()
    {
        // Arrange
        _servico.Setup(s => s.Listar(1, It.IsAny<FiltroDeLembretes>(), It.IsAny<ConsultaPaginada>()))
            .Returns(new Pagina<Lembrete>([NovoLembrete(id: 1)], 1));

        // Act
        var resultado = CriarController().GetByResponsavel(1, new FiltroDeLembretes(), new ConsultaPaginada());

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
        _servico.Setup(s => s.Listar(1, It.IsAny<FiltroDeLembretes>(), It.IsAny<ConsultaPaginada>()))
            .Returns(new Pagina<Lembrete>([NovoLembrete()], 25));

        // Act
        var resultado = CriarController().GetByResponsavel(1, new FiltroDeLembretes(), new ConsultaPaginada { Pagina = 2, Tamanho = 10 });

        // Assert
        var pagina = resultado.Should().BeOfType<OkObjectResult>().Which.Value
            .Should().BeOfType<PaginaDeResultados<LembreteResposta>>().Subject;
        pagina.TotalDePaginas.Should().Be(3);
        pagina.Links.Should().Contain(l => l.Rel == "prev" && l.Href.Contains("pagina=1"));
        pagina.Links.Should().Contain(l => l.Rel == "next" && l.Href.Contains("pagina=3"));
    }

    [Fact]
    public void GetByResponsavel_OrdenacaoInvalida_DeveDevolver400ComOCampoEmErrors()
    {
        // Arrange
        _servico.Setup(s => s.Listar(1, It.IsAny<FiltroDeLembretes>(), It.IsAny<ConsultaPaginada>()))
            .Returns(Erro.Invalido("ordenarPor", "Ordene por um destes campos: dataAgendada, tipo, status, id."));

        // Act
        var resultado = CriarController().GetByResponsavel(1, new FiltroDeLembretes(), new ConsultaPaginada { OrdenarPor = "senha" });

        // Assert
        var erro = resultado.Should().BeOfType<BadRequestObjectResult>().Which.Value
            .Should().BeOfType<ValidationProblemDetails>().Subject;
        erro.Errors.Should().ContainKey("ordenarPor");
    }

    [Fact]
    public void GetByResponsavelETipo_TipoNaRota_DeveVirarFiltroDaListagem()
    {
        // Arrange
        _servico.Setup(s => s.Listar(1, It.IsAny<FiltroDeLembretes>(), It.IsAny<ConsultaPaginada>()))
            .Returns(new Pagina<Lembrete>([], 0));

        // Act
        CriarController().GetByResponsavelETipo(1, TipoLembrete.VACINA, new ConsultaPaginada());

        // Assert
        _servico.Verify(s => s.Listar(1, It.Is<FiltroDeLembretes>(f => f.Tipo == TipoLembrete.VACINA),
            It.IsAny<ConsultaPaginada>()), Times.Once);
    }

    [Fact]
    public void GetById_LembreteDoResponsavelLogado_DeveDevolverOLembreteComLinkSelf()
    {
        // Arrange
        _servico.Setup(s => s.Obter(3, 1)).Returns(NovoLembrete(id: 3));

        // Act
        var resultado = CriarController(responsavelLogado: 1).GetById(3);

        // Assert
        var lembrete = resultado.Should().BeOfType<OkObjectResult>().Which.Value
            .Should().BeOfType<LembreteResposta>().Subject;
        lembrete.Links.Should().Contain(l => l.Rel == "self" && l.Href == "/api/lembretes/3");
    }

    [Fact]
    public void GetById_DeveRepassarOResponsavelDoTokenComoSolicitante()
    {
        // Arrange
        _servico.Setup(s => s.Obter(3, 2)).Returns(Erro.Proibido("Você só pode acessar os seus próprios dados"));

        // Act
        var resultado = CriarController(responsavelLogado: 2).GetById(3);

        // Assert
        resultado.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(403);
    }

    [Fact]
    public void AtualizarStatus_Sucesso_DeveDevolverNoContent()
    {
        // Arrange
        _servico.Setup(s => s.AlterarStatus(1, StatusLembrete.ENVIADO, 1)).Returns(Resultado.Ok());

        // Act
        var resultado = CriarController().AtualizarStatus(1, new AtualizarStatusDto { Status = StatusLembrete.ENVIADO });

        // Assert
        resultado.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public void Delete_LembreteInexistente_DeveDevolverNotFoundComProblemDetails()
    {
        // Arrange
        _servico.Setup(s => s.Remover(404, 1)).Returns(Erro.NaoEncontrado("Lembrete não encontrado"));

        // Act
        var resultado = CriarController().Delete(404);

        // Assert
        var problema = resultado.Should().BeOfType<NotFoundObjectResult>().Which.Value
            .Should().BeOfType<ProblemDetails>().Subject;
        problema.Detail.Should().Be("Lembrete não encontrado");
    }

    [Fact]
    public async Task GetHistorico_MongoNaoConfigurado_DeveDevolver503()
    {
        // Arrange
        _servico.Setup(s => s.ListarHistoricoAsync(1, It.IsAny<FiltroDeHistorico>(), It.IsAny<ConsultaPaginada>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Erro.Indisponivel("Histórico de mensagens indisponível: MongoDB não configurado."));

        // Act
        var resultado = await CriarController().GetHistorico(1, new FiltroDeHistorico(), new ConsultaPaginada());

        // Assert
        resultado.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(503);
    }

    [Fact]
    public async Task GetHistorico_ComRegistros_DeveDevolverAPaginaSemOCorpoBruto()
    {
        // Arrange
        _servico.Setup(s => s.ListarHistoricoAsync(1, It.IsAny<FiltroDeHistorico>(), It.IsAny<ConsultaPaginada>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Pagina<RegistroDeMensagem>(
                [new RegistroDeMensagem { Id = "a1", ResponsavelId = 1, Resultado = "Confirmar", Motivo = "lembrete criado", Corpo = "{...}" }], 1));

        // Act
        var resultado = await CriarController().GetHistorico(1, new FiltroDeHistorico(), new ConsultaPaginada());

        // Assert
        var pagina = resultado.Should().BeOfType<OkObjectResult>().Which.Value
            .Should().BeOfType<PaginaDeResultados<RegistroDeMensagemResposta>>().Subject;
        pagina.Itens.Should().ContainSingle().Which.Motivo.Should().Be("lembrete criado");
        typeof(RegistroDeMensagemResposta).GetProperty("Corpo").Should().BeNull();
    }

    private static Lembrete NovoLembrete(long id = 1, long responsavelId = 1) => new()
    {
        Id = id, ResponsavelId = responsavelId, PetId = 7, Tipo = TipoLembrete.VACINA,
        DataAgendada = new DateOnly(2026, 9, 15), Mensagem = "Vacina antirrábica agendada"
    };
}
