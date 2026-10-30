using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Vitalis.Dto;
using Vitalis.Erros;
using Vitalis.Integracao;
using Vitalis.Observabilidade;
using Vitalis.Repositories;
using Vitalis.Seguranca;

[ApiController]
[Route("api/responsavel")]
public class ResponsavelsApiController : ControllerBase
{
    private readonly IResponsavelRepository _repo;
    private readonly IAutenticadorDeServico _servico;
    private readonly IEmissorDeToken _tokens;
    private readonly IPethubJavaClient _java;
    // Logger usado para o registro estruturado das operações do controller
    private readonly ILogger<ResponsavelsApiController> _logger;

    public ResponsavelsApiController(
        IResponsavelRepository repo,
        IAutenticadorDeServico servico,
        IEmissorDeToken tokens,
        IPethubJavaClient java,
        ILogger<ResponsavelsApiController> logger)
    {
        _repo = repo;
        _servico = servico;
        _tokens = tokens;
        _java = java;
        _logger = logger;
    }

    /// <summary>Busca o responsável com endereços e contatos. Aceita o JWT do próprio ou o JWT de serviço do Java.</summary>
    [HttpGet("{id:long}")]
    [ApenasProprietario("id", AceitaServico = true)]
    [ProducesResponseType<ResponsavelResposta>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public IActionResult GetById(long id)
    {
        // Grava log estruturado contendo o parâmetro da busca
        _logger.LogInformation("Buscando responsável com ID: {ResponsavelId}", id);

        var responsavel = _repo.GetById(id);
        if (responsavel == null)
        {
            // Grava log de aviso indicando recurso não encontrado
            _logger.LogWarning("Responsável com ID {ResponsavelId} não foi encontrado.", id);
            return this.NaoEncontrado("Responsavel não encontrado");
        }

        return Ok(ResponsavelResposta.De(responsavel));
    }

    /// <summary>Busca por CPF. Uso do pethub-java, com JWT de serviço no cabeçalho <c>X-Service-Authorization</c>.</summary>
    [HttpGet("buscar")]
    public IActionResult BuscarPorCpf([FromQuery] string cpf)
    {
        if (!_servico.TokenDoJavaValido(Request))
        {
            // Grava log de aviso indicando tentativa de acesso não autorizada
            _logger.LogWarning("Tentativa de busca por CPF sem token de serviço válido.");
            return this.NaoAutorizado("Token de serviço inválido");
        }

        if (string.IsNullOrWhiteSpace(cpf))
            return this.RequisicaoInvalida("CPF é obrigatório");

        _logger.LogInformation("Buscando responsável por CPF para integração com o backend Java.");

        var responsavel = _repo.GetByCpf(cpf);
        if (responsavel == null)
        {
            _logger.LogWarning("Nenhum responsável encontrado para o CPF informado.");
            return this.NaoEncontrado("Responsavel não encontrado");
        }

        return Ok(new { responsavel.Id, responsavel.Nome, responsavel.Cpf, responsavel.Email, responsavel.Ativo });
    }

    /// <summary>Cadastra o responsável e já devolve o token JWT.</summary>
    [HttpPost("cadastro")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public IActionResult Cadastrar([FromBody] CadastrarResponsavelDto dto)
    {
        // Inicia um Span customizado via ActivitySource para rastreamento refinado
        using var activity =
            AplicacaoMetricas.ActivitySourceAplicacao.StartActivity("CadastrarResponsavelEndpoint");
        activity?.SetTag("responsavel.nome", dto.Nome);

        if (!ModelState.IsValid)
        {
            // Registra a falha de validação no Span e incrementa a métrica de erro
            activity?.SetStatus(ActivityStatusCode.Error, "Dados inválidos");
            AplicacaoMetricas.ResponsaveisCadastradosContador.Add(1,
                new KeyValuePair<string, object?>("status", "erro_validacao"));

            _logger.LogWarning("Cadastro de responsável rejeitado por dados inválidos.");
            return this.DadosInvalidos();
        }

        try
        {
            if (_repo.GetByCpf(dto.Cpf) != null)
            {
                activity?.SetStatus(ActivityStatusCode.Error, "CPF já cadastrado");
                AplicacaoMetricas.ResponsaveisCadastradosContador.Add(1,
                    new KeyValuePair<string, object?>("status", "erro_cpf_duplicado"));

                _logger.LogWarning("Tentativa de cadastrar responsável com CPF já existente.");
                return this.Conflito("CPF já cadastrado");
            }

            _logger.LogInformation("Tentando cadastrar responsável: {NomeResponsavel}", dto.Nome);

            var responsavel = new Responsavel
            {
                Nome  = dto.Nome,
                Cpf   = dto.Cpf,
                Email = dto.Email,
                Senha = dto.Senha,
                Ativo = true
            };

            _repo.Add(responsavel);

            // Incrementa a métrica customizada de responsáveis cadastrados
            AplicacaoMetricas.ResponsaveisCadastradosContador.Add(1,
                new KeyValuePair<string, object?>("status", "sucesso"));

            _logger.LogInformation("Responsável {ResponsavelId} cadastrado com sucesso.", responsavel.Id);

            return CreatedAtAction(nameof(GetById), new { id = responsavel.Id },
                new { responsavel.Id, responsavel.Nome, responsavel.Email, Token = _tokens.Emitir(responsavel) });
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Falha inesperada no cadastro");
            AplicacaoMetricas.ResponsaveisCadastradosContador.Add(1,
                new KeyValuePair<string, object?>("status", "erro_interno"));

            _logger.LogError(ex,
                "Falha inesperada ao cadastrar o responsável {NomeResponsavel}.", dto.Nome);

            return this.ErroInterno("Erro interno ao cadastrar responsável");
        }
    }

    /// <summary>Autentica por e-mail e senha e devolve o token JWT.</summary>
    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public IActionResult Login([FromBody] LoginDto dto)
    {
        if (!ModelState.IsValid)
            return this.DadosInvalidos();

        _logger.LogInformation("Tentativa de login recebida.");

        var responsavel = _repo.GetByEmail(dto.Email);
        if (responsavel == null || !BCrypt.Net.BCrypt.Verify(dto.Senha, responsavel.Senha))
        {
            _logger.LogWarning("Login recusado por credenciais inválidas.");
            return this.NaoAutorizado("Credenciais inválidas");
        }

        if (!responsavel.Ativo)
        {
            _logger.LogWarning("Login recusado: conta {ResponsavelId} está desativada.", responsavel.Id);
            return this.NaoAutorizado("Conta desativada");
        }

        _logger.LogInformation("Responsável {ResponsavelId} autenticado com sucesso.", responsavel.Id);
        return Ok(new { responsavel.Id, responsavel.Nome, responsavel.Email, Token = _tokens.Emitir(responsavel) });
    }

    /// <summary>Atualiza nome, CPF e e-mail.</summary>
    [HttpPut("{id:long}")]
    [ApenasProprietario("id")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public IActionResult Update(long id, [FromBody] AtualizarResponsavelDto dto)
    {
        if (!ModelState.IsValid)
            return this.DadosInvalidos();

        var existente = _repo.GetById(id);
        if (existente == null)
        {
            _logger.LogWarning("Atualização falhou: responsável {ResponsavelId} não encontrado.", id);
            return this.NaoEncontrado("Responsavel não encontrado");
        }

        existente.Nome  = dto.Nome;
        existente.Email = dto.Email;
        existente.Cpf   = dto.Cpf;

        _repo.Update(existente);

        _logger.LogInformation("Responsável {ResponsavelId} atualizado com sucesso.", id);
        return NoContent();
    }

    /// <summary>
    /// Apaga primeiro os pets do responsável no serviço clínico e só depois o cadastro. Se o Java não
    /// confirmar, nada é apagado aqui: assim não sobram pets sem dono. Repetir a chamada é seguro.
    /// </summary>
    [HttpDelete("{id:long}")]
    [ApenasProprietario("id")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct = default)
    {
        var responsavel = _repo.GetById(id);
        if (responsavel == null)
        {
            _logger.LogWarning("Remoção falhou: responsável {ResponsavelId} não encontrado.", id);
            return this.NaoEncontrado("Responsavel não encontrado");
        }

        try
        {
            await _java.ApagarPetsDoResponsavelAsync(id, ct);
        }
        catch (ServicoIndisponivelException ex)
        {
            _logger.LogError(ex, "Remoção do responsável {ResponsavelId} interrompida: o serviço clínico não confirmou a remoção dos pets.", id);
            return this.Indisponivel("Não foi possível apagar os pets do responsável agora. Tente novamente.");
        }

        _repo.Delete(id);

        _logger.LogInformation("Responsável {ResponsavelId} removido com sucesso.", id);
        return NoContent();
    }
}
