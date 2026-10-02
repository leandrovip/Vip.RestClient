# Desenvolvimento

## Projetos e dependências

A fonte de verdade para configuração são os arquivos de projeto e a solução versionados:

- `src/Vip.RestClient/Vip.RestClient.csproj`: biblioteca `netstandard2.0`, `LangVersion` `latest`, versão-base `1.0.0`, MIT e `Newtonsoft.Json` `13.0.3`.
- `tests/Vip.RestClient.Demo/Vip.RestClient.Demo.csproj`: executável manual `net9.0-windows`, com `ImplicitUsings` e `Nullable` desabilitados, dependência `Vip.Extensions` `1.0.22` e referência de projeto à biblioteca.
- `tests/Vip.RestClient.Tests/Vip.RestClient.Tests.csproj`: suíte automatizada xUnit com target `net9.0`, sem target Windows; usa handler HTTP falso para não depender de rede.
- `src/Vip.RestClient.sln`: inclui a biblioteca, a demonstração e a suíte automatizada.

Não há `global.json` ou fixação de SDK observada. O target Windows do executável de demonstração não permite inferir que a biblioteca `netstandard2.0` ou a suíte `.Tests` sejam exclusivamente Windows. A demonstração e os testes automatizados são projetos distintos.

## Compilação e empacotamento local

### Comandos

Restaure e compile a solução; o build inclui a demonstração `net9.0-windows` e a suíte `.Tests` `net9.0`:

```sh
dotnet restore ./src/Vip.RestClient.sln
dotnet build ./src/Vip.RestClient.sln --configuration Release --no-restore
```

Para executar testes automatizados, aponte explicitamente ao projeto xUnit:

```sh
dotnet test ./tests/Vip.RestClient.Tests/Vip.RestClient.Tests.csproj --configuration Release
```

Depois de restaurar a solução, também é possível acrescentar `--no-restore` ao comando de teste. Não use a demonstração `.Demo` como teste. As alternativas de build isolado e empacotamento abaixo não foram executadas na validação registrada. Para compilar isoladamente a biblioteca (com restore implícito):

```sh
dotnet build ./src/Vip.RestClient/Vip.RestClient.csproj --configuration Release
```

Para gerar um pacote local, sem publicar:

```sh
dotnet pack ./src/Vip.RestClient/Vip.RestClient.csproj --configuration Release
```

O destino padrão do pacote é `src/Vip.RestClient/bin/Release`. `src/nuget.config` define `repositoryPath` como `../packages`; é configuração legada e não deve ser tratada como garantia do layout usado pelo restore moderno.

### Validação registrada

Em 2026-10-01, em Windows com SDK .NET `10.0.401`, `dotnet restore ./src/Vip.RestClient.sln` e `dotnet build ./src/Vip.RestClient.sln --configuration Release --no-restore` foram concluídos sem avisos nem erros. Após a integração de `FromHttpClient`, `dotnet test ./tests/Vip.RestClient.Tests/Vip.RestClient.Tests.csproj --configuration Release --no-build --no-restore` terminou com **70 aprovados, 0 falhos e 0 ignorados** (57 existentes e 13 novos).

O teste de superfície preservou a baseline versionada (117 linhas) e verifica separadamente a assinatura aprovada de `FromHttpClient`, além dos tipos/membros visíveis, defaults opcionais e constraints genéricas registrados. Esse snapshot não é garantia completa de compatibilidade binária; projetos consumidores reais não foram compilados. Linux não foi validado; a demonstração, build isolado da biblioteca, `dotnet pack` e publicação não foram executados.

## Demonstração manual (não é teste automatizado)

`tests/Vip.RestClient.Demo/Program.cs` chama `ClientTests.Run`. O programa faz chamadas reais a `https://httpbin.org/` e termina com `Console.ReadKey()`. O comando abaixo é somente para execução manual, com autorização para acesso de rede e interação; não o execute como validação automática:

```sh
dotnet run --project ./tests/Vip.RestClient.Demo/Vip.RestClient.Demo.csproj
```

O projeto `.Demo` tem `OutputType` `Exe` e não declara framework de testes nem asserts. A suíte automatizada é o projeto `.Tests`; seus testes de caracterização preservam/registram comportamentos legados, não os corrigem. O comando de teste explícito acima é a validação comportamental apropriada; não infira que o build sozinho o substitui.

## Workflow NuGet

`.github/workflows/nuget.yml` é acionado manualmente (`workflow_dispatch`) e declara input opcional `typeBuild`. Usa `ubuntu-latest`, `actions/checkout@main`, `actions/setup-dotnet@v1` com SDK `9.0.x`, tenta construir a solução, empacota a biblioteca e inclui uma etapa `dotnet nuget push` que consome o secret `NUGET_TOKEN`. A versão base no workflow deriva de `1.0.${GITHUB_RUN_NUMBER}` e o input é concatenado como sufixo.

O step chamado `Checkout branch develop` não fixa uma referência `develop`; o nome é apenas o texto do step e a ação usa `@main`. As referências `@main` e `@v1` das actions são mutáveis. O workflow usa o comando legado `::set-env`, declara `ACTIONS_ALLOW_UNSECURE_COMMANDS: true` e configura `DOTNET_CLI_TELEMETRY_OPTOUT: false` (isso não desativa telemetria). **O workflow não executa a suíte de testes.** O build da solução em runner Ubuntu também precisa ser avaliado em relação ao target Windows do executável; a compatibilidade cross-platform do fluxo permanece pendente, portanto não se afirma que o pipeline funciona.

Não execute, dispare ou altere a etapa de publicação sem solicitação explícita e autorização apropriada. O comando local `dotnet pack` acima não publica pacote.

## Higiene e fontes

Considere os projetos, a solução, o código e o workflow versionados como evidência. Não use conteúdo de `bin/`, `obj/` ou `.vs/` para inferir configuração atual. O `.gitignore` contém marcadores literais de conflito repetidos; isso é uma pendência observada, não uma instrução para corrigir neste documento. Esta documentação não altera esse arquivo.
