using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Vitalis.Tests.Unit.Aplicacao;

/// <summary>Também cobre o fluxo comum de <c>ServicoDeItensDoResponsavel</c>, compartilhado com os contatos.</summary>
public class ServicoDeEnderecosTests
{
    private readonly Mock<IResponsavelEnderecoRepository> _repositorio = new();
    private readonly ServicoDeEnderecos _servico;

    public ServicoDeEnderecosTests()
        => _servico = new ServicoDeEnderecos(_repositorio.Object, NullLogger<ServicoDeEnderecos>.Instance);

    /// <summary>Simula o banco: a lista e a busca por id devolvem as mesmas instâncias, como o EF faz.</summary>
    private void NoBanco(long responsavelId, params ResponsavelEndereco[] enderecos)
    {
        _repositorio.Setup(r => r.ListarDoResponsavel(responsavelId)).Returns(enderecos);
        foreach (var endereco in enderecos)
            _repositorio.Setup(r => r.ObterPorId(endereco.Id)).Returns(endereco);
    }

    [Fact]
    public void Listar_FiltrosEOrdenacao_DeveRepassarAoRepositorioEDevolverAPagina()
    {
        // Arrange
        var filtro = new FiltroDeEnderecos { Estado = "SP" };
        var consulta = new ConsultaPaginada { OrdenarPor = "cidade" };
        _repositorio.Setup(r => r.Consultar(1, filtro, consulta)).Returns(([NovoEndereco(1)], 7));

        // Act
        var resultado = _servico.Listar(1, filtro, consulta);

        // Assert
        resultado.Valor.Itens.Should().ContainSingle();
        resultado.Valor.Total.Should().Be(7);
    }

    [Fact]
    public void Listar_OrdenacaoPorCampoDesconhecido_DeveDevolverInvalidoSemConsultar()
    {
        // Act
        var resultado = _servico.Listar(1, new FiltroDeEnderecos(), new ConsultaPaginada { OrdenarPor = "cpf" });

        // Assert
        resultado.Erro!.Tipo.Should().Be(TipoDeErro.Invalido);
        resultado.Erro.Campo.Should().Be("ordenarPor");
        _repositorio.Verify(r => r.Consultar(It.IsAny<long>(), It.IsAny<FiltroDeEnderecos>(), It.IsAny<ConsultaPaginada>()), Times.Never);
    }

    [Fact]
    public void Obter_EnderecoDoProprioResponsavel_DeveDevolverOEndereco()
    {
        // Arrange
        NoBanco(1, NovoEndereco(id: 4));

        // Act
        var resultado = _servico.Obter(1, 4);

        // Assert
        resultado.Valor.Id.Should().Be(4);
    }

    [Fact]
    public void Obter_EnderecoDeOutroResponsavel_DeveDevolverNaoEncontradoSemRevelarQueExiste()
    {
        // Arrange
        NoBanco(2, NovoEndereco(id: 4, responsavelId: 2));

        // Act
        var resultado = _servico.Obter(1, 4);

        // Assert
        resultado.Erro.Should().Be(Erro.NaoEncontrado("Endereço não encontrado"));
    }

    [Fact]
    public void Adicionar_PrimeiroEnderecoDoResponsavel_DeveVirarPrincipalEAssociarAoResponsavel()
    {
        // Arrange
        NoBanco(1);
        ResponsavelEndereco? gravado = null;
        _repositorio.Setup(r => r.Adicionar(It.IsAny<ResponsavelEndereco>())).Callback<ResponsavelEndereco>(e => gravado = e);

        // Act
        _servico.Adicionar(1, NovoDto(principal: false));

        // Assert
        gravado!.Principal.Should().BeTrue();
        gravado.ResponsavelId.Should().Be(1);
        _repositorio.Verify(r => r.SalvarAlteracoes(), Times.Once);
    }

    [Fact]
    public void Adicionar_NovoPrincipal_DeveDesmarcarOAnteriorNaMesmaGravacao()
    {
        // Arrange
        var anterior = NovoEndereco(id: 1, principal: true);
        NoBanco(1, anterior);

        // Act
        var novo = _servico.Adicionar(1, NovoDto(principal: true));

        // Assert
        novo.Principal.Should().BeTrue();
        anterior.Principal.Should().BeFalse();
        _repositorio.Verify(r => r.SalvarAlteracoes(), Times.Once);
    }

    [Fact]
    public void Adicionar_SegundoEnderecoNaoPrincipal_DeveManterOPrincipalAtual()
    {
        // Arrange
        var atual = NovoEndereco(id: 1, principal: true);
        NoBanco(1, atual);

        // Act
        var novo = _servico.Adicionar(1, NovoDto(principal: false));

        // Assert
        novo.Principal.Should().BeFalse();
        atual.Principal.Should().BeTrue();
    }

    [Fact]
    public void Atualizar_EnderecoExistente_DeveAplicarOsDadosSemTrocarOsIdentificadores()
    {
        // Arrange
        var existente = NovoEndereco(id: 4, responsavelId: 2);
        NoBanco(2, existente);
        var dados = NovoDto();
        dados.Cidade = "Campinas";

        // Act
        var resultado = _servico.Atualizar(2, 4, dados);

        // Assert
        resultado.Sucesso.Should().BeTrue();
        existente.Cidade.Should().Be("Campinas");
        existente.Id.Should().Be(4);
        existente.ResponsavelId.Should().Be(2);
        _repositorio.Verify(r => r.SalvarAlteracoes(), Times.Once);
    }

    [Fact]
    public void Atualizar_EnderecoInexistente_DeveDevolverNaoEncontradoSemGravar()
    {
        // Arrange
        _repositorio.Setup(r => r.ObterPorId(404)).Returns((ResponsavelEndereco?)null);

        // Act
        var resultado = _servico.Atualizar(1, 404, NovoDto());

        // Assert
        resultado.Erro!.Tipo.Should().Be(TipoDeErro.NaoEncontrado);
        _repositorio.Verify(r => r.SalvarAlteracoes(), Times.Never);
    }

    [Fact]
    public void Remover_EnderecoPrincipal_DevePassarAMarcacaoParaOMaisRecenteDosQueSobram()
    {
        // Arrange
        var principal = NovoEndereco(id: 1, principal: true);
        var antigo = NovoEndereco(id: 2);
        var recente = NovoEndereco(id: 3);
        NoBanco(1, principal, antigo, recente);

        // Act
        var resultado = _servico.Remover(1, 1);

        // Assert
        resultado.Sucesso.Should().BeTrue();
        recente.Principal.Should().BeTrue();
        antigo.Principal.Should().BeFalse();
        _repositorio.Verify(r => r.Remover(principal), Times.Once);
        _repositorio.Verify(r => r.SalvarAlteracoes(), Times.Once);
    }

    [Fact]
    public void Remover_EnderecoDeOutroResponsavel_DeveDevolverNaoEncontradoSemRemover()
    {
        // Arrange
        NoBanco(2, NovoEndereco(id: 4, responsavelId: 2));

        // Act
        var resultado = _servico.Remover(1, 4);

        // Assert
        resultado.Erro!.Tipo.Should().Be(TipoDeErro.NaoEncontrado);
        _repositorio.Verify(r => r.Remover(It.IsAny<ResponsavelEndereco>()), Times.Never);
    }

    [Fact]
    public void DefinirPrincipal_ComDoisEnderecos_DeveTransferirAMarcacao()
    {
        // Arrange
        var primeiro = NovoEndereco(id: 1, principal: true);
        var segundo = NovoEndereco(id: 2);
        NoBanco(1, primeiro, segundo);

        // Act
        var resultado = _servico.DefinirPrincipal(1, 2);

        // Assert
        resultado.Sucesso.Should().BeTrue();
        primeiro.Principal.Should().BeFalse();
        segundo.Principal.Should().BeTrue();
        _repositorio.Verify(r => r.SalvarAlteracoes(), Times.Once);
    }

    [Fact]
    public void DefinirPrincipal_EnderecoDeOutroResponsavel_DeveDevolverNaoEncontradoSemGravar()
    {
        // Arrange
        NoBanco(2, NovoEndereco(id: 4, responsavelId: 2));

        // Act
        var resultado = _servico.DefinirPrincipal(1, 4);

        // Assert
        resultado.Erro!.Tipo.Should().Be(TipoDeErro.NaoEncontrado);
        _repositorio.Verify(r => r.SalvarAlteracoes(), Times.Never);
    }

    private static ResponsavelEndereco NovoEndereco(long id, long responsavelId = 1, bool principal = false) => new()
    {
        Id = id, ResponsavelId = responsavelId, Logradouro = "Av. Paulista", Numero = "1000",
        Bairro = "Bela Vista", Cidade = "São Paulo", Estado = "SP", Cep = "01310100", Principal = principal
    };

    private static EnderecoDto NovoDto(bool principal = false) => new()
    {
        Logradouro = "Av. Paulista", Numero = "1000", Bairro = "Bela Vista", Cidade = "São Paulo",
        Estado = "SP", Cep = "01310100", Principal = principal
    };
}
