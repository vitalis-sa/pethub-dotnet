using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Vitalis.Erros;

namespace Vitalis.Seguranca;

/// <summary>
/// O responsável só acessa os próprios dados: o id da rota precisa ser o do token.
/// Com <see cref="AceitaServico"/>, o outro serviço (JWT de serviço assinado pelo Java) também passa.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ApenasProprietarioAttribute(string chaveDaRota = "responsavelId")
    : Attribute, IAuthorizationFilter
{
    public bool AceitaServico { get; init; }

    public void OnAuthorization(AuthorizationFilterContext contexto)
    {
        var http = contexto.HttpContext;

        if (AceitaServico && http.RequestServices.GetRequiredService<IAutenticadorDeServico>().TokenDoJavaValido(http.Request))
            return;

        var logado = http.User.ResponsavelId();
        if (logado is null)
        {
            contexto.Result = new UnauthorizedObjectResult(
                Problemas.Criar(http, StatusCodes.Status401Unauthorized, "Autenticação necessária"));
            return;
        }

        var daRota = contexto.RouteData.Values[chaveDaRota]?.ToString();
        if (!long.TryParse(daRota, out var id) || id != logado)
            contexto.Result = new ObjectResult(
                Problemas.Criar(http, StatusCodes.Status403Forbidden, "Você só pode acessar os seus próprios dados"))
            { StatusCode = StatusCodes.Status403Forbidden };
    }
}
