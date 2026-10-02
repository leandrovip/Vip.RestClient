# Uso

Os exemplos abaixo são trechos de consumo ilustrativos e seguros: não contêm credenciais e não devem ser executados como parte da validação. `https://api.example.com` é um endereço reservado para exemplo. A suíte automatizada está separada em `tests/Vip.RestClient.Tests`; a demonstração manual fica em `tests/Vip.RestClient.Demo`. A API real deve fornecer a base e os contratos de dados apropriados.

## Criar cliente e ler resposta

```csharp
using System.Threading.Tasks;
using Vip.RestClient;

public sealed class Item
{
    public int Id { get; set; }
    public string Name { get; set; }
}

public static class Example
{
    public static async Task ReadAsync()
    {
        var client = new ClientApi("https://api.example.com/");
        Response<Item> response = await client.GetAsync<Item>("items/42");

        if (response.IsSuccessStatusCode)
        {
            Item item = response.Data;
            // Consumir item conforme a aplicação.
        }
        else
        {
            string errorBody = response.ErrorResponseData;
            // Decidir como a aplicação tratará o status e o corpo.
        }
    }
}
```

`ClientApi` acrescenta a barra final ausente à base. Endpoints relativos são resolvidos contra essa base; iniciar o endpoint com `/` pode remover o caminho existente da base. Respostas HTTP não bem-sucedidas são envelopes, não exceções automáticas.

## Usar um `HttpClient` gerenciado pela aplicação

O construtor original continua recebendo um `HttpClientHandler` opcional e cria seu próprio `HttpClient`. Para adaptar uma instância `HttpClient` existente, use `ClientApi.FromHttpClient(string baseUrl, HttpClient httpClient, JsonSerializerSettings jsonSerializerSettings = null)`. O exemplo recebe o cliente de fora e não o cria nem o descarta:

```csharp
using System.Net.Http;
using System.Threading.Tasks;
using Vip.RestClient;

public static class ExternalClientExample
{
    public static async Task<Item> ReadAsync(HttpClient suppliedClient)
    {
        var client = ClientApi.FromHttpClient("https://api.example.com/", suppliedClient);
        Response<Item> response = await client.GetAsync<Item>("items/42");
        response.EnsureSuccessStatusCode();
        return response.Data;
    }
}
```

O chamador permanece responsável pelo ciclo de vida e pela configuração da instância. A factory não usa `HttpClient.BaseAddress`, não altera timeout, headers padrão nem descompressão, e não registra integração com DI ou com uma factory de clientes. Se a aplicação usa uma estratégia própria de factory, ela pode passar a instância que gerencia.

## Enviar uma request preparada com cancelamento

`ClientApi` também oferece `SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)` e `SendAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)`, que retornam `Task<Response>` e `Task<Response<T>>`. São os únicos métodos com token por chamada; as sobrecargas de conveniência por verbo e suas extensões continuam sem `CancellationToken`.

O consumidor prepara a mensagem e seu `HttpContent`, aguarda o envio e mantém ownership para descartá-los (por exemplo, com `using`). O conteúdo segue diretamente, sem serialização JSON automática nem uso das `JsonSerializerSettings` do cliente. URI relativa da mensagem é resolvida contra `BaseUri` explícita; URI absoluta continua podendo substituir a base. O token é obrigatório; use `CancellationToken.None` quando não houver cancelamento a solicitar. Consulte [Arquitetura](arquitetura.md) para ordem de validação/eventos, buffering e limites da cooperação do token.

## JSON de saída e status

Objetos passados aos overloads de POST/PUT/PATCH são serializados como JSON UTF-8. `JsonSerializerSettings` opcionais, recebidas pelo construtor legado ou por `FromHttpClient`, afetam essa serialização de saída, não o parsing de resposta:

```csharp
using System.Threading.Tasks;
using Newtonsoft.Json;
using Vip.RestClient;

public static class Example
{
    public static async Task CreateAsync()
    {
        var settings = new JsonSerializerSettings();
        var client = new ClientApi("https://api.example.com", jsonSerializerSettings: settings);
        Response response = await client.PostAsync("items", new { Name = "Exemplo" });

        if (!response.IsSuccessStatusCode)
        {
            // Alternativa: response.EnsureSuccessStatusCode(); lança para status de erro.
            string body = response.ErrorResponseData;
        }
    }
}
```

Não coloque segredos em exemplos ou logs. `EnsureSuccessStatusCode<TError>()` tenta desserializar `ErrorResponseData` sem as settings do cliente e ainda lança a exceção tipada para status malsucedido.

## Parâmetros, IDs e formulários

As extensões de `RestExtensions` incluem sobrecargas genéricas para IDs inteiros ou GUID, como `client.GetAsync<Item>("items", 42)`, além de GET genérico com pares chave/valor ou propriedades de um objeto anônimo. `Helper` codifica os valores da query, mas não as chaves; sempre concatena `?`, sem combinar uma query já existente. Objetos complexos não são convertidos em estruturas aninhadas e propriedade nula pode lançar durante `ToString()`.

Exemplo de GET com propriedades convertidas em query; `Item` é o tipo ilustrado acima:

```csharp
using System.Threading.Tasks;
using Vip.RestClient;

public static class QueryExample
{
    public static async Task SearchAsync()
    {
        var client = new ClientApi("https://api.example.com");
        Response<Item> response = await client.GetAsync<Item>("items", new { category = "demo", limit = 10 });
    }
}
```

As extensões de formulário retornam `Response<T>` e aceitam estas fontes:

- `MultipartFormPostAsync<T>`: `Dictionary<string, string>`, `NameValueCollection` ou `IEnumerable<KeyValuePair<string, string>>`. Cada par é enviado como um campo textual usando `StringContent`; não há helper específico para upload de arquivos.
- `FormUrlEncodedPostAsync<T>`: as mesmas fontes e também `object`, cujas propriedades são convertidas em pares por `Helper.BuildParams`.

Exemplo de formulário URL-encoded:

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using Vip.RestClient;

public static class FormExample
{
    public static async Task SendFormAsync()
    {
        var client = new ClientApi("https://api.example.com");
        Response<Item> response = await client.FormUrlEncodedPostAsync<Item>(
            "items",
            new Dictionary<string, string> { { "name", "Exemplo" } });
    }
}
```

O overload genérico `PostAsync<T>(endpoint, HttpContent)` aceita conteúdo HTTP pronto. Não há overload não genérico especificamente tipado para `HttpContent`; se passado ao `PostAsync(endpoint, object)`, o valor segue a serialização JSON do parâmetro `object`, em vez de ser enviado diretamente como conteúdo.

Para o catálogo das sobrecargas principais de `ClientApi` e extensões, consulte [Arquitetura](arquitetura.md).

## Headers e eventos

O cliente oferece `SetHeader`, `SetAuthorization`, `SetAuthorizationBearer` e `RemoveAuthorization`. Esses métodos alteram headers padrão do cliente. `BeforeSend` permite observar/modificar a mensagem antes do envio; `ResponseDataReceived` só é chamado no caminho genérico e antes do parsing final. Evite registrar tokens, dados pessoais ou corpos sensíveis em eventos.

Quando o `HttpClient` fornecido é compartilhado, os métodos de headers padrão e `ConfigureHttpClient` alteram a instância comum; alterações podem afetar outros wrappers que a usam. A `baseUrl` não limita o host: endpoints absolutos podem substituí-la, sem uma política de origem adicional.

## Limitações relevantes

`ClientApi` não implementa `IDisposable`. Somente os novos overloads `SendAsync` recebem `CancellationToken`; os métodos de conveniência por verbo e suas extensões continuam sem token. Falhas de transporte e de parsing podem lançar. `Response<T>.Data` de tipo `Stream` não deve ser presumido utilizável após o retorno, pois a resposta HTTP é descartada. JWT é decodificado, mas não validado. Leia [Arquitetura](arquitetura.md) e [Particularidades](particularidades.md) antes de depender desses comportamentos.
