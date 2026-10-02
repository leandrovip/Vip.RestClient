# Arquitetura e contratos observados

Este texto descreve o snapshot atual com base principalmente em [`ClientApi.cs`](../src/Vip.RestClient/ClientApi.cs), [`Response.cs`](../src/Vip.RestClient/Models/Response.cs), [`RestExtensions.cs`](../src/Vip.RestClient/Extensions/RestExtensions.cs), [`Helper.cs`](../src/Vip.RestClient/Utils/Helper.cs) e nos arquivos JWT. Não transforma limitações em garantias nem recomendações em comportamento implementado.

## Inicialização e URI

`ClientApi` recebe `baseUrl`, um `HttpClientHandler` opcional e `JsonSerializerSettings` opcionais. Acrescenta `/` ao fim de `baseUrl` quando ausente e cria `BaseUri`. Cria seu próprio `HttpClient` a partir do handler fornecido ou de um novo; não recebe um `HttpClient` pronto. Sobrescreve `AutomaticDecompression` para `GZip | Deflate` e adiciona `Accept: application/json`.

Os métodos resolvem endpoints com `new Uri(BaseUri, endpoint)`. URI absoluta pode substituir a base; um endpoint iniciado por `/` pode trocar o caminho-base pelo caminho na raiz do host. A normalização da barra final não impede esse comportamento normal de resolução de URI.

## Fluxo de requisição e ciclo de vida

As APIs assíncronas abrangem GET, POST, PUT, PATCH, DELETE e OPTIONS. Em ambos os fluxos internos, `BeforeSend` é emitido com o `HttpRequestMessage` antes do registro do instante inicial; se um handler lançar, a chamada também lança e o envio não ocorre. O envio usa `HttpCompletionOption.ResponseHeadersRead`.

O cliente não implementa `IDisposable` e seus métodos não aceitam `CancellationToken`. Não há política própria de retry, renovação ou validação de token. `ConfigureHttpClient(Action<HttpClient>)` permite configurar o `HttpClient` interno, inclusive seu timeout; os valores padrão de timeout não são substituídos pela biblioteca. Os métodos normalmente colocam a mensagem de requisição e a resposta em `using`, mas o caminho de OPTIONS cria a mensagem sem `using`.

## Conteúdo enviado

Objetos enviados em POST, PUT e PATCH são serializados por `JsonConvert.SerializeObject(value, _jsonSettings)` e enviados como UTF-8 `application/json`. O overload genérico `PostAsync<T>(string, HttpContent)` e as extensões de formulário enviam `HttpContent` diretamente; não passam esse conteúdo pela serialização JSON. Não há overload não genérico de POST especificamente tipado para `HttpContent`: se um `HttpContent` for passado ao overload `PostAsync(string, object)`, ele será tratado pelo caminho de serialização do parâmetro `object`.

As `JsonSerializerSettings` fornecidas são usadas somente na serialização de saída. `GetResponseAsync<T>` desserializa corpos JSON com `JsonConvert.DeserializeObject<T>(content)` sem settings. Parsing de corpo de erro e de conteúdo JWT também não reaproveita `_jsonSettings`.

## Leitura de resposta e envelopes

O fluxo não genérico retorna `Response`. Em sucesso, não lê nem guarda o corpo. Em status não bem-sucedido, `Response.Build` lê o corpo de forma síncrona com `.Result` para preencher `ErrorResponseData`. O envelope inclui status, motivo, flags, headers, referência a `RequestMessage`, erro e duração.

O fluxo genérico retorna `Response<T>`:

- Para `T == string`, lê o corpo como texto; em sucesso atribui esse texto a `Data`.
- Para `T == byte[]` ou `T == Stream`, lê o conteúdo binário antes de verificar o status. Assim, `Data` pode conter bytes ou stream também em resposta de erro e `ErrorResponseData` fica sem esse corpo binário.
- Para outros tipos, lê texto. Em erro, mantém esse texto em `ErrorResponseData` e `Data` continua em `default(T)`. Em sucesso, desserializa JSON sem settings, salvo o tratamento específico de `Jwt` descrito adiante.

No caminho genérico, somente um conteúdo cujo prefixo literal seja `%7B` passa por `WebUtility.UrlDecode`; isso pode alterar o texto que é devolvido como `string`. O tipo exato `Jwt` é tratado de forma especial: aspas são removidas do conteúdo, se encontradas, antes de `Jwt.Parse`. `Jwt<T>` não recebe tratamento especial em `ClientApi`.

Falhas HTTP não se tornam automaticamente exceções: os métodos retornam o envelope, e `EnsureSuccessStatusCode()` ou `EnsureSuccessStatusCode<TError>()` lança `UnsuccessfulStatusCodeException` somente quando invocado pelo consumidor. O método genérico tenta interpretar o erro, mas usa `default(TError)` se o parsing falhar. Falhas de transporte, eventos e desserialização podem lançar antes de um envelope ser retornado.

`HttpResponseMessage` é descartado antes do retorno. O `Stream` de `Data` não deve ser considerado streaming reutilizável ou garantidamente disponível após o retorno, pois está associado ao conteúdo/resposta descartados. `RequestMessage` e os headers expostos são referências a objetos associados à resposta já encerrada; não representam uma resposta ainda aberta.

## Eventos e duração

`BeforeSend` existe nos fluxos genérico e não genérico. `ResponseDataReceived` é invocado somente no fluxo genérico, depois de ler o conteúdo e antes de decodificar/deserializar o texto e construir o envelope. Para `byte[]` e `Stream`, a propriedade `Content` do evento é `null`. Os handlers de evento podem lançar e interromper a operação.

`ResponseEvent.Received` é preenchido com `DateTime.UtcNow`. Já `Response.Duration` é calculado como `DateTime.Now - start`; inclui o tempo de envio e processamento do fluxo, inclusive o evento e parsing no genérico, mas exclui o handler de `BeforeSend`, pois o relógio começa depois dele. Não é uma medição monotônica.

## Parâmetros, headers e formulários

As extensões em `RestExtensions` acrescentam IDs `int` e `Guid` ao caminho em vários verbos. Para GET, permitem parâmetros por pares ou objeto. `Helper.BuildParams` enumera propriedades públicas por reflexão: valores `decimal`, `float` e `double` usam cultura invariável; outros valores usam `ToString()` comum. Valores nulos de propriedades podem lançar, e objetos/coleções complexas não são achatados.

`Helper.BuildUrl` codifica valores, mas não as chaves, e sempre concatena `?`, mesmo que o endpoint já contenha uma consulta. O overload `OptionsAsync` que recebe objeto converte propriedades em headers; não as trata como query string.

As extensões de POST de formulário são genéricas (`Response<T>`): `MultipartFormPostAsync<T>` aceita `Dictionary<string, string>`, `NameValueCollection` ou `IEnumerable<KeyValuePair<string, string>>`; cada par vira um campo `StringContent` em `MultipartFormDataContent`. Não há helper específico para arquivos. `FormUrlEncodedPostAsync<T>` aceita essas mesmas fontes e também `object`, convertido em pares por `Helper.BuildParams`, e usa `FormUrlEncodedContent`.

## Catálogo público resumido

Tabela das principais sobrecargas de `ClientApi` e das extensões públicas de `RestExtensions`. `Response` indica retorno não genérico; `Response<T>` indica retorno tipado.

| Verbo | `ClientApi` | Extensões (`RestExtensions`) |
| --- | --- | --- |
| GET | `GetAsync(endpoint)` → `Response`; `GetAsync<T>(endpoint)` → `Response<T>` | Somente genéricas: IDs `int`/`Guid` no caminho; query por `KeyValuePair<string,string>[]` ou `object`. |
| POST | Sem corpo → `Response`; `object` → `Response` ou `Response<T>`; `HttpContent` → somente `Response<T>`. | `object` com ID `int`/`Guid` → versões `Response` e `Response<T>`. |
| PUT | `object` → `Response` ou `Response<T>`. | Somente não genérica: `object` com ID `int`/`Guid` → `Response`. |
| PATCH | Com ou sem `object` → versões `Response` e `Response<T>`. | Com `object` e ID `int`/`Guid` → versões `Response` e `Response<T>`. |
| DELETE | Sem corpo → `Response` ou `Response<T>`. | ID `int`/`Guid` → versões `Response` e `Response<T>`. |
| OPTIONS | Somente não genérica: `object` (propriedades convertidas em headers), `IEnumerable<(string, string)>` ou `IEnumerable<KeyValuePair<string, string>>`. | Não há extensões OPTIONS. |
| POST de formulário | Não há overload específico em `ClientApi` para construir formulários. | `MultipartFormPostAsync<T>` e `FormUrlEncodedPostAsync<T>` retornam `Response<T>`; as fontes aceitas estão descritas acima. |

O overload de `PostAsync(string, object)` serializa seu argumento como JSON. A ausência de um overload não genérico específico para `HttpContent` não significa que a linguagem impeça passar uma instância ao parâmetro `object`; esse caso não é o envio direto de conteúdo que o overload genérico `PostAsync<T>(string, HttpContent)` oferece. Para OPTIONS, o argumento objeto também representa propriedades convertidas em headers, não query string.

Headers padrão podem ser alterados com `SetHeader`, `SetAuthorization`, `SetAuthorizationBearer` e `RemoveAuthorization`. Há locks em algumas mutações, mas isso não constitui uma garantia de segurança completa entre threads para todas as operações do cliente.

## JWT

`JwtBase.ParseText` exige token não vazio e três segmentos separados por ponto, decodifica Base64url em header/payload e converte a assinatura em bytes. `Jwt.Parse` usa `ParseText` e desserializa o payload diretamente em `JwtGeneric`, sem criar um `Jwt<JwtGeneric>` intermediário; `Jwt<T>.Parse` permite payload tipado. Isso é apenas decodificação e parsing. Não há validação de assinatura, expiração, issuer ou audience.

`JwtGeneric` não declara atributos `JsonProperty` para nomes padronizados como `iss`, `exp`, `iat`, `nbf`, `sub` e `aud`, e seus nomes de propriedade diferem de vários nomes de claim. Não se deve presumir mapeamento correto desses claims. Além disso, base64 inválido pode resultar em `null` no decoder; para header/payload, `GetBase64` acessa `arr.Length` sem validar `null` e pode lançar `NullReferenceException`.

## Tipos de exceção

`UnsuccessfulStatusCodeException` e sua versão genérica carregam o envelope `Response`; a variante tipada também expõe `ErrorInformation`. `ApiException` possui construtor privado e fábrica interna, mas não participa do fluxo observado em `ClientApi`. Veja [Particularidades](particularidades.md) para recomendações e limites.

## Projeto de testes

`tests/Vip.RestClient.Tests` é a suíte automatizada xUnit, separada do executável manual `tests/Vip.RestClient.Demo`. Os testes usam um handler HTTP falso para não depender de requests externos e caracterizam comportamentos legados; isso não altera nem corrige o contrato da biblioteca.
