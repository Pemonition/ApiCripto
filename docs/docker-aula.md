# Docker: aplicação do aprendizado da aula

Esta branch já continha Dockerfile multi-stage, PostgreSQL, volume e health checks. O aprendizado foi aplicado à convivência com outra API local, sem reescrever a infraestrutura existente.

## Rodar junto com a API da aula

Em PowerShell:

```powershell
$env:CARTEIRA_API_PORT = '8081'
$env:CARTEIRA_DB_PORT = '5434'
docker compose config --quiet
docker compose up -d --build
```

Swagger: http://localhost:8081/swagger. O banco continua sendo `banco:5432` do ponto de vista da API; `localhost:5434` é o acesso pelo computador. Os valores padrão continuam 8080 e 5432.

As portas são publicadas somente na interface local, adequadas a este ambiente de estudo. Para acesso por outros computadores seria necessária uma configuração explícita de rede.

## Verificação e limites

Em 30/09/2026 foi validada a resolução do Compose com as portas alternativas. Esta alteração não constitui teste da inicialização completa de ApiCripto.

Atenção para próximo trabalho: o health check da API chama `wget`, mas o Dockerfile não instala esse programa explicitamente. Conferir sua presença na imagem e validar `/health` antes de considerar a monitoração pronta. Não confundir processo iniciado com serviço saudável.

Há outra branch de autenticação usando SQLite e estrutura diferente. Esta documentação descreve a base `evolucao` com PostgreSQL; não afirma que as duas versões foram integradas.
