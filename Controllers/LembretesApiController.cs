using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    private readonly ILembreteRepository _repo;
    // Logger usado para o registro estruturado das operações do controller
    private readonly ILogger<LembretesApiController> _logger;

    public LembretesApiController(ILembreteRepository repo, ILogger<LembretesApiController> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    [HttpGet("responsavel/{responsavelId:long}")]
    [ApenasProprietario]
    public IActionResult GetByResponsavel(long responsavelId)
    {
        _logger.LogInformation("Buscando lembretes do responsável {ResponsavelId}.", responsavelId);

        return Ok(_repo.GetByResponsavelId(responsavelId));
    }

    /// <summary>Próximas doses de medicamento (ainda não liberadas), para o app agendar as notificações locais.</summary>
    [HttpGet("responsavel/{responsavelId:long}/doses-futuras")]
    [ApenasProprietario]
    public IActionResult GetDosesFuturas(long responsavelId)
    {
        _logger.LogInformation("Buscando doses futuras do responsável {ResponsavelId}.", responsavelId);

        return Ok(_repo.GetDosesFuturas(responsavelId, Vitalis.Configuracao.Relogio.Agora(), LimiteDeDosesFuturas));
    }

    [HttpGet("responsavel/{responsavelId:long}/tipo/{tipo}")]
    [ApenasProprietario]
    public IActionResult GetByResponsavelETipo(long responsavelId, TipoLembrete tipo)
    {
        _logger.LogInformation(
            "Buscando lembretes do responsável {ResponsavelId} do tipo {TipoLembrete}.", responsavelId, tipo);

        return Ok(_repo.GetByResponsavelIdETipo(responsavelId, tipo));
    }

    [HttpPatch("{id:long}/status")]
    public IActionResult AtualizarStatus(long id, [FromBody] AtualizarStatusDto dto)
    {
        var lembrete = _repo.GetById(id);
        if (lembrete == null)
        {
            _logger.LogWarning("Atualização de status falhou: lembrete {LembreteId} não encontrado.", id);
            return NotFound(new { erro = "Lembrete não encontrado" });
        }

        if (!User.EhOResponsavel(lembrete.ResponsavelId))
            return Forbid();

        _repo.AtualizarStatus(id, dto.Status);

        _logger.LogInformation(
            "Status do lembrete {LembreteId} atualizado para {StatusLembrete}.", id, dto.Status);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public IActionResult Delete(long id)
    {
        var lembrete = _repo.GetById(id);
        if (lembrete == null)
        {
            _logger.LogWarning("Remoção falhou: lembrete {LembreteId} não encontrado.", id);
            return NotFound(new { erro = "Lembrete não encontrado" });
        }

        if (!User.EhOResponsavel(lembrete.ResponsavelId))
            return Forbid();

        _repo.Delete(id);

        _logger.LogInformation("Lembrete {LembreteId} removido com sucesso.", id);
        return NoContent();
    }
}
