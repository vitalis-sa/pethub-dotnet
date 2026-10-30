using Microsoft.AspNetCore.Mvc;
using Vitalis.Consultas;
using Vitalis.Dto;
using Vitalis.Erros;
using Vitalis.Repositories;
using Vitalis.Seguranca;

/// <summary>Contatos (telefones) do responsável autenticado. Ao marcar um como principal, os demais são desmarcados.</summary>
[ApiController]
[ApenasProprietario]
[Route("api/responsavel/{responsavelId:long}/contatos")]
public class ResponsavelContatoController : ControllerBase
{
    private readonly IResponsavelContatoRepository _repo;
    // Logger usado para o registro estruturado das operações do controller
    private readonly ILogger<ResponsavelContatoController> _logger;

    public ResponsavelContatoController(
        IResponsavelContatoRepository repo,
        ILogger<ResponsavelContatoController> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    /// <summary>Lista os contatos do responsável, com filtros, ordenação e paginação.</summary>
    /// <remarks>Ordenação (<c>ordenarPor</c>): <c>id</c> (padrão), <c>tipo</c>, <c>telefone</c>.</remarks>
    [HttpGet]
    [ProducesResponseType<PaginaDeResultados<ContatoResposta>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public IActionResult GetAll(long responsavelId, [FromQuery] FiltroDeContatos filtro, [FromQuery] ConsultaPaginada consulta)
    {
        if (!consulta.OrdenacaoValida(FiltroDeContatos.CamposOrdenaveis))
            return this.CampoInvalido("ordenarPor",
                $"Ordene por um destes campos: {string.Join(", ", FiltroDeContatos.CamposOrdenaveis)}.");

        // Grava log estruturado contendo o responsável e a página consultada
        _logger.LogInformation("Buscando contatos do responsável {ResponsavelId} (página {Pagina}).",
            responsavelId, consulta.Pagina);

        var (itens, total) = _repo.Consultar(responsavelId, filtro, consulta);
        return Ok(PaginaDeResultados.Montar(itens.Select(ContatoResposta.De).ToList(), total, consulta, Request));
    }

    /// <summary>Busca um contato do responsável.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType<ContatoResposta>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public IActionResult GetById(long responsavelId, long id)
    {
        var contato = _repo.GetById(id);
        if (contato == null || contato.ResponsavelId != responsavelId)
        {
            // Grava log de aviso indicando recurso não encontrado
            _logger.LogWarning("Contato {ContatoId} não encontrado para o responsável {ResponsavelId}.",
                id, responsavelId);
            return this.NaoEncontrado("Contato não encontrado");
        }

        return Ok(ContatoResposta.De(contato));
    }

    /// <summary>Adiciona um contato. O primeiro do responsável vira o principal.</summary>
    [HttpPost]
    [ProducesResponseType<ContatoResposta>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public IActionResult Add(long responsavelId, [FromBody] ResponsavelContato contato)
    {
        if (!ModelState.IsValid)
            return this.DadosInvalidos();

        contato.ResponsavelId = responsavelId;
        _repo.Add(contato);

        _logger.LogInformation("Contato {ContatoId} cadastrado para o responsável {ResponsavelId}.",
            contato.Id, responsavelId);

        return CreatedAtAction(nameof(GetById),
            new { responsavelId, id = contato.Id }, ContatoResposta.De(contato));
    }

    /// <summary>Atualiza um contato.</summary>
    [HttpPut("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public IActionResult Update(long responsavelId, long id, [FromBody] ResponsavelContato contato)
    {
        if (!ModelState.IsValid)
            return this.DadosInvalidos();

        var existente = _repo.GetById(id);
        if (existente == null || existente.ResponsavelId != responsavelId)
            return this.NaoEncontrado("Contato não encontrado");

        contato.Id      = id;
        contato.ResponsavelId = responsavelId;
        _repo.Update(contato);
        return NoContent();
    }

    /// <summary>Remove um contato. Se era o principal, o mais recente dos que sobram assume.</summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public IActionResult Delete(long responsavelId, long id)
    {
        var existente = _repo.GetById(id);
        if (existente == null || existente.ResponsavelId != responsavelId)
            return this.NaoEncontrado("Contato não encontrado");

        _repo.Delete(id);
        return NoContent();
    }

    /// <summary>Define o contato como principal e desmarca os demais.</summary>
    [HttpPatch("{id:long}/principal")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public IActionResult SetPrincipal(long responsavelId, long id)
    {
        var existente = _repo.GetById(id);
        if (existente == null || existente.ResponsavelId != responsavelId)
            return this.NaoEncontrado("Contato não encontrado");

        _repo.SetPrincipal(responsavelId, id);
        return NoContent();
    }
}
