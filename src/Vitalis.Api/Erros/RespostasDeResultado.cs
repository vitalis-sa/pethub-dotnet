using Microsoft.AspNetCore.Mvc;
using Vitalis.Application.Resultados;

namespace Vitalis.Api.Erros;

/// <summary>Traduz o desfecho de um caso de uso para HTTP: sucesso vira a resposta pedida, erro vira problem+json.</summary>
public static class RespostasDeResultado
{
    public static IActionResult Responder<T>(this ControllerBase controller, Resultado<T> resultado, Func<T, IActionResult> sucesso)
        => resultado.Sucesso ? sucesso(resultado.Valor) : controller.Problema(resultado.Erro!);

    public static IActionResult Responder(this ControllerBase controller, Resultado resultado, Func<IActionResult> sucesso)
        => resultado.Sucesso ? sucesso() : controller.Problema(resultado.Erro!);

    public static IActionResult Problema(this ControllerBase controller, Erro erro) => erro.Tipo switch
    {
        TipoDeErro.Invalido when erro.Campo is not null => controller.CampoInvalido(erro.Campo, erro.Mensagem),
        TipoDeErro.Invalido => controller.RequisicaoInvalida(erro.Mensagem),
        TipoDeErro.NaoAutorizado => controller.NaoAutorizado(erro.Mensagem),
        TipoDeErro.Proibido => controller.Proibido(erro.Mensagem),
        TipoDeErro.NaoEncontrado => controller.NaoEncontrado(erro.Mensagem),
        TipoDeErro.Conflito => controller.Conflito(erro.Mensagem),
        TipoDeErro.Indisponivel => controller.Indisponivel(erro.Mensagem),
        _ => throw new ArgumentOutOfRangeException(nameof(erro), erro.Tipo, "Tipo de erro sem tradução para HTTP.")
    };
}
