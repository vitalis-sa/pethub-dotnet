using Microsoft.Extensions.Options;
using Vitalis.Configuracao;
using Vitalis.Seguranca;

namespace Vitalis.Integracao;

public class ServicoIndisponivelException(string mensagem, Exception? causa = null) : Exception(mensagem, causa);

/// <summary>Conversa com o pethub-java com um JWT de serviço assinado, cujo subject é o responsável.</summary>
public interface IPethubJavaClient
{
    Task<HttpResponseMessage> EncaminharAsync(
        HttpMethod metodo, string caminhoComQuery, long responsavelId, HttpContent? conteudo, CancellationToken ct);

    /// <summary>Apaga todos os pets do responsável (e o histórico deles) no Java. Idempotente.</summary>
    Task ApagarPetsDoResponsavelAsync(long responsavelId, CancellationToken ct);
}

public class PethubJavaClient(HttpClient http, IAutenticadorDeServico servico, IOptions<PethubJavaOpcoes> opcoes)
    : IPethubJavaClient
{
    public async Task<HttpResponseMessage> EncaminharAsync(
        HttpMethod metodo, string caminhoComQuery, long responsavelId, HttpContent? conteudo, CancellationToken ct)
    {
        using var pedido = new HttpRequestMessage(metodo, new Uri(caminhoComQuery, UriKind.Relative)) { Content = conteudo };
        // Token HS256 de 60 s; o "sub" é o responsável em nome de quem o Java vai agir.
        pedido.Headers.Add(AutenticadorDeServico.Cabecalho, "Bearer " + servico.EmitirParaJava(responsavelId));

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
