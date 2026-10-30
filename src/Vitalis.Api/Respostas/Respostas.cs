using Vitalis.Api.Hateoas;
using Vitalis.Application.Dtos;
using Vitalis.Application.Historico;
using Vitalis.Domain.Entidades;
using Vitalis.Domain.Enums;

namespace Vitalis.Api.Respostas;

// Respostas da API. Os campos são os mesmos que as entidades já devolviam, acrescidos de "links" (HATEOAS):
// quem lia a resposta antiga continua lendo a nova.

public record EnderecoResposta(
    long Id, long ResponsavelId, string Logradouro, string Numero, string? Complemento,
    string Bairro, string Cidade, string Estado, string Cep, bool Principal, IReadOnlyList<Link> Links)
{
    public static EnderecoResposta De(ResponsavelEndereco e) => new(
        e.Id, e.ResponsavelId, e.Logradouro, e.Numero, e.Complemento, e.Bairro, e.Cidade, e.Estado, e.Cep,
        e.Principal, Hateoas.Links.DoEndereco(e.ResponsavelId, e.Id));
}

public record ContatoResposta(
    long Id, long ResponsavelId, string Tipo, string Telefone, bool Principal, IReadOnlyList<Link> Links)
{
    public static ContatoResposta De(ResponsavelContato c) => new(
        c.Id, c.ResponsavelId, c.Tipo, c.Telefone, c.Principal, Hateoas.Links.DoContato(c.ResponsavelId, c.Id));
}

public record LembreteResposta(
    long Id, long ResponsavelId, long PetId, TipoLembrete Tipo, DateOnly DataAgendada, DateTime? DataHora,
    string Mensagem, StatusLembrete Status, long? ReferenciaId, string? ReferenciaTipo, DateTime CreatedAt,
    IReadOnlyList<Link> Links)
{
    public static LembreteResposta De(Lembrete l) => new(
        l.Id, l.ResponsavelId, l.PetId, l.Tipo, l.DataAgendada, l.DataHora, l.Mensagem, l.Status,
        l.ReferenciaId, l.ReferenciaTipo, l.CreatedAt, Hateoas.Links.DoLembrete(l.ResponsavelId, l.Id));
}

public record ResponsavelResposta(
    long Id, string Nome, string Email, string Cpf, bool Ativo, DateTime CreatedAt,
    IReadOnlyList<EnderecoResposta> Enderecos, IReadOnlyList<ContatoResposta> Contatos, IReadOnlyList<Link> Links)
{
    public static ResponsavelResposta De(Responsavel r) => new(
        r.Id, r.Nome, r.Email, r.Cpf, r.Ativo, r.CreatedAt,
        r.Enderecos.Select(EnderecoResposta.De).ToList(),
        r.Contatos.Select(ContatoResposta.De).ToList(),
        Hateoas.Links.DoResponsavel(r.Id));
}

/// <summary>Uma mensagem da fila, como ficou registrada no MongoDB. O corpo bruto fica só no banco (auditoria).</summary>
public record RegistroDeMensagemResposta(
    string? Id, DateTime RecebidaEm, string? Tipo, long? PetId, string? ReferenciaTipo, long? ReferenciaId,
    string Resultado, string Motivo, IReadOnlyList<Link> Links)
{
    public static RegistroDeMensagemResposta De(RegistroDeMensagem r, long responsavelId) => new(
        r.Id, r.RecebidaEm, r.Tipo, r.PetId, r.ReferenciaTipo, r.ReferenciaId, r.Resultado, r.Motivo,
        Hateoas.Links.DoRegistroDeHistorico(responsavelId));
}

/// <summary>Resposta do cadastro e do login: quem entrou e o JWT para o cabeçalho <c>Authorization</c>.</summary>
public record SessaoResposta(long Id, string Nome, string Email, string Token)
{
    public static SessaoResposta De(Sessao sessao)
        => new(sessao.Responsavel.Id, sessao.Responsavel.Nome, sessao.Responsavel.Email, sessao.Token);
}

/// <summary>Dados mínimos do responsável para o pethub-java (busca por CPF).</summary>
public record ResponsavelResumoResposta(long Id, string Nome, string Cpf, string Email, bool Ativo)
{
    public static ResponsavelResumoResposta De(Responsavel r) => new(r.Id, r.Nome, r.Cpf, r.Email, r.Ativo);
}
