using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitalis.Api.Erros;
using Vitalis.Api.Seguranca;
using Vitalis.Application.Abstracoes;

namespace Vitalis.Api.Controllers;

/// <summary>
/// Porta de entrada do responsável para o serviço clínico (pethub-java).
/// O Java só reconhece o responsável pela credencial de serviço; por isso o aplicativo fala com
/// este gateway, que valida o JWT do Vitalis e repassa o pedido dizendo de quem ele é. As regras
/// (o pet é do responsável? a consulta é de um pet dele?) continuam sendo do Java.
/// </summary>
[ApiController]
[Authorize]
[Route("api/clinico")]
public class GatewayClinicoController(IPethubJavaClient java, ILogger<GatewayClinicoController> logger)
    : ControllerBase
{
    private static readonly HashSet<string> RecursosPermitidos = new(StringComparer.OrdinalIgnoreCase)
    {
        "pets", "consultas", "exames", "diagnosticos", "pedidos-medicos",
        "vacinas-tratamentos", "veterinarios", "unidades"
    };

    [AcceptVerbs("GET", "POST", "PUT", "PATCH", "DELETE")]
    [Route("{**caminho}")]
    public async Task<IActionResult> Encaminhar(string caminho, CancellationToken ct)
    {
        if (!CaminhoPermitido(caminho))
            return this.NaoEncontrado("Recurso não encontrado");

        var responsavelId = User.ResponsavelId();
        if (responsavelId is null)
            return this.NaoAutorizado("Autenticação necessária");

        try
        {
            using var resposta = await java.EncaminharAsync(
                new HttpMethod(Request.Method), $"/api/{caminho}{Request.QueryString}",
                responsavelId.Value, CopiarCorpo(), ct);

            return new ContentResult
            {
                StatusCode = (int)resposta.StatusCode,
                Content = await resposta.Content.ReadAsStringAsync(ct),
                ContentType = resposta.Content.Headers.ContentType?.ToString() ?? "application/json"
            };
        }
        catch (ServicoIndisponivelException ex)
        {
            logger.LogWarning(ex, "Gateway clínico: serviço Java indisponível para {Metodo} /api/{Caminho}.", Request.Method, caminho);
            return this.Indisponivel(ex.Message);
        }
    }

    private static bool CaminhoPermitido(string caminho)
    {
        if (string.IsNullOrWhiteSpace(caminho) || caminho.Contains("..") || caminho.Contains('\\') || caminho.Contains('%'))
            return false;
        return RecursosPermitidos.Contains(caminho.Split('/')[0]);
    }

    private StreamContent? CopiarCorpo()
    {
        if (Request.ContentLength is null or 0)
            return null;

        var conteudo = new StreamContent(Request.Body);
        if (Request.ContentType is not null)
            conteudo.Headers.TryAddWithoutValidation("Content-Type", Request.ContentType);
        return conteudo;
    }
}
