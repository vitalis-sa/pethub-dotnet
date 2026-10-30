using System.Collections.Concurrent;
using Vitalis.Consultas;
using Vitalis.Historico;

namespace Vitalis.Tests.Integration.Fixtures;

/// <summary>Substitui o MongoDB nos testes de integração, com o mesmo filtro, ordenação e paginação.</summary>
public sealed class HistoricoEmMemoria : IHistoricoDeMensagens
{
    private readonly ConcurrentQueue<RegistroDeMensagem> _registros = new();

    public bool Habilitado => true;

    public IReadOnlyCollection<RegistroDeMensagem> Registros => _registros.ToArray();

    public void Registrar(RegistroDeMensagem registro)
    {
        registro.Id ??= Guid.NewGuid().ToString("N");
        _registros.Enqueue(registro);
    }

    public void Limpar() => _registros.Clear();

    public Task<(IReadOnlyList<RegistroDeMensagem> Itens, long Total)> ConsultarAsync(
        long responsavelId, FiltroDeHistorico filtro, ConsultaPaginada consulta, CancellationToken ct = default)
    {
        var consultaBase = _registros.Where(r => r.ResponsavelId == responsavelId
            && (filtro.Resultado is null || r.Resultado == filtro.Resultado)
            && (filtro.Tipo is null || r.Tipo == filtro.Tipo.ToUpperInvariant()));

        var desc = consulta.Decrescente(padrao: true);
        var ordenada = consulta.CampoDeOrdenacao("recebidaEm").ToLowerInvariant() switch
        {
            "resultado" => desc ? consultaBase.OrderByDescending(r => r.Resultado) : consultaBase.OrderBy(r => r.Resultado),
            _ => desc ? consultaBase.OrderByDescending(r => r.RecebidaEm) : consultaBase.OrderBy(r => r.RecebidaEm)
        };

        var lista = ordenada.ToList();
        IReadOnlyList<RegistroDeMensagem> pagina = lista.Skip(consulta.Ignorar).Take(consulta.Tamanho).ToList();
        return Task.FromResult((pagina, (long)lista.Count));
    }
}
