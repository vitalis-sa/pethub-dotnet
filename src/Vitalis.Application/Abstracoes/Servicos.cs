using Vitalis.Domain.Entidades;

namespace Vitalis.Application.Abstracoes;

// Portas para serviços técnicos. As implementações ficam em Vitalis.Infrastructure.

/// <summary>Horário do negócio (America/Sao_Paulo), qualquer que seja o fuso do servidor.</summary>
public interface IRelogio
{
    DateTime Agora();
}

/// <summary>Hash de senha. A senha em texto nunca é guardada.</summary>
public interface IHasherDeSenha
{
    string Gerar(string senha);
    bool Verificar(string senha, string hash);
}

/// <summary>Emite o JWT que o responsável usa no app.</summary>
public interface IEmissorDeToken
{
    string Emitir(Responsavel responsavel);
}

public static class CabecalhosDeServico
{
    /// <summary>Cabeçalho do JWT de serviço trocado entre o Vitalis e o pethub-java.</summary>
    public const string Autorizacao = "X-Service-Authorization";
}

/// <summary>
/// Autenticação entre o Vitalis e o pethub-java por JWT (HS256) assinado com um segredo que só os dois
/// backends conhecem, independente do segredo do JWT do usuário.
/// </summary>
public interface IAutenticadorDeServico
{
    /// <summary>Token curto para uma chamada ao Java em nome do responsável (<c>sub</c>).</summary>
    string EmitirParaJava(long responsavelId);

    /// <summary>O valor do cabeçalho <see cref="CabecalhosDeServico.Autorizacao"/> é um token do Java ainda válido?</summary>
    bool TokenDoJavaValido(string? valorDoCabecalho);
}

/// <summary>O pethub-java não respondeu ou recusou a operação.</summary>
public class ServicoIndisponivelException(string mensagem, Exception? causa = null) : Exception(mensagem, causa);

/// <summary>Conversa com o pethub-java com um JWT de serviço assinado, cujo subject é o responsável.</summary>
public interface IPethubJavaClient
{
    Task<HttpResponseMessage> EncaminharAsync(
        HttpMethod metodo, string caminhoComQuery, long responsavelId, HttpContent? conteudo, CancellationToken ct);

    /// <summary>Apaga todos os pets do responsável (e o histórico deles) no Java. Idempotente.</summary>
    Task ApagarPetsDoResponsavelAsync(long responsavelId, CancellationToken ct);
}
