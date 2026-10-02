# Particularidades e limites observados

Registro dos comportamentos que podem ser fáceis de interpretar incorretamente no snapshot atual. As referências levam aos arquivos de implementação; sugestões aparecem em seção separada e não descrevem funcionalidades existentes.

## Respostas, erros e corpo

- Status HTTP malsucedido normalmente produz um `Response`/`Response<T>`; exceção só é lançada quando se chama `EnsureSuccessStatusCode` (ou sua versão genérica). Já transporte, handlers de eventos e alguns parses podem lançar antes do envelope.
- O caminho não genérico legado de conveniência guarda corpo somente para erro e faz a leitura de erro com `.Result`. O caminho genérico lê como texto ou binário conforme `T`; bytes/stream são lidos antes da checagem de status e não são copiados para `ErrorResponseData` em erro.
- O evento de resposta não é observador universal: `ResponseDataReceived` só ocorre no caminho genérico. No caso de `byte[]` ou `Stream`, `ResponseEvent.Content` é nulo; `DownloadAsync` também não emite esse evento.
- A resposta HTTP é descartada antes de retornar os envelopes dos métodos de leitura existentes. Não dependa de `Response<Stream>.Data` permanecer acessível; `DownloadAsync` é distinto: copia para o destino recebido enquanto a resposta está aberta e não retorna o stream de origem.
- `Duration` usa relógio de parede `DateTime.Now`; inclui envio/processamento e o evento de resposta do fluxo genérico, além da cópia em `DownloadAsync`. O evento `BeforeSend` acontece antes do início da medição.

## `DownloadAsync`

- `DownloadAsync(string endpoint, Stream destination, CancellationToken cancellationToken)` faz GET para um destino fornecido pelo consumidor. Endpoints absolutos ainda podem apontar a outro host, e um endpoint iniciado por `/` segue as regras usuais de resolução contra a base.
- `endpoint == null` gera `ArgumentNullException` (`ParamName == "endpoint"`); destino nulo gera `ArgumentNullException` (`ParamName == "destination"`) e um destino sem escrita gera `ArgumentException` (`ParamName == "destination"`). São observadas como falhas da tarefa assíncrona. A validação precede URI e token pré-cancelado.
- Envia com `ResponseHeadersRead` e copia em blocos usando buffer de cópia solicitado de 81.920 bytes. Isso não assegura uso absoluto de memória constante, pois handlers/conteúdos podem bufferizar e destinos em memória crescem com os dados. Não há garantias numéricas de memória ou throughput.
- Para status HTTP malsucedido, não lê o corpo e não escreve no destino; `ErrorResponseData` é `null`. O consumidor pode chamar `EnsureSuccessStatusCode()` explicitamente.
- O stream de destino continua sob ownership da aplicação. A cópia começa na posição atual e não descarrega, reposiciona, trunca ou descarta o destino. Erros de transporte, leitura/escrita ou cancelamento podem deixar conteúdo parcial; não há rollback e a biblioteca não fecha o `ClientApi`/`HttpClient` por causa da falha.
- O token participa do envio e da cópia, mas `ReadAsStreamAsync()` não recebe token neste target. O runtime, handler e streams podem cooperar ou ignorar o cancelamento; timeout do `HttpClient` cobre a espera pelos headers, não é prazo total da cópia. Um token com prazo definido pela aplicação também não força implementações não cooperativas a parar.

## URL, parâmetros e headers

- O endpoint é resolvido pela regra de URI do .NET. Um endpoint absoluto substitui a base; um endpoint começando por `/` aponta à raiz do host e descarta caminho da base.
- `Helper.BuildUrl` concatena sempre `?` e não mescla query existente. Codifica os valores, mas deixa as chaves como estão. `BuildParams` não achata objetos/coleções e usa `ToString()` comum para tipos que não sejam `decimal`, `float` ou `double`; valor de propriedade nulo pode causar exceção.
- `OptionsAsync(endpoint, object)` converte propriedades em headers. Não deve ser confundido com o overload de GET que converte objeto em query.
- A mutação de headers padrão tem locks parciais; isso não garante segurança total de threads em todas as operações.

## `HttpClient` externo

- `FromHttpClient` recebe a instância exata, usa a `baseUrl` explícita e não modifica `BaseAddress`, `Timeout`, headers padrão ou descompressão do handler. Não acrescenta `Accept` nem infere a base do `HttpClient`.
- `ClientApi` não implementa `IDisposable`; o wrapper não descarta explicitamente o cliente fornecido. O chamador mantém o ownership e o ciclo de vida.
- Os métodos `SetHeader`/`SetAuthorization*` e `ConfigureHttpClient` operam sobre o cliente comum. Wrappers que compartilham a instância também compartilham os efeitos dessas alterações; os locks existentes não dão garantia geral de segurança entre threads.
- A base explícita não é uma allowlist de hosts: endpoints absolutos podem direcionar requests a outra origem e não há guard de origem; headers padrão do cliente podem acompanhar esses requests. Não dependa da factory para isolar headers sensíveis.
- O `CancellationToken` existe nos overloads `SendAsync` e em `DownloadAsync`. Em `SendAsync`, a cooperação no envio e buffering depende do runtime, handler e `HttpContent`; parsing e eventos síncronos posteriores não são interrompidos. O cancelamento por chamada não descarta o cliente compartilhado nem chama `CancelPendingRequests`.
- `SendAsync` usa `ResponseContentRead`, que bufferiza o corpo em memória mesmo antes de tratar o sucesso não genérico. `DownloadAsync` usa `ResponseHeadersRead` e copia para stream externo; são fluxos distintos, e o primeiro não passa a transmitir o corpo para o segundo automaticamente.

## Serialização

`JsonSerializerSettings` opcionais do construtor ou de `FromHttpClient` só valem para serialização de objetos de saída. Desserialização da resposta, parsing do erro e parsing JWT usam chamadas Json.NET sem essas settings. Somente POST genérico recebe `HttpContent` como argumento direto; os demais objetos são serializados como JSON.

No genérico, strings de sucesso recebem texto. Só conteúdo iniciado literalmente por `%7B` é submetido a URL decode antes de seguir. Isso pode alterar uma string retornada. O tipo `Jwt` exato tem tratamento específico; `Jwt<T>` não.

## JWT não equivale a validação

`JwtBase` separa segmentos e tenta decodificar Base64url. Isso não verifica assinatura, expiração, emissor ou audiência. `JwtGeneric` não fornece mapeamento explícito de `iss`, `exp`, `iat`, `nbf`, `sub` e `aud` para suas propriedades, então não se deve prometer esse mapeamento padrão. Um segmento Base64 inválido pode virar `null`; ao decodificar header/payload, o acesso subsequente a `arr.Length` pode lançar `NullReferenceException`. Portanto, também não é correto dizer que entradas malformadas são integralmente toleradas.

## Exceções e demonstração

`ApiException` tem construtor privado e uma fábrica interna não usada pelo fluxo de `ClientApi` observado. Para status HTTP, o fluxo de consumo documentado é o envelope e os métodos explícitos `EnsureSuccessStatusCode`.

O programa em `tests/Vip.RestClient.Demo/` é uma demonstração HTTP interativa: faz requests ao httpbin e pausa com `Console.ReadKey`; seus modelos, incluindo `Vip.RestClient.Demo.Models.Response`, não são contratos da biblioteca nem testes automatizados. A suíte xUnit está separada em `tests/Vip.RestClient.Tests/`, usa handler HTTP falso sem rede externa e caracteriza comportamentos legados sem corrigi-los.

## Recomendações (não são comportamento atual)

- Ao integrar com serviços reais, trate `Response<Stream>.Data` dos métodos existentes como potencialmente indisponível após a chamada; para copiar uma resposta a um destino externo, considere `DownloadAsync` e mantenha esse destino sob controle da aplicação.
- Para proteger um arquivo anterior contra falha ou cancelamento, considere baixar em caminho temporário exclusivo, limpá-lo em caso de falha e só promovê-lo após sucesso. Evite `FileMode.Create` no caminho final antes da chamada, pois trunca o arquivo; a biblioteca não implementa rollback nem garante atomicidade de movimentação.
- Revise query strings pré-existentes, nomes de chaves e valores nulos antes de usar `Helper` em dados não triviais.
- Valide JWT com uma implementação apropriada para o contrato de segurança da aplicação; não use a decodificação deste projeto como autenticação/autorização.
- Ao ampliar o cancelamento cooperativo de `SendAsync`/`DownloadAsync` ou o gerenciamento de ciclo de vida, acrescente testes para esses limites sem tratar a caracterização como correção automática do comportamento legado.

Esses itens são sugestões de cautela, não declarações de que a biblioteca valide tokens ou gerencie o ciclo de vida do cliente externo.
