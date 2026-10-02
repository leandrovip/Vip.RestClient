# Orientações da biblioteca

Escopo: `src/Vip.RestClient/`. Siga também as regras de compatibilidade, fontes e documentação do [`AGENTS.md` da raiz](../../AGENTS.md).

- `ClientApi.cs` é a implementação central de requisições; verifique nela o comportamento real antes de editar ou documentar chamadas.
- Todos os tipos da biblioteca usam o namespace `Vip.RestClient`, inclusive os arquivos em `Models`, `Events`, `Extensions`, `Exceptions` e `Utils`.
- Preserve estilo local: há namespaces em bloco e file-scoped, regiões e sobrecargas `Async`; não aplique uma convenção global sem solicitação.
- Alterações em contratos, serialização, respostas, eventos, URLs ou JWT exigem atualização da documentação pertinente em [`../../docs/arquitetura.md`](../../docs/arquitetura.md), [`../../docs/uso.md`](../../docs/uso.md) e/ou [`../../docs/particularidades.md`](../../docs/particularidades.md).
- O projeto é `netstandard2.0`, usa `LangVersion` `latest` e Newtonsoft.Json 13.0.3. Não infira suporte a comportamentos ausentes no código.
- A demonstração manual está em `tests/Vip.RestClient.Demo/`; os testes automatizados estão em `tests/Vip.RestClient.Tests/`. Use a suíte `.Tests` para validar comportamento e não trate apenas um build como evidência de cobertura.
