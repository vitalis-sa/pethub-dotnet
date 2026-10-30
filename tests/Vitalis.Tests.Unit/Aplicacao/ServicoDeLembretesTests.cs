using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Vitalis.Tests.Unit.Aplicacao;

public class ServicoDeLembretesTests
{
    private readonly Mock<ILembreteRepository> _repositorio = new();
    private readonly Mock<IHistoricoDeMensagens> _historico = new();
    private readonly Mock<IRelogio> _relogio = new();
    private readonly ServicoDeLembretes _servico;

    private static readonly DateTime Agora = new(2026, 10, 30, 14, 0, 0);

    public ServicoDeLembretesTests()
    {
        _relogio.Setup(r => r.Agora()).Returns(Agora);
        _servico = new ServicoDeLembretes(_repositorio.Object, _historico.Object, _relogio.Object,
            NullLogger<ServicoDeLembretes>.Instance);
    }

    [Fact]
    public void Listar_FiltrosEOrdenacao_DeveRepassarTudoAoRepositorio()
    {
        // Arrange
        var filtro = new FiltroDeLembretes { Tipo = TipoLembrete.CONSULTA, Status = StatusLembrete.PENDENTE, PetId = 5 };
        var consulta = new ConsultaPaginada { OrdenarPor = "tipo", Direcao = "asc" };
        _repositorio.Setup(r => r.Consultar(1, filtro, consulta)).Returns(([NovoLembrete()], 25));

        // Act
        var resultado = _servico.Listar(1, filtro, consulta);

        // Assert
        resultado.Valor.Total.Should().Be(25);
        _repositorio.Verify(r => r.Consultar(1, filtro, consulta), Times.Once);
    }

    [Fact]
    public void Listar_OrdenacaoPorCampoDesconhecido_DeveDevolverInvalidoSemConsultar()
    {
        // Act
        var resultado = _servico.Listar(1, new FiltroDeLembretes(), new ConsultaPaginada { OrdenarPor = "senha" });

        // Assert
        resultado.Erro!.Campo.Should().Be("ordenarPor");
        _repositorio.Verify(r => r.Consultar(It.IsAny<long>(), It.IsAny<FiltroDeLembretes>(), It.IsAny<ConsultaPaginada>()), Times.Never);
    }

    [Fact]
    public void Listar_PeriodoComInicioDepoisDoFim_DeveDevolverInvalidoNoCampoDe()
    {
        // Arrange
        var filtro = new FiltroDeLembretes { De = new DateOnly(2026, 12, 1), Ate = new DateOnly(2026, 11, 1) };

        // Act
        var resultado = _servico.Listar(1, filtro, new ConsultaPaginada());

        // Assert
        resultado.Erro.Should().Be(Erro.Invalido("de", "A data inicial não pode ser depois da final."));
    }

    [Fact]
    public void ListarDosesFuturas_DeveUsarOHorarioDoNegocioEOLimiteDoIos()
    {
        // Arrange
        _repositorio.Setup(r => r.ListarDosesFuturas(1, Agora, ServicoDeLembretes.LimiteDeDosesFuturas))
            .Returns([NovoLembrete(id: 7, tipo: TipoLembrete.MEDICAMENTO)]);

        // Act
        var doses = _servico.ListarDosesFuturas(1);

        // Assert
        doses.Should().ContainSingle(d => d.Id == 7);
        ServicoDeLembretes.LimiteDeDosesFuturas.Should().BeLessThan(64);
    }

    [Fact]
    public async Task ListarHistorico_MongoNaoConfigurado_DeveDevolverIndisponivel()
    {
        // Arrange
        _historico.SetupGet(h => h.Habilitado).Returns(false);

        // Act
        var resultado = await _servico.ListarHistoricoAsync(1, new FiltroDeHistorico(), new ConsultaPaginada());

        // Assert
        resultado.Erro!.Tipo.Should().Be(TipoDeErro.Indisponivel);
    }

    [Fact]
    public async Task ListarHistorico_MongoConfigurado_DeveDevolverAPaginaDoHistorico()
    {
        // Arrange
        _historico.SetupGet(h => h.Habilitado).Returns(true);
        _historico.Setup(h => h.ConsultarAsync(1, It.IsAny<FiltroDeHistorico>(), It.IsAny<ConsultaPaginada>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Pagina<RegistroDeMensagem>([new RegistroDeMensagem { Motivo = "lembrete criado" }], 1));

        // Act
        var resultado = await _servico.ListarHistoricoAsync(1, new FiltroDeHistorico(), new ConsultaPaginada());

        // Assert
        resultado.Valor.Itens.Should().ContainSingle().Which.Motivo.Should().Be("lembrete criado");
    }

    [Fact]
    public async Task ListarHistorico_OrdenacaoPorCampoDesconhecido_DeveDevolverInvalidoSemConsultar()
    {
        // Arrange
        _historico.SetupGet(h => h.Habilitado).Returns(true);

        // Act
        var resultado = await _servico.ListarHistoricoAsync(1, new FiltroDeHistorico(), new ConsultaPaginada { OrdenarPor = "corpo" });

        // Assert
        resultado.Erro!.Tipo.Should().Be(TipoDeErro.Invalido);
        _historico.Verify(h => h.ConsultarAsync(It.IsAny<long>(), It.IsAny<FiltroDeHistorico>(), It.IsAny<ConsultaPaginada>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Obter_LembreteDoProprioResponsavel_DeveDevolverOLembrete()
    {
        // Arrange
        _repositorio.Setup(r => r.ObterPorId(3)).Returns(NovoLembrete(id: 3, responsavelId: 1));

        // Act
        var resultado = _servico.Obter(3, solicitanteId: 1);

        // Assert
        resultado.Valor.Id.Should().Be(3);
    }

    [Theory]
    [InlineData(2L)]
    [InlineData(null)]
    public void Obter_LembreteDeOutroResponsavelOuSemSolicitante_DeveDevolverProibido(long? solicitante)
    {
        // Arrange
        _repositorio.Setup(r => r.ObterPorId(3)).Returns(NovoLembrete(id: 3, responsavelId: 1));

        // Act
        var resultado = _servico.Obter(3, solicitante);

        // Assert
        resultado.Erro!.Tipo.Should().Be(TipoDeErro.Proibido);
    }

    [Fact]
    public void Obter_LembreteInexistente_DeveDevolverNaoEncontrado()
    {
        // Arrange
        _repositorio.Setup(r => r.ObterPorId(404)).Returns((Lembrete?)null);

        // Act
        var resultado = _servico.Obter(404, solicitanteId: 1);

        // Assert
        resultado.Erro.Should().Be(Erro.NaoEncontrado("Lembrete não encontrado"));
    }

    [Fact]
    public void AlterarStatus_LembreteDoProprioResponsavel_DeveAlterarEGravar()
    {
        // Arrange
        var lembrete = NovoLembrete(id: 1, responsavelId: 1);
        _repositorio.Setup(r => r.ObterPorId(1)).Returns(lembrete);

        // Act
        var resultado = _servico.AlterarStatus(1, StatusLembrete.ENVIADO, solicitanteId: 1);

        // Assert
        resultado.Sucesso.Should().BeTrue();
        lembrete.Status.Should().Be(StatusLembrete.ENVIADO);
        _repositorio.Verify(r => r.SalvarAlteracoes(), Times.Once);
    }

    [Fact]
    public void AlterarStatus_LembreteDeOutroResponsavel_DeveDevolverProibidoSemAlterar()
    {
        // Arrange
        var lembrete = NovoLembrete(id: 1, responsavelId: 1);
        _repositorio.Setup(r => r.ObterPorId(1)).Returns(lembrete);

        // Act
        var resultado = _servico.AlterarStatus(1, StatusLembrete.FALHOU, solicitanteId: 2);

        // Assert
        resultado.Erro!.Tipo.Should().Be(TipoDeErro.Proibido);
        lembrete.Status.Should().Be(StatusLembrete.PENDENTE);
        _repositorio.Verify(r => r.SalvarAlteracoes(), Times.Never);
    }

    [Fact]
    public void AlterarStatus_LembreteInexistente_DeveDevolverNaoEncontradoSemGravar()
    {
        // Arrange
        _repositorio.Setup(r => r.ObterPorId(999)).Returns((Lembrete?)null);

        // Act
        var resultado = _servico.AlterarStatus(999, StatusLembrete.FALHOU, solicitanteId: 1);

        // Assert
        resultado.Erro!.Tipo.Should().Be(TipoDeErro.NaoEncontrado);
        _repositorio.Verify(r => r.SalvarAlteracoes(), Times.Never);
    }

    [Fact]
    public void Remover_LembreteDoProprioResponsavel_DeveRemoverEGravar()
    {
        // Arrange
        var lembrete = NovoLembrete(id: 1, responsavelId: 1);
        _repositorio.Setup(r => r.ObterPorId(1)).Returns(lembrete);

        // Act
        var resultado = _servico.Remover(1, solicitanteId: 1);

        // Assert
        resultado.Sucesso.Should().BeTrue();
        _repositorio.Verify(r => r.Remover(lembrete), Times.Once);
        _repositorio.Verify(r => r.SalvarAlteracoes(), Times.Once);
    }

    [Fact]
    public void Remover_LembreteDeOutroResponsavel_DeveDevolverProibidoSemRemover()
    {
        // Arrange
        _repositorio.Setup(r => r.ObterPorId(1)).Returns(NovoLembrete(id: 1, responsavelId: 1));

        // Act
        var resultado = _servico.Remover(1, solicitanteId: 2);

        // Assert
        resultado.Erro!.Tipo.Should().Be(TipoDeErro.Proibido);
        _repositorio.Verify(r => r.Remover(It.IsAny<Lembrete>()), Times.Never);
    }

    [Fact]
    public void LiberarDosesVencidas_DeveLiberarAsDosesCujoHorarioChegouEGravar()
    {
        // Arrange
        var vencidas = new List<Lembrete>
        {
            NovoLembrete(id: 1, tipo: TipoLembrete.MEDICAMENTO),
            NovoLembrete(id: 2, tipo: TipoLembrete.MEDICAMENTO)
        };
        _repositorio.Setup(r => r.ListarDosesVencidas(Agora)).Returns(vencidas);

        // Act
        var liberadas = _servico.LiberarDosesVencidas();

        // Assert
        liberadas.Should().Be(2);
        vencidas.Should().OnlyContain(d => d.Status == StatusLembrete.ENVIADO);
        _repositorio.Verify(r => r.SalvarAlteracoes(), Times.Once);
    }

    private static Lembrete NovoLembrete(long id = 1, long responsavelId = 1, TipoLembrete tipo = TipoLembrete.VACINA) => new()
    {
        Id = id, ResponsavelId = responsavelId, PetId = 7, Tipo = tipo,
        DataAgendada = new DateOnly(2026, 9, 15), Mensagem = "Vacina antirrábica agendada"
    };
}
