using System.Net.Http.Json;
using System.Text.Json;
using Vitalis.Models;

namespace Vitalis.Tests.Integration.Fixtures;

public static class DadosDeIntegracao
{
    public const string SenhaPadrao = "SenhaSegura@123";

    public static object NovoCadastro(
        string nome = "Pedro Chasci",
        string cpf = "12345678901",
        string email = "pedro@pethub.com")
        => new { nome, cpf, email, senha = SenhaPadrao };

    public static object NovoLogin(string email = "pedro@pethub.com", string senha = SenhaPadrao)
        => new { email, senha };

    public static object NovoEndereco()
        => new
        {
            logradouro = "Av. Paulista",
            numero = "1000",
            complemento = "Sala 42",
            bairro = "Bela Vista",
            cidade = "São Paulo",
            estado = "SP",
            cep = "01310100",
            principal = false
        };

    public static object NovoContato(string telefone = "11999998888")
        => new { tipo = "CELULAR", telefone, principal = false };

    /// <summary>Cadastra o responsável e deixa o cliente autenticado como ele (o cadastro devolve o token).</summary>
    public static async Task<long> CadastrarResponsavelAsync(
        HttpClient client, string cpf = "12345678901", string email = "pedro@pethub.com")
    {
        var resposta = await client.PostAsJsonAsync("/api/responsavel/cadastro", NovoCadastro(cpf: cpf, email: email));
        resposta.EnsureSuccessStatusCode();

        var criado = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Autenticar(client, criado.GetProperty("token").GetString()!);
        return criado.GetProperty("id").GetInt64();
    }

    /// <summary>Faz login e deixa o cliente autenticado como esse responsável.</summary>
    public static async Task EntrarAsync(HttpClient client, string email)
    {
        var resposta = await client.PostAsJsonAsync("/api/responsavel/login", NovoLogin(email: email));
        resposta.EnsureSuccessStatusCode();

        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Autenticar(client, corpo.GetProperty("token").GetString()!);
    }

    private static void Autenticar(HttpClient client, string token)
        => client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
}
