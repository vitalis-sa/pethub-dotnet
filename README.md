# Vitalis API

API RESTful desenvolvida em **ASP.NET Core (.NET 10)** responsável pelo domínio do **Responsável** no sistema PetHub.

O PetHub é um sistema veterinário composto por **dois serviços**. Cada um é dono das próprias tabelas
(podem usar o mesmo schema Oracle ou bancos separados) e nenhum acessa o banco do outro:

| Serviço | Tecnologia | Responsabilidade |
|---|---|---|
| **Vitalis (este)** | C# .NET 10 | Cadastro e login do **responsável**, endereços, contatos, **lembretes** e o gateway do responsável para o serviço clínico |
| **pethub-java** | Java 21 + Spring Boot | Veterinários, pets, consultas, prontuário, exames, e-mail de confirmação e IA (Gemini) |

Como conversam:

| Fluxo | Direção | Como |
|---|---|---|
| Lembrete (consulta marcada, vacina, exame, medicamento) | Java → **RabbitMQ** → Vitalis | O Java só publica; **só o Vitalis consome** (`Mensageria/LembreteConsumidor`) |
| Responsável usa pets, consultas, exames... | App → Vitalis `/api/clinico/**` → Java | O Vitalis valida o JWT e repassa ao Java com um JWT de serviço HS256 de 60 s (`X-Service-Authorization`, `sub` = responsável); o Java aplica as regras de posse |
| Java precisa do nome/e-mail/CPF do responsável | Java → Vitalis | `GET /api/responsavel/{id}` e `/buscar?cpf=` com JWT de serviço assinado pelo Java |
| Responsável é excluído | Vitalis → Java | `DELETE /api/pets/responsavel/{id}` **antes** de apagar o cadastro; se o Java não confirmar, nada é apagado |

O veterinário não passa por aqui: ele faz login direto no Java.

Na **3ª sprint** a aplicação ganhou uma camada de **monitoramento e observabilidade** (Health Checks, logging estruturado com Serilog e tracing/métricas com OpenTelemetry) e uma suíte de **testes automatizados** no padrão AAA, com testes unitários e de integração.

---

## Tecnologias

**Aplicação**

- .NET 10 / ASP.NET Core Web API
- Entity Framework Core 10 + Oracle.EntityFrameworkCore
- BCrypt.Net-Next (hash de senhas)
- JWT (`Microsoft.AspNetCore.Authentication.JwtBearer`) — autenticação do responsável
- RabbitMQ.Client 7 — consumo da fila de lembretes (CloudAMQP)
- Swashbuckle (Swagger / OpenAPI 3.0)

**Monitoramento e observabilidade**

- `Microsoft.Extensions.Diagnostics.HealthChecks` — verificações de saúde da API
- **Serilog** (`Serilog.AspNetCore`, `Serilog.Sinks.Console`, `Serilog.Sinks.File`) — logging estruturado
- **OpenTelemetry** (`Extensions.Hosting`, `Instrumentation.AspNetCore`, `Instrumentation.Http`, `Exporter.Console`, `Exporter.OpenTelemetryProtocol`) — tracing distribuído e métricas

**Testes**

- **xUnit** — framework de testes
- **Moq** — mocking de dependências
- **FluentAssertions** — asserções expressivas
- `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory`) — testes de integração
- `Microsoft.EntityFrameworkCore.InMemory` — banco em memória nos testes de integração
- **coverlet** — cobertura de código

---

## Pré-requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Acesso ao banco Oracle (FIAP ou local)
- `dotnet-ef` instalado globalmente:

```bash
dotnet tool install --global dotnet-ef
export PATH="$PATH:$HOME/.dotnet/tools"
```

---

## Instalação e execução

```bash
# 1. Clonar o repositório
git clone https://github.com/vitalis-sa/PetHub-.NET.git
cd PetHub-.NET

# 2. Restaurar dependências
dotnet restore

# 3. Definir as variáveis de ambiente (veja "Configuração" abaixo): conexão Oracle, ServicoAuth__Segredo, Jwt__Segredo...

# 4. Rodar a aplicação (as tabelas do Vitalis são criadas sozinhas se ainda não existirem)
dotnet run
```

**Banco de dados.** Na subida, o Vitalis cria só as próprias tabelas (`TB_RESPONSAVEL`, `TB_RESPONSAVEL_ENDERECO`,
`TB_RESPONSAVEL_CONTATO`, `TB_LEMBRETE`) se `TB_RESPONSAVEL` não existir (`Dados/InicializadorDoBanco.cs`). O
Java faz o mesmo com as dele. Por isso **a ordem de subida não importa** e nenhum serviço derruba as tabelas do
outro. Não há chave estrangeira entre os dois lados. Para desligar: `Banco__CriarTabelas=false`.

A API sobe em `http://localhost:5192`.
O Swagger fica disponível em `http://localhost:5192/swagger`.

---

## Estrutura da solução

```
Vitalis/
├── Vitalis.slnx                  # Solução: API + projetos de teste
├── Controllers/                  # Endpoints da API (instrumentados com ILogger, Activity e Counter)
│   └── GatewayClinicoController.cs     # /api/clinico/** → pethub-java
├── Configuracao/Opcoes.cs        # Jwt, RabbitMq e PethubJava
├── Dados/AppDbContext.cs  InicializadorDoBanco.cs
├── Dto/  Models/  Repositories/
├── Integracao/PethubJavaClient.cs      # HttpClient para o Java (credencial de serviço)
├── Mensageria/                         # LembreteConsumidor (RabbitMQ) e ProcessadorDeLembrete
├── Seguranca/                          # JWT, credencial de serviço e [ApenasProprietario]
├── Health/                       # Health Checks customizados (3ª sprint)
│   ├── BancoDadosHealthCheck.cs        # Conectividade com o Oracle
│   ├── ServicoExternoHealthCheck.cs    # Disponibilidade do backend Java
│   └── RespostaHealthCheck.cs          # ResponseWriter JSON do endpoint /health
├── Middlewares/
│   └── CorrelationIdMiddleware.cs      # X-Correlation-ID no log estruturado
├── Observabilidade/
│   └── AplicacaoMetricas.cs            # Meter, Counters e ActivitySource da aplicação
├── Program.cs                    # Serilog, JWT, RabbitMQ, Health Checks e OpenTelemetry
└── tests/
    ├── Vitalis.Tests.Unit/             # Testes unitários
    │   ├── Dominio/  Aplicacao/  Mensageria/  Seguranca/  Fixtures/
    └── Vitalis.Tests.Integration/      # Testes de integração
        ├── Endpoints/  Monitoramento/  Fixtures/   # JavaFalso substitui o pethub-java
```

---

## Monitoramento e Observabilidade

### Health Checks

A API expõe o endpoint nativo de diagnóstico em **`GET /health`**, construído sobre
`Microsoft.Extensions.Diagnostics.HealthChecks`. Ele agrega duas verificações customizadas
que implementam a interface `IHealthCheck`:

| Verificação | O que faz | Resultado |
|---|---|---|
| `banco_dados` | Testa a conexão com o Oracle através do `AppDbContext` e mede a latência | `Healthy` ou `Unhealthy`, sempre com `LatenciaMs` |
| `servico_externo` | Faz um `GET` HTTP no backend Java (`ServicosExternos:PethubJava`) | `Healthy` ou `Unhealthy`, com `LatenciaMs` e `StatusCode` |

O status agregado define o código HTTP:

| Status geral | Campo `status` | HTTP |
|---|---|---|
| Todas as verificações saudáveis | `Healthy` | `200 OK` |
| Qualquer verificação com falha | `Unhealthy` | `503 Service Unavailable` |

O corpo é escrito em **JSON** por um `ResponseWriter` customizado
(`Health/RespostaHealthCheck.cs`), registrado no `MapHealthChecks` via `HealthCheckOptions`.
Em vez do texto simples com o status agregado, ele detalha **cada verificação**:

| Campo | Conteúdo |
|---|---|
| `status` | Status agregado da API |
| `duracaoTotalMs` | Tempo total gasto no conjunto de verificações |
| `verificadoEm` | Instante (UTC) da coleta |
| `verificacoes[].nome` | Nome registrado no `AddCheck<>` (`banco_dados`, `servico_externo`) |
| `verificacoes[].status` | `Healthy`, `Degraded` ou `Unhealthy` |
| `verificacoes[].descricao` | Mensagem devolvida pelo `IHealthCheck` |
| `verificacoes[].duracaoMs` | Duração daquela verificação isolada |
| `verificacoes[].dados` | Dados coletados pelo check (`LatenciaMs`, `StatusCode`) |
| `verificacoes[].erro` | Mensagem da exceção, quando a verificação falha |

Como monitorar pelo terminal:

```bash
# Corpo completo da resposta
curl -s http://localhost:5192/health | jq

# Apenas o status agregado
curl -s http://localhost:5192/health | jq -r .status

# Apenas o código HTTP — útil para orquestradores (Kubernetes, Azure)
curl -o /dev/null -w "%{http_code}\n" http://localhost:5192/health
```

Exemplo de saída com o banco no ar e o backend Java fora:

```json
{
  "status": "Unhealthy",
  "duracaoTotalMs": 2043.51,
  "verificadoEm": "2026-09-12T22:34:07.1183920+00:00",
  "verificacoes": [
    {
      "nome": "banco_dados",
      "status": "Healthy",
      "descricao": "Conexão com o Banco de Dados estabelecida com sucesso.",
      "duracaoMs": 41.27,
      "dados": {
        "LatenciaMs": 39
      }
    },
    {
      "nome": "servico_externo",
      "status": "Unhealthy",
      "descricao": "Serviço externo (pethub-java) inacessível.",
      "duracaoMs": 2043.18,
      "dados": {
        "LatenciaMs": 2042
      },
      "erro": "Connection refused (localhost:8080)"
    }
  ]
}
```

> Para simular uma falha de conexão em sala, basta apontar a `OracleConnection`
> para um host inexistente ou parar o backend Java: o `/health` passa a responder
> `Unhealthy` com HTTP 503.

### Logging estruturado (Serilog)

O Serilog substitui o provedor de logging padrão da Microsoft (`builder.Host.UseSerilog()`)
e grava em dois **Sinks** simultâneos:

| Sink | Destino |
|---|---|
| Console | Template `[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}` |
| Arquivo | `logs/vitalis-YYYYMMDD.log`, com rotação diária |

**Níveis de log usados nos controllers:**

| Nível | Quando |
|---|---|
| `Information` | Início das consultas, cadastros e atualizações bem-sucedidas |
| `Warning` | Recurso não encontrado, credenciais inválidas, Service Token inválido, dados rejeitados |
| `Error` | Falhas inesperadas no cadastro (`POST /api/responsavel/cadastro`) e na gravação de lembretes vindos da fila: o `try/catch` chama `_logger.LogError(ex, ...)` com a exceção completa, marca o span como `Error`, contabiliza a métrica com `status=erro_interno` e devolve `500` |

**Correlation ID:** o `CorrelationIdMiddleware` lê o header `X-Correlation-ID` da requisição
(ou gera um novo GUID), devolve o valor no header da resposta e o injeta no `LogContext` do
Serilog — de modo que **todos os logs daquela requisição carregam o mesmo identificador**,
permitindo rastrear a chamada de ponta a ponta.

```bash
# A resposta ecoa o mesmo identificador enviado
curl -i -H "X-Correlation-ID: aula-42" http://localhost:5192/health
```

### Tracing distribuído e métricas (OpenTelemetry)

O OpenTelemetry é registrado em `Program.cs` com o nome de serviço `Vitalis.API` e exporta
tudo no **Console** (`AddConsoleExporter`), permitindo inspecionar traces e métricas
diretamente no terminal do Kestrel.

**Tracing** — combina auto-instrumentação com spans manuais:

| Fonte | O que rastreia |
|---|---|
| `AddAspNetCoreInstrumentation()` | Requisições HTTP recebidas pela API |
| `AddHttpClientInstrumentation()` | Chamadas HTTP de saída (ex.: verificação do serviço externo) |
| `AddSource("Vitalis.API")` | Spans manuais criados com o `ActivitySource` da aplicação |

O cadastro de responsável abre um span próprio via `ActivitySource` e anexa tags de negócio.
Em caso de falha, o span é marcado com `ActivityStatusCode.Error`.

**Métricas** — a classe `AplicacaoMetricas` centraliza o `Meter` da aplicação:

| Métrica | Tipo | Descrição |
|---|---|---|
| `responsaveis_cadastrados_total` | Counter | Responsáveis cadastrados, com a tag `status` |
| `lembretes_criados_total` | Counter | Lembretes gravados a partir da fila, com a tag `status` (`sucesso`, `erro_validacao`, `erro_interno`) |

A tag `status` distingue os desfechos (`sucesso`, `erro_validacao`, `erro_cpf_duplicado`, `erro_interno`), o que permite acompanhar a **taxa de erros** por operação de negócio.

Já o **tempo de resposta** de todas as rotas vem da auto-instrumentação
`AddAspNetCoreInstrumentation()`, que publica o histograma `http.server.request.duration`
particionado por rota e status HTTP:

```
Metric Name: http.server.request.duration, Description: Duration of HTTP server requests., Unit: s, Metric Type: Histogram
(...) http.request.method: GET http.response.status_code: 503 http.route: /health
Value: Sum: 16.0242643 Count: 1 Min: 16.0242643 Max: 16.0242643
```

O pacote `OpenTelemetry.Exporter.OpenTelemetryProtocol` também está instalado: basta trocar
`AddConsoleExporter()` por `AddOtlpExporter()` para enviar os dados a um coletor externo
(Jaeger, Prometheus, Grafana ou Aspire Dashboard).

### Configuração

**Segredos e endereços vêm de variáveis de ambiente**, nunca do código. No `appsettings.json` ficam só valores
vazios ou de exemplo.

| Variável | Para quê | Obrigatória |
|---|---|---|
| `ConnectionStrings__OracleConnection` | Conexão Oracle | sim |
| `ServicoAuth__Segredo` | Segredo (mín. 32 caracteres) que assina o JWT de serviço entre o Vitalis e o Java. **Mesmo valor do `SERVICE_JWT_SECRET` do Java.** Vazio = nada é emitido nem aceito | sim |
| `Jwt__Segredo` | Chave de assinatura do token do responsável (mín. 32 bytes). A API não sobe sem ela | sim |
| `Jwt__Emissor`, `Jwt__Audiencia`, `Jwt__ExpiracaoMinutos` | Emissor (padrão `vitalis`), audiência (padrão `vitalis-app`) e validade (padrão 120 min) | não |
| `RabbitMq__Host`, `__Porta`, `__Usuario`, `__Senha`, `__VirtualHost`, `__Ssl` | Broker. No CloudAMQP: porta `5671`, `Ssl=true`, vhost = usuário. Sem `Host`, o consumidor fica desligado | sim, para lembretes |
| `PethubJava__UrlBase` | URL do serviço Java (padrão `http://localhost:8080`) | sim |
| `ServicosExternos__PethubJava` | URL sondada pelo health check (padrão: `/api-docs` do Java) | não |
| `Banco__CriarTabelas` | Cria as tabelas do Vitalis na subida (padrão `true`) | não |

- **Fila:** `RabbitMq__Exchange`, `__RoutingKey` e `__Fila` já vêm com os mesmos valores do Java
  (`pethub.lembretes.exchange`, `lembrete.criar`, `pethub.lembretes.queue`). O Vitalis também declara exchange, fila e
  ligação (idempotente), então a mensagem não se perde se o Java publicar antes do Vitalis subir.
- **Cloud Run (padrão, sem instância mínima):** o consumidor só esvazia a fila enquanto a instância estiver acordada.
  Os lembretes ficam guardados na fila durável e são gravados na próxima vez que o serviço subir.
- **Migrations:** o projeto não usa mais migrations do EF. O arquivo `Migrations/AppDbContextModelSnapshot.cs` é um
  resto do modelo anterior; pode ser apagado.
---

## Testes automatizados

A solução tem testes organizados em dois projetos separados por camada:

| Projeto | Escopo |
|---|---|
| `tests/Vitalis.Tests.Unit` | Camadas de **Domínio** e **Aplicação**, com dependências isoladas por Moq |
| `tests/Vitalis.Tests.Integration` | Fluxo HTTP completo via `WebApplicationFactory` |

### Como executar

```bash
# Todos os testes da solução
dotnet test

# Somente os testes unitários
dotnet test tests/Vitalis.Tests.Unit

# Somente os testes de integração
dotnet test tests/Vitalis.Tests.Integration

# Com relatório de cobertura (gera coverage.cobertura.xml em TestResults/)
dotnet test --collect:"XPlat Code Coverage"

# Saída detalhada, com o nome de cada teste executado
dotnet test --logger "console;verbosity=detailed"

# Filtrar por classe ou por nome
dotnet test --filter FullyQualifiedName~LembretesApiControllerTests
dotnet test --filter FullyQualifiedName~ProcessadorDeLembreteTests
```

No Visual Studio: **Teste → Gerenciador de Testes** (`Ctrl + E, T`) e **Executar Todos os Testes**.

Os testes **não dependem do banco Oracle**: os unitários substituem os repositórios por mocks
e os de integração trocam o `AppDbContext` por um banco em memória exclusivo de cada execução.

Para visualizar a cobertura em HTML:

```bash
dotnet tool install --global dotnet-reportgenerator-globaltool
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"coveragereport" -reporttypes:Html
```

### Padrão AAA e nomenclatura

Todos os testes seguem o padrão **AAA (Arrange, Act, Assert)**, com as três etapas comentadas,
e a convenção **`MetodoTestado_Cenario_ResultadoEsperado`**:

```csharp
[Fact]
public void Cadastrar_DadosValidos_DeveSalvarNoRepositorioERetornarCreated()
{
    // Arrange
    var dto = NovoCadastroDto();
    _repositorioMock.Setup(r => r.GetByCpf(dto.Cpf)).Returns((Responsavel?)null);
    _repositorioMock.Setup(r => r.Add(It.IsAny<Responsavel>())).Callback<Responsavel>(r => r.Id = 10);
    var controller = CriarController();

    // Act
    var resultado = controller.Cadastrar(dto);

    // Assert
    var created = resultado.Should().BeOfType<CreatedAtActionResult>().Subject;
    created.RouteValues!["id"].Should().Be(10L);
    // Verifica se o método Add do repositório foi chamado exatamente 1 vez
    _repositorioMock.Verify(r => r.Add(It.Is<Responsavel>(x => x.Cpf == dto.Cpf)), Times.Once);
}
```

| Exemplo | Leitura |
|---|---|
| `GetById_ResponsavelInexistente_DeveRetornarNotFound` | Método `GetById`, cenário "responsável não existe", esperado `404` |
| `GetLembretesPorResponsavel_DeOutroResponsavel_DeveRetornar403` | Responsável tentando ver lembretes de outro |
| `Construtor_NomeInvalido_DeveRetornarErroNoCampoNome` | Regra de validação do domínio |

### Fixtures e Collection Fixtures

O contexto compartilhado entre os testes é montado com os recursos do xUnit:

| Fixture | Tipo | Compartilha |
|---|---|---|
| `ConfiguracaoFixture` | `IClassFixture` | o autenticador de serviço com segredo de teste e a montagem do `HttpContext` (e do responsável logado) dos controllers |
| `VitalisWebApplicationFactory` | `ICollectionFixture` | Uma única instância da API em memória (banco em memória e `JavaFalso` no lugar do pethub-java; RabbitMQ desligado) |

### O que os testes cobrem

**Unitários — Domínio** (`tests/Vitalis.Tests.Unit/Dominio`)
Regras de validação de `Responsavel`, `ResponsavelEndereco`, `ResponsavelContato` e `Lembrete`:
campos obrigatórios, limites de tamanho (nome, CPF, UF, CEP), valores padrão e os enums
`TipoLembrete` / `StatusLembrete`. Usa `[Theory]` com `[InlineData]` para os cenários parametrizados.

**Unitários — Aplicação** (`tests/Vitalis.Tests.Unit/Aplicacao`)
Os controllers com dependências simuladas por Moq: sucesso, `404`, `400`, `409` de CPF duplicado, `401` de login e de
JWT de serviço inválido, `403` para dados de outro responsável, `503` quando o Java não confirma a exclusão dos pets, o
gateway clínico (lista de recursos permitidos, bloqueio de `..`) e `500` com `LogError`.

**Unitários — Mensageria e Segurança**
`ProcessadorDeLembrete` (mensagem válida, repetida, inválida, responsável inexistente, banco fora → devolve à fila),
emissão do JWT e o atributo `[ApenasProprietario]`.

**Integração — Endpoints** (`tests/Vitalis.Tests.Integration/Endpoints`)
Fluxo HTTP completo: cadastro e login (com token), senha nunca na resposta, `401`/`403` por token ausente ou de outro
responsável, credencial de serviço no `GET /api/responsavel/{id}`, exclusão que depende do Java, lembretes criados
a partir de mensagens da fila sem duplicar, gateway clínico, e a regra de endereço/contato principal.

**Integração — Monitoramento** (`tests/Vitalis.Tests.Integration/Monitoramento`)
O endpoint `/health`: status agregado e HTTP `503`, `Content-Type` `application/json`, o
detalhamento de cada verificação no corpo (nome, status, descrição e duração) e a
identificação de qual check falhou. Cobre também o eco do header `X-Correlation-ID`, a
geração de identificadores distintos por requisição e a disponibilidade do documento OpenAPI.

---

## Documentação das rotas

### Monitoramento

| Método | Rota | Descrição |
|---|---|---|
| `GET` | `/health` | Saúde da API: conexão com o Oracle e disponibilidade do backend Java |

### Autenticação

O responsável faz login (`POST /api/responsavel/login`) ou se cadastra e recebe um **JWT** (`token`). Ele vai no header
`Authorization: Bearer {token}` nas rotas abaixo. Cada responsável só acessa os **próprios** dados (`403` caso contrário).

### Responsáveis — `/api/responsavel`

| Método | Rota | Descrição | Auth |
|---|---|---|---|
| `GET` | `/api/responsavel/{id}` | Busca o responsável com endereços e contatos | JWT do próprio, ou JWT de serviço do Java |
| `GET` | `/api/responsavel/buscar?cpf={cpf}` | Busca por CPF (chamado pelo Java) | JWT de serviço do Java (`X-Service-Authorization`) |
| `POST` | `/api/responsavel/cadastro` | Cadastra e já devolve o `token` | — |
| `POST` | `/api/responsavel/login` | Autentica e devolve o `token` | — |
| `PUT` | `/api/responsavel/{id}` | Atualiza nome, CPF e e-mail | JWT do próprio |
| `DELETE` | `/api/responsavel/{id}` | Apaga os pets no Java e depois o cadastro (`503` se o Java não confirmar) | JWT do próprio |

### Endereços — `/api/responsavel/{responsavelId}/enderecos` (JWT do próprio)

| Método | Rota | Descrição |
|---|---|---|
| `GET` | `/api/responsavel/{responsavelId}/enderecos` | Lista endereços do responsável |
| `GET` | `/api/responsavel/{responsavelId}/enderecos/{id}` | Busca endereço por ID |
| `POST` | `/api/responsavel/{responsavelId}/enderecos` | Adiciona endereço ao responsável |
| `PUT` | `/api/responsavel/{responsavelId}/enderecos/{id}` | Atualiza endereço |
| `DELETE` | `/api/responsavel/{responsavelId}/enderecos/{id}` | Remove endereço |
| `PATCH` | `/api/responsavel/{responsavelId}/enderecos/{id}/principal` | Define como endereço principal |

> Ao marcar um endereço como principal, os demais são automaticamente desmarcados.

### Contatos — `/api/responsavel/{responsavelId}/contatos` (JWT do próprio)

| Método | Rota | Descrição |
|---|---|---|
| `GET` | `/api/responsavel/{responsavelId}/contatos` | Lista contatos do responsável |
| `GET` | `/api/responsavel/{responsavelId}/contatos/{id}` | Busca contato por ID |
| `POST` | `/api/responsavel/{responsavelId}/contatos` | Adiciona contato ao responsável |
| `PUT` | `/api/responsavel/{responsavelId}/contatos/{id}` | Atualiza contato |
| `DELETE` | `/api/responsavel/{responsavelId}/contatos/{id}` | Remove contato |
| `PATCH` | `/api/responsavel/{responsavelId}/contatos/{id}/principal` | Define como contato principal |

### Lembretes — `/api/lembretes` (JWT)

Os lembretes **não são criados por HTTP**: chegam pela fila (veja abaixo). O responsável só consulta, muda o status e apaga os seus.

| Método | Rota | Descrição |
|---|---|---|
| `GET` | `/api/lembretes/responsavel/{responsavelId}` | Lista os lembretes do responsável |
| `GET` | `/api/lembretes/responsavel/{responsavelId}/tipo/{tipo}` | Filtra por tipo (`VACINA`, `CONSULTA`, `EXAME`, `MEDICAMENTO`) |
| `PATCH` | `/api/lembretes/{id}/status` | Atualiza o status (`PENDENTE`, `ENVIADO`, `FALHOU`) |
| `DELETE` | `/api/lembretes/{id}` | Remove o lembrete |

> `tipo` e `status` trafegam como **texto** (`"CONSULTA"`, `"PENDENTE"`), no corpo e na rota.

### Gateway clínico — `/api/clinico/**` (JWT)

Repassa ao pethub-java, dizendo de qual responsável é o pedido. Aceita `GET`, `POST`, `PUT`, `PATCH` e `DELETE` para
`pets`, `consultas`, `exames`, `diagnosticos`, `pedidos-medicos`, `vacinas-tratamentos`, `veterinarios` e `unidades`.
Exemplos: `POST /api/clinico/consultas` (agendar), `POST /api/clinico/pets` (cadastrar pet), `GET /api/clinico/consultas/{id}/receita`.
O status e o corpo da resposta do Java são devolvidos como vieram; `503` se o Java estiver fora.

---

## Lembretes pela fila (RabbitMQ)

O Java publica a mensagem **depois** de gravar a consulta (ou pedido/vacina). O Vitalis consome:

```json
{ "responsavelId": 7, "petId": 5, "tipo": "CONSULTA", "dataAgendada": "2026-11-01",
  "mensagem": "Consulta agendada para Rex em 2026-11-01T14:30", "referenciaId": 100, "referenciaTipo": "Consulta" }
```

- Exchange `pethub.lembretes.exchange` (direct, durável), routing key `lembrete.criar`, fila `pethub.lembretes.queue` (durável).
- `ack` só depois de gravar. Se `referenciaTipo` + `referenciaId` + responsável já existem, o lembrete é **atualizado** (nova data e texto, status volta a `PENDENTE`) em vez de duplicado: assim a mensagem repetida não muda nada, e uma consulta remarcada reaproveita o mesmo lembrete.
- Mensagem inválida ou de responsável inexistente é **descartada** (para não voltar para sempre); falha do banco devolve a mensagem à fila.

---

> Atalho: o projeto também lê o arquivo opcional `appsettings.Local.json` (ignorado pelo git) com a seção `RabbitMq`. Use o que preferir; o user-secrets abaixo continua valendo.

## RabbitMQ local (CloudAMQP) com user-secrets

O segredo não vai em `appsettings.json`. Em desenvolvimento, guarde-o nos *user-secrets* (ficam na sua máquina, fora do repositório), na pasta do projeto:

```bash
dotnet user-secrets set "RabbitMq:Host" "jaragua-01.lmq.cloudamqp.com"
dotnet user-secrets set "RabbitMq:Porta" "5671"
dotnet user-secrets set "RabbitMq:Usuario" "wkgzhezg"
dotnet user-secrets set "RabbitMq:VirtualHost" "wkgzhezg"
dotnet user-secrets set "RabbitMq:Ssl" "true"
dotnet user-secrets set "RabbitMq:Senha" "<senha do painel do CloudAMQP>"
```

O Java usa os mesmos valores no `.env` (`RABBIT_*`). Em produção, as variáveis de ambiente `RabbitMq__*`.

## Senhas e segurança

- Senhas dos responsáveis são armazenadas com hash **BCrypt** — nunca em texto puro
- A senha **nunca é retornada** em nenhum response da API (verificado por teste de integração)
- Tokens JWT assinados com `Jwt__Segredo`; a API não inicia sem uma chave de ao menos 32 bytes
- A autenticação entre serviços é por JWT HS256: token de 60 s, emissor/público/assinatura/validade conferidos; se o segredo de serviço não estiver configurado, **tudo é recusado**
- Segredos (banco, JWT, RabbitMQ, segredo de serviço) vêm de variáveis de ambiente

---

## Integrantes

- Pedro Chasci Puga — RM565154
- Ana Flavia Camelo — RM561489
- Gustavo kenji Terada — RM562745
- João Guilherme Carvalho Novaes — RM566234
- Lucas Figueiredo Vieira — RM561342
