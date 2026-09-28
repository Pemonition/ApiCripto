# Verificação do piloto — 2026-09-28

Base: f05505ad6d4f80c8b13fbdd95a482632db3d24a4. Branch: feat/autenticacao-autorizacao.
Ambiente: Windows, SDK .NET 10.0.302, alvo net8.0, runtime ASP.NET Core 8.0.17.

- Base anterior compilada: zero avisos/erros. Execução HTTP local: 7 ativos; cadastro/consulta/exclusão de corretora; entrada inválida retornou 400.
- Após alterações: dotnet test ApiCripto.sln --no-restore --nologo --logger "trx;LogFileName=autenticacao.trx".
- Resultado final: **14 aprovados, 0 falhas, 0 ignorados**, aproximadamente 13 segundos. Compilação de API/testes sem avisos reportados.
- Primeira execução: 13 passaram; 1 teste de regressão falhou ao desserializar enum textual como número. Corrigida a leitura no teste, preservando o contrato da API; execução completa repetida.
- SQLite temporário com migrations e autenticação reais nos testes. Binance substituída por fake; acesso externo não foi validado.
- Verificação HTTP com servidor local: banco criado na base anterior recebeu a migration aditiva e continuou com 7 ativos. Comando local de criação de administrador concluiu. Cadastro/login de conta fictícia funcionou; GET favoritos sem token retornou 401; favorito recebeu dono correto e foi excluído ao terminar.
- Contas fictícias da verificação ficaram somente no banco local ignorado pelo Git, com senhas aleatórias descartadas. Não há credencial de demonstração reutilizável.
- O ambiente restrito emitiu mensagens de acesso ao DPAPI/EventLog durante a inicialização local. A API permaneceu acessível e os testes HTTP passaram; não foi alterada a configuração de segurança do Windows.
- git diff --check: sem erros de whitespace. Banco, binários e TRX ficam ignorados.

Casos implementados e executados estão em ApiCripto.Tests/SegurancaTests.cs. O TRX é gerado em ApiCripto.Tests/TestResults/autenticacao.trx (não versionado). Este resumo acompanha o código testado nesta branch; não afirma funcionamento em produção ou compreensão já demonstrada pelo aluno.
