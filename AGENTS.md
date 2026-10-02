# Orientação para agentes

Este arquivo contém regras operacionais para mudanças no repositório. A documentação funcional está em [`docs/README.md`](docs/README.md); consulte também [estrutura](docs/estrutura.md), [arquitetura](docs/arquitetura.md), [uso](docs/uso.md), [desenvolvimento](docs/desenvolvimento.md) e [particularidades](docs/particularidades.md).

## Mapa rápido

- `src/Vip.RestClient/`: biblioteca `Vip.RestClient`, projeto `netstandard2.0`.
- `src/Vip.RestClient.sln`: solução com biblioteca, demonstração e suíte automatizada.
- `tests/Vip.RestClient.Demo/`: executável manual `net9.0-windows`; não é um projeto de testes automatizados.
- `tests/Vip.RestClient.Tests/`: suíte automatizada xUnit, target `net9.0`.
- `docs/`: documentação em português do Brasil; o mapa hierárquico fica em `docs/estrutura.md`.
- `LICENSE`: licença MIT.
- `.github/workflows/nuget.yml`: fluxo manual de build/empacotamento/publicação NuGet; não o execute para validar mudanças.

Orientações específicas por área estão em [`src/Vip.RestClient/AGENTS.md`](src/Vip.RestClient/AGENTS.md) e [`tests/Vip.RestClient.Demo/AGENTS.md`](tests/Vip.RestClient.Demo/AGENTS.md).

## Regras para mudanças

- Preserve compatibilidade pública: não remova nem altere assinaturas, tipos, nomes, semântica de retorno ou comportamento observável sem pedido e avaliação explícitos.
- Leia a implementação e os projetos relevantes antes de descrever ou alterar contratos. Código e arquivos de projeto versionados são a fonte de verdade; não use `bin/`, `obj/` ou `.vs/` como evidência.
- Não confunda a demonstração em `tests/Vip.RestClient.Demo/` com a suíte xUnit em `tests/Vip.RestClient.Tests/`: a demonstração acessa serviço HTTP externo e aguarda entrada em `Console.ReadKey`; não a execute como validação automática. Use o projeto `.Tests` para testes automatizados e não trate apenas um build como evidência de cobertura comportamental.
- Ao mudar contratos, APIs, instruções de compilação ou comportamento documentado, atualize os documentos correspondentes em `docs/` na mesma alteração.
- Mantenha exemplos seguros: não inclua credenciais ou dados reais. URLs de exemplo devem usar `https://api.example.com` e não devem ser executadas.
- Siga o estilo local do arquivo; a biblioteca mistura namespaces em bloco e file-scoped e usa `#region`. Não imponha uma formatação global nova.
- Separe claramente o comportamento observado de sugestões ou recomendações. Não invente decisões históricas, suporte ou garantias que não estejam no código versionado.
- Não altere arquivos fora do escopo solicitado. Não publique pacotes, faça requisições HTTP deliberadas, nem execute workflows de publicação.

## Comandos seguros

Os comandos abaixo são referências; sua presença nesta lista não significa que foram executados ou validados. O build da solução inclui a demonstração com target `net9.0-windows` e a suíte com target `net9.0`. A evidência histórica de build anterior à inclusão da suíte não valida o estado atual da solução.

```sh
dotnet restore ./src/Vip.RestClient.sln
dotnet build ./src/Vip.RestClient.sln --configuration Release --no-restore
dotnet test ./tests/Vip.RestClient.Tests/Vip.RestClient.Tests.csproj --configuration Release
```

Alternativas para trabalhar somente com a biblioteca ou gerar um pacote local:

```sh
dotnet build ./src/Vip.RestClient/Vip.RestClient.csproj --configuration Release
dotnet pack ./src/Vip.RestClient/Vip.RestClient.csproj --configuration Release
```

`dotnet pack` gera pacote local (por padrão em `bin/Release`); não publica. A demonstração manual pode ser iniciada com `dotnet run --project ./tests/Vip.RestClient.Demo/Vip.RestClient.Demo.csproj`, mas envolve rede externa, interação pelo console e target `net9.0-windows`; não é teste automatizado. Para a suíte, execute `dotnet test` explicitamente no projeto `Vip.RestClient.Tests`; prefira o caminho acima a inferir quais projetos serão executados ao passar a solução inteira.
