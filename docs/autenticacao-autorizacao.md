# Autenticação e autorização — piloto ApiCripto

## Referências e decisões

Professor: [rannyer/AulaAuth01 na revisão 1c2c43f](https://github.com/rannyer/AulaAuth01/tree/1c2c43fbcff81220e4302498b2ce3c0d80410938).
Foram lidos Program.cs, AuthController.cs, ProdutosController.cs e Usuario.cs. A aula publicada usa usuários em lista estática, comparação direta de senha, token JWT e restrição por perfil.

Nossa adaptação acrescenta usuários no SQLite, senhas com PasswordHasher do ASP.NET Core, identificador estável em sub, configuração externa de chave, política por usuário específico, favoritos privados e testes de integração. Não copiamos credenciais do professor. Propriedade de recurso e política por identidade específica são adaptações nossas, não algo atribuído à aula.

Fontes da pesquisa:
- [OWASP: autorização](https://cheatsheetseries.owasp.org/cheatsheets/Authorization_Cheat_Sheet.html): negar por padrão e verificar cada requisição.
- [OWASP: acesso por objeto](https://api-security.owasp.org/editions/2023/en/0xa1-broken-object-level-authorization/): conferir o acesso a cada objeto recebido por ID.
- [Microsoft: Bearer](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-8.0).
- [Microsoft: recursos](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/resourcebased?view=aspnetcore-8.0).

Decisão: catálogo de ativos/cotações continua público; favoritos são privados. Cada favorito pertence ao usuário autenticado e aponta para um ativo. Não há exceção automática para Admin acessar favorito alheio. O campo Monitorado do ativo continua global, sem alteração pelos favoritos.

## Regras

| Operação | Acesso |
|---|---|
| GET catálogo e cotações | Público |
| Cadastro/login | Público; limite compartilhado de 10 tentativas/minuto/IP |
| GET /api/auth/me | Usuário autenticado |
| POST/PUT/DELETE catálogo e cotações manuais | Perfil Admin |
| POST sincronização individual/lote | ID igual a Autorizacao:SincronizadorUsuarioId |
| CRUD /api/favoritos | Identidade autenticada e dona do recurso |
| Nova rota sem regra | Autenticação exigida pela política padrão |

401: token ausente/inválido. 403: identidade válida sem permissão. 404: favorito inexistente ou pertencente a outro usuário. Campos extras nos DTOs de autenticação/favoritos retornam 400; o cliente não escolhe perfil ou dono. Login duplicado no cadastro retorna 409.

## Fluxo explicado

1. Registrar valida a entrada e grava o hash com salt; não armazena a senha original.
2. Login confere o hash e emite JWT por 30 minutos, com sub (identidade), role (perfil) e jti.
3. O cliente envia Authorization: Bearer TOKEN.
4. O middleware valida assinatura, algoritmo, emissor, destinatário e validade; confere também se usuário/perfil continuam válidos no banco.
5. A política verifica perfil ou identidade específica. Admin não ganha sincronização automaticamente.
6. FavoritoService combina ID do recurso e UsuarioId da identidade autenticada em leitura, edição e exclusão. Listagem filtra pelo mesmo dono; criação deriva o dono do token.
7. O controller converte resultado em resposta HTTP.

JWT é assinado, não criptografado. Não colocar senhas ou segredos nas claims. Nome/email podem mudar; por isso a permissão específica usa identificador estável.

## Execução local

Da raiz do repositório, com SDK .NET 8 ou posterior e runtime ASP.NET Core 8, execute no PowerShell:

    dotnet restore
    $env:Jwt__Key = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
    dotnet run --project ApiCripto --no-launch-profile --urls http://127.0.0.1:5000

A chave temporária fica no ambiente deste terminal. Ao trocar a chave, tokens anteriores deixam de valer. Para persistência, use user-secrets no ambiente Development ou um gerenciador de segredos. O projeto tem UserSecretsId. Variáveis de ambiente funcionam em qualquer ambiente. Sem chave de pelo menos 32 bytes, a API falha explicitamente. Não há conta ou senha padrão.

Abra http://127.0.0.1:5000/swagger, cadastre uma conta fictícia em POST /api/auth/registrar (login de 3–80 caracteres: letras, números, ponto, traço ou sublinhado; senha de 12–128 caracteres). Use POST /api/auth/login e cole apenas o token em Authorize.

Para criar administrador, pare a API e execute com Jwt__Key configurada:

    $env:Bootstrap__Login = Read-Host "Login do administrador"
    $env:Bootstrap__Senha = Read-Host "Senha (12–128 caracteres)" -MaskInput
    dotnet run --project ApiCripto --no-launch-profile -- --criar-admin
    Remove-Item Env:Bootstrap__Senha, Env:Bootstrap__Login

MaskInput requer PowerShell 7. Esse comando local cria a conta e encerra sem iniciar servidor. Não promove login já existente. O cadastro HTTP sempre cria Usuario.

Para autorizar sincronização, registre uma conta comum e obtenha seu id no cadastro ou em /api/auth/me. Configure Autorizacao__SincronizadorUsuarioId no terminal com esse ID e reinicie a API. Sem configuração, ninguém sincroniza.

A migration adiciona Usuarios e Favoritos sem apagar o catálogo. A reversão remove essas tabelas; não reverter banco com dados que deseja preservar. global.json aceita SDK mais novo mantendo o alvo net8.0: esta máquina tinha SDKs 9/10 e runtime 8.

## Testes e limites

    dotnet test ApiCripto.sln --logger "trx;LogFileName=autenticacao.trx"

SQLite temporário, migrations, middleware JWT e autorização são reais nos testes; apenas a Binance é substituída por dados controlados. Isso não valida a conectividade externa.

Casos: cadastro/hash; tentativa de escolher perfil; login inválido; tokens ausentes, expirados, sem expiração e com assinatura/emissor/destinatário/algoritmo inválidos; perfil forjado; regras por perfil/identidade; isolamento Alice/Bob/Admin; troca de IDs; tentativa de enviar dono no corpo; edição e exclusão; catálogo público; limite de login.

Piloto local de aprendizado, sem afirmação de prontidão para produção. Antes de exposição pública, definir HTTPS, política de cadastro/recuperação, limites por conta/distribuídos, gestão e rotação de chaves e auditoria. Não há refresh token, revogação individual por logout, recuperação de senha ou MFA. Exclusão do usuário ou mudança de perfil invalida a identidade na próxima chamada. O limitador é local por processo e usa IP da conexão; proxies exigem análise de configuração antes de implantar.

## Exercício pequeno

Crie Alice e Bob. Alice adiciona um favorito. Bob tenta consultar e alterar esse mesmo ID: deve receber 404. Alice consulta novamente e confirma que a nota não mudou. Explique por que um token válido de Bob não basta e localize a condição correspondente em FavoritoService.
