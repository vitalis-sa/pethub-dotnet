using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Vitalis.Tests.Unit.Aplicacao;

/// <summary>O fluxo comum está coberto em <see cref="ServicoDeEnderecosTests"/>; aqui, o que é próprio do contato.</summary>
public class ServicoDeContatosTests
{
    private readonly Mock<IResponsavelContatoRepository> _repositorio = new();
    private readonly ServicoDeContatos _servico;

    public ServicoDeContatosTests()
        => _servico = new ServicoDeContatos(_repositorio.Object, NullLogger<ServicoDeContatos>.Instance);

    [Fact]
    public void Listar_OrdenacaoPorTelefone_DeveSerAceita()
    {
        // Arrange
        var consulta = new ConsultaPaginada { OrdenarPor = "telefone" };
        _repositorio.Setup(r => r.Consultar(1, It.IsAny<FiltroDeContatos>(), consulta)).Returns(([], 0));

        // Act
        var resultado = _servico.Listar(1, new FiltroDeContatos(), consulta);

        // Assert
        resultado.Sucesso.Should().BeTrue();
    }

    [Fact]
    public void Listar_OrdenacaoPorCampoDoEndereco_DeveDevolverInvalido()
    {
        // Act
        var resultado = _servico.Listar(1, new FiltroDeContatos(), new ConsultaPaginada { OrdenarPor = "cidade" });

        // Assert
        resultado.Erro!.Mensagem.Should().Be("Ordene por um destes campos: id, tipo, telefone.");
    }

    [Fact]
    public void Obter_ContatoInexistente_DeveDevolverNaoEncontradoComAMensagemDoContato()
    {
        // Arrange
        _repositorio.Setup(r => r.ObterPorId(99)).Returns((ResponsavelContato?)null);

        // Act
        var resultado = _servico.Obter(1, 99);

        // Assert
        resultado.Erro.Should().Be(Erro.NaoEncontrado("Contato não encontrado"));
    }

    [Fact]
    public void Adicionar_ContatoValido_DeveConverterODtoEAssociarAoResponsavel()
    {
        // Arrange
        _repositorio.Setup(r => r.ListarDoResponsavel(3)).Returns([]);

        // Act
        var contato = _servico.Adicionar(3, new ContatoDto { Tipo = "CELULAR", Telefone = "11999998888" });

        // Assert
        contato.ResponsavelId.Should().Be(3);
        contato.Telefone.Should().Be("11999998888");
        contato.Principal.Should().BeTrue();
        _repositorio.Verify(r => r.Adicionar(contato), Times.Once);
    }

    [Fact]
    public void Atualizar_ContatoExistente_DeveAplicarOsDadosSemTrocarOsIdentificadores()
    {
        // Arrange
        var existente = new ResponsavelContato { Id = 4, ResponsavelId = 2, Tipo = "CELULAR", Telefone = "11911111111" };
        _repositorio.Setup(r => r.ObterPorId(4)).Returns(existente);
        _repositorio.Setup(r => r.ListarDoResponsavel(2)).Returns([existente]);

        // Act
        var resultado = _servico.Atualizar(2, 4, new ContatoDto { Tipo = "TELEFONE", Telefone = "1133334444" });

        // Assert
        resultado.Sucesso.Should().BeTrue();
        existente.Tipo.Should().Be("TELEFONE");
        existente.Telefone.Should().Be("1133334444");
        existente.Id.Should().Be(4);
        existente.ResponsavelId.Should().Be(2);
    }

    [Fact]
    public void DefinirPrincipal_ContatoDoProprioResponsavel_DeveMarcarEGravar()
    {
        // Arrange
        var contato = new ResponsavelContato { Id = 5, ResponsavelId = 1, Tipo = "CELULAR", Telefone = "11911111111" };
        _repositorio.Setup(r => r.ObterPorId(5)).Returns(contato);
        _repositorio.Setup(r => r.ListarDoResponsavel(1)).Returns([contato]);

        // Act
        var resultado = _servico.DefinirPrincipal(1, 5);

        // Assert
        resultado.Sucesso.Should().BeTrue();
        contato.Principal.Should().BeTrue();
        _repositorio.Verify(r => r.SalvarAlteracoes(), Times.Once);
    }
}
