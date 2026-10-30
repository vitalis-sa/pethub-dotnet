using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Vitalis.Tests.Integration.Fixtures;
using Xunit;

namespace Vitalis.Tests.Integration.Endpoints;

/// <summary>Paginação, ordenação, filtros e HATEOAS nas consultas, e o histórico de mensagens (MongoDB).</summary>
[Collection(VitalisApiCollection.Name)]
public class ConsultasEHateoasEndpointsTests : IDisposable
{
    private readonly VitalisWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ConsultasEHateoasEndpointsTests(VitalisWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.LimparBanco();
        _client = factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetEnderecos_TresEnderecosEmPaginasDeDois_DevePaginarENavegarPeloLinkNext()
    {
        // Arrange
        var responsavelId = await DadosDeIntegracao.CadastrarResponsavelAsync(_client);
        for (var i = 0; i < 3; i++)
            (await _client.PostAsJsonAsync($"/api/responsavel/{responsavelId}/enderecos", DadosDeIntegracao.NovoEndereco()))
                .EnsureSuccessStatusCode();

        // Act
        var primeira = await _client.GetFromJsonAsync<JsonElement>($"/api/responsavel/{responsavelId}/enderecos?tamanho=2");
        var next = Link(primeira, "next");
        var segunda = await _client.GetFromJsonAsync<JsonElement>(next);

        // Assert
        primeira.GetProperty("itens").GetArrayLength().Should().Be(2);
        primeira.GetProperty("totalDeItens").GetInt64().Should().Be(3);
        primeira.GetProperty("totalDePaginas").GetInt32().Should().Be(2);
        next.Should().Contain("pagina=2").And.Contain("tamanho=2");
        segunda.GetProperty("itens").GetArrayLength().Should().Be(1);
        segunda.GetProperty("links").EnumerateArray().Should().NotContain(l => l.GetProperty("rel").GetString() == "next");
    }

    [Fact]
    public async Task GetContatos_OrdenadosPorTelefoneDecrescente_DeveRespeitarAOrdem()
    {
        // Arrange
        var responsavelId = await DadosDeIntegracao.CadastrarResponsavelAsync(_client);
        foreach (var telefone in new[] { "11911111111", "11933333333", "11922222222" })
            (await _client.PostAsJsonAsync($"/api/responsavel/{responsavelId}/contatos", DadosDeIntegracao.NovoContato(telefone)))
                .EnsureSuccessStatusCode();

        // Act
        var pagina = await _client.GetFromJsonAsync<JsonElement>(
            $"/api/responsavel/{responsavelId}/contatos?ordenarPor=telefone&direcao=desc");

        // Assert
        pagina.GetProperty("itens").EnumerateArray().Select(c => c.GetProperty("telefone").GetString())
            .Should().Equal("11933333333", "11922222222", "11911111111");
    }

    [Fact]
    public async Task GetContatos_FiltroPrincipal_DeveRetornarSoOContatoPrincipal()
    {
        // Arrange
        var responsavelId = await DadosDeIntegracao.CadastrarResponsavelAsync(_client);
        foreach (var telefone in new[] { "11911111111", "11922222222" })
            (await _client.PostAsJsonAsync($"/api/responsavel/{responsavelId}/contatos", DadosDeIntegracao.NovoContato(telefone)))
                .EnsureSuccessStatusCode();

        // Act
        var pagina = await _client.GetFromJsonAsync<JsonElement>($"/api/responsavel/{responsavelId}/contatos?principal=true");

        // Assert
        var itens = pagina.GetProperty("itens");
        itens.GetArrayLength().Should().Be(1);
        itens[0].GetProperty("telefone").GetString().Should().Be("11911111111");
    }

    [Fact]
    public async Task GetLembretes_FiltroPorTipoNaQuery_DeveFiltrarEManterOFiltroNosLinks()
    {
        // Arrange
        var responsavelId = await DadosDeIntegracao.CadastrarResponsavelAsync(_client);
        _factory.CriarLembrete(responsavelId, TipoLembrete.VACINA, referenciaId: 1);
        _factory.CriarLembrete(responsavelId, TipoLembrete.EXAME, referenciaId: 2);

        // Act
        var pagina = await _client.GetFromJsonAsync<JsonElement>($"/api/lembretes/responsavel/{responsavelId}?tipo=EXAME");

        // Assert
        var itens = pagina.GetProperty("itens");
        itens.GetArrayLength().Should().Be(1);
        itens[0].GetProperty("tipo").GetString().Should().Be("EXAME");
        Link(pagina, "self").Should().Contain("tipo=EXAME");
    }

    [Fact]
    public async Task GetLembretes_OrdenarPorCampoInexistente_DeveRetornar400EmProblemDetails()
    {
        // Arrange
        var responsavelId = await DadosDeIntegracao.CadastrarResponsavelAsync(_client);

        // Act
        var resposta = await _client.GetAsync($"/api/lembretes/responsavel/{responsavelId}?ordenarPor=senha");
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        corpo.GetProperty("errors").TryGetProperty("ordenarPor", out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetLembretes_TamanhoAcimaDoMaximo_DeveSerBarradoPelaValidacaoAutomatica()
    {
        // Arrange
        var responsavelId = await DadosDeIntegracao.CadastrarResponsavelAsync(_client);

        // Act
        var resposta = await _client.GetAsync($"/api/lembretes/responsavel/{responsavelId}?tamanho=500");
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        corpo.TryGetProperty("traceId", out _).Should().BeTrue();
        corpo.TryGetProperty("erro", out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetResponsavel_LinksDoRecurso_DevemLevarAosRecursosRelacionados()
    {
        // Arrange
        var responsavelId = await DadosDeIntegracao.CadastrarResponsavelAsync(_client);

        // Act
        var responsavel = await _client.GetFromJsonAsync<JsonElement>($"/api/responsavel/{responsavelId}");
        var enderecos = await _client.GetAsync(Link(responsavel, "enderecos"));
        var lembretes = await _client.GetAsync(Link(responsavel, "lembretes"));

        // Assert
        Link(responsavel, "self").Should().Be($"/api/responsavel/{responsavelId}");
        enderecos.StatusCode.Should().Be(HttpStatusCode.OK);
        lembretes.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetLembrete_LinkSelfDaListagem_DeveLevarAoProprioLembrete()
    {
        // Arrange
        var responsavelId = await DadosDeIntegracao.CadastrarResponsavelAsync(_client);
        var lembreteId = _factory.CriarLembrete(responsavelId);
        var pagina = await _client.GetFromJsonAsync<JsonElement>($"/api/lembretes/responsavel/{responsavelId}");

        // Act
        var lembrete = await _client.GetFromJsonAsync<JsonElement>(Link(pagina.GetProperty("itens")[0], "self"));

        // Assert
        lembrete.GetProperty("id").GetInt64().Should().Be(lembreteId);
    }

    [Fact]
    public async Task GetHistorico_AposMensagemDaFila_DeveListarOQueFoiFeitoSemExporOCorpoBruto()
    {
        // Arrange
        var responsavelId = await DadosDeIntegracao.CadastrarResponsavelAsync(_client);
        _factory.ReceberMensagem($$"""
            {"responsavelId":{{responsavelId}},"petId":5,"tipo":"CONSULTA","dataAgendada":"2026-11-01",
             "mensagem":"Consulta agendada para Rex","referenciaId":100,"referenciaTipo":"Consulta"}
            """);

        // Act
        var pagina = await _client.GetFromJsonAsync<JsonElement>($"/api/lembretes/responsavel/{responsavelId}/historico");

        // Assert
        var registro = pagina.GetProperty("itens")[0];
        registro.GetProperty("resultado").GetString().Should().Be("Confirmar");
        registro.GetProperty("motivo").GetString().Should().Be("lembrete criado");
        registro.GetProperty("tipo").GetString().Should().Be("CONSULTA");
        registro.TryGetProperty("corpo", out _).Should().BeFalse();
    }

    [Fact]
    public async Task GetHistorico_DeOutroResponsavel_DeveRetornar403()
    {
        // Arrange
        var outro = await DadosDeIntegracao.CadastrarResponsavelAsync(_client, cpf: "11111111111", email: "outro@pethub.com");
        await DadosDeIntegracao.CadastrarResponsavelAsync(_client, cpf: "22222222222", email: "eu@pethub.com");

        // Act
        var resposta = await _client.GetAsync($"/api/lembretes/responsavel/{outro}/historico");

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static string Link(JsonElement recurso, string rel)
        => recurso.GetProperty("links").EnumerateArray()
            .Single(l => l.GetProperty("rel").GetString() == rel)
            .GetProperty("href").GetString()!;
}
