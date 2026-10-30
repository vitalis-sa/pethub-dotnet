namespace Vitalis.Infrastructure.Configuracao;

/// <summary>Assinatura dos tokens que o Vitalis emite para o responsável (seção "Jwt").</summary>
public class JwtOpcoes
{
    public string Segredo { get; set; } = "";
    public string Emissor { get; set; } = "vitalis";
    /// <summary>Para quem o token vale (app do responsável). Diferente do público do token de serviço.</summary>
    public string Audiencia { get; set; } = "vitalis-app";
    public int ExpiracaoMinutos { get; set; } = 120;
}

/// <summary>Broker de mensagens (seção "RabbitMq"). Sem Host o consumidor fica desligado.</summary>
public class RabbitOpcoes
{
    public string Host { get; set; } = "";
    public int Porta { get; set; } = 5672;
    public string Usuario { get; set; } = "guest";
    public string Senha { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";
    public bool Ssl { get; set; }

    // Devem ser iguais aos declarados pelo pethub-java (RabbitConfig).
    public string Exchange { get; set; } = "pethub.lembretes.exchange";
    public string RoutingKey { get; set; } = "lembrete.criar";
    public string Fila { get; set; } = "pethub.lembretes.queue";

    public bool Configurado => !string.IsNullOrWhiteSpace(Host);
}

/// <summary>
/// Segredo do JWT de serviço entre o Vitalis e o Java (seção "ServicoAuth"), mín. 32 bytes.
/// Mesmo valor do SERVICE_JWT_SECRET do Java.
/// </summary>
public class ServicoOpcoes
{
    public string Segredo { get; set; } = "";
}

/// <summary>Serviço Java (seção "PethubJava"): clínico, pets e consultas.</summary>
public class PethubJavaOpcoes
{
    public string UrlBase { get; set; } = "http://localhost:8080";
    public int TimeoutSegundos { get; set; } = 15;
}

/// <summary>
/// MongoDB (seção "Mongo"): histórico das mensagens recebidas da fila de lembretes.
/// Sem ConnectionString o histórico fica desligado e a API funciona normalmente.
/// </summary>
public class MongoOpcoes
{
    public string ConnectionString { get; set; } = "";
    public string Banco { get; set; } = "vitalis";
    public string ColecaoHistorico { get; set; } = "historico_mensagens";
    /// <summary>Quanto esperar por um servidor antes de desistir (a gravação do histórico não pode travar a fila).</summary>
    public int TimeoutSegundos { get; set; } = 5;

    public bool Configurado => !string.IsNullOrWhiteSpace(ConnectionString);
}
