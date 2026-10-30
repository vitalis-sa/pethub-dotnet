using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitalis.Consultas;
using Vitalis.Dto;
using Vitalis.Erros;
using Vitalis.Historico;
using Vitalis.Models;
using Vitalis.Repositories;
using Vitalis.Seguranca;

/// <summary>
/// Lembretes do responsável. Eles nascem de mensagens do pethub-java (veja Mensageria/LembreteConsumidor),
/// por isso não existe POST: aqui o responsável só consulta, marca o status e apaga os próprios.
/// </summary>
[ApiController]
[Authorize]
[Route("api/lembretes")]
public class LembretesApiController : ControllerBase
{
    // O iOS aceita até 64 notificações locais pendentes: não adianta devolver mais do que isso.
    private const int LimiteDeDosesFuturas = 60;
    private const string SemPermissao = "Você só pode acessar os seus próprios dados";

    private readonly ILembreteRepository _repo;
    private readonly IHistoricoDeMensagens _historico;
    // Logger usado para o registro estruturado das operações do controller
    private readonly ILogger<LembretesApiController> _logger;

    public LembretesApiController(
        ILembreteRepository repo,
        IHistoricoDeMensagens historico,
        ILogger<LembretesApiController> logger)
    {
        _repo = repo;
        _historico = historico;
        _logger = logger;
    }

    /// <summary>Lista os lembretes do responsável, com filtros, ordenação e paginação.</summary>
    /// <remarks>
    /// Filtros: <c>tipo</c>, <c>status</c>, <c>petId</c>, <c>de</c> e <c>ate</c> (data agendada, <c>yyyy-MM-dd</c>).
    /// Ordenação (<c>ordenarPor</c>): <c>dataAgendada</c> (padrão, mais recentes primeiro), <c>tipo</c>, <c>status</c>, <c>id</c>.
    /// </remarks>
    [HttpGet("responsavel/{responsavelId:long}")]
    [ApenasProprietario]
    [ProducesResponseType<PaginaDeResultados<LembreteResposta>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public IActionResult GetByResponsavel(
        long responsavelId, [FromQuery] FiltroDeLembretes filtro, [FromQuery] ConsultaPaginada consulta)
    {
        if (!consulta.OrdenacaoValida(FiltroDeLembretes.CamposOrdenaveis))
            return this.CampoInvalido("ordenarPor",
                $"Ordene por um destes campos: {string.Join(", ", FiltroDeLembretes.CamposOrdenaveis)}.");

        if (filtro.De is not null && filtro.Ate is not null && filtro.De > filtro.Ate)
            return this.CampoInvalido("de", "A data inicial não pode ser depois da final.");

        _logger.LogInformation("Buscando lembretes do responsável {ResponsavelId} (página {Pagina}).",
            responsavelId, consulta.Pagina);

        var (itens, total) = _repo.Consultar(responsavelId, filtro, consulta);
        return Ok(PaginaDeResultados.Montar(itens.Select(LembreteResposta.De).ToList(), total, consulta, Request));
    }

    /// <summary>Atalho para filtrar por tipo. Equivale a <c>GET /api/lembretes/responsavel/{id}?tipo={tipo}</c>.</summary>
    [HttpGet("responsavel/{responsavelId:long}/tipo/{tipo}")]
    [ApenasProprietario]
    [ProducesResponseType<PaginaDeResultados<LembreteResposta>>(StatusCodes.Status200OK, "application/json")]
    public IActionResult GetByResponsavelETipo(long responsavelId, TipoLembrete tipo, [FromQuery] ConsultaPaginada consulta)
        => GetByResponsavel(responsavelId, new FiltroDeLembretes { Tipo = tipo }, consulta);

    /// <summary>Próximas doses de medicamento (ainda não liberadas), para o app agendar as notificações locais.</summary>
    [HttpGet("responsavel/{responsavelId:long}/doses-futuras")]
    [ApenasProprietario]
    [ProducesResponseType<IEnumerable<LembreteResposta>>(StatusCodes.Status200OK, "application/json")]
    public IActionResult GetDosesFuturas(long responsavelId)
    {
        _logger.LogInformation("Buscando doses futuras do responsável {ResponsavelId}.", responsavelId);

        var doses = _repo.GetDosesFuturas(responsavelId, Vitalis.Configuracao.Relogio.Agora(), LimiteDeDosesFuturas);
        return Ok(doses.Select(LembreteResposta.De).ToList());
    }

    /// <summary>
    /// Histórico das mensagens que o pethub-java enviou pela fila para este responsável e o que o Vitalis fez
    /// com cada uma (lido do MongoDB).
    /// </summary>
    /// <remarks>
    /// Filtros: <c>resultado</c> (<c>Confirmar</c>, <c>Descartar</c>, <c>Repetir</c>), <c>tipo</c>, <c>de</c> e <c>ate</c> (UTC).
    /// Ordenação (<c>ordenarPor</c>): <c>recebidaEm</c> (padrão, mais recentes primeiro), <c>resultado</c>, <c>tipo</c>.
    /// Responde 503 quando o MongoDB não está configurado.
    /// </remarks>
    [HttpGet("responsavel/{responsavelId:long}/historico")]
    [ApenasProprietario]
    [ProducesResponseType<PaginaDeResultados<RegistroDeMensagemResposta>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<IActionResult> GetHistorico(
        long responsavelId, [FromQuery] FiltroDeHistorico filtro, [FromQuery] ConsultaPaginada consulta,
        CancellationToken ct = default)
    {
        if (!_historico.Habilitado)
            return this.Indisponivel("Histórico de mensagens indisponível: MongoDB não configurado.");

        if (!consulta.OrdenacaoValida(FiltroDeHistorico.CamposOrdenaveis))
            return this.CampoInvalido("ordenarPor",
                $"Ordene por um destes campos: {string.Join(", ", FiltroDeHistorico.CamposOrdenaveis)}.");

        _logger.LogInformation("Buscando histórico de mensagens do responsável {ResponsavelId} (página {Pagina}).",
            responsavelId, consulta.Pagina);

        var (itens, total) = await _historico.ConsultarAsync(responsavelId, filtro, consulta, ct);
        var respostas = itens.Select(r => RegistroDeMensagemResposta.De(r, responsavelId)).ToList();
        return Ok(PaginaDeResultados.Montar(respostas, total, consulta, Request));
    }

    /// <summary>Busca um lembrete do responsável autenticado.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType<LembreteResposta>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public IActionResult GetById(long id)
    {
        var lembrete = _repo.GetById(id);
        if (lembrete == null)
        {
            _logger.LogWarning("Lembrete {LembreteId} não encontrado.", id);
            return this.NaoEncontrado("Lembrete não encontrado");
        }

        if (!User.EhOResponsavel(lembrete.ResponsavelId))
            return this.Proibido(SemPermissao);

        return Ok(LembreteResposta.De(lembrete));
    }

    /// <summary>Atualiza o status do lembrete (<c>PENDENTE</c>, <c>ENVIADO</c>, <c>FALHOU</c>).</summary>
    [HttpPatch("{id:long}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public IActionResult AtualizarStatus(long id, [FromBody] AtualizarStatusDto dto)
    {
        var lembrete = _repo.GetById(id);
        if (lembrete == null)
        {
            _logger.LogWarning("Atualização de status falhou: lembrete {LembreteId} não encontrado.", id);
            return this.NaoEncontrado("Lembrete não encontrado");
        }

        if (!User.EhOResponsavel(lembrete.ResponsavelId))
            return this.Proibido(SemPermissao);

        _repo.AtualizarStatus(id, dto.Status);

        _logger.LogInformation(
            "Status do lembrete {LembreteId} atualizado para {StatusLembrete}.", id, dto.Status);
        return NoContent();
    }

    /// <summary>Remove o lembrete.</summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public IActionResult Delete(long id)
    {
        var lembrete = _repo.GetById(id);
        if (lembrete == null)
        {
            _logger.LogWarning("Remoção falhou: lembrete {LembreteId} não encontrado.", id);
            return this.NaoEncontrado("Lembrete não encontrado");
        }

        if (!User.EhOResponsavel(lembrete.ResponsavelId))
            return this.Proibido(SemPermissao);

        _repo.Delete(id);

        _logger.LogInformation("Lembrete {LembreteId} removido com sucesso.", id);
        return NoContent();
    }
}
