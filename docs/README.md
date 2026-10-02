# Documentação do Vip.RestClient

Documentação em português do Brasil para o snapshot atual do repositório. Ela descreve o código e os arquivos de projeto versionados; não usa `bin/`, `obj/` ou `.vs/` como fontes. Em caso de divergência, confira as fontes versionadas indicadas em cada tópico: esta documentação não substitui o código.

## Navegação

- [Estrutura do repositório](estrutura.md): mapa hierárquico e localização dos tipos.
- [Arquitetura e contratos](arquitetura.md): fluxo de transporte, respostas, eventos, serialização e JWT.
- [Uso](uso.md): exemplos C# seguros e observações de chamada.
- [Desenvolvimento](desenvolvimento.md): projetos, comandos disponíveis, demonstração e workflow.
- [Particularidades e limitações observadas](particularidades.md): comportamentos que merecem atenção e recomendações explicitamente separadas.

## Visão geral

`Vip.RestClient` é uma biblioteca cliente REST cuja implementação principal está em `src/Vip.RestClient/ClientApi.cs`. O projeto de biblioteca declara `netstandard2.0`, `LangVersion` `latest`, versão-base `1.0.0`, licença MIT e Newtonsoft.Json `13.0.3` (`src/Vip.RestClient/Vip.RestClient.csproj`). As chamadas expõem envelopes `Response` e `Response<T>`.

A solução `src/Vip.RestClient.sln` inclui a biblioteca, o executável manual `tests/Vip.RestClient.Demo` (target `net9.0-windows`) e a suíte automatizada xUnit `tests/Vip.RestClient.Tests` (target `net9.0`). A demonstração chama HTTP externo e pausa no console; a suíte automatizada usa handler HTTP falso e não depende de rede. Consulte [Desenvolvimento](desenvolvimento.md) para comandos e distinções entre os projetos.

## Escopo e atualização

Os documentos registram o snapshot atual e devem ser revistos quando os contratos, projetos, exemplos ou instruções mudarem. Recomendações não descrevem funcionalidades já implementadas. Não há aqui promessa de suporte, garantia de segurança, resultado de build ou estado de publicação além do que as fontes versionadas declaram.
