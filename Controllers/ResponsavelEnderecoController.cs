using Microsoft.AspNetCore.Mvc;
using Vitalis.Consultas;
using Vitalis.Dto;
using Vitalis.Erros;
using Vitalis.Repositories;
using Vitalis.Seguranca;

/// <summary>Endereços do responsável autenticado. Ao marcar um como principal, os demais são desmarcados.</summary>
[ApiController]
[ApenasProprietario]
[Route("api/responsavel/{responsavelId:long}/enderecos")]
public class ResponsavelEnderecoController : ControllerBase
{
    private readonly IResponsavelEnderecoRepository _repo;
    // Logger usado para o registro estruturado das operações do controller
    private readonly ILogger<ResponsavelEnderecoController> _logger;

    public ResponsavelEnderecoController(
        IResponsavelEnderecoRepository repo,
        ILogger<ResponsavelEnderecoController> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    /// <summary>Lista os endereços do responsável, com filtros, ordenação e paginação.</summary>
    /// <remarks>Ordenação (<c>ordenarPor</c>): <c>id</c> (padrão), <c>cidade</c>, <c>estado</c>, <c>bairro</c>, <c>logradouro</c>.</remarks>
    [HttpGet]
    [ProducesResponseType<PaginaDeResultados<EnderecoResposta>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public IActionResult GetAll(long responsavelId, [FromQuery] FiltroDeEnderecos filtro, [FromQuery] ConsultaPaginada consulta)
    {
        if (!consulta.OrdenacaoValida(FiltroDeEnderecos.CamposOrdenaveis))
            return this.CampoInvalido("ordenarPor",
                $"Ordene por um destes campos: {string.Join(", ", FiltroDeEnderecos.CamposOrdenaveis)}.");

        // Grava log estruturado contendo o responsável e a página consultada
        _logger.LogInformation("Buscando endereços do responsável {ResponsavelId} (página {Pagina}).",
            responsavelId, consulta.Pagina);

        var (itens, total) = _repo.Consultar(responsavelId, filtro, consulta);
        return Ok(PaginaDeResultados.Montar(itens.Select(EnderecoResposta.De).ToList(), total, consulta, Request));
    }

    /// <summary>Busca um endereço do responsável.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType<EnderecoResposta>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public IActionResult GetById(long responsavelId, long id)
    {
        var endereco = _repo.GetById(id);
        if (endereco == null || endereco.ResponsavelId != responsavelId)
        {
            // Grava log de aviso indicando recurso não encontrado
            _logger.LogWarning("Endereço {EnderecoId} não encontrado para o responsável {ResponsavelId}.",
                id, responsavelId);
            return this.NaoEncontrado("Endereço não encontrado");
        }

        return Ok(EnderecoResposta.De(endereco));
    }

    /// <summary>Adiciona um endereço. O primeiro do responsável vira o principal.</summary>
    [HttpPost]
    [ProducesResponseType<EnderecoResposta>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public IActionResult Add(long responsavelId, [FromBody] ResponsavelEndereco endereco)
    {
        if (!ModelState.IsValid)
            return this.DadosInvalidos();

        endereco.ResponsavelId = responsavelId;
        _repo.Add(endereco);

        _logger.LogInformation("Endereço {EnderecoId} cadastrado para o responsável {ResponsavelId}.",
            endereco.Id, responsavelId);

        return CreatedAtAction(nameof(GetById),
            new { responsavelId, id = endereco.Id }, EnderecoResposta.De(endereco));
    }

    /// <summary>Atualiza um endereço.</summary>
    [HttpPut("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public IActionResult Update(long responsavelId, long id, [FromBody] ResponsavelEndereco endereco)
    {
        if (!ModelState.IsValid)
            return this.DadosInvalidos();

        var existente = _repo.GetById(id);
        if (existente == null || existente.ResponsavelId != responsavelId)
            return this.NaoEncontrado("Endereço não encontrado");

        endereco.Id      = id;
        endereco.ResponsavelId = responsavelId;
        _repo.Update(endereco);
        return NoContent();
    }

    /// <summary>Remove um endereço. Se era o principal, o mais recente dos que sobram assume.</summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public IActionResult Delete(long responsavelId, long id)
    {
        var existente = _repo.GetById(id);
        if (existente == null || existente.ResponsavelId != responsavelId)
            return this.NaoEncontrado("Endereço não encontrado");

        _repo.Delete(id);
        return NoContent();
    }

    /// <summary>Define o endereço como principal e desmarca os demais.</summary>
    [HttpPatch("{id:long}/principal")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public IActionResult SetPrincipal(long responsavelId, long id)
    {
        var existente = _repo.GetById(id);
        if (existente == null || existente.ResponsavelId != responsavelId)
            return this.NaoEncontrado("Endereço não encontrado");

        _repo.SetPrincipal(responsavelId, id);
        return NoContent();
    }
}
