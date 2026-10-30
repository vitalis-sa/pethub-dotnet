using Microsoft.AspNetCore.Mvc;
using Vitalis.Api.Erros;
using Vitalis.Api.Respostas;
using Vitalis.Api.Seguranca;
using Vitalis.Application.Abstracoes;
using Vitalis.Application.Dtos;
using Vitalis.Application.Servicos;

namespace Vitalis.Api.Controllers;

/// <summary>Cadastro, login e dados do responsável.</summary>
[ApiController]
[Route("api/responsavel")]
public class ResponsaveisController(IServicoDeResponsaveis servico, IAutenticadorDeServico autenticadorDeServico)
    : ControllerBase
{
    /// <summary>Busca o responsável com endereços e contatos. Aceita o JWT do próprio ou o JWT de serviço do Java.</summary>
    [HttpGet("{id:long}")]
    [ApenasProprietario("id", AceitaServico = true)]
    [ProducesResponseType<ResponsavelResposta>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public IActionResult GetById(long id)
        => this.Responder(servico.Obter(id), r => Ok(ResponsavelResposta.De(r)));

    /// <summary>Busca por CPF. Uso do pethub-java, com JWT de serviço no cabeçalho <c>X-Service-Authorization</c>.</summary>
    [HttpGet("buscar")]
    [ProducesResponseType<ResponsavelResumoResposta>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public IActionResult BuscarPorCpf([FromQuery] string cpf)
    {
        if (!autenticadorDeServico.TokenDoJavaValido(Request.Headers[CabecalhosDeServico.Autorizacao].ToString()))
            return this.NaoAutorizado("Token de serviço inválido");

        return this.Responder(servico.BuscarPorCpf(cpf), r => Ok(ResponsavelResumoResposta.De(r)));
    }

    /// <summary>Cadastra o responsável e já devolve o token JWT.</summary>
    [HttpPost("cadastro")]
    [ProducesResponseType<SessaoResposta>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public IActionResult Cadastrar([FromBody] CadastrarResponsavelDto dados)
        => this.Responder(servico.Cadastrar(dados),
            sessao => CreatedAtAction(nameof(GetById), new { id = sessao.Responsavel.Id }, SessaoResposta.De(sessao)));

    /// <summary>Autentica por e-mail e senha e devolve o token JWT.</summary>
    [HttpPost("login")]
    [ProducesResponseType<SessaoResposta>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public IActionResult Login([FromBody] LoginDto credenciais)
        => this.Responder(servico.Entrar(credenciais), sessao => Ok(SessaoResposta.De(sessao)));

    /// <summary>Atualiza nome, CPF e e-mail.</summary>
    [HttpPut("{id:long}")]
    [ApenasProprietario("id")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public IActionResult Update(long id, [FromBody] AtualizarResponsavelDto dados)
        => this.Responder(servico.Atualizar(id, dados), NoContent);

    /// <summary>
    /// Apaga primeiro os pets do responsável no serviço clínico e só depois o cadastro. Se o Java não
    /// confirmar, nada é apagado aqui (503). Repetir a chamada é seguro.
    /// </summary>
    [HttpDelete("{id:long}")]
    [ApenasProprietario("id")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct = default)
        => this.Responder(await servico.ExcluirAsync(id, ct), NoContent);
}
