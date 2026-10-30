using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Vitalis.Mensageria;
using Vitalis.Models;
using Vitalis.Tests.Integration.Fixtures;
using Xunit;

namespace Vitalis.Tests.Integration.Endpoints;

[Collection(VitalisApiCollection.Name)]
public class LembreteEndpointsTests : IDisposable
{
    private readonly VitalisWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public LembreteEndpointsTests(VitalisWebApplicationFactory factory)
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
    public async Task PostLembrete_NaoDeveExistir_OsLembretesChegamPelaFila()
    {
        // Arrange
        var responsavelId = await DadosDeIntegracao.CadastrarResponsavelAsync(_client);

        // Act
        var resposta = await _client.PostAsJsonAsync("/api/lembretes", new { responsavelId, mensagem = "x" });

        // Assert
        resposta.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task MensagemDaFila_Valida_DeveCriarLembretePendenteEvisivelParaOResponsavel()
    {
        // Arrange
        var responsavelId = await DadosDeIntegracao.CadastrarResponsavelAsync(_client);
        var json = $$"""
            {"responsavelId":{{responsavelId}},"petId":5,"tipo":"CONSULTA","dataAgendada":"2026-11-01",
             "mensagem":"Consulta agendada para Rex","referenciaId":100,"referenciaTipo":"Consulta"}
            """;

        // Act
        var resultado = _factory.ReceberMensagem(json);
        var lembretes = (await _client.GetFromJsonAsync<JsonElement>($"/api/lembretes/responsavel/{responsavelId}")).GetProperty("itens");

        // Assert
        resultado.Should().Be(ResultadoDaMensagem.Confirmar);
        lembretes.GetArrayLength().Should().Be(1);
        lembretes[0].GetProperty("tipo").GetString().Should().Be("CONSULTA");
        lembretes[0].GetProperty("status").GetString().Should().Be("PENDENTE");
    }

    [Fact]
    public async Task MensagemDaFila_EntregueDuasVezes_NaoDeveDuplicarOLembrete()
    {
        // Arrange
        var responsavelId = await DadosDeIntegracao.CadastrarResponsavelAsync(_client);
        var json = $$"""
            {"responsavelId":{{responsavelId}},"petId":5,"tipo":"CONSULTA","dataAgendada":"2026-11-01",
             "mensagem":"Consulta agendada","referenciaId":100,"referenciaTipo":"Consulta"}
            """;

        // Act
        _factory.ReceberMensagem(json);
        _factory.ReceberMensagem(json);
        var lembretes = (await _client.GetFromJsonAsync<JsonElement>($"/api/lembretes/responsavel/{responsavelId}")).GetProperty("itens");

        // Assert
        lembretes.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public void MensagemDaFila_ParaResponsavelInexistente_DeveSerDescartada()
    {
        // Arrange
        const string json = """
            {"responsavelId":999999,"petId":5,"tipo":"CONSULTA","dataAgendada":"2026-11-01","mensagem":"x"}
            """;

        // Act
        var resultado = _factory.ReceberMensagem(json);

        // Assert
        resultado.Should().Be(ResultadoDaMensagem.Descartar);
    }

    [Fact]
    public async Task GetLembretesPorResponsavel_SemToken_DeveRetornar401()
    {
        // Arrange
        using var anonimo = _factory.CreateClient();

        // Act
        var resposta = await anonimo.GetAsync("/api/lembretes/responsavel/1");

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetLembretesPorResponsavel_LembretesDeVariosResponsaveis_DeveRetornarApenasOsDoResponsavel()
    {
        // Arrange
        var primeiro = await DadosDeIntegracao.CadastrarResponsavelAsync(_client, cpf: "11111111111", email: "a@pethub.com");
        var segundo = await DadosDeIntegracao.CadastrarResponsavelAsync(_client, cpf: "22222222222", email: "b@pethub.com");
        _factory.CriarLembrete(primeiro, referenciaId: 1);
        _factory.CriarLembrete(segundo, referenciaId: 2);
        await DadosDeIntegracao.EntrarAsync(_client, "a@pethub.com");

        // Act
        var lembretes = (await _client.GetFromJsonAsync<JsonElement>($"/api/lembretes/responsavel/{primeiro}")).GetProperty("itens");

        // Assert
        lembretes.GetArrayLength().Should().Be(1);
        lembretes[0].GetProperty("responsavelId").GetInt64().Should().Be(primeiro);
    }

    [Fact]
    public async Task GetLembretesPorResponsavel_DeOutroResponsavel_DeveRetornar403()
    {
        // Arrange
        var alvo = await DadosDeIntegracao.CadastrarResponsavelAsync(_client, cpf: "11111111111", email: "a@pethub.com");
        await DadosDeIntegracao.CadastrarResponsavelAsync(_client, cpf: "22222222222", email: "b@pethub.com");

        // Act
        var resposta = await _client.GetAsync($"/api/lembretes/responsavel/{alvo}");

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetLembretesPorResponsavelETipo_TipoInformado_DeveRetornarApenasOsDaqueleTipo()
    {
        // Arrange
        var responsavelId = await DadosDeIntegracao.CadastrarResponsavelAsync(_client);
        _factory.CriarLembrete(responsavelId, TipoLembrete.VACINA, referenciaId: 1);
        _factory.CriarLembrete(responsavelId, TipoLembrete.EXAME, referenciaId: 2);

        // Act
        var lembretes = (await _client.GetFromJsonAsync<JsonElement>(
            $"/api/lembretes/responsavel/{responsavelId}/tipo/VACINA")).GetProperty("itens");

        // Assert
        lembretes.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task GetLembretesPorResponsavelETipo_TipoInexistente_DeveRetornar400()
    {
        // Arrange
        var responsavelId = await DadosDeIntegracao.CadastrarResponsavelAsync(_client);

        // Act
        var resposta = await _client.GetAsync($"/api/lembretes/responsavel/{responsavelId}/tipo/BANHO");

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PatchStatus_LembreteDoResponsavel_DeveRetornar204EAtualizarOStatus()
    {
        // Arrange
        var responsavelId = await DadosDeIntegracao.CadastrarResponsavelAsync(_client);
        var lembreteId = _factory.CriarLembrete(responsavelId);

        // Act
        var resposta = await _client.PatchAsJsonAsync($"/api/lembretes/{lembreteId}/status", new { status = "ENVIADO" });
        var lembretes = (await _client.GetFromJsonAsync<JsonElement>($"/api/lembretes/responsavel/{responsavelId}")).GetProperty("itens");

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
        lembretes[0].GetProperty("status").GetString().Should().Be("ENVIADO");
    }

    [Fact]
    public async Task PatchStatus_LembreteDeOutroResponsavel_DeveRetornar403()
    {
        // Arrange
        var dono = await DadosDeIntegracao.CadastrarResponsavelAsync(_client, cpf: "11111111111", email: "a@pethub.com");
        var lembreteId = _factory.CriarLembrete(dono);
        await DadosDeIntegracao.CadastrarResponsavelAsync(_client, cpf: "22222222222", email: "b@pethub.com");

        // Act
        var resposta = await _client.PatchAsJsonAsync($"/api/lembretes/{lembreteId}/status", new { status = "ENVIADO" });

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PatchStatus_LembreteInexistente_DeveRetornar404()
    {
        // Arrange
        await DadosDeIntegracao.CadastrarResponsavelAsync(_client);

        // Act
        var resposta = await _client.PatchAsJsonAsync("/api/lembretes/999999/status", new { status = "FALHOU" });

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteLembrete_LembreteDoResponsavel_DeveRetornar204ERemoverORegistro()
    {
        // Arrange
        var responsavelId = await DadosDeIntegracao.CadastrarResponsavelAsync(_client);
        var lembreteId = _factory.CriarLembrete(responsavelId);

        // Act
        var resposta = await _client.DeleteAsync($"/api/lembretes/{lembreteId}");
        var lembretes = (await _client.GetFromJsonAsync<JsonElement>($"/api/lembretes/responsavel/{responsavelId}")).GetProperty("itens");

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
        lembretes.GetArrayLength().Should().Be(0);
    }
}
