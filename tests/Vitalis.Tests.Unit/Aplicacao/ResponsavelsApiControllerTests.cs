using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using FluentAssertions;
using Moq;
using Vitalis.Integracao;
using Vitalis.Repositories;
using Vitalis.Seguranca;
using Vitalis.Tests.Unit.Fixtures;
using Xunit;

namespace Vitalis.Tests.Unit.Aplicacao;

public class ResponsavelsApiControllerTests : IClassFixture<ConfiguracaoFixture>
{
    private readonly ConfiguracaoFixture _fixture;
    private readonly Mock<IResponsavelRepository> _repositorioMock;
    private readonly Mock<ILogger<ResponsavelsApiController>> _loggerMock;
    private readonly Mock<IEmissorDeToken> _tokensMock;
    private readonly Mock<IPethubJavaClient> _javaMock;

    public ResponsavelsApiControllerTests(ConfiguracaoFixture fixture)
    {
        _fixture = fixture;
        _repositorioMock = new Mock<IResponsavelRepository>();
        _loggerMock = new Mock<ILogger<ResponsavelsApiController>>();
        _tokensMock = new Mock<IEmissorDeToken>();
        _tokensMock.Setup(t => t.Emitir(It.IsAny<Responsavel>())).Returns("jwt-de-teste");
        _javaMock = new Mock<IPethubJavaClient>();
    }

    private ResponsavelsApiController CriarController(string? serviceToken = null)
        => ConfiguracaoFixture.ComHttpContext(
            new ResponsavelsApiController(
                _repositorioMock.Object,
                _fixture.Autenticador,
                _tokensMock.Object,
                _javaMock.Object,
                _loggerMock.Object),
            serviceToken);

    [Fact]
    public void GetById_ResponsavelExistente_DeveRetornarOkComOsDados()
    {
        // Arrange
        var existente = NovoResponsavel(id: 1);
        _repositorioMock.Setup(r => r.GetById(1)).Returns(existente);
        var controller = CriarController();

        // Act
        var resultado = controller.GetById(1);

        // Assert
        resultado.Should().BeOfType<OkObjectResult>();
        _repositorioMock.Verify(r => r.GetById(1), Times.Once);
    }

    [Fact]
    public void GetById_ResponsavelInexistente_DeveRetornarNotFound()
    {
        // Arrange
        _repositorioMock.Setup(r => r.GetById(999)).Returns((Responsavel?)null);
        var controller = CriarController();

        // Act
        var resultado = controller.GetById(999);

        // Assert
        resultado.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public void BuscarPorCpf_ServiceTokenValido_DeveRetornarOkComOResponsavel()
    {
        // Arrange
        _repositorioMock.Setup(r => r.GetByCpf("12345678901")).Returns(NovoResponsavel());
        var controller = CriarController(ChavesDeTeste.TokenDoJava());

        // Act
        var resultado = controller.BuscarPorCpf("12345678901");

        // Assert
        resultado.Should().BeOfType<OkObjectResult>();
        _repositorioMock.Verify(r => r.GetByCpf("12345678901"), Times.Once);
    }

    [Fact]
    public void BuscarPorCpf_ServiceTokenInvalido_DeveRetornarUnauthorizedSemConsultarORepositorio()
    {
        // Arrange
        var controller = CriarController("token-errado");

        // Act
        var resultado = controller.BuscarPorCpf("12345678901");

        // Assert
        resultado.Should().BeOfType<UnauthorizedObjectResult>();
        _repositorioMock.Verify(r => r.GetByCpf(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void BuscarPorCpf_CpfEmBranco_DeveRetornarBadRequest()
    {
        // Arrange
        var controller = CriarController(ChavesDeTeste.TokenDoJava());

        // Act
        var resultado = controller.BuscarPorCpf("   ");

        // Assert
        resultado.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void BuscarPorCpf_CpfNaoCadastrado_DeveRetornarNotFound()
    {
        // Arrange
        _repositorioMock.Setup(r => r.GetByCpf("00000000000")).Returns((Responsavel?)null);
        var controller = CriarController(ChavesDeTeste.TokenDoJava());

        // Act
        var resultado = controller.BuscarPorCpf("00000000000");

        // Assert
        resultado.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public void Cadastrar_DadosValidos_DeveSalvarNoRepositorioERetornarCreated()
    {
        // Arrange
        var dto = NovoCadastroDto();
        _repositorioMock.Setup(r => r.GetByCpf(dto.Cpf)).Returns((Responsavel?)null);
        _repositorioMock.Setup(r => r.Add(It.IsAny<Responsavel>()))
            .Callback<Responsavel>(r => r.Id = 10);
        var controller = CriarController();

        // Act
        var resultado = controller.Cadastrar(dto);

        // Assert
        var created = resultado.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.RouteValues!["id"].Should().Be(10L);
        created.Value!.GetType().GetProperty("Token")!.GetValue(created.Value).Should().Be("jwt-de-teste");
        _repositorioMock.Verify(r => r.Add(It.Is<Responsavel>(x => x.Cpf == dto.Cpf && x.Ativo)), Times.Once);
    }

    [Fact]
    public void Cadastrar_CpfJaExistente_DeveRetornarConflictSemSalvar()
    {
        // Arrange
        var dto = NovoCadastroDto();
        _repositorioMock.Setup(r => r.GetByCpf(dto.Cpf)).Returns(NovoResponsavel());
        var controller = CriarController();

        // Act
        var resultado = controller.Cadastrar(dto);

        // Assert
        resultado.Should().BeOfType<ConflictObjectResult>();
        _repositorioMock.Verify(r => r.Add(It.IsAny<Responsavel>()), Times.Never);
    }

    [Fact]
    public void Cadastrar_ModelStateInvalido_DeveRetornarBadRequestSemSalvar()
    {
        // Arrange
        var controller = CriarController();
        controller.ModelState.AddModelError(nameof(CadastrarResponsavelDto.Email), "E-mail inválido");

        // Act
        var resultado = controller.Cadastrar(NovoCadastroDto());

        // Assert
        resultado.Should().BeOfType<BadRequestObjectResult>();
        _repositorioMock.Verify(r => r.Add(It.IsAny<Responsavel>()), Times.Never);
    }

    [Fact]
    public void Login_CredenciaisValidas_DeveRetornarOkComOsDadosDoResponsavel()
    {
        // Arrange
        var responsavel = NovoResponsavelComSenhaHasheada();
        _repositorioMock.Setup(r => r.GetByEmail(responsavel.Email)).Returns(responsavel);
        var controller = CriarController();

        // Act
        var resultado = controller.Login(new LoginDto { Email = responsavel.Email, Senha = SenhaPadrao });

        // Assert
        var ok = resultado.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value!.GetType().GetProperty("Token")!.GetValue(ok.Value).Should().Be("jwt-de-teste");
    }

    [Fact]
    public void Login_SenhaIncorreta_DeveRetornarUnauthorized()
    {
        // Arrange
        var responsavel = NovoResponsavelComSenhaHasheada();
        _repositorioMock.Setup(r => r.GetByEmail(responsavel.Email)).Returns(responsavel);
        var controller = CriarController();

        // Act
        var resultado = controller.Login(new LoginDto { Email = responsavel.Email, Senha = "senha-errada" });

        // Assert
        resultado.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public void Login_EmailNaoCadastrado_DeveRetornarUnauthorized()
    {
        // Arrange
        _repositorioMock.Setup(r => r.GetByEmail("ninguem@pethub.com")).Returns((Responsavel?)null);
        var controller = CriarController();

        // Act
        var resultado = controller.Login(new LoginDto { Email = "ninguem@pethub.com", Senha = SenhaPadrao });

        // Assert
        resultado.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public void Login_ContaDesativada_DeveRetornarUnauthorized()
    {
        // Arrange
        var responsavel = NovoResponsavelComSenhaHasheada();
        responsavel.Ativo = false;
        _repositorioMock.Setup(r => r.GetByEmail(responsavel.Email)).Returns(responsavel);
        var controller = CriarController();

        // Act
        var resultado = controller.Login(new LoginDto { Email = responsavel.Email, Senha = SenhaPadrao });

        // Assert
        resultado.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public void Update_ResponsavelExistente_DeveAtualizarERetornarNoContent()
    {
        // Arrange
        var existente = NovoResponsavel(id: 5);
        _repositorioMock.Setup(r => r.GetById(5)).Returns(existente);
        var controller = CriarController();
        var dto = NovaAtualizacaoDto(nome: "Nome Atualizado", email: "novo@pethub.com");

        // Act
        var resultado = controller.Update(5, dto);

        // Assert
        resultado.Should().BeOfType<NoContentResult>();
        existente.Nome.Should().Be("Nome Atualizado");
        existente.Email.Should().Be("novo@pethub.com");
        _repositorioMock.Verify(r => r.Update(existente), Times.Once);
    }

    [Fact]
    public void Update_ResponsavelInexistente_DeveRetornarNotFoundSemAtualizar()
    {
        // Arrange
        _repositorioMock.Setup(r => r.GetById(404)).Returns((Responsavel?)null);
        var controller = CriarController();

        // Act
        var resultado = controller.Update(404, NovaAtualizacaoDto());

        // Assert
        resultado.Should().BeOfType<NotFoundObjectResult>();
        _repositorioMock.Verify(r => r.Update(It.IsAny<Responsavel>()), Times.Never);
    }

    [Fact]
    public async Task Delete_ResponsavelExistente_DeveApagarOsPetsNoJavaEDepoisRemoverOCadastro()
    {
        // Arrange
        _repositorioMock.Setup(r => r.GetById(3)).Returns(NovoResponsavel(id: 3));
        var controller = CriarController();

        // Act
        var resultado = await controller.Delete(3);

        // Assert
        resultado.Should().BeOfType<NoContentResult>();
        _javaMock.Verify(j => j.ApagarPetsDoResponsavelAsync(3, It.IsAny<CancellationToken>()), Times.Once);
        _repositorioMock.Verify(r => r.Delete(3), Times.Once);
    }

    [Fact]
    public async Task Delete_JavaNaoConfirmaARemocaoDosPets_DeveRetornar503SemRemoverOCadastro()
    {
        // Arrange
        _repositorioMock.Setup(r => r.GetById(3)).Returns(NovoResponsavel(id: 3));
        _javaMock.Setup(j => j.ApagarPetsDoResponsavelAsync(3, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ServicoIndisponivelException("fora do ar"));
        var controller = CriarController();

        // Act
        var resultado = await controller.Delete(3);

        // Assert
        resultado.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(503);
        _repositorioMock.Verify(r => r.Delete(It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public async Task Delete_ResponsavelInexistente_DeveRetornarNotFoundSemChamarOJavaNemRemover()
    {
        // Arrange
        _repositorioMock.Setup(r => r.GetById(404)).Returns((Responsavel?)null);
        var controller = CriarController();

        // Act
        var resultado = await controller.Delete(404);

        // Assert
        resultado.Should().BeOfType<NotFoundObjectResult>();
        _javaMock.Verify(j => j.ApagarPetsDoResponsavelAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
        _repositorioMock.Verify(r => r.Delete(It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public void Cadastrar_RepositorioIndisponivel_DeveRegistrarLogDeErroERetornar500()
    {
        // Arrange
        var dto = NovoCadastroDto();
        _repositorioMock.Setup(r => r.GetByCpf(dto.Cpf)).Returns((Responsavel?)null);
        _repositorioMock.Setup(r => r.Add(It.IsAny<Responsavel>()))
            .Throws(new InvalidOperationException("Banco de dados indisponível"));
        var controller = CriarController();

        // Act
        var resultado = controller.Cadastrar(dto);

        // Assert
        var erro = resultado.Should().BeOfType<ObjectResult>().Subject;
        erro.StatusCode.Should().Be(500);
        _loggerMock.Verify(l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()),
            Times.Once);
    }

    private const string SenhaPadrao = "SenhaSegura@123";

    private static Responsavel NovoResponsavel(long id = 1) => new()
    {
        Id = id, Nome = "Pedro Chasci", Cpf = "12345678901",
        Email = "pedro@pethub.com", Senha = SenhaPadrao, Ativo = true
    };

    private static Responsavel NovoResponsavelComSenhaHasheada()
    {
        var responsavel = NovoResponsavel();
        responsavel.Senha = BCrypt.Net.BCrypt.HashPassword(SenhaPadrao);
        return responsavel;
    }

    private static AtualizarResponsavelDto NovaAtualizacaoDto(
        string nome = "Pedro Chasci", string email = "pedro@pethub.com") => new()
    {
        Nome = nome, Cpf = "12345678901", Email = email
    };

    private static CadastrarResponsavelDto NovoCadastroDto(
        string nome = "Pedro Chasci", string email = "pedro@pethub.com") => new()
    {
        Nome = nome, Cpf = "12345678901", Email = email, Senha = SenhaPadrao
    };
}
