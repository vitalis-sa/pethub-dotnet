using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Vitalis.Tests.Unit.Fixtures;
using Xunit;

namespace Vitalis.Tests.Unit.Api;

public class ApenasProprietarioAttributeTests
{
    private static AuthorizationFilterContext Contexto(string idDaRota, long? logado = null, string? tokenDeServico = null)
    {
        var http = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddSingleton<IAutenticadorDeServico>(ChavesDeTeste.NovoAutenticador())
                .BuildServiceProvider()
        };
        if (logado is not null)
            http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", logado.ToString()!)], "teste"));
        if (tokenDeServico is not null)
            http.Request.Headers[CabecalhosDeServico.Autorizacao] = "Bearer " + tokenDeServico;

        var rota = new RouteData();
        rota.Values["responsavelId"] = idDaRota;
        return new AuthorizationFilterContext(new ActionContext(http, rota, new ActionDescriptor()), []);
    }

    [Fact]
    public void OnAuthorization_ResponsavelAcessandoOsProprioDados_DevePermitir()
    {
        var contexto = Contexto("5", logado: 5);

        new ApenasProprietarioAttribute().OnAuthorization(contexto);

        contexto.Result.Should().BeNull();
    }

    [Fact]
    public void OnAuthorization_ResponsavelAcessandoDadosDeOutro_DeveRetornar403()
    {
        var contexto = Contexto("5", logado: 6);

        new ApenasProprietarioAttribute().OnAuthorization(contexto);

        contexto.Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(403);
    }

    [Fact]
    public void OnAuthorization_SemToken_DeveRetornar401()
    {
        var contexto = Contexto("5");

        new ApenasProprietarioAttribute().OnAuthorization(contexto);

        contexto.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public void OnAuthorization_CredencialDeServicoSemPermissaoParaServico_DeveRetornar401()
    {
        var contexto = Contexto("5", tokenDeServico: ChavesDeTeste.TokenDoJava());

        new ApenasProprietarioAttribute().OnAuthorization(contexto);

        contexto.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public void OnAuthorization_CredencialDeServicoQuandoAceita_DevePermitir()
    {
        var contexto = Contexto("5", tokenDeServico: ChavesDeTeste.TokenDoJava());

        new ApenasProprietarioAttribute { AceitaServico = true }.OnAuthorization(contexto);

        contexto.Result.Should().BeNull();
    }

    [Fact]
    public void OnAuthorization_CredencialDeServicoErrada_DeveRetornar401()
    {
        var contexto = Contexto("5", tokenDeServico: "errado");

        new ApenasProprietarioAttribute { AceitaServico = true }.OnAuthorization(contexto);

        contexto.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }
}
