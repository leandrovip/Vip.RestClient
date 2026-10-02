# Particularidades e limites observados

Registro dos comportamentos que podem ser fáceis de interpretar incorretamente no snapshot atual. As referências levam aos arquivos de implementação; sugestões aparecem em seção separada e não descrevem funcionalidades existentes.

## Respostas, erros e corpo

- Status HTTP malsucedido normalmente produz um `Response`/`Response<T>`; exceção só é lançada quando se chama `EnsureSuccessStatusCode` (ou sua versão genérica). Já transporte, handlers de eventos e alguns parses podem lançar antes do envelope.
- O caminho não genérico guarda corpo somente para erro e faz a leitura de erro com `.Result`. O caminho genérico lê como texto ou binário conforme `T`; bytes/stream são lidos antes da checagem de status e não são copiados para `ErrorResponseData` em erro.
- O evento de resposta não é observador universal: `ResponseDataReceived` só ocorre no caminho genérico. No caso de `byte[]` ou `Stream`, `ResponseEvent.Content` é nulo.
- A resposta HTTP é descartada antes de retornar o envelope. Não dependa de `Response<Stream>.Data` permanecer acessível; o código não garante um stream aberto após o retorno.
- `Duration` usa relógio de parede `DateTime.Now`; inclui envio/processamento e o evento de resposta do fluxo genérico. O evento `BeforeSend` acontece antes do início da medição.

## URL, parâmetros e headers

- O endpoint é resolvido pela regra de URI do .NET. Um endpoint absoluto substitui a base; um endpoint começando por `/` aponta à raiz do host e descarta caminho da base.
- `Helper.BuildUrl` concatena sempre `?` e não mescla query existente. Codifica os valores, mas deixa as chaves como estão. `BuildParams` não achata objetos/coleções e usa `ToString()` comum para tipos que não sejam `decimal`, `float` ou `double`; valor de propriedade nulo pode causar exceção.
- `OptionsAsync(endpoint, object)` converte propriedades em headers. Não deve ser confundido com o overload de GET que converte objeto em query.
- A mutação de headers padrão tem locks parciais; isso não garante segurança total de threads em todas as operações.

## Serialização

`JsonSerializerSettings` do construtor só vale para serialização de objetos de saída. Desserialização da resposta, parsing do erro e parsing JWT usam chamadas Json.NET sem essas settings. Somente POST genérico recebe `HttpContent` como argumento direto; os demais objetos são serializados como JSON.

No genérico, strings de sucesso recebem texto. Só conteúdo iniciado literalmente por `%7B` é submetido a URL decode antes de seguir. Isso pode alterar uma string retornada. O tipo `Jwt` exato tem tratamento específico; `Jwt<T>` não.

## JWT não equivale a validação

`JwtBase` separa segmentos e tenta decodificar Base64url. Isso não verifica assinatura, expiração, emissor ou audiência. `JwtGeneric` não fornece mapeamento explícito de `iss`, `exp`, `iat`, `nbf`, `sub` e `aud` para suas propriedades, então não se deve prometer esse mapeamento padrão. Um segmento Base64 inválido pode virar `null`; ao decodificar header/payload, o acesso subsequente a `arr.Length` pode lançar `NullReferenceException`. Portanto, também não é correto dizer que entradas malformadas são integralmente toleradas.

## Exceções e demonstração

`ApiException` tem construtor privado e uma fábrica interna não usada pelo fluxo de `ClientApi` observado. Para status HTTP, o fluxo de consumo documentado é o envelope e os métodos explícitos `EnsureSuccessStatusCode`.

O programa em `tests/Vip.RestClient.Demo/` é uma demonstração HTTP interativa: faz requests ao httpbin e pausa com `Console.ReadKey`; seus modelos, incluindo `Vip.RestClient.Demo.Models.Response`, não são contratos da biblioteca nem testes automatizados. A suíte xUnit está separada em `tests/Vip.RestClient.Tests/`, usa handler HTTP falso sem rede externa e caracteriza comportamentos legados sem corrigi-los.

## Recomendações (não são comportamento atual)

- Ao integrar com serviços reais, trate streams retornados como potencialmente indisponíveis após a conclusão da chamada; prefira um fluxo de consumo cujo ciclo de vida esteja sob controle da aplicação.
- Revise query strings pré-existentes, nomes de chaves e valores nulos antes de usar `Helper` em dados não triviais.
- Valide JWT com uma implementação apropriada para o contrato de segurança da aplicação; não use a decodificação deste projeto como autenticação/autorização.
- Em mudanças futuras, avalie cenários de cancelamento e ciclo de vida/disposição do cliente; acrescente testes à suíte automatizada para os comportamentos pertinentes, sem tratá-los como correções do comportamento legado por si só.

Esses itens são sugestões de cautela, não declarações de que a biblioteca já forneça validação, cancelamento ou gerenciamento de cliente.
