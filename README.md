# ApiCripto

API REST em ASP.NET Core (.NET 10) que cadastra corretoras e ativos de criptomoeda
e alimenta o histórico de preços com dados reais da **API pública da Binance**.

Projeto acadêmico — Devs2Blu, disciplina de C#/API.

---

## Atendimento ao enunciado

| Requisito | Onde está |
|---|---|
| Duas ou mais entidades | `Corretora`, `Ativo`, `Cotacao` (`models/`) |
| Relação 1-N entre entidades | **Corretora 1-N Ativo** e **Ativo 1-N Cotacao** (`infra/AppDbContext.cs`) |
| Pelo menos 2 filtros na entidade principal | **6 filtros** em `Ativo`: `corretoraId`, `simbolo`, `tipo`, `precoMin`, `precoMax`, `monitorado` (`dtos/AtivoFiltro.cs` + `repositories/AtivoRepository.ListarAsync`) |
| Banco de dados à escolha do aluno | SQLite (arquivo `apicripto.db`, criado sozinho na primeira execução) |

---

## Como rodar

```bash
cd ApiCripto
dotnet run
```

Abra **http://localhost:5000/swagger**.

Na primeira execução o programa cria o banco (`Database.Migrate()`) e popula
dados de exemplo (`DbSeeder`), então a API já sobe funcionando — inclusive sem
internet.

Se quiser começar do zero de novo: apague o arquivo `ApiCripto/apicripto.db` e rode outra vez.

### Testes

```bash
dotnet test
```

São 12 testes: filtros do repositório rodando em SQLite real (em memória), regras dos
services com uma Binance falsa (sem internet) e a API inteira subida com
`WebApplicationFactory` (`/health`, listagem, 404 em ProblemDetails).

### Com Docker

```bash
docker build -t apicripto .
docker run -p 8080:8080 apicripto      # http://localhost:8080/swagger
```

### Pré-requisitos

- .NET SDK 10.0
- `dotnet-ef` só é necessário para **criar novas migrations**:
  `dotnet tool install --global dotnet-ef --version 10.0.14`

---

## Endpoints

### Corretora — `/api/corretora`
| Método | Rota | O que faz |
|---|---|---|
| GET | `/api/corretora` | lista |
| GET | `/api/corretora/{id}` | busca por id |
| POST | `/api/corretora` | cadastra |
| PUT | `/api/corretora/{id}` | atualiza |
| DELETE | `/api/corretora/{id}` | remove (400 se tiver ativos) |

### Ativo (entidade principal) — `/api/ativo`
| Método | Rota | O que faz |
|---|---|---|
| GET | `/api/ativo` | lista **com os filtros** |
| GET | `/api/ativo/{id}` | busca por id |
| POST | `/api/ativo` | cadastra |
| PUT | `/api/ativo/{id}` | atualiza |
| DELETE | `/api/ativo/{id}` | remove (cotações vão junto, em cascata) |

Filtros combináveis:

```
/api/ativo?corretoraId=1
/api/ativo?simbolo=btc
/api/ativo?tipo=Stablecoin
/api/ativo?precoMin=100&precoMax=5000
/api/ativo?monitorado=true
/api/ativo?corretoraId=1&tipo=Cripto&precoMin=1000&ordenarPor=preco&desc=true
```

### Cotação (rota aninhada) — `/api/ativo/{ativoId}/cotacao`
| Método | Rota | O que faz |
|---|---|---|
| GET | `/api/ativo/{ativoId}/cotacao?desde=&ate=&limite=` | histórico, do mais novo para o mais antigo |
| GET | `/api/ativo/{ativoId}/cotacao/{id}` | uma cotação |
| POST | `/api/ativo/{ativoId}/cotacao` | lançamento manual |
| DELETE | `/api/ativo/{ativoId}/cotacao/{id}` | remove |

### Sincronização com a Binance — `/api/sincronizacao`
| Método | Rota | O que faz |
|---|---|---|
| POST | `/api/sincronizacao/ativo/{ativoId}` | busca o preço real de um ativo e grava |
| POST | `/api/sincronizacao` | sincroniza todos os monitorados e devolve um relatório |

---

### Saúde — `/health`
| Método | Rota | O que faz |
|---|---|---|
| GET | `/health` | `Healthy` se a API e o banco respondem (usado pela nuvem) |

---

## Sincronização automática

Além dos endpoints manuais, um `BackgroundService` (`services/SincronizacaoAutomaticaWorker.cs`)
sincroniza todos os ativos monitorados **na subida da API e a cada 15 minutos**:

```json
"Sincronizacao": {
  "IntervaloMinutos": 15,
  "LimitePorMinuto": 10
}
```

- `IntervaloMinutos: 0` desliga o worker.
- `LimitePorMinuto` é o rate limit dos endpoints `POST /api/sincronizacao*` (HTTP 429 acima disso),
  para ninguém queimar a cota da Binance com a API pública na internet.

---

## Configuração da Binance

`appsettings.json`:

```json
"Binance": {
  "BaseUrl": "https://data-api.binance.vision",
  "TimeoutSegundos": 15
}
```

`data-api.binance.vision` é o endpoint público de market data da Binance: não
exige chave de API e **não é bloqueado por região**. O `api.binance.com` responde
**HTTP 451** em várias redes e provedores — se quiser usá-lo, é só trocar a
`BaseUrl`. O endpoint consumido é `GET /api/v3/ticker/24hr?symbol=BTCUSDT`.

---

## Estrutura

```
ApiCripto/
├── ApiCripto.sln
├── global.json                 SDK 10.0
└── ApiCripto/
    ├── Program.cs              composição: DI, DbContext, HttpClient, Swagger
    ├── appsettings.json        connection string + config da Binance
    ├── models/                 Corretora, Ativo, Cotacao, TipoAtivo
    ├── dtos/                   contratos de entrada/saída (AtivoFiltro mora aqui)
    ├── infra/                  AppDbContext, DbSeeder
    ├── integracoes/            BinanceClient + DTO do JSON da Binance
    ├── repositories/           acesso a dados (única camada que conhece o EF)
    ├── services/               regras de negócio
    ├── controllers/            HTTP puro
    └── Migrations/             geradas por dotnet ef
tests/
└── ApiCripto.Tests/            xUnit: repositório, services e API em memória
```

Fluxo de uma requisição:

```
HTTP -> Controller -> Service -> Repository -> EF Core -> SQLite
                        |
                        +-> BinanceClient -> API da Binance (nos endpoints de sincronização)
```

Dois documentos acompanham o projeto, com públicos diferentes:

- **`manual-apicripto.html`** — como *usar* a API: cadastrar corretora, cadastrar ativo,
  trazer preço, filtrar e ler os erros. Comece por aqui.
- **`documentacao-apicripto.html`** — como a API foi *construída*: por que cada decisão
  foi tomada e como estender.

---

## Deploy gratuito no Render

O repositório já tem `Dockerfile` e `render.yaml`, então o deploy é por Blueprint:

1. Entre em <https://render.com> com sua conta do GitHub.
2. **New → Blueprint** → escolha o repositório `ApiCripto` → **Apply**.
3. Em alguns minutos a API fica em `https://apicripto-xxxx.onrender.com/swagger`.
4. Todo `git push` na `main` gera um deploy novo (e o CI do GitHub roda os testes antes).

Limitações do plano gratuito, e por que não atrapalham aqui:

- **Dorme após ~15 min sem acesso**; a primeira requisição depois disso leva ~1 min.
- **Disco não é persistente**: a cada deploy/reinício o SQLite nasce de novo. O `DbSeeder`
  recria os dados de exemplo e o worker já troca os preços pelos reais da Binance na subida.
  Para guardar o histórico de verdade, troque para PostgreSQL (ex.: Neon, gratuito) —
  `UseSqlite` → `UseNpgsql`, como explicado no `Program.cs`.

Variáveis de ambiente aceitas (o `__` substitui o `:` do appsettings):

| Variável | Padrão |
|---|---|
| `PORT` | definida pela plataforma |
| `ConnectionStrings__Default` | `Data Source=/app/data/apicripto.db` (no Docker) |
| `Binance__BaseUrl` | `https://data-api.binance.vision` |
| `Sincronizacao__IntervaloMinutos` | `15` |
| `Sincronizacao__LimitePorMinuto` | `10` |

---

## Melhorias desta versão

- **.NET 8 → .NET 10** (o suporte do .NET 8 termina em 10/11/2026; o 10 é LTS até 2028) e EF Core 10.
- **Sincronização automática** em segundo plano (`BackgroundService` + `PeriodicTimer`).
- **Pronto para nuvem**: `Dockerfile` multi-stage rodando como usuário sem privilégios,
  `render.yaml`, porta pela variável `PORT`.
- **`/health`** com checagem do banco (`AddDbContextCheck`).
- **Rate limit** nos endpoints de sincronização.
- **ProblemDetails** padrão (RFC 9457) para erro não tratado e rota inexistente.
- **CORS** liberado para um front consumir a API.
- **Testes automatizados** (xUnit) e **CI no GitHub Actions** rodando testes + build Docker +
  teste de fumaça do container a cada push/PR.
