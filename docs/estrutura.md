# Estrutura do repositório

Mapa hierárquico do snapshot documentado. Arquivos gerados e locais de IDE (`bin/`, `obj/`, `.vs/`) são deliberadamente omitidos: não são fontes versionadas de verdade para esta documentação.

```text
Vip.RestClient/
├── AGENTS.md                         # orientação operacional geral
├── .gitignore                         # regras atuais; contém marcadores de conflito
├── LICENSE                           # licença MIT
├── README.md                         # apresentação, instalação e início rápido
├── docs/
│   ├── README.md                     # entrada desta documentação
│   ├── estrutura.md                  # este mapa hierárquico
│   ├── arquitetura.md                # fluxos e contratos observados
│   ├── uso.md                        # exemplos de consumo
│   ├── desenvolvimento.md            # projetos e comandos
│   └── particularidades.md           # limites e recomendações
├── .github/
│   └── workflows/
│       └── nuget.yml                 # workflow manual de empacotamento/publicação
├── src/
│   ├── Vip.RestClient.sln            # inclui biblioteca, demonstração e suíte automatizada
│   ├── nuget.config                  # repositoryPath legado
│   └── Vip.RestClient/
│       ├── AGENTS.md                 # orientação local da biblioteca
│       ├── Vip.RestClient.csproj     # biblioteca netstandard2.0
│       ├── ClientApi.cs              # orquestra transporte, chamadas e envelopes
│       ├── Events/
│       │   └── ResponseEvent.cs      # dados da observação de resposta
│       ├── Exceptions/
│       │   ├── ApiException.cs       # tipo/fábrica interna não usados pelo fluxo observado
│       │   └── UnsuccessfulStatusCodeException.cs
│       ├── Extensions/
│       │   └── RestExtensions.cs     # IDs, parâmetros e posts de formulário
│       ├── Models/
│       │   ├── JwtBase.cs            # separação/decodificação das partes do token
│       │   ├── Jwt.cs                # JWT tipado e não tipado
│       │   ├── JwtGeneric.cs         # modelo genérico de claims
│       │   └── Response.cs           # envelopes Response e Response<T>
│       └── Utils/
│           └── Helper.cs             # reflexão e construção de parâmetros/URL
└── tests/
    ├── Vip.RestClient.Demo/
    │   ├── AGENTS.md                  # orientação local da demonstração
    │   ├── Vip.RestClient.Demo.csproj # executável net9.0-windows
    │   ├── Program.cs                 # chama ClientTests.Run
    │   ├── ClientTests.cs              # demonstração HTTP interativa
    │   └── Models/                    # modelos de apoio da demonstração
    │       ├── Args.cs
    │       ├── Data.cs
    │       ├── Headers.cs
    │       ├── Login.cs
    │       ├── Response.cs
    │       └── User.cs
    └── Vip.RestClient.Tests/           # suíte xUnit automatizada, target net9.0
        ├── AGENTS.md                  # orientação dos testes de caracterização
        ├── Vip.RestClient.Tests.csproj # configuração do projeto xUnit
        ├── ApiSurfaceCharacterizationTests.cs # confere snapshot versionado da API
        ├── ApiSurface.baseline.txt    # baseline versionada da API
        ├── ClientApiTests.cs          # caracterização de requests/responses
        ├── ExternalHttpClientTests.cs # factory e ownership do HttpClient externo
        ├── SendAsyncCancellationTests.cs # cancelamento das requests preparadas
        ├── RestExtensionsTests.cs     # IDs, parâmetros e formulários
        ├── JwtTests.cs                # parsing e comportamento JWT
        └── Utils/
            ├── ApiSurface.cs
            ├── AsyncFakeHandler.cs
            ├── ClientHarness.cs
            ├── FakeHandler.cs
            ├── ResponseHelper.cs
            ├── TrackingStream.cs
            ├── PrefixStringConverter.cs
            ├── JwtHelper.cs
            ├── Dtos/
            │   ├── Payload.cs
            │   ├── ErrorDto.cs
            │   └── JwtClaims.cs
            ├── Models/
            │   ├── ThrowingPayload.cs
            │   ├── NullableProperty.cs
            │   ├── CultureValues.cs
            │   ├── LegacyValue.cs
            │   └── LegacyConstructorConsumer.cs
            └── Contents/
                ├── BlockingContent.cs
                ├── CountingContent.cs
                ├── FaultingContent.cs
                └── TrackingContent.cs
```

## Responsabilidades dos arquivos

- [`ClientApi.cs`](../src/Vip.RestClient/ClientApi.cs) forma URIs, prepara requests, envia pelo `HttpClient` privado e constrói respostas. É a fonte principal para o contrato HTTP.
- [`RestExtensions.cs`](../src/Vip.RestClient/Extensions/RestExtensions.cs) acrescenta sobrecargas de IDs `int`/`Guid`, parâmetros de consulta e POST de formulários. [`Helper.cs`](../src/Vip.RestClient/Utils/Helper.cs) faz reflexão e conversão de parâmetros.
- [`Response.cs`](../src/Vip.RestClient/Models/Response.cs) contém os envelopes da biblioteca, parsing opcional de erro e métodos explícitos para exigir status de sucesso.
- [`JwtBase.cs`](../src/Vip.RestClient/Models/JwtBase.cs), [`Jwt.cs`](../src/Vip.RestClient/Models/Jwt.cs) e [`JwtGeneric.cs`](../src/Vip.RestClient/Models/JwtGeneric.cs) separam e decodificam componentes JWT; não validam criptograficamente o token.
- [`ResponseEvent.cs`](../src/Vip.RestClient/Events/ResponseEvent.cs) define os dados emitidos por `ResponseDataReceived`. [`UnsuccessfulStatusCodeException.cs`](../src/Vip.RestClient/Exceptions/UnsuccessfulStatusCodeException.cs) define as exceções usadas pelos métodos `EnsureSuccessStatusCode`.
- [`ApiException.cs`](../src/Vip.RestClient/Exceptions/ApiException.cs) é uma classe pública com construtor privado e fábrica interna, não acionada pelo fluxo observado em `ClientApi`.
- Os tipos no diretório de modelos da demonstração usam namespace `Vip.RestClient.Demo.Models`; não são os envelopes da biblioteca. A demonstração não deve ser confundida com `Vip.RestClient.Tests`, a suíte automatizada.
- Em `Vip.RestClient.Tests`, `ClientApiTests.cs`, `ExternalHttpClientTests.cs`, `SendAsyncCancellationTests.cs`, `RestExtensionsTests.cs` e `JwtTests.cs` agrupam a caracterização por área; `ApiSurfaceCharacterizationTests.cs` confere `ApiSurface.baseline.txt`. `Utils/` reúne handlers/harnesses síncronos e assíncronos, DTOs/modelos e conteúdos/streams de apoio, incluindo conteúdo bloqueável para casos de cancelamento e o consumidor derivado do construtor legado.

Todos os tipos da biblioteca declaram o namespace `Vip.RestClient`, mesmo estando em subpastas. A base mantém tanto namespaces em bloco quanto file-scoped e usa `#region`; isso descreve o código existente e não estabelece uma regra de formatação global.
