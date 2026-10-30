using Microsoft.Extensions.Logging;
using Vitalis.Application.Abstracoes;
using Vitalis.Application.Consultas;
using Vitalis.Application.Dtos;
using Vitalis.Application.Resultados;
using Vitalis.Domain.Entidades;
using Vitalis.Domain.Servicos;

namespace Vitalis.Application.Servicos;

/// <summary>Casos de uso de uma lista do responsável com um item principal (endereços e contatos).</summary>
public interface IServicoDeItensDoResponsavel<TItem, TDto, TFiltro>
    where TItem : class, IItemDoResponsavel
{
    Resultado<Pagina<TItem>> Listar(long responsavelId, TFiltro filtro, ConsultaPaginada consulta);
    Resultado<TItem> Obter(long responsavelId, long id);
    /// <summary>O primeiro item do responsável vira o principal; um novo principal desmarca os demais.</summary>
    TItem Adicionar(long responsavelId, TDto dados);
    Resultado Atualizar(long responsavelId, long id, TDto dados);
    /// <summary>Se o removido era o principal, o mais recente dos que sobram assume.</summary>
    Resultado Remover(long responsavelId, long id);
    Resultado DefinirPrincipal(long responsavelId, long id);
}

public interface IServicoDeEnderecos : IServicoDeItensDoResponsavel<ResponsavelEndereco, EnderecoDto, FiltroDeEnderecos>;

public interface IServicoDeContatos : IServicoDeItensDoResponsavel<ResponsavelContato, ContatoDto, FiltroDeContatos>;

/// <summary>
/// Fluxo comum de endereços e contatos. Cada item só é visível para o próprio responsável: um id de outro
/// responsável é tratado como inexistente (404), sem revelar que existe.
/// </summary>
public abstract class ServicoDeItensDoResponsavel<TItem, TDto, TFiltro>(
    IRepositorioDeItensDoResponsavel<TItem, TFiltro> repositorio,
    ILogger logger) : IServicoDeItensDoResponsavel<TItem, TDto, TFiltro>
    where TItem : class, IItemDoResponsavel
{
    /// <summary>Nome do item nas mensagens ("Endereço", "Contato").</summary>
    protected abstract string Nome { get; }
    protected abstract IReadOnlyCollection<string> CamposOrdenaveis { get; }
    protected abstract TItem Criar(long responsavelId, TDto dados);
    protected abstract void Aplicar(TDto dados, TItem item);

    private string MensagemDeNaoEncontrado => $"{Nome} não encontrado";

    public Resultado<Pagina<TItem>> Listar(long responsavelId, TFiltro filtro, ConsultaPaginada consulta)
    {
        if (!consulta.OrdenacaoValida(CamposOrdenaveis))
            return Erro.Invalido("ordenarPor", $"Ordene por um destes campos: {string.Join(", ", CamposOrdenaveis)}.");

        logger.LogInformation("Buscando {Item} do responsável {ResponsavelId} (página {Pagina}).",
            Nome, responsavelId, consulta.Pagina);

        var (itens, total) = repositorio.Consultar(responsavelId, filtro, consulta);
        return new Pagina<TItem>(itens, total);
    }

    public Resultado<TItem> Obter(long responsavelId, long id)
    {
        var item = DoResponsavel(responsavelId, id);
        if (item is null)
        {
            logger.LogWarning("{Item} {Id} não encontrado para o responsável {ResponsavelId}.", Nome, id, responsavelId);
            return Erro.NaoEncontrado(MensagemDeNaoEncontrado);
        }

        return item;
    }

    public TItem Adicionar(long responsavelId, TDto dados)
    {
        var existentes = repositorio.ListarDoResponsavel(responsavelId);
        var novo = Criar(responsavelId, dados);

        RegraDoPrincipal.AoIncluir(existentes, novo);
        repositorio.Adicionar(novo);
        repositorio.SalvarAlteracoes();

        logger.LogInformation("{Item} {Id} cadastrado para o responsável {ResponsavelId}.", Nome, novo.Id, responsavelId);
        return novo;
    }

    public Resultado Atualizar(long responsavelId, long id, TDto dados)
    {
        var item = DoResponsavel(responsavelId, id);
        if (item is null)
            return Erro.NaoEncontrado(MensagemDeNaoEncontrado);

        Aplicar(dados, item);
        RegraDoPrincipal.AoAtualizar(repositorio.ListarDoResponsavel(responsavelId), item);
        repositorio.SalvarAlteracoes();

        logger.LogInformation("{Item} {Id} atualizado.", Nome, id);
        return Resultado.Ok();
    }

    public Resultado Remover(long responsavelId, long id)
    {
        var item = DoResponsavel(responsavelId, id);
        if (item is null)
            return Erro.NaoEncontrado(MensagemDeNaoEncontrado);

        RegraDoPrincipal.AoRemover(repositorio.ListarDoResponsavel(responsavelId), item);
        repositorio.Remover(item);
        repositorio.SalvarAlteracoes();

        logger.LogInformation("{Item} {Id} removido.", Nome, id);
        return Resultado.Ok();
    }

    public Resultado DefinirPrincipal(long responsavelId, long id)
    {
        var item = DoResponsavel(responsavelId, id);
        if (item is null)
            return Erro.NaoEncontrado(MensagemDeNaoEncontrado);

        RegraDoPrincipal.Definir(repositorio.ListarDoResponsavel(responsavelId), item);
        repositorio.SalvarAlteracoes();

        logger.LogInformation("{Item} {Id} definido como principal.", Nome, id);
        return Resultado.Ok();
    }

    private TItem? DoResponsavel(long responsavelId, long id)
    {
        var item = repositorio.ObterPorId(id);
        return item is not null && item.ResponsavelId == responsavelId ? item : null;
    }
}

public class ServicoDeEnderecos(IResponsavelEnderecoRepository repositorio, ILogger<ServicoDeEnderecos> logger)
    : ServicoDeItensDoResponsavel<ResponsavelEndereco, EnderecoDto, FiltroDeEnderecos>(repositorio, logger),
      IServicoDeEnderecos
{
    protected override string Nome => "Endereço";
    protected override IReadOnlyCollection<string> CamposOrdenaveis => FiltroDeEnderecos.CamposOrdenaveis;
    protected override ResponsavelEndereco Criar(long responsavelId, EnderecoDto dados) => dados.ParaEntidade(responsavelId);
    protected override void Aplicar(EnderecoDto dados, ResponsavelEndereco item) => dados.AplicarEm(item);
}

public class ServicoDeContatos(IResponsavelContatoRepository repositorio, ILogger<ServicoDeContatos> logger)
    : ServicoDeItensDoResponsavel<ResponsavelContato, ContatoDto, FiltroDeContatos>(repositorio, logger),
      IServicoDeContatos
{
    protected override string Nome => "Contato";
    protected override IReadOnlyCollection<string> CamposOrdenaveis => FiltroDeContatos.CamposOrdenaveis;
    protected override ResponsavelContato Criar(long responsavelId, ContatoDto dados) => dados.ParaEntidade(responsavelId);
    protected override void Aplicar(ContatoDto dados, ResponsavelContato item) => dados.AplicarEm(item);
}
