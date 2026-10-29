using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Oracle.EntityFrameworkCore.Infrastructure;
using Vitalis.Configuracao;
using Vitalis.Dados;
using Vitalis.Health;
using Vitalis.Integracao;
using Vitalis.Doses;
using Vitalis.Mensageria;
using Vitalis.Middlewares;
using Vitalis.Observabilidade;
using Vitalis.Repositories;
using Vitalis.Seguranca;
using Serilog;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/vitalis-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddDbContext<AppDbContext>(options =>
    // Bancos Oracle anteriores ao 23ai não têm literal booleano: sem isso o EF gera "THEN True ELSE False"
    // em todo Any()/Exists e a consulta falha com ORA-00904: "FALSE": invalid identifier.
    options.UseOracle(builder.Configuration.GetConnectionString("OracleConnection"),
        oracle => oracle.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)));

builder.Services.AddScoped<IResponsavelRepository,         ResponsavelRepository>();
builder.Services.AddScoped<IResponsavelEnderecoRepository, ResponsavelEnderecoRepository>();
builder.Services.AddScoped<IResponsavelContatoRepository,  ResponsavelContatoRepository>();
builder.Services.AddScoped<ILembreteRepository,            LembreteRepository>();

// Configuração: tudo que é segredo ou endereço vem de variável de ambiente (veja o README).
builder.Services.Configure<JwtOpcoes>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<RabbitOpcoes>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.Configure<PethubJavaOpcoes>(builder.Configuration.GetSection("PethubJava"));
builder.Services.Configure<ServicoOpcoes>(builder.Configuration.GetSection("ServicoAuth"));
// Autenticação entre serviços: JWT HS256 assinado com o segredo de serviço.
builder.Services.AddSingleton<IAutenticadorDeServico, AutenticadorDeServico>();

builder.Services.AddOptions<JwtOpcoes>()
    .Validate(o => ChaveDeAssinatura.SegredoValido(o.Segredo),
        $"Jwt:Segredo (variável Jwt__Segredo) precisa ter ao menos {ChaveDeAssinatura.TamanhoMinimoEmBytes} bytes.")
    .ValidateOnStart();

// Autenticação do responsável: o Vitalis emite o JWT e valida no próprio serviço.
builder.Services.AddSingleton<IEmissorDeToken, EmissorDeToken>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOpcoes>>((bearer, jwt) =>
    {
        bearer.MapInboundClaims = false;
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Value.Emissor,
            ValidateAudience = true,
            ValidAudience = jwt.Value.Audiencia,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = ChaveDeAssinatura.Criar(jwt.Value.Segredo),
            NameClaimType = "name"
        };
    });
builder.Services.AddAuthorization();

// Serviço clínico (Java) e mensageria.
builder.Services.AddHttpClient<IPethubJavaClient, PethubJavaClient>((servicos, http) =>
{
    var java = servicos.GetRequiredService<IOptions<PethubJavaOpcoes>>().Value;
    http.BaseAddress = new Uri(java.UrlBase);
    http.Timeout = TimeSpan.FromSeconds(java.TimeoutSegundos);
});
builder.Services.AddScoped<IProcessadorDeLembrete, ProcessadorDeLembrete>();
builder.Services.AddHostedService<LembreteConsumidor>();
builder.Services.AddHostedService<EnviadorDeLembretes>();

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title       = "Vitalis API",
        Version     = "v1",
        Description = "API do domínio do Responsável — PetHub (cadastro, endereços, contatos, lembretes e gateway para o serviço clínico)"
    });

    // Botão "Authorize" do Swagger: informe apenas o token JWT devolvido por /api/responsavel/login.
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Informe apenas o token JWT."
    });
    c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
    });
});

builder.Services.AddHttpClient();

builder.Services.AddHealthChecks()
    .AddCheck<BancoDadosHealthCheck>("banco_dados")
    .AddCheck<ServicoExternoHealthCheck>("servico_externo");

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(AplicacaoMetricas.NomeServico))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSource(AplicacaoMetricas.NomeServico)
            .AddConsoleExporter();
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddMeter(AplicacaoMetricas.NomeServico)
            .AddConsoleExporter();
    });

// CORS aberto: o app web (Expo, localhost:8081) e outros clientes chamam esta API direto do navegador.
// A autenticação é por JWT no cabeçalho Authorization, não por cookie, então AllowAnyOrigin é seguro.
builder.Services.AddCors(opcoes => opcoes.AddDefaultPolicy(politica => politica
    .AllowAnyOrigin()
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

// Segredo de serviço inválido derruba a subida, em vez de falhar só na primeira chamada.
_ = app.Services.GetRequiredService<IAutenticadorDeServico>();

if (app.Configuration.GetValue("Banco:CriarTabelas", true))
    InicializadorDoBanco.CriarTabelasSeNecessario(app.Services);

app.UseSerilogRequestLogging();
app.UseMiddleware<CorrelationIdMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Vitalis API v1");
    c.RoutePrefix = "swagger";
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
}

// Sem UseHttpsRedirection: no GCP o TLS termina no balanceador e o contêiner recebe HTTP.

app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = RespostaHealthCheck.EscreverAsync
});

app.MapControllers();

app.Run();

public partial class Program;
