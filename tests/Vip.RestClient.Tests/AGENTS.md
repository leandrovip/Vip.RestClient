# Orientações dos testes de caracterização

- Mantenha os casos, dados e assertions existentes; classes de teste na raiz contêm apenas métodos de teste.
- Apoios ficam em `Utils/`: DTOs em `Dtos/`, modelos em `Models/` e conteúdos HTTP em `Contents/`.
- Intercepte todas as chamadas HTTP, use somente URLs de exemplo e tokens sintéticos; não altere o baseline público.
