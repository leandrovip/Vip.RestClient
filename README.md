# Vip.RestClient

[![Versão no NuGet](https://img.shields.io/nuget/v/Vip.RestClient.svg)](https://www.nuget.org/packages/Vip.RestClient/)
[![Downloads no NuGet](https://img.shields.io/nuget/dt/Vip.RestClient?label=NuGet%20downloads&style=flat-square)](https://www.nuget.org/packages/Vip.RestClient/)
[![Licença MIT](https://img.shields.io/github/license/leandrovip/Vip.RestClient)](LICENSE)

Biblioteca cliente REST para .NET, com chamadas assíncronas, respostas tipadas ou não tipadas e extensões para parâmetros e formulários. Este README resume instalação e uso inicial; os [documentos do projeto](docs/README.md) detalham os contratos e particularidades. Para contexto portátil de LLMs, consulte [`llms.txt`](llms.txt); ele não substitui documentação nem `AGENTS.md`.

## Visão geral

`ClientApi` oferece operações HTTP para GET, POST, PUT, PATCH, DELETE e OPTIONS. Os métodos retornam `Response` ou `Response<T>`; status HTTP malsucedidos são representados no envelope e podem ser transformados em exceção explicitamente pelo consumidor.

O projeto também inclui extensões para IDs e consultas GET, envios JSON, campos de formulário, configuração de headers e eventos de observação. Consulte [Arquitetura](docs/arquitetura.md) para o catálogo de overloads e detalhes do comportamento.

## Recursos

- Chamadas assíncronas para GET, POST, PUT, PATCH, DELETE e OPTIONS.
- Download de resposta GET copiado em blocos para um `Stream` gravável fornecido pelo consumidor.
- Respostas com metadados HTTP e, na forma genérica, dados desserializados; há suporte a leitura de texto e bytes.
- Serialização JSON UTF-8 para objetos enviados por POST, PUT e PATCH.
- Extensões para query e IDs `int`/`Guid`, com disponibilidade diferente conforme o verbo e a forma genérica.
- POST de formulário `multipart/form-data` e `application/x-www-form-urlencoded` para campos textuais.
- Headers padrão configuráveis, incluindo métodos auxiliares para `Authorization`.
- Eventos `BeforeSend` e `ResponseDataReceived` (este último apenas no fluxo genérico).
- Envio de `HttpRequestMessage` preparado pelo consumidor, com token de cancelamento por chamada em `SendAsync`.
- Tipos utilitários para decodificar a estrutura de JWT; **não validam tokens**.

Esses recursos descrevem a implementação atual, não uma promessa de compatibilidade com todas as combinações de overloads. Por exemplo, não existe overload de HEAD e os métodos de IDs não são simétricos entre os verbos. O [catálogo de API](docs/arquitetura.md) e a [documentação de uso](docs/uso.md) mostram as diferenças.

## Requisitos

- A biblioteca tem como target **.NET Standard 2.0** e usa Newtonsoft.Json 13.0.3.
- A demonstração em `tests/Vip.RestClient.Demo/` tem como target **`net9.0-windows`**. Esse target pertence ao executável demonstrativo, não à biblioteca.
- A suíte automatizada em `tests/Vip.RestClient.Tests/` usa xUnit e target **`net9.0`**, sem o target Windows da demonstração.
- Target framework e versão do SDK instalado são conceitos diferentes. A solução não fixa um SDK por `global.json`; use um SDK compatível com os projetos que pretende compilar.

O target Windows do programa de demonstração não significa que a biblioteca `netstandard2.0` seja exclusivamente Windows. Consulte [Desenvolvimento](docs/desenvolvimento.md) para detalhes dos projetos.

## Instalação

No diretório do projeto consumidor, use um dos métodos abaixo.

### .NET CLI

```powershell
dotnet add package Vip.RestClient
```

### Package Manager Console

```powershell
Install-Package Vip.RestClient
```

Também é possível consultar a página do pacote [Vip.RestClient no NuGet](https://www.nuget.org/packages/Vip.RestClient/).

## Início rápido

O exemplo define um DTO e um método assíncrono que busca um recurso. Os exemplos de chamadas HTTP deste README são ilustrativos e não devem ser executados como validação; usam `https://api.example.com` como endereço de exemplo.

```csharp
using System.Threading.Tasks;
using Vip.RestClient;

public sealed class Item
{
    public int Id { get; set; }
    public string Name { get; set; }
}

public static class ApiExample
{
    public static async Task<Item> ReadItemAsync()
    {
        var client = new ClientApi("https://api.example.com/");
        Response<Item> response = await client.GetAsync<Item>("items/42");

        response.EnsureSuccessStatusCode();
        return response.Data;
    }
}
```

`ClientApi` normaliza a barra final da URL-base e resolve endpoints relativos em relação a ela. Um endpoint iniciado por `/` pode substituir o caminho existente da base. Não coloque credenciais ou dados reais em exemplos, código de demonstração ou logs.

### Reutilizar um `HttpClient` da aplicação

Além do construtor legado, há a factory pública:

`public static ClientApi FromHttpClient(string baseUrl, HttpClient httpClient, JsonSerializerSettings jsonSerializerSettings = null)`.

Trecho de uso dentro de um método assíncrono; o `HttpClient` é recebido da aplicação e não é criado nem descartado pelo wrapper:

```csharp
using System.Net.Http;
using System.Threading.Tasks;
using Vip.RestClient;

public static class SharedClientExample
{
    public static async Task<Item> ReadItemAsync(HttpClient suppliedClient)
    {
        var api = ClientApi.FromHttpClient("https://api.example.com/", suppliedClient);
        Response<Item> response = await api.GetAsync<Item>("items/42");
        response.EnsureSuccessStatusCode();
        return response.Data;
    }
}
```

O exemplo não deve ser executado. O consumidor mantém a responsabilidade pelo ciclo de vida e pela configuração desse `HttpClient`. A factory usa o `baseUrl` explícito; não infere a base de `HttpClient.BaseAddress`, nem altera timeout, headers padrão ou descompressão. Ela não configura nem registra integração de DI ou de factory de clientes.

### Enviar uma request preparada com cancelamento

Os overloads públicos são `Task<Response> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)` e `Task<Response<T>> SendAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)`. O token é obrigatório; passe `CancellationToken.None` para não solicitar cancelamento nessa chamada. Dentro de um método assíncrono (com `Item` do exemplo inicial):

```csharp
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Vip.RestClient;

public static class CancelableRequestExample
{
    public static async Task<Item> ReadAsync(HttpClient httpClient, CancellationToken cancellationToken)
    {
        var api = ClientApi.FromHttpClient("https://api.example.com/", httpClient);
        using var request = new HttpRequestMessage(HttpMethod.Get, "items/42");
        Response<Item> response = await api.SendAsync<Item>(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return response.Data;
    }
}
```

O request (incluindo `Content`) é preparado e descartado pelo chamador; o `HttpClient` recebido também continua sob ownership da aplicação. O conteúdo segue diretamente, sem serialização automática pelo `JsonSerializerSettings`. O novo envio bufferiza o corpo antes do parsing; detalhes de URI, cooperação do cancelamento e consumo de memória estão em [Arquitetura](docs/arquitetura.md).

## Operações e formas de resposta

| Operação | Sobrecargas principais | Observações |
| --- | --- | --- |
| GET | `Response` ou `Response<T>` | Extensões genéricas permitem IDs e query. Não há extensão de query não genérica. |
| POST | Sem corpo: `Response`; objeto: `Response` ou `Response<T>` | `HttpContent` direto está disponível somente no overload genérico. Extensões de ID aceitam objeto, em formas genérica e não genérica. |
| PUT | Objeto: `Response` ou `Response<T>` | Extensões de ID existem somente na forma não genérica. |
| PATCH | Com ou sem objeto: `Response` ou `Response<T>` | Extensões de ID com objeto existem em formas genérica e não genérica. |
| DELETE | `Response` ou `Response<T>` | Extensões de ID existem em formas genérica e não genérica. |
| OPTIONS | Somente `Response` | Recebe propriedades de objeto, sequência de tuplas ou sequência de pares como headers. |
| Request preparada | `SendAsync(request, cancellationToken)` → `Task<Response>`; `SendAsync<T>(...)` → `Task<Response<T>>` | Recebem request e conteúdo preparados pelo consumidor; os métodos de conveniência por verbo não têm token. |
| Download em stream | `DownloadAsync(endpoint, destination, cancellationToken)` → `Task<Response>` | Copia GET bem-sucedido para o stream fornecido; resposta HTTP malsucedida retorna metadados sem copiar o corpo. |
| POST de formulário | `Response<T>` | Extensões genéricas para campos multipart ou URL-encoded. |

A tabela é um resumo, não uma lista de todas as assinaturas; o [catálogo de arquitetura](docs/arquitetura.md) informa os overloads e tipos aceitos.

### Query e IDs

Trecho para usar dentro do método assíncrono do cliente; `Item` é o DTO definido no exemplo inicial:

```csharp
var client = new ClientApi("https://api.example.com");
Response<Item> response = await client.GetAsync<Item>(
    "items",
    new { page = 1, category = "demo" });
```

O overload de ID também é genérico, por exemplo `await client.GetAsync<Item>("items", 42)`. A construção da query codifica valores, mas não chaves e sempre acrescenta `?`; não combina automaticamente uma query já presente. As regras completas estão em [Uso](docs/uso.md).

### JSON e formulários

Objetos enviados por POST, PUT e PATCH são serializados como JSON UTF-8. As `JsonSerializerSettings` opcionais do construtor ou de `FromHttpClient` são usadas somente na serialização de saída, não na desserialização das respostas.

O POST genérico também pode receber `HttpContent` pronto por meio de `PostAsync<T>(endpoint, content)`. Não há overload não genérico de POST especificamente tipado para `HttpContent`. Se uma instância for passada ao parâmetro `object`, ela segue o caminho de serialização JSON desse overload, não o envio direto de conteúdo.

As extensões `MultipartFormPostAsync<T>` aceitam `Dictionary<string, string>`, `NameValueCollection` ou `IEnumerable<KeyValuePair<string, string>>`; cada par vira um campo textual. `FormUrlEncodedPostAsync<T>` aceita as mesmas fontes e também um `object` convertido em pares. Não há helper específico de upload de arquivos.

### Headers, timeout e eventos

Antes de enviar requisições, podem ser configurados headers padrão com `SetHeader`, `SetAuthorization` ou `SetAuthorizationBearer`; `RemoveAuthorization` remove o header de autorização. Os auxiliares de autorização não validam nem renovam credenciais.

Ao compartilhar o `HttpClient` externo entre wrappers, essas alterações de headers padrão são compartilhadas. A URL-base não limita o destino: endpoints absolutos podem apontar para outro host, e headers padrão podem acompanhar esses requests. Não use defaults sensíveis supondo que a factory imponha isolamento de origem.

No corpo do método assíncrono que prepara o cliente, antes da primeira chamada:

```csharp
var client = new ClientApi("https://api.example.com");
client.SetHeader("Accept-Language", "pt-BR");
client.ConfigureHttpClient(http => http.Timeout = System.TimeSpan.FromSeconds(30));
Response<Item> response = await client.GetAsync<Item>("items/42");
```

`ConfigureHttpClient` permite configurar o `HttpClient` interno, inclusive timeout. `BeforeSend` recebe a mensagem antes do envio. `ResponseDataReceived` observa somente respostas do fluxo genérico, após a leitura e antes do parsing final; para tipos binários seu campo `Content` é nulo. Evite registrar corpos de resposta ou outros dados sensíveis em eventos.

### Baixar para um stream fornecido pelo consumidor

`DownloadAsync` envia um GET e copia o corpo de sucesso para um `Stream` gravável recebido da aplicação, sem retornar um stream associado à resposta HTTP. Trecho dentro de um método assíncrono que recebe `HttpClient suppliedClient`, `Stream destination` e `CancellationToken cancellationToken`:

```csharp
var api = ClientApi.FromHttpClient("https://api.example.com/", suppliedClient);
Response response = await api.DownloadAsync(
    "files/example", destination, cancellationToken);
response.EnsureSuccessStatusCode();
```

O trecho é ilustrativo e não deve ser executado. A aplicação mantém ownership do `HttpClient` e do destino. Em status malsucedido, o método retorna metadados sem ler o corpo de erro nem escrever no destino; `EnsureSuccessStatusCode()` pode lançar. Falhas durante a cópia podem deixar dados parciais, sem rollback. Para preservar um arquivo anterior, a aplicação pode usar um temporário exclusivo, limpá-lo em falhas e promovê-lo somente após sucesso; a movimentação não tem garantia geral de atomicidade.

O novo fluxo usa leitura de headers e copia o corpo com buffer de 81.920 bytes solicitado ao stream. Isso evita o buffer integral feito pelo `SendAsync` para requests preparados, mas não promete memória constante em qualquer handler/destino, taxa de transferência ou interrupção imediata em todo runtime. Mais detalhes sobre cancelamento e limites estão em [Arquitetura](docs/arquitetura.md) e [Particularidades](docs/particularidades.md).

## Tratamento de erros

Verifique `IsSuccessStatusCode` em `Response` ou `Response<T>`. Para status HTTP malsucedido nos fluxos de leitura textual, o corpo fica em `ErrorResponseData`. Nos retornos `byte[]` e `Stream` dos métodos genéricos existentes, o conteúdo é lido antes da verificação do status e pode estar em `Data` mesmo em erro; `ErrorResponseData` permanece nulo. `DownloadAsync` é diferente: em status malsucedido, não lê o corpo nem escreve no destino, e `ErrorResponseData` é nulo. Também é possível chamar `EnsureSuccessStatusCode()` para lançar `UnsuccessfulStatusCodeException` explicitamente. A forma genérica `EnsureSuccessStatusCode<TError>()` tenta desserializar os dados de erro para o tipo indicado e lança a exceção genérica; se a conversão falhar, a informação tipada fica com o valor padrão.

No fluxo não genérico legado de conveniência, o corpo de sucesso não é lido nem guardado. `SendAsync(request, cancellationToken)` também retorna `Response`, mas bufferiza o corpo antes de construir o envelope e não expõe o corpo de sucesso. No genérico, o valor de `Data` depende do tipo solicitado: `string` recebe texto, `byte[]` recebe bytes e outros tipos são desserializados no sucesso. Falhas de transporte, buffering, handlers de eventos ou parsing também podem lançar antes de um envelope ser retornado.

## Limitações importantes

- Os métodos de conveniência por verbo não recebem `CancellationToken`; os overloads `SendAsync` e `DownloadAsync` aceitam token por chamada. `ClientApi` não implementa `IDisposable` e não fornece política própria de retry ou renovação de token. O `HttpClient` passado à factory continua sob responsabilidade do chamador.
- `Response<T>.Data` de tipo `Stream` nos métodos de leitura existentes não deve ser presumido utilizável após o retorno: a resposta HTTP é descartada antes que o envelope seja devolvido. `DownloadAsync` é distinto: copia para um stream fornecido pela aplicação enquanto a resposta está aberta e não retorna o stream de origem.
- `ResponseDataReceived` não é chamado no caminho não genérico; no fluxo genérico, `Content` é nulo para `byte[]` e `Stream`.
- As configurações JSON fornecidas ao construtor valem apenas para serialização de saída.
- JWT é apenas decodificado e analisado; não há validação de assinatura, expiração, issuer ou audience. Não use essa decodificação como validação de autenticação/autorização.

Consulte [Arquitetura](docs/arquitetura.md) e [Particularidades](docs/particularidades.md) antes de depender de detalhes de ciclo de vida, parsing ou parâmetros.

## Estrutura e documentação

| Documento | Conteúdo |
| --- | --- |
| [Índice da documentação](docs/README.md) | Navegação e visão geral. |
| [Estrutura](docs/estrutura.md) | Mapa hierárquico dos projetos e arquivos. |
| [Arquitetura](docs/arquitetura.md) | Contratos de transporte, respostas, eventos, serialização e API. |
| [Uso](docs/uso.md) | Exemplos e detalhes de chamadas. |
| [Desenvolvimento](docs/desenvolvimento.md) | Projetos, comandos e demonstração. |
| [Particularidades](docs/particularidades.md) | Limites e recomendações separados do comportamento existente. |

## Desenvolvimento

Na raiz do repositório, restaure e compile a solução:

```powershell
dotnet restore ./src/Vip.RestClient.sln
dotnet build ./src/Vip.RestClient.sln --configuration Release --no-restore
dotnet test ./tests/Vip.RestClient.Tests/Vip.RestClient.Tests.csproj --configuration Release
```

O comando `dotnet test` deve apontar explicitamente para o projeto `.Tests`; a suíte usa handler HTTP falso e não depende de rede. Os testes de caracterização registram comportamentos legados sem corrigi-los e também exercitam a API de download.

Validação integrada anterior, em Windows com SDK .NET `10.0.401` (2026-10-01): restore/build sem avisos nem erros; suíte com 82 aprovados, 0 falhos e 0 ignorados (70 anteriores + 12 novos). Essa etapa antecede `DownloadAsync`. Na validação integrada de 2026-10-02, no mesmo SDK e ambiente, restore/build terminaram sem avisos nem erros; a suíte teve 91 aprovados, 0 falhos e 0 ignorados (82 anteriores + 9 novos). Linux e projetos consumidores reais não foram validados; demonstração, downloads HTTP reais, empacotamento e publicação não foram executados. Veja [Desenvolvimento](docs/desenvolvimento.md) para os limites do snapshot de API.

`tests/Vip.RestClient.Demo` é uma demonstração manual, não uma suíte de testes. Ela faz requisições HTTP reais ao httpbin e aguarda `Console.ReadKey()`. **Não execute como validação automática.** Seu target é `net9.0-windows`; isso não altera o target da biblioteca.

## Contribuição

- Preserve compatibilidade pública: não altere assinaturas ou comportamentos observáveis sem solicitação e avaliação explícitas.
- Ao mudar APIs, contratos ou instruções, atualize os documentos correspondentes em `docs/`.
- Mantenha exemplos sem segredos e use `https://api.example.com` como URL ilustrativa; não os execute durante revisão documental.
- Diferencie a demonstração manual de testes automatizados e não publique pacotes como parte de validação.

## Licença

Este projeto é distribuído sob a [licença MIT](LICENSE).
