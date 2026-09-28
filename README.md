# ApiCripto

## Piloto de autenticação e autorização

A API agora exige configuração de Jwt:Key para iniciar. Leituras do catálogo
continuam públicas; alterações exigem Admin e sincronização exige o ID de um
operador configurado. Há cadastro/login e favoritos privados por usuário.
Consulte o [guia de configuração, regras, testes e exercício](docs/autenticacao-autorizacao.md)
antes de executar os comandos abaixo. As permissões atuais estão no guia;
os exemplos antigos de escrita precisam de um token autorizado.
Não há contas ou senhas padrão.

API REST em ASP.NET Core (.NET 8) que cadastra corretoras e ativos de criptomoeda
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

### Pré-requisitos

- .NET SDK 8.0
- `dotnet-ef` só é necessário para **criar novas migrations**:
  `dotnet tool install --global dotnet-ef --version 8.0.11`

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
├── global.json                 SDK 8.0
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
