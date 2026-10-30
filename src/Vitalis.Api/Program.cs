using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Vitalis.Api.Erros;
using Vitalis.Api.Health;
using Vitalis.Api.Middlewares;
using Vitalis.Application;
using Vitalis.Application.Abstracoes;
using Vitalis.Application.Observabilidade;
using Vitalis.Infrastructure;
using Vitalis.Infrastructure.Configuracao;
using Vitalis.Infrastructure.Persistencia;
using Vitalis.Infrastructure.Seguranca;

// Raiz de composição: cada camada registra o que é dela (AddAplicacao, AddInfraestrutura) e aqui fica só o
// que é da camada HTTP (controllers, JWT, erros, Swagger, logs e telemetria).

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

builder.Services
    .AddAplicacao()
    .AddInfraestrutura(builder.Configuration);

// Autenticação do responsável: o Vitalis emite o JWT (EmissorDeToken) e valida no próprio serviço.
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

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        // Chaves de dicionário em camelCase, como as propriedades: "errors": { "tamanho": [...] }.
        o.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
    });

// Tratamento de erros global e num formato só (application/problem+json, RFC 9457): exceções não tratadas
// (TratadorGlobalDeExcecoes), validação automática do [ApiController], 401/403/404 sem corpo e os erros
// dos casos de uso (Erros/RespostasDeResultado.cs) passam todos pelo mesmo Completar.
builder.Services.AddProblemDetails(opcoes =>
    opcoes.CustomizeProblemDetails = contexto => Problemas.Completar(contexto.HttpContext, contexto.ProblemDetails));
builder.Services.AddExceptionHandler<TratadorGlobalDeExcecoes>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title       = "Vitalis API",
        Version     = "v1",
        Description = "API do domínio do Responsável — PetHub (cadastro, endereços, contatos, lembretes e gateway para o serviço clínico)"
    });

    // Parâmetros de query documentados como a API os lê: pagina, tamanho, ordenarPor...
    c.DescribeAllParametersInCamelCase();

    // Os comentários /// dos controllers e DTOs aparecem no Swagger.
    foreach (var projeto in new[] { "Vitalis.Api", "Vitalis.Application" })
    {
        var xml = Path.Combine(AppContext.BaseDirectory, $"{projeto}.xml");
        if (File.Exists(xml))
            c.IncludeXmlComments(xml, includeControllerXmlComments: true);
    }

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

// Migrations do EF Core na subida. "Banco:CriarTabelas" é o nome antigo da chave e continua valendo.
if (app.Configuration.GetValue("Banco:AplicarMigracoes", app.Configuration.GetValue("Banco:CriarTabelas", true)))
    InicializadorDoBanco.AplicarMigracoes(app.Services);

app.UseSerilogRequestLogging();
// O Correlation ID vem antes do tratador de erros para que o log da exceção e o corpo do erro levem o mesmo id.
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Vitalis API v1");
    c.RoutePrefix = "swagger";
});

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
