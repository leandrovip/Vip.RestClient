# Orientações da demonstração

Escopo: `tests/Vip.RestClient.Demo/`. Siga também as regras da [orientação raiz](../../AGENTS.md).

- Este projeto é um executável manual com target `net9.0-windows`, não uma suíte de testes. `Program` chama `ClientTests.Run`.
- `ClientTests.Run` envia requisições HTTP reais ao httpbin e termina com `Console.ReadKey`; não execute como validação automatizada nem sem autorização para rede/interação.
- Os modelos em `Models/` pertencem ao namespace `Vip.RestClient.Demo.Models`. `Vip.RestClient.Demo.Models.Response` é diferente do envelope `Vip.RestClient.Response` da biblioteca.
- A suíte automatizada xUnit pertence a `tests/Vip.RestClient.Tests/`; não confunda seus resultados com os da demonstração manual.
- Ao mudar um contrato documentado da biblioteca, atualize `../../docs/uso.md` ou `../../docs/particularidades.md` conforme aplicável.
