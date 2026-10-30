using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Vitalis.Tests.Unit.Fixtures;
using Xunit;

namespace Vitalis.Tests.Unit.Api;

/// <summary>A camada HTTP do responsável: token de serviço, 201 com o JWT e links do recurso.</summary>
public class ResponsaveisControllerTests : IClassFixture<ConfiguracaoFixture>
{
    private readonly ConfiguracaoFixture _fixture;
    private readonly Mock<IServicoDeResponsaveis> _servico = new();

    public ResponsaveisControllerTests(ConfiguracaoFixture fixture) => _fixture = fixture;

    private ResponsaveisController CriarController(string? tokenDeServico = null)
        => ConfiguracaoFixture.ComHttpContext(
            new ResponsaveisController(_servico.Object, _fixture.Autenticador), tokenDeServico);

    [Fact]
    public void BuscarPorCpf_TokenDeServicoValido_DeveDevolverOResumoDoResponsavel()
    {
        // Arrange
        _servico.Setup(s => s.BuscarPorCpf("12345678901")).Returns(NovoResponsavel());

        // Act
        var resultado = CriarController(ChavesDeTeste.TokenDoJava()).BuscarPorCpf("12345678901");

        // Assert
        var resumo = resultado.Should().BeOfType<OkObjectResult>().Which.Value
            .Should().BeOfType<ResponsavelResumoResposta>().Subject;
        resumo.Cpf.Should().Be("12345678901");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("token-qualquer")]
    public void BuscarPorCpf_SemTokenDeServicoValido_DeveDevolver401SemChamarOCasoDeUso(string? token)
    {
        // Act
        var resultado = CriarController(token).BuscarPorCpf("12345678901");

        // Assert
        resultado.Should().BeOfType<UnauthorizedObjectResult>();
        _servico.Verify(s => s.BuscarPorCpf(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Cadastrar_Sucesso_DeveDevolver201ApontandoParaOResponsavelComOToken()
    {
        // Arrange
        var dados = new CadastrarResponsavelDto { Nome = "Pedro", Cpf = "12345678901", Email = "p@pethub.com", Senha = "x" };
        _servico.Setup(s => s.Cadastrar(dados)).Returns(new Sessao(NovoResponsavel(id: 10), "jwt-de-teste"));

        // Act
        var resultado = CriarController().Cadastrar(dados);

        // Assert
        var criado = resultado.Should().BeOfType<CreatedAtActionResult>().Subject;
        criado.RouteValues!["id"].Should().Be(10L);
        criado.Value.Should().BeOfType<SessaoResposta>().Which.Token.Should().Be("jwt-de-teste");
    }

    [Fact]
    public void Cadastrar_CpfDuplicado_DeveDevolver409()
    {
        // Arrange
        _servico.Setup(s => s.Cadastrar(It.IsAny<CadastrarResponsavelDto>())).Returns(Erro.Conflito("CPF já cadastrado"));

        // Act
        var resultado = CriarController().Cadastrar(new CadastrarResponsavelDto());

        // Assert
        resultado.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public void Login_CredenciaisValidas_DeveDevolverOTokenSemASenha()
    {
        // Arrange
        _servico.Setup(s => s.Entrar(It.IsAny<LoginDto>())).Returns(new Sessao(NovoResponsavel(), "jwt-de-teste"));

        // Act
        var resultado = CriarController().Login(new LoginDto { Email = "pedro@pethub.com", Senha = "x" });

        // Assert
        var sessao = resultado.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<SessaoResposta>().Subject;
        sessao.Token.Should().Be("jwt-de-teste");
        typeof(SessaoResposta).GetProperty("Senha").Should().BeNull();
    }

    [Fact]
    public void GetById_ResponsavelExistente_DeveDevolverOsDadosComOsLinksDoRecurso()
    {
        // Arrange
        _servico.Setup(s => s.Obter(1)).Returns(NovoResponsavel());

        // Act
        var resultado = CriarController().GetById(1);

        // Assert
        var responsavel = resultado.Should().BeOfType<OkObjectResult>().Which.Value
            .Should().BeOfType<ResponsavelResposta>().Subject;
        responsavel.Links.Select(l => l.Rel).Should().Contain(["self", "enderecos", "contatos", "lembretes", "historico"]);
    }

    [Fact]
    public async Task Delete_JavaIndisponivel_DeveDevolver503()
    {
        // Arrange
        _servico.Setup(s => s.ExcluirAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Erro.Indisponivel("Não foi possível apagar os pets do responsável agora. Tente novamente."));

        // Act
        var resultado = await CriarController().Delete(1);

        // Assert
        resultado.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(503);
    }

    private static Responsavel NovoResponsavel(long id = 1) => new()
    {
        Id = id, Nome = "Pedro Chasci", Cpf = "12345678901", Email = "pedro@pethub.com", Senha = "hash", Ativo = true
    };
}
