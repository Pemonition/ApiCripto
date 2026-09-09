# Carteira

[![CI](https://github.com/Pemonition/ApiCripto/actions/workflows/ci.yml/badge.svg)](https://github.com/Pemonition/ApiCripto/actions/workflows/ci.yml)

API REST para acompanhar ativos e cotações de investimento, alimentada por APIs
públicas de corretoras. Começou como cripto (Binance) e é modelada para receber
outras classes de ativo — ações, FIIs, renda fixa — sem reescrever o núcleo.

> Evolução do projeto entregue na Devs2Blu. A versão avaliada está congelada na
> tag [`entrega-devs2blu`](../../tree/entrega-devs2blu).

---

## Rodar

**Com Docker** (não precisa de .NET nem PostgreSQL na máquina):

```bash
docker compose up --build
```

→ http://localhost:8080/swagger

**Sem Docker** (precisa de .NET 8 e um PostgreSQL rodando):

```bash
dotnet run --project src/Carteira.Api
```

→ http://localhost:5000/swagger

A connection string fica em `src/Carteira.Api/appsettings.json` ou na variável
de ambiente `ConnectionStrings__Default`. As migrations e o seed são aplicados
na subida, então o banco nasce pronto.

**Testes:**

```bash
dotnet test
```

---

## Arquitetura

Quatro projetos, e a direção das referências é a arquitetura:

```
Carteira.Api  ──►  Carteira.Infrastructure  ──►  Carteira.Application  ──►  Carteira.Domain
   HTTP                EF Core, HttpClient          regras de negócio        entidades puras
```

| Projeto | Responsabilidade | O que ele NÃO pode conhecer |
|---|---|---|
| `Domain` | Entidades, filtros de consulta, interfaces de repositório | nada — **zero PackageReference** |
| `Application` | Regras de negócio, DTOs, contratos de integração | EF Core, HTTP, ASP.NET |
| `Infrastructure` | EF Core, repositórios, clientes de corretora | ASP.NET |
| `Api` | Rotas, status HTTP, Swagger | regra de negócio |

O ponto de separar em projetos, e não só em pastas: **o compilador passa a
garantir a regra**. `Carteira.Domain.csproj` não referencia pacote nenhum, então
é impossível uma entidade ganhar um atributo do EF Core ou saber o que é um
status HTTP. Em um projeto único, isso seria só convenção — e convenção sem
verificação dura até o primeiro prazo apertado.

### Modelo de dados

```
Corretora ──1:N──► Ativo ──1:N──► Cotacao
          Restrict       Cascade
```

Os dois relacionamentos apagam de formas diferentes de propósito. Apagar uma
corretora com ativos destruiria todo o histórico de preços em silêncio, então o
banco recusa (`Restrict`) e a API responde `400` com o motivo. Já uma cotação é
o histórico *daquele* ativo e não significa nada sozinha, então acompanha o pai
(`Cascade`).

---

## Endpoints

### Ativo — entidade principal

| Método | Rota | |
|---|---|---|
| GET | `/api/ativo` | lista **filtrada e paginada** |
| GET | `/api/ativo/{id}` | busca por id |
| POST | `/api/ativo` | cadastra |
| PUT | `/api/ativo/{id}` | atualiza cadastro (não mexe em preço) |
| DELETE | `/api/ativo/{id}` | remove; cotações vão em cascata |

Seis filtros, todos opcionais e combináveis:

```
/api/ativo?corretoraId=1
/api/ativo?simbolo=btc
/api/ativo?tipo=Stablecoin
/api/ativo?precoMin=100&precoMax=5000
/api/ativo?monitorado=true
/api/ativo?corretoraId=1&tipo=Cripto&precoMin=1000&ordenarPor=preco&desc=true
/api/ativo?pagina=2&tamanhoPagina=50
```

A resposta é um `PagedResult<T>` com `itens`, `total`, `pagina`,
`tamanhoPagina`, `totalPaginas`, `temProxima` e `temAnterior`.

### Corretora — `/api/corretora`
CRUD completo. `DELETE` devolve `400` se a corretora ainda tiver ativos.

### Cotação — `/api/ativo/{ativoId}/cotacao`
Rota aninhada, porque cotação não existe fora de um ativo. `GET` aceita
`desde`, `ate` e `limite`; `POST` lança preço manualmente.

### Sincronização — `/api/sincronizacao`
| Método | Rota | |
|---|---|---|
| POST | `/api/sincronizacao/ativo/{id}` | puxa o preço real de um ativo |
| POST | `/api/sincronizacao?provedor=BINANCE` | sincroniza todos os monitorados |

---

## Decisões

**Campos espelho no `Ativo`.** `UltimoPreco`, `Variacao24h` e `AtualizadoEm` são
cópia da cotação mais recente. Desnormalização proposital: sem eles, o filtro de
faixa de preço rodaria sobre um subselect na tabela de cotações em toda
listagem. O preço é o risco de divergirem — e a defesa é `CotacaoService.RegistrarAsync`
ser o **único** ponto do sistema que grava cotação e espelho.

**`UsuarioId` antes de existir autenticação.** O `AppDbContext` aplica filtro
global (`HasQueryFilter`) em `Corretora`, `Ativo` e `Cotacao`, então toda
consulta já nasce isolada por usuário — inclusive as que forem escritas daqui a
dois anos por quem não lembrar que o sistema é multiusuário. A alternativa,
filtrar manualmente em cada repositório, funciona até o primeiro esquecimento;
e esse esquecimento não dá erro, dá vazamento de dados entre usuários.
`IUsuarioAtual` hoje devolve um Guid fixo; quando entrar JWT, muda uma classe.

**Provedores por dicionário, não por `switch`.** `IProvedorDeCotacao` expõe um
`Codigo` que casa com `Corretora.CodigoIntegracao`. O `SincronizacaoService`
recebe `IEnumerable<IProvedorDeCotacao>` e indexa por esse código. Plugar uma
segunda corretora é escrever a classe e registrá-la — o núcleo não muda.

**DTOs de saída, não entidades.** Impede `UsuarioId` de vazar na resposta,
tira os `[JsonIgnore]` de dentro do domínio e desacopla o contrato da API do
esquema do banco. De quebra, o `ReferenceHandler.IgnoreCycles` deixou de ser
necessário: sem entidade na resposta, não há ciclo para quebrar.

**Sincronização em lote sequencial.** As corretoras limitam requisições por IP
(HTTP 429) e o `DbContext` do EF Core não é thread-safe. `Task.WhenAll` aqui
quebraria os dois — e a segunda falha só apareceria sob carga.

**Paginação com teto.** `tamanhoPagina` é limitado a 100 no próprio filtro.
Sem isso, `?tamanhoPagina=999999` recria o `SELECT *` que a paginação veio
evitar.

---

## Corretoras integradas

| Corretora | Código | Endpoint |
|---|---|---|
| Binance | `BINANCE` | `GET /api/v3/ticker/24hr` — público, sem chave |

`Binance:BaseUrl` aponta para `https://data-api.binance.vision`, o endpoint
público oficial de market data. O host principal (`api.binance.com`) responde
**HTTP 451** (bloqueio por região) em várias redes e datacenters.

Falhas da corretora viram `502 Bad Gateway` com mensagem específica — símbolo
inexistente, bloqueio regional, limite de requisições, timeout. `502` porque
esta API está de pé e quem falhou foi o serviço do qual ela depende: `500`
diria "o bug é meu", `400` diria "a culpa é do cliente".

Requisições à corretora usam retentativa com espera exponencial (2s, 4s, 8s)
via Polly, incluindo em `429` — insistir no mesmo ritmo prolongaria o bloqueio.

---

## Estrutura

```
Carteira/
├── docker-compose.yml          API + PostgreSQL
├── Dockerfile                  build multi-stage, usuário sem privilégios
├── .github/workflows/ci.yml    build, testes e imagem Docker a cada push
├── src/
│   ├── Carteira.Domain/        Entidades, Consultas, Repositorios (interfaces)
│   ├── Carteira.Application/   Dtos, Servicos, Integracoes (contratos)
│   ├── Carteira.Infrastructure/ Persistencia, Repositorios, Integracoes/Binance
│   └── Carteira.Api/           Controllers, Program.cs
└── tests/
    └── Carteira.Tests/         testes de regra de negócio com repositórios fake
```

## Próximos passos

- [ ] `Candle` OHLCV via `/api/v3/klines` — o `ticker/24hr` dá uma foto do
      instante, insuficiente para gráfico, média móvel ou backtest
- [ ] Coletor automático (`BackgroundService`) idempotente, com índice único em
      `(AtivoId, Intervalo, AberturaEm)`
- [ ] `Operacao` (compra/venda), preço médio e resultado da carteira
- [ ] Segunda fonte de dados: ações e FIIs brasileiros
- [ ] Autenticação JWT — a costura já está pronta em `IUsuarioAtual`
