# TremauxLock

TremauxLock foi reescrito como um cofre local recuperavel para Windows. Em vez de esconder a pasta com ACL e atributo de sistema, ele agora protege os arquivos com criptografia autenticada e fluxo de recuperacao.

## O que mudou

- O bloqueio antigo por permissao e ocultacao foi removido.
- A pasta `private` passa a ser criptografada de verdade em `private.locked`.
- `private.locked` e `private.vault.json` ficam ocultos enquanto o cofre estiver bloqueado.
- Cada arquivo usa `AES-GCM` com nonce aleatorio e validacao de integridade.
- A senha protege uma chave mestra aleatoria usando `PBKDF2-SHA256`.
- Cada bloqueio gera uma chave de recuperacao separada.
- O desbloqueio aceita senha ou chave de recuperacao.
- Cinco tentativas invalidas seguidas ativam uma pausa temporaria.
- A interface foi redesenhada com visual moderno e foco em clareza operacional.

## Estrutura do cofre

- `private`
  Pasta de trabalho em claro quando o cofre esta desbloqueado.
- `private.locked`
  Conteudo criptografado do cofre.
- `private.vault.json`
  Metadados do cofre, incluindo salts e chaves mestras protegidas.

## Fluxo de uso

1. Rode o `TremauxLock.exe`.
2. Coloque os arquivos desejados dentro de `private`.
3. Clique em `Bloquear cofre` e defina a senha.
4. Guarde a chave de recuperacao exibida pelo app.
5. Para restaurar os arquivos, use `Desbloquear com senha` ou `Usar chave de recuperacao`.

## Build rapido

Na raiz do projeto:

```bat
build-exe.bat
```

Saida esperada:

`bin\Release\net9.0-windows\win-x64\publish\TremauxLock.exe`

## Build manual

```powershell
dotnet restore -r win-x64
dotnet publish .\LockerApp.csproj -c Release -r win-x64 --self-contained true --no-restore /p:PublishSingleFile=true /p:EnableCompressionInSingleFile=true /p:DebugType=None /p:DebugSymbols=false
```

## Observacoes de seguranca

- O projeto foi desenhado para ser recuperavel. Ele nao tenta criar bloqueio irreversivel.
- A chave de recuperacao precisa ser guardada fora da pasta do app.
- A remocao de arquivos em claro usa exclusao normal do sistema, nao limpeza criptografica do disco.
- Arquivos muito grandes ainda sao processados em memoria durante a criptografia e descriptografia.
