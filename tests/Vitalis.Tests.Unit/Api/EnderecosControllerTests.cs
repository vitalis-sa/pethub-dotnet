using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Vitalis.Tests.Unit.Fixtures;
using Xunit;

namespace Vitalis.Tests.Unit.Api;

/// <summary>A camada HTTP de endereços (contatos usam o mesmo fluxo): links de cada item e 201 com Location.</summary>
public class EnderecosControllerTests
{
    private readonly Mock<IServicoDeEnderecos> _servico = new();

    private EnderecosController CriarController() => ConfiguracaoFixture.ComHttpContext(new EnderecosController(_servico.Object));

    [Fact]
    public void GetAll_PaginaComEnderecos_DeveTrazerOsLinksDeCadaEndereco()
    {
        // Arrange
        _servico.Setup(s => s.Listar(1, It.IsAny<FiltroDeEnderecos>(), It.IsAny<ConsultaPaginada>()))
            .Returns(new Pagina<ResponsavelEndereco>([NovoEndereco(id: 1)], 1));

        // Act
        var resultado = CriarController().GetAll(1, new FiltroDeEnderecos(), new ConsultaPaginada());

        // Assert
        var pagina = resultado.Should().BeOfType<OkObjectResult>().Which.Value
            .Should().BeOfType<PaginaDeResultados<EnderecoResposta>>().Subject;
        pagina.Itens[0].Links.Should().Contain(l => l.Rel == "definir-principal"
                                                    && l.Href == "/api/responsavel/1/enderecos/1/principal");
    }

    [Fact]
    public void GetById_EnderecoExistente_DeveDevolverOsMesmosCamposDaEntidadeMaisLinks()
    {
        // Arrange
        var endereco = NovoEndereco(id: 4);
        _servico.Setup(s => s.Obter(1, 4)).Returns(endereco);

        // Act
        var resultado = CriarController().GetById(1, 4);

        // Assert
        var resposta = resultado.Should().BeOfType<OkObjectResult>().Which.Value
            .Should().BeOfType<EnderecoResposta>().Subject;
        resposta.Cidade.Should().Be(endereco.Cidade);
        resposta.Links.Should().Contain(l => l.Rel == "self" && l.Href == "/api/responsavel/1/enderecos/4");
    }

    [Fact]
    public void Add_EnderecoValido_DeveDevolver201ApontandoParaONovoEndereco()
    {
        // Arrange
        var dados = new EnderecoDto
        {
            Logradouro = "Av. Paulista", Numero = "1000", Bairro = "Bela Vista", Cidade = "São Paulo", Estado = "SP", Cep = "01310100"
        };
        _servico.Setup(s => s.Adicionar(1, dados)).Returns(NovoEndereco(id: 9));

        // Act
        var resultado = CriarController().Add(1, dados);

        // Assert
        var criado = resultado.Should().BeOfType<CreatedAtActionResult>().Subject;
        criado.RouteValues!["id"].Should().Be(9L);
        criado.Value.Should().BeOfType<EnderecoResposta>();
    }

    [Fact]
    public void SetPrincipal_EnderecoDeOutroResponsavel_DeveDevolver404()
    {
        // Arrange
        _servico.Setup(s => s.DefinirPrincipal(1, 4)).Returns(Erro.NaoEncontrado("Endereço não encontrado"));

        // Act
        var resultado = CriarController().SetPrincipal(1, 4);

        // Assert
        resultado.Should().BeOfType<NotFoundObjectResult>();
    }

    private static ResponsavelEndereco NovoEndereco(long id, long responsavelId = 1) => new()
    {
        Id = id, ResponsavelId = responsavelId, Logradouro = "Av. Paulista", Numero = "1000",
        Bairro = "Bela Vista", Cidade = "São Paulo", Estado = "SP", Cep = "01310100"
    };
}
