using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitalis.Api.Erros;
using Vitalis.Api.Respostas;
using Vitalis.Api.Seguranca;
using Vitalis.Application.Consultas;
using Vitalis.Application.Dtos;
using Vitalis.Application.Servicos;
using Vitalis.Domain.Enums;

namespace Vitalis.Api.Controllers;

/// <summary>
/// Lembretes do responsável. Eles nascem de mensagens do pethub-java pela fila, por isso não existe POST:
/// aqui o responsável só consulta, marca o status e apaga os próprios.
/// </summary>
[ApiController]
[Authorize]
[Route("api/lembretes")]
public class LembretesController(IServicoDeLembretes servico) : ControllerBase
{
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
        => this.Responder(servico.Listar(responsavelId, filtro, consulta),
            pagina => Ok(PaginaDeResultados.Montar(pagina, LembreteResposta.De, consulta, Request)));

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
        => Ok(servico.ListarDosesFuturas(responsavelId).Select(LembreteResposta.De).ToList());

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
        => this.Responder(await servico.ListarHistoricoAsync(responsavelId, filtro, consulta, ct),
            pagina => Ok(PaginaDeResultados.Montar(pagina, r => RegistroDeMensagemResposta.De(r, responsavelId), consulta, Request)));

    /// <summary>Busca um lembrete do responsável autenticado.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType<LembreteResposta>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public IActionResult GetById(long id)
        => this.Responder(servico.Obter(id, User.ResponsavelId()), l => Ok(LembreteResposta.De(l)));

    /// <summary>Atualiza o status do lembrete (<c>PENDENTE</c>, <c>ENVIADO</c>, <c>FALHOU</c>).</summary>
    [HttpPatch("{id:long}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public IActionResult AtualizarStatus(long id, [FromBody] AtualizarStatusDto dados)
        => this.Responder(servico.AlterarStatus(id, dados.Status, User.ResponsavelId()), NoContent);

    /// <summary>Remove o lembrete.</summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public IActionResult Delete(long id)
        => this.Responder(servico.Remover(id, User.ResponsavelId()), NoContent);
}
