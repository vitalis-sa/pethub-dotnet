using Microsoft.AspNetCore.Diagnostics;
using Vitalis.Integracao;

namespace Vitalis.Erros;

/// <summary>
/// Última barreira da pipeline: qualquer exceção que escape de um controller, filtro ou middleware vira um
/// <c>application/problem+json</c> com o status adequado, e não uma página de erro ou um corpo vazio.
/// As exceções conhecidas ganham o status certo; o resto vira 500 sem expor detalhes internos ao cliente.
/// </summary>
public sealed class TratadorGlobalDeExcecoes(
    IProblemDetailsService problemas,
    IHostEnvironment ambiente,
    ILogger<TratadorGlobalDeExcecoes> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception excecao, CancellationToken ct)
    {
        var (status, detalhe) = Classificar(excecao);

        if (status >= StatusCodes.Status500InternalServerError)
            logger.LogError(excecao, "Exceção não tratada em {Metodo} {Caminho}; respondendo {Status}.",
                http.Request.Method, http.Request.Path, status);
        else
            logger.LogWarning(excecao, "Requisição {Metodo} {Caminho} recusada com {Status}.",
                http.Request.Method, http.Request.Path, status);

        http.Response.StatusCode = status;

        var problema = Problemas.Criar(http, status, detalhe);
        if (ambiente.IsDevelopment())
            problema.Extensions["excecao"] = excecao.GetType().Name;

        return await problemas.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails = problema,
            Exception = excecao
        });
    }

    private static (int Status, string Detalhe) Classificar(Exception excecao) => excecao switch
    {
        ServicoIndisponivelException e => (StatusCodes.Status503ServiceUnavailable, e.Message),
        BadHttpRequestException e => (e.StatusCode, "A requisição não pôde ser lida."),
        _ => (StatusCodes.Status500InternalServerError,
              "Ocorreu um erro inesperado. Informe o correlationId ao suporte.")
    };
}
