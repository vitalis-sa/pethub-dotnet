using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Vitalis.Application.Abstracoes;
using Vitalis.Application.Dtos;
using Vitalis.Application.Observabilidade;
using Vitalis.Application.Resultados;
using Vitalis.Domain.Entidades;

namespace Vitalis.Application.Servicos;

/// <summary>Casos de uso do responsável: cadastro, login, consulta, atualização e exclusão.</summary>
public interface IServicoDeResponsaveis
{
    Resultado<Responsavel> Obter(long id);
    /// <summary>Usado pelo pethub-java para achar o dono de um pet pelo CPF.</summary>
    Resultado<Responsavel> BuscarPorCpf(string cpf);
    Resultado<Sessao> Cadastrar(CadastrarResponsavelDto dados);
    Resultado<Sessao> Entrar(LoginDto credenciais);
    Resultado Atualizar(long id, AtualizarResponsavelDto dados);
    /// <summary>Apaga primeiro os pets no serviço clínico e só depois o cadastro.</summary>
    Task<Resultado> ExcluirAsync(long id, CancellationToken ct = default);
}

public class ServicoDeResponsaveis(
    IResponsavelRepository repositorio,
    IHasherDeSenha hasher,
    IEmissorDeToken tokens,
    IPethubJavaClient java,
    IRelogio relogio,
    ILogger<ServicoDeResponsaveis> logger) : IServicoDeResponsaveis
{
    private const string NaoEncontrado = "Responsavel não encontrado";

    public Resultado<Responsavel> Obter(long id)
    {
        logger.LogInformation("Buscando responsável com ID: {ResponsavelId}", id);

        var responsavel = repositorio.ObterPorId(id);
        if (responsavel is null)
        {
            logger.LogWarning("Responsável com ID {ResponsavelId} não foi encontrado.", id);
            return Erro.NaoEncontrado(NaoEncontrado);
        }

        return responsavel;
    }

    public Resultado<Responsavel> BuscarPorCpf(string cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf))
            return Erro.Invalido("cpf", "CPF é obrigatório");

        logger.LogInformation("Buscando responsável por CPF para integração com o backend Java.");

        var responsavel = repositorio.ObterPorCpf(cpf);
        if (responsavel is null)
        {
            logger.LogWarning("Nenhum responsável encontrado para o CPF informado.");
            return Erro.NaoEncontrado(NaoEncontrado);
        }

        return responsavel;
    }

    public Resultado<Sessao> Cadastrar(CadastrarResponsavelDto dados)
    {
        // Span próprio do cadastro, com tags de negócio, e a métrica de responsáveis cadastrados por desfecho.
        using var atividade = AplicacaoMetricas.ActivitySourceAplicacao.StartActivity("CadastrarResponsavel");
        atividade?.SetTag("responsavel.nome", dados.Nome);

        try
        {
            if (repositorio.ObterPorCpf(dados.Cpf) is not null)
            {
                Registrar(atividade, "erro_cpf_duplicado", "CPF já cadastrado");
                logger.LogWarning("Tentativa de cadastrar responsável com CPF já existente.");
                return Erro.Conflito("CPF já cadastrado");
            }

            logger.LogInformation("Tentando cadastrar responsável: {NomeResponsavel}", dados.Nome);

            var responsavel = new Responsavel
            {
                Nome = dados.Nome,
                Cpf = dados.Cpf,
                Email = dados.Email,
                Senha = hasher.Gerar(dados.Senha),
                Ativo = true,
                CreatedAt = relogio.Agora()
            };
            repositorio.Adicionar(responsavel);
            repositorio.SalvarAlteracoes();

            Registrar(atividade, "sucesso");
            logger.LogInformation("Responsável {ResponsavelId} cadastrado com sucesso.", responsavel.Id);

            return new Sessao(responsavel, tokens.Emitir(responsavel));
        }
        catch (Exception)
        {
            // A exceção segue para o tratador global da API, que registra o LogError e responde 500.
            Registrar(atividade, "erro_interno", "Falha inesperada no cadastro");
            throw;
        }
    }

    public Resultado<Sessao> Entrar(LoginDto credenciais)
    {
        logger.LogInformation("Tentativa de login recebida.");

        var responsavel = repositorio.ObterPorEmail(credenciais.Email);
        if (responsavel is null || !hasher.Verificar(credenciais.Senha, responsavel.Senha))
        {
            logger.LogWarning("Login recusado por credenciais inválidas.");
            return Erro.NaoAutorizado("Credenciais inválidas");
        }

        if (!responsavel.Ativo)
        {
            logger.LogWarning("Login recusado: conta {ResponsavelId} está desativada.", responsavel.Id);
            return Erro.NaoAutorizado("Conta desativada");
        }

        logger.LogInformation("Responsável {ResponsavelId} autenticado com sucesso.", responsavel.Id);
        return new Sessao(responsavel, tokens.Emitir(responsavel));
    }

    public Resultado Atualizar(long id, AtualizarResponsavelDto dados)
    {
        var responsavel = repositorio.ObterPorId(id);
        if (responsavel is null)
        {
            logger.LogWarning("Atualização falhou: responsável {ResponsavelId} não encontrado.", id);
            return Erro.NaoEncontrado(NaoEncontrado);
        }

        responsavel.AtualizarCadastro(dados.Nome, dados.Cpf, dados.Email);
        repositorio.SalvarAlteracoes();

        logger.LogInformation("Responsável {ResponsavelId} atualizado com sucesso.", id);
        return Resultado.Ok();
    }

    /// <summary>
    /// Se o Java não confirmar a remoção dos pets, nada é apagado aqui: assim não sobram pets sem dono.
    /// Repetir a chamada é seguro.
    /// </summary>
    public async Task<Resultado> ExcluirAsync(long id, CancellationToken ct = default)
    {
        var responsavel = repositorio.ObterPorId(id);
        if (responsavel is null)
        {
            logger.LogWarning("Remoção falhou: responsável {ResponsavelId} não encontrado.", id);
            return Erro.NaoEncontrado(NaoEncontrado);
        }

        try
        {
            await java.ApagarPetsDoResponsavelAsync(id, ct);
        }
        catch (ServicoIndisponivelException ex)
        {
            logger.LogError(ex,
                "Remoção do responsável {ResponsavelId} interrompida: o serviço clínico não confirmou a remoção dos pets.", id);
            return Erro.Indisponivel("Não foi possível apagar os pets do responsável agora. Tente novamente.");
        }

        repositorio.Remover(responsavel);
        repositorio.SalvarAlteracoes();

        logger.LogInformation("Responsável {ResponsavelId} removido com sucesso.", id);
        return Resultado.Ok();
    }

    private static void Registrar(Activity? atividade, string status, string? erro = null)
    {
        if (erro is not null)
            atividade?.SetStatus(ActivityStatusCode.Error, erro);

        AplicacaoMetricas.ResponsaveisCadastradosContador.Add(1, new KeyValuePair<string, object?>("status", status));
    }
}
