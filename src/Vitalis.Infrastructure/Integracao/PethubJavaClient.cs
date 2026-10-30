using Vitalis.Application.Abstracoes;

namespace Vitalis.Infrastructure.Integracao;

/// <summary>
/// Cliente HTTP do pethub-java. O endereço e o tempo limite vêm de <c>PethubJavaOpcoes</c>, aplicados no
/// registro do HttpClient (<c>DependencyInjection</c>).
/// </summary>
public class PethubJavaClient(HttpClient http, IAutenticadorDeServico servico) : IPethubJavaClient
{
    public async Task<HttpResponseMessage> EncaminharAsync(
        HttpMethod metodo, string caminhoComQuery, long responsavelId, HttpContent? conteudo, CancellationToken ct)
    {
        using var pedido = new HttpRequestMessage(metodo, new Uri(caminhoComQuery, UriKind.Relative)) { Content = conteudo };
        // Token HS256 de 60 s; o "sub" é o responsável em nome de quem o Java vai agir.
        pedido.Headers.Add(CabecalhosDeServico.Autorizacao, "Bearer " + servico.EmitirParaJava(responsavelId));

        try
        {
            // O corpo é lido pelo chamador; por isso a resposta só é liberada quando ele terminar.
            return await http.SendAsync(pedido, HttpCompletionOption.ResponseContentRead, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            throw new ServicoIndisponivelException("O serviço clínico (pethub-java) não respondeu.", ex);
        }
    }

    public async Task ApagarPetsDoResponsavelAsync(long responsavelId, CancellationToken ct)
    {
        using var resposta = await EncaminharAsync(
            HttpMethod.Delete, $"/api/pets/responsavel/{responsavelId}", responsavelId, null, ct);

        if (!resposta.IsSuccessStatusCode)
            throw new ServicoIndisponivelException(
                $"O serviço clínico recusou apagar os pets do responsável (HTTP {(int)resposta.StatusCode}).");
    }
}
