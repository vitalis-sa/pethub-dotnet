using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using Vitalis.Application.Consultas;
using Vitalis.Application.Historico;
using Vitalis.Infrastructure.Configuracao;

namespace Vitalis.Infrastructure.Historico;

public sealed class HistoricoDeMensagensMongo : IHistoricoDeMensagens
{
    private readonly IMongoCollection<RegistroDeMensagem> _colecao;

    static HistoricoDeMensagensMongo()
    {
        // Campos em camelCase no banco ("responsavelId", "recebidaEm"), como no resto da API e no padrão do MongoDB.
        ConventionRegistry.Register("vitalis-historico",
            new ConventionPack { new CamelCaseElementNameConvention() },
            tipo => tipo == typeof(RegistroDeMensagem));

        // O documento é uma classe simples da aplicação; o mapeamento para o MongoDB fica só aqui.
        if (!BsonClassMap.IsClassMapRegistered(typeof(RegistroDeMensagem)))
            BsonClassMap.RegisterClassMap<RegistroDeMensagem>(mapa =>
            {
                mapa.AutoMap();
                mapa.MapIdMember(r => r.Id)
                    .SetIdGenerator(StringObjectIdGenerator.Instance)
                    .SetSerializer(new StringSerializer(BsonType.ObjectId));
                mapa.MapMember(r => r.RecebidaEm).SetSerializer(new DateTimeSerializer(DateTimeKind.Utc));
            });
    }

    public HistoricoDeMensagensMongo(IMongoClient cliente, IOptions<MongoOpcoes> opcoes)
    {
        var mongo = opcoes.Value;
        _colecao = cliente.GetDatabase(mongo.Banco).GetCollection<RegistroDeMensagem>(mongo.ColecaoHistorico);
    }

    public bool Habilitado => true;

    public void Registrar(RegistroDeMensagem registro) => _colecao.InsertOne(registro);

    public async Task<Pagina<RegistroDeMensagem>> ConsultarAsync(
        long responsavelId, FiltroDeHistorico filtro, ConsultaPaginada consulta, CancellationToken ct = default)
    {
        var f = Builders<RegistroDeMensagem>.Filter;
        var condicao = f.Eq(r => r.ResponsavelId, responsavelId);

        if (!string.IsNullOrWhiteSpace(filtro.Resultado))
            condicao &= f.Eq(r => r.Resultado, filtro.Resultado.Trim());
        if (!string.IsNullOrWhiteSpace(filtro.Tipo))
            condicao &= f.Eq(r => r.Tipo, filtro.Tipo.Trim().ToUpperInvariant());
        if (filtro.De is not null)
            condicao &= f.Gte(r => r.RecebidaEm, filtro.De.Value);
        if (filtro.Ate is not null)
            condicao &= f.Lte(r => r.RecebidaEm, filtro.Ate.Value);

        var total = await _colecao.CountDocumentsAsync(condicao, cancellationToken: ct);
        var itens = await _colecao.Find(condicao)
            .Sort(Ordenacao(consulta))
            .Skip(consulta.Deslocamento())
            .Limit(consulta.Tamanho)
            .ToListAsync(ct);

        return new Pagina<RegistroDeMensagem>(itens, total);
    }

    /// <summary>Índice da consulta principal (responsável, mais recentes primeiro). Criar de novo não faz nada.</summary>
    public Task CriarIndicesAsync(CancellationToken ct)
        => _colecao.Indexes.CreateOneAsync(new CreateIndexModel<RegistroDeMensagem>(
            Builders<RegistroDeMensagem>.IndexKeys.Ascending(r => r.ResponsavelId).Descending(r => r.RecebidaEm),
            new CreateIndexOptions { Name = "ix_responsavel_recebida" }), cancellationToken: ct);

    private static SortDefinition<RegistroDeMensagem> Ordenacao(ConsultaPaginada consulta)
    {
        var s = Builders<RegistroDeMensagem>.Sort;
        var desc = consulta.Decrescente(padrao: true);
        SortDefinition<RegistroDeMensagem> Por(System.Linq.Expressions.Expression<Func<RegistroDeMensagem, object?>> campo)
            => desc ? s.Descending(campo) : s.Ascending(campo);

        var principal = consulta.CampoDeOrdenacao("recebidaEm").ToLowerInvariant() switch
        {
            "resultado" => Por(r => r.Resultado),
            "tipo" => Por(r => r.Tipo),
            _ => Por(r => r.RecebidaEm)
        };
        // Desempate estável entre páginas.
        return s.Combine(principal, s.Descending(r => r.Id));
    }
}

/// <summary>Usado quando não há MongoDB configurado: não grava nada e não tem o que consultar.</summary>
public sealed class HistoricoDesligado : IHistoricoDeMensagens
{
    public bool Habilitado => false;

    public void Registrar(RegistroDeMensagem registro) { }

    public Task<Pagina<RegistroDeMensagem>> ConsultarAsync(
        long responsavelId, FiltroDeHistorico filtro, ConsultaPaginada consulta, CancellationToken ct = default)
        => Task.FromResult(new Pagina<RegistroDeMensagem>([], 0));
}

/// <summary>
/// Cria os índices do MongoDB em segundo plano, logo depois da subida. Se o Mongo estiver fora do ar, só registra
/// o aviso: a API e a fila continuam funcionando, e o índice é criado na próxima subida.
/// </summary>
public sealed class PreparadorDoHistorico(IHistoricoDeMensagens historico, ILogger<PreparadorDoHistorico> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken parada)
    {
        if (historico is not HistoricoDeMensagensMongo mongo)
            return;

        try
        {
            await mongo.CriarIndicesAsync(parada);
            logger.LogInformation("Índices do histórico de mensagens (MongoDB) conferidos.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Não foi possível criar os índices do histórico no MongoDB.");
        }
    }
}
