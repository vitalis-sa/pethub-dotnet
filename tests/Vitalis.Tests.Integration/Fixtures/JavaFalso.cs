using System.Net;
using System.Text;
using Vitalis.Integracao;

namespace Vitalis.Tests.Integration.Fixtures;

/// <summary>Substitui o pethub-java nos testes: registra o que o Vitalis pediu e pode simular queda.</summary>
public class JavaFalso : IPethubJavaClient
{
    public bool Fora { get; set; }
    public List<(string Metodo, string Caminho, long ResponsavelId)> Encaminhados { get; } = [];
    public List<long> ResponsaveisComPetsApagados { get; } = [];

    public void Reiniciar()
    {
        Fora = false;
        Encaminhados.Clear();
        ResponsaveisComPetsApagados.Clear();
    }

    public Task<HttpResponseMessage> EncaminharAsync(
        HttpMethod metodo, string caminhoComQuery, long responsavelId, HttpContent? conteudo, CancellationToken ct)
    {
        if (Fora)
            throw new ServicoIndisponivelException("Serviço clínico fora do ar (simulado).");

        Encaminhados.Add((metodo.Method, caminhoComQuery, responsavelId));
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]", Encoding.UTF8, "application/json")
        });
    }

    public Task ApagarPetsDoResponsavelAsync(long responsavelId, CancellationToken ct)
    {
        if (Fora)
            throw new ServicoIndisponivelException("Serviço clínico fora do ar (simulado).");

        ResponsaveisComPetsApagados.Add(responsavelId);
        return Task.CompletedTask;
    }
}
