using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Vitalis.Tests.Unit.Aplicacao;

public class ServicoDeResponsaveisTests
{
    private readonly Mock<IResponsavelRepository> _repositorio = new();
    private readonly Mock<IHasherDeSenha> _hasher = new();
    private readonly Mock<IEmissorDeToken> _tokens = new();
    private readonly Mock<IPethubJavaClient> _java = new();
    private readonly Mock<IRelogio> _relogio = new();
    private readonly ServicoDeResponsaveis _servico;

    private static readonly DateTime Agora = new(2026, 10, 30, 10, 0, 0);

    public ServicoDeResponsaveisTests()
    {
        _hasher.Setup(h => h.Gerar(It.IsAny<string>())).Returns<string>(senha => "hash:" + senha);
        _hasher.Setup(h => h.Verificar(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>((senha, hash) => hash == "hash:" + senha);
        _tokens.Setup(t => t.Emitir(It.IsAny<Responsavel>())).Returns("jwt-de-teste");
        _relogio.Setup(r => r.Agora()).Returns(Agora);

        _servico = new ServicoDeResponsaveis(_repositorio.Object, _hasher.Object, _tokens.Object, _java.Object,
            _relogio.Object, NullLogger<ServicoDeResponsaveis>.Instance);
    }

    [Fact]
    public void Obter_ResponsavelExistente_DeveDevolverOResponsavel()
    {
        // Arrange
        var existente = NovoResponsavel(id: 1);
        _repositorio.Setup(r => r.ObterPorId(1)).Returns(existente);

        // Act
        var resultado = _servico.Obter(1);

        // Assert
        resultado.Sucesso.Should().BeTrue();
        resultado.Valor.Should().BeSameAs(existente);
    }

    [Fact]
    public void Obter_ResponsavelInexistente_DeveDevolverNaoEncontrado()
    {
        // Arrange
        _repositorio.Setup(r => r.ObterPorId(999)).Returns((Responsavel?)null);

        // Act
        var resultado = _servico.Obter(999);

        // Assert
        resultado.Erro!.Tipo.Should().Be(TipoDeErro.NaoEncontrado);
    }

    [Fact]
    public void BuscarPorCpf_CpfCadastrado_DeveDevolverOResponsavel()
    {
        // Arrange
        _repositorio.Setup(r => r.ObterPorCpf("12345678901")).Returns(NovoResponsavel());

        // Act
        var resultado = _servico.BuscarPorCpf("12345678901");

        // Assert
        resultado.Valor.Cpf.Should().Be("12345678901");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BuscarPorCpf_CpfEmBranco_DeveDevolverInvalidoSemConsultar(string cpf)
    {
        // Act
        var resultado = _servico.BuscarPorCpf(cpf);

        // Assert
        resultado.Erro.Should().Be(Erro.Invalido("cpf", "CPF é obrigatório"));
        _repositorio.Verify(r => r.ObterPorCpf(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void BuscarPorCpf_CpfNaoCadastrado_DeveDevolverNaoEncontrado()
    {
        // Arrange
        _repositorio.Setup(r => r.ObterPorCpf("00000000000")).Returns((Responsavel?)null);

        // Act
        var resultado = _servico.BuscarPorCpf("00000000000");

        // Assert
        resultado.Erro!.Tipo.Should().Be(TipoDeErro.NaoEncontrado);
    }

    [Fact]
    public void Cadastrar_DadosValidos_DeveGuardarSoOHashDaSenhaComADataDoRelogioEEmitirToken()
    {
        // Arrange
        var dados = NovoCadastro();
        Responsavel? gravado = null;
        _repositorio.Setup(r => r.Adicionar(It.IsAny<Responsavel>())).Callback<Responsavel>(r => gravado = r);

        // Act
        var resultado = _servico.Cadastrar(dados);

        // Assert
        resultado.Sucesso.Should().BeTrue();
        resultado.Valor.Token.Should().Be("jwt-de-teste");
        gravado!.Senha.Should().Be("hash:" + dados.Senha).And.NotBe(dados.Senha);
        gravado.CreatedAt.Should().Be(Agora);
        gravado.Ativo.Should().BeTrue();
        _repositorio.Verify(r => r.SalvarAlteracoes(), Times.Once);
    }

    [Fact]
    public void Cadastrar_CpfJaExistente_DeveDevolverConflitoSemGravar()
    {
        // Arrange
        var dados = NovoCadastro();
        _repositorio.Setup(r => r.ObterPorCpf(dados.Cpf)).Returns(NovoResponsavel());

        // Act
        var resultado = _servico.Cadastrar(dados);

        // Assert
        resultado.Erro!.Tipo.Should().Be(TipoDeErro.Conflito);
        _repositorio.Verify(r => r.Adicionar(It.IsAny<Responsavel>()), Times.Never);
        _repositorio.Verify(r => r.SalvarAlteracoes(), Times.Never);
    }

    [Fact]
    public void Cadastrar_BancoIndisponivel_DevePropagarAExcecaoParaOTratadorGlobalSemEmitirToken()
    {
        // Arrange
        _repositorio.Setup(r => r.SalvarAlteracoes()).Throws(new InvalidOperationException("ORA-12541"));

        // Act
        var cadastrar = () => _servico.Cadastrar(NovoCadastro());

        // Assert
        cadastrar.Should().Throw<InvalidOperationException>();
        _tokens.Verify(t => t.Emitir(It.IsAny<Responsavel>()), Times.Never);
    }

    [Fact]
    public void Entrar_CredenciaisValidas_DeveDevolverOResponsavelEOToken()
    {
        // Arrange
        _repositorio.Setup(r => r.ObterPorEmail("pedro@pethub.com")).Returns(NovoResponsavel(senha: "hash:Senha@123"));

        // Act
        var resultado = _servico.Entrar(new LoginDto { Email = "pedro@pethub.com", Senha = "Senha@123" });

        // Assert
        resultado.Valor.Token.Should().Be("jwt-de-teste");
        resultado.Valor.Responsavel.Email.Should().Be("pedro@pethub.com");
    }

    [Fact]
    public void Entrar_SenhaIncorreta_DeveDevolverNaoAutorizado()
    {
        // Arrange
        _repositorio.Setup(r => r.ObterPorEmail("pedro@pethub.com")).Returns(NovoResponsavel(senha: "hash:Senha@123"));

        // Act
        var resultado = _servico.Entrar(new LoginDto { Email = "pedro@pethub.com", Senha = "errada" });

        // Assert
        resultado.Erro.Should().Be(Erro.NaoAutorizado("Credenciais inválidas"));
        _tokens.Verify(t => t.Emitir(It.IsAny<Responsavel>()), Times.Never);
    }

    [Fact]
    public void Entrar_EmailNaoCadastrado_DeveDevolverAMesmaMensagemDeSenhaIncorreta()
    {
        // Arrange
        _repositorio.Setup(r => r.ObterPorEmail(It.IsAny<string>())).Returns((Responsavel?)null);

        // Act
        var resultado = _servico.Entrar(new LoginDto { Email = "ninguem@pethub.com", Senha = "x" });

        // Assert
        resultado.Erro.Should().Be(Erro.NaoAutorizado("Credenciais inválidas"));
    }

    [Fact]
    public void Entrar_ContaDesativada_DeveDevolverNaoAutorizado()
    {
        // Arrange
        var desativado = NovoResponsavel(senha: "hash:Senha@123");
        desativado.Ativo = false;
        _repositorio.Setup(r => r.ObterPorEmail("pedro@pethub.com")).Returns(desativado);

        // Act
        var resultado = _servico.Entrar(new LoginDto { Email = "pedro@pethub.com", Senha = "Senha@123" });

        // Assert
        resultado.Erro.Should().Be(Erro.NaoAutorizado("Conta desativada"));
    }

    [Fact]
    public void Atualizar_ResponsavelExistente_DeveAlterarOsDadosEPreservarASenha()
    {
        // Arrange
        var existente = NovoResponsavel(senha: "hash:original");
        _repositorio.Setup(r => r.ObterPorId(1)).Returns(existente);

        // Act
        var resultado = _servico.Atualizar(1, new AtualizarResponsavelDto
        {
            Nome = "Pedro Atualizado", Cpf = "98765432100", Email = "novo@pethub.com"
        });

        // Assert
        resultado.Sucesso.Should().BeTrue();
        existente.Nome.Should().Be("Pedro Atualizado");
        existente.Email.Should().Be("novo@pethub.com");
        existente.Senha.Should().Be("hash:original");
        _repositorio.Verify(r => r.SalvarAlteracoes(), Times.Once);
    }

    [Fact]
    public void Atualizar_ResponsavelInexistente_DeveDevolverNaoEncontradoSemGravar()
    {
        // Arrange
        _repositorio.Setup(r => r.ObterPorId(404)).Returns((Responsavel?)null);

        // Act
        var resultado = _servico.Atualizar(404, new AtualizarResponsavelDto { Nome = "x", Cpf = "1", Email = "x@x.com" });

        // Assert
        resultado.Erro!.Tipo.Should().Be(TipoDeErro.NaoEncontrado);
        _repositorio.Verify(r => r.SalvarAlteracoes(), Times.Never);
    }

    [Fact]
    public async Task Excluir_ResponsavelExistente_DeveApagarOsPetsNoJavaAntesDeRemoverOCadastro()
    {
        // Arrange
        var existente = NovoResponsavel(id: 3);
        var ordem = new List<string>();
        _repositorio.Setup(r => r.ObterPorId(3)).Returns(existente);
        _java.Setup(j => j.ApagarPetsDoResponsavelAsync(3, It.IsAny<CancellationToken>()))
            .Callback(() => ordem.Add("java")).Returns(Task.CompletedTask);
        _repositorio.Setup(r => r.Remover(existente)).Callback(() => ordem.Add("remover"));

        // Act
        var resultado = await _servico.ExcluirAsync(3);

        // Assert
        resultado.Sucesso.Should().BeTrue();
        ordem.Should().Equal("java", "remover");
        _repositorio.Verify(r => r.SalvarAlteracoes(), Times.Once);
    }

    [Fact]
    public async Task Excluir_JavaNaoConfirmaARemocaoDosPets_DeveDevolverIndisponivelSemRemoverOCadastro()
    {
        // Arrange
        _repositorio.Setup(r => r.ObterPorId(3)).Returns(NovoResponsavel(id: 3));
        _java.Setup(j => j.ApagarPetsDoResponsavelAsync(3, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ServicoIndisponivelException("fora do ar"));

        // Act
        var resultado = await _servico.ExcluirAsync(3);

        // Assert
        resultado.Erro!.Tipo.Should().Be(TipoDeErro.Indisponivel);
        _repositorio.Verify(r => r.Remover(It.IsAny<Responsavel>()), Times.Never);
        _repositorio.Verify(r => r.SalvarAlteracoes(), Times.Never);
    }

    [Fact]
    public async Task Excluir_ResponsavelInexistente_DeveDevolverNaoEncontradoSemChamarOJava()
    {
        // Arrange
        _repositorio.Setup(r => r.ObterPorId(404)).Returns((Responsavel?)null);

        // Act
        var resultado = await _servico.ExcluirAsync(404);

        // Assert
        resultado.Erro!.Tipo.Should().Be(TipoDeErro.NaoEncontrado);
        _java.Verify(j => j.ApagarPetsDoResponsavelAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Responsavel NovoResponsavel(long id = 1, string senha = "hash:x") => new()
    {
        Id = id, Nome = "Pedro Chasci", Cpf = "12345678901", Email = "pedro@pethub.com", Senha = senha, Ativo = true
    };

    private static CadastrarResponsavelDto NovoCadastro() => new()
    {
        Nome = "Pedro Chasci", Cpf = "12345678901", Email = "pedro@pethub.com", Senha = "SenhaSegura@123"
    };
}
