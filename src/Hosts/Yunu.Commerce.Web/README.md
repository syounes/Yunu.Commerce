# Yunu.Commerce.Web

Frontend React (Vite + TypeScript + CSS puro) do fluxo de criação de propostas de produto
via linguagem natural ("Crie produtos conversando"), consumindo a API `Yunu.Commerce.Api`.

Este projeto reproduz fielmente o pacote de referência aprovado, disponível em
`docs/front/Yunu-Commerce-React-Reference.zip`. O layout, CSS, componentes e comportamento
não devem ser redesenhados sem atualizar antes o pacote de referência.

## Pré-requisitos

- Node.js 18+ (recomendado 20+)
- API `Yunu.Commerce.Api` em execução em `https://localhost:7241` com o certificado de
  desenvolvimento ASP.NET confiável na máquina (`dotnet dev-certs https --trust`).

## Executando localmente

```bash
cd src/Hosts/Yunu.Commerce.Web
npm install
cp .env.example .env.development
npm run dev
```

A aplicação sobe em `http://localhost:5173` (porta fixa, `strictPort`).

### Variáveis de ambiente

```env
VITE_YUNU_API_BASE_URL=https://localhost:7241
VITE_USE_MOCK=false
```

- `VITE_YUNU_API_BASE_URL`: base da API `Yunu.Commerce.Api`. Nunca é hardcoded nos componentes;
  é lido apenas em `src/api/productProposalsApi.ts`.
- `VITE_USE_MOCK`: quando `true`, usa a massa de dados de `src/mock/productProposal.mock.ts`
  (com `setTimeout` simulando latência). O fluxo real nunca usa `setTimeout`.

## Scripts

```bash
npm run dev       # Vite dev server
npm run build      # tsc -b && vite build
npm run preview    # preview do build de produção
npm test           # Vitest + React Testing Library
```

## Fluxo funcional

```text
Usuário informa a intenção
  -> POST /api/catalog/product-proposals
  -> "Preparando sua proposta"
  -> recebe proposalId (contrato atual)
  -> GET /api/catalog/product-proposals/{proposalId}
  -> ProductProposalCard
  -> um SkuProposalCard por SKU
```

O cliente HTTP (`src/api/productProposalsApi.ts`) suporta os dois contratos:

1. **Atual**: `POST` retorna somente `proposalId`; o cliente executa o `GET` em seguida.
2. **Futuro**: `POST` já retorna `product`, `skus`, `source` e `resolution` no `201 Created`;
   nesse caso o `GET` adicional não é executado.

## Executando API + Frontend simultaneamente no Visual Studio 2022

1. Clique com o botão direito na solution -> **Configurar Projetos de Inicialização...**
2. Selecione **Vários projetos de inicialização**.
3. Defina `Yunu.Commerce.Api` como **Iniciar**.
4. Para o frontend, abra um terminal (Terminal do Desenvolvedor do VS ou PowerShell) na pasta
   `src/Hosts/Yunu.Commerce.Web` e rode `npm run dev`, pois o Visual Studio 2022 não executa
   scripts `npm` automaticamente para projetos Vite fora do formato `.esproj`.
5. Acesse `http://localhost:5173` no navegador enquanto a API roda em `https://localhost:7241`.

> Alternativa: use dois terminais integrados do Visual Studio (`View > Terminal`), um rodando
> `dotnet run --project src/Hosts/Yunu.Commerce.Api` e outro rodando `npm run dev` dentro de
> `src/Hosts/Yunu.Commerce.Web`.

## Testes

Os testes usam Vitest + React Testing Library e não chamam a API, MongoDB ou Azure reais
(o `fetch` é mockado). Cobrem: renderização inicial, contador de caracteres, botão desabilitado,
POST+GET, POST completo direto, renderização de produto/SKUs, atributos Text/Enum/Measurement,
erros 400/422/503/conexão e navegação por teclado (`Ctrl+Enter`).

```bash
npm test
```

## Restrições desta etapa

- Os botões **Editar detalhes**, **Editar SKU**, **Salvar para depois** e
  **Confirmar e criar produto** permanecem visíveis, porém desabilitados/sem efeito, pois os
  endpoints correspondentes ainda não existem.
- Nenhum dado de marca, família, código ou GTIN é inventado; quando ausente, exibe-se
  "A definir" ou "Não informado", conforme o pacote de referência.
