using Microsoft.AspNetCore.Mvc;
using Vitalis.Api.Erros;
using Vitalis.Api.Respostas;
using Vitalis.Api.Seguranca;
using Vitalis.Application.Consultas;
using Vitalis.Application.Dtos;
using Vitalis.Application.Servicos;

namespace Vitalis.Api.Controllers;

/// <summary>Contatos (telefones) do responsável autenticado. Ao marcar um como principal, os demais são desmarcados.</summary>
[ApiController]
[ApenasProprietario]
[Route("api/responsavel/{responsavelId:long}/contatos")]
public class ContatosController(IServicoDeContatos servico) : ControllerBase
{
    /// <summary>Lista os contatos do responsável, com filtros, ordenação e paginação.</summary>
    /// <remarks>Ordenação (<c>ordenarPor</c>): <c>id</c> (padrão), <c>tipo</c>, <c>telefone</c>.</remarks>
    [HttpGet]
    [ProducesResponseType<PaginaDeResultados<ContatoResposta>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public IActionResult GetAll(long responsavelId, [FromQuery] FiltroDeContatos filtro, [FromQuery] ConsultaPaginada consulta)
        => this.Responder(servico.Listar(responsavelId, filtro, consulta),
            pagina => Ok(PaginaDeResultados.Montar(pagina, ContatoResposta.De, consulta, Request)));

    /// <summary>Busca um contato do responsável.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType<ContatoResposta>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public IActionResult GetById(long responsavelId, long id)
        => this.Responder(servico.Obter(responsavelId, id), item => Ok(ContatoResposta.De(item)));

    /// <summary>Adiciona um contato. O primeiro do responsável vira o principal.</summary>
    [HttpPost]
    [ProducesResponseType<ContatoResposta>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public IActionResult Add(long responsavelId, [FromBody] ContatoDto dados)
    {
        var item = servico.Adicionar(responsavelId, dados);
        return CreatedAtAction(nameof(GetById), new { responsavelId, id = item.Id }, ContatoResposta.De(item));
    }

    /// <summary>Atualiza um contato.</summary>
    [HttpPut("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public IActionResult Update(long responsavelId, long id, [FromBody] ContatoDto dados)
        => this.Responder(servico.Atualizar(responsavelId, id, dados), NoContent);

    /// <summary>Remove um contato. Se era o principal, o mais recente dos que sobram assume.</summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public IActionResult Delete(long responsavelId, long id)
        => this.Responder(servico.Remover(responsavelId, id), NoContent);

    /// <summary>Define o contato como principal e desmarca os demais.</summary>
    [HttpPatch("{id:long}/principal")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public IActionResult SetPrincipal(long responsavelId, long id)
        => this.Responder(servico.DefinirPrincipal(responsavelId, id), NoContent);
}
