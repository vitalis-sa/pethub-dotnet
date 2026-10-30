using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Vitalis.Tests.Unit.Fixtures;

public class ConfiguracaoFixture
{
    public IAutenticadorDeServico Autenticador { get; } = ChavesDeTeste.NovoAutenticador();

    public static T ComHttpContext<T>(T controller, string? serviceToken = null, long? responsavelLogado = null)
        where T : ControllerBase
    {
        // O DefaultHttpContext nasce com Method vazio; o gateway monta um HttpMethod a partir dele.
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Get;

        if (serviceToken is not null)
            httpContext.Request.Headers[CabecalhosDeServico.Autorizacao] = "Bearer " + serviceToken;

        if (responsavelLogado is not null)
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("sub", responsavelLogado.Value.ToString())], authenticationType: "teste"));

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }
}
