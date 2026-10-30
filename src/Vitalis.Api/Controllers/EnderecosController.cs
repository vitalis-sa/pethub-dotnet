using Microsoft.AspNetCore.Mvc;
using Vitalis.Api.Erros;
using Vitalis.Api.Respostas;
using Vitalis.Api.Seguranca;
using Vitalis.Application.Consultas;
using Vitalis.Application.Dtos;
using Vitalis.Application.Servicos;

namespace Vitalis.Api.Controllers;

/// <summary>Endereços do responsável autenticado. Ao marcar um como principal, os demais são desmarcados.</summary>
[ApiController]
[ApenasProprietario]
[Route("api/responsavel/{responsavelId:long}/enderecos")]
public class EnderecosController(IServicoDeEnderecos servico) : ControllerBase
{
    /// <summary>Lista os endereços do responsável, com filtros, ordenação e paginação.</summary>
    /// <remarks>Ordenação (<c>ordenarPor</c>): <c>id</c> (padrão), <c>cidade</c>, <c>estado</c>, <c>bairro</c>, <c>logradouro</c>.</remarks>
    [HttpGet]
    [ProducesResponseType<PaginaDeResultados<EnderecoResposta>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public IActionResult GetAll(long responsavelId, [FromQuery] FiltroDeEnderecos filtro, [FromQuery] ConsultaPaginada consulta)
        => this.Responder(servico.Listar(responsavelId, filtro, consulta),
            pagina => Ok(PaginaDeResultados.Montar(pagina, EnderecoResposta.De, consulta, Request)));

    /// <summary>Busca um endereço do responsável.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType<EnderecoResposta>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public IActionResult GetById(long responsavelId, long id)
        => this.Responder(servico.Obter(responsavelId, id), item => Ok(EnderecoResposta.De(item)));

    /// <summary>Adiciona um endereço. O primeiro do responsável vira o principal.</summary>
    [HttpPost]
    [ProducesResponseType<EnderecoResposta>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public IActionResult Add(long responsavelId, [FromBody] EnderecoDto dados)
    {
        var item = servico.Adicionar(responsavelId, dados);
        return CreatedAtAction(nameof(GetById), new { responsavelId, id = item.Id }, EnderecoResposta.De(item));
    }

    /// <summary>Atualiza um endereço.</summary>
    [HttpPut("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public IActionResult Update(long responsavelId, long id, [FromBody] EnderecoDto dados)
        => this.Responder(servico.Atualizar(responsavelId, id, dados), NoContent);

    /// <summary>Remove um endereço. Se era o principal, o mais recente dos que sobram assume.</summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public IActionResult Delete(long responsavelId, long id)
        => this.Responder(servico.Remover(responsavelId, id), NoContent);

    /// <summary>Define o endereço como principal e desmarca os demais.</summary>
    [HttpPatch("{id:long}/principal")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public IActionResult SetPrincipal(long responsavelId, long id)
        => this.Responder(servico.DefinirPrincipal(responsavelId, id), NoContent);
}
