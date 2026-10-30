using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.WebUtilities;
using Vitalis.Middlewares;

namespace Vitalis.Erros;

/// <summary>
/// Todo erro da API sai no mesmo formato: <c>application/problem+json</c> (RFC 9457), com <c>traceId</c> e
/// <c>correlationId</c> para cruzar com os logs. O campo <c>erro</c> repete o <c>detail</c> para o app, que
/// lê esse nome desde as primeiras sprints.
/// </summary>
public static class Problemas
{
    private const string TituloDeValidacao = "Um ou mais campos são inválidos.";
    private const string DetalheDeValidacao = "Corrija os campos listados em 'errors'.";

    private static readonly Dictionary<int, string> Tipos = new()
    {
        [400] = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
        [401] = "https://tools.ietf.org/html/rfc9110#section-15.5.2",
        [403] = "https://tools.ietf.org/html/rfc9110#section-15.5.4",
        [404] = "https://tools.ietf.org/html/rfc9110#section-15.5.5",
        [409] = "https://tools.ietf.org/html/rfc9110#section-15.5.10",
        [500] = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
        [503] = "https://tools.ietf.org/html/rfc9110#section-15.6.4"
    };

    public static ProblemDetails Criar(HttpContext http, int status, string detalhe)
    {
        var problema = new ProblemDetails { Status = status, Detail = detalhe };
        Completar(http, problema);
        return problema;
    }

    /// <summary>
    /// Preenche os campos comuns. É chamado também pelo <c>CustomizeProblemDetails</c>, então vale para os erros
    /// gerados pelo próprio ASP.NET (validação automática, 401/403 sem corpo, rota inexistente).
    /// </summary>
    public static void Completar(HttpContext http, ProblemDetails problema)
    {
        // A validação automática do [ApiController] vem com título em inglês; padroniza com o resto da API.
        if (problema is ValidationProblemDetails && (problema.Title is null || problema.Title.StartsWith("One or more validation errors")))
        {
            problema.Title = TituloDeValidacao;
            problema.Detail ??= DetalheDeValidacao;
        }

        var status = problema.Status ?? http.Response.StatusCode;
        problema.Status ??= status;
        problema.Title ??= ReasonPhrases.GetReasonPhrase(status);
        problema.Type ??= Tipos.GetValueOrDefault(status);
        problema.Instance ??= http.Request.Path;

        problema.Extensions["traceId"] = Activity.Current?.Id ?? http.TraceIdentifier;

        var correlacao = http.Response.Headers[CorrelationIdMiddleware.CorrelationIdHeader].ToString();
        if (!string.IsNullOrEmpty(correlacao))
            problema.Extensions["correlationId"] = correlacao;

        problema.Extensions["erro"] = problema.Detail ?? problema.Title;
    }

    public static BadRequestObjectResult RequisicaoInvalida(this ControllerBase c, string detalhe)
        => new(Criar(c.HttpContext, StatusCodes.Status400BadRequest, detalhe));

    /// <summary>Erros de validação campo a campo, no formato <c>ValidationProblemDetails</c>.</summary>
    public static BadRequestObjectResult DadosInvalidos(this ControllerBase c, ModelStateDictionary? estado = null)
    {
        var problema = new ValidationProblemDetails(estado ?? c.ModelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = TituloDeValidacao,
            Detail = DetalheDeValidacao
        };
        Completar(c.HttpContext, problema);
        return new BadRequestObjectResult(problema);
    }

    /// <summary>Atalho para um único campo inválido (ex.: <c>ordenarPor</c> com valor fora da lista).</summary>
    public static BadRequestObjectResult CampoInvalido(this ControllerBase c, string campo, string mensagem)
    {
        var estado = new ModelStateDictionary();
        estado.AddModelError(campo, mensagem);
        return c.DadosInvalidos(estado);
    }

    public static UnauthorizedObjectResult NaoAutorizado(this ControllerBase c, string detalhe)
        => new(Criar(c.HttpContext, StatusCodes.Status401Unauthorized, detalhe));

    public static NotFoundObjectResult NaoEncontrado(this ControllerBase c, string detalhe)
        => new(Criar(c.HttpContext, StatusCodes.Status404NotFound, detalhe));

    public static ConflictObjectResult Conflito(this ControllerBase c, string detalhe)
        => new(Criar(c.HttpContext, StatusCodes.Status409Conflict, detalhe));

    public static ObjectResult Proibido(this ControllerBase c, string detalhe)
        => new(Criar(c.HttpContext, StatusCodes.Status403Forbidden, detalhe)) { StatusCode = StatusCodes.Status403Forbidden };

    public static ObjectResult Indisponivel(this ControllerBase c, string detalhe)
        => new(Criar(c.HttpContext, StatusCodes.Status503ServiceUnavailable, detalhe)) { StatusCode = StatusCodes.Status503ServiceUnavailable };

    public static ObjectResult ErroInterno(this ControllerBase c, string detalhe)
        => new(Criar(c.HttpContext, StatusCodes.Status500InternalServerError, detalhe)) { StatusCode = StatusCodes.Status500InternalServerError };
}
