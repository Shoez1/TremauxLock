# TremauxLock (win-x64)

TremauxLock e um ocultador local de pastas para Windows. Ele esconde os arquivos colocados na pasta `private` em menos de 1 segundo, mesmo com varios GBs. O foco e ser simples, instantaneo e reversivel.

> Isto NAO e criptografia: o conteudo continua em claro dentro de `private.hidden`, so some da vista. Esconde de curiosos, nao de analise tecnica.

## O que ele faz

- Cria uma pasta `private` ao lado do executavel.
- Oculta `private` em `private.hidden` com uma senha simples (minimo 4 caracteres).
- Mostra a pasta de volta com a mesma senha.
- Pausa novas tentativas por 15 segundos apos 5 senhas erradas.
- Mantem o escopo limitado a pasta do app, sem varrer o PC inteiro.

## Estrutura das pastas

- `private`: pasta de trabalho em claro quando esta visivel.
- `private.hidden` + `private.hide.json`: pasta escondida e metadados da ocultacao (a senha nunca fica gravada em claro, so o hash).
- Pastas `private.locked` / `private.vault.json` de versoes com criptografia nao sao abertas por esta versao; ela avisa na tela.

## Fluxo de uso

1. Rode `TremauxLock.exe`.
2. Coloque arquivos dentro da pasta `private`.
3. Clique em `Ocultar pasta` e defina uma senha simples.
4. Para restaurar, clique em `Mostrar pasta` e digite a senha.

## Build rapido

Na raiz do projeto:

```bat
build-exe.bat
```

Saida esperada:

```text
dist\win-x64\TremauxLock.exe
```

## Build manual

```powershell
dotnet restore -r win-x64
dotnet publish .\LockerApp.csproj -c Release -r win-x64 --self-contained true --no-restore /p:PublishSingleFile=true /p:EnableCompressionInSingleFile=true /p:DebugType=None /p:DebugSymbols=false
```

## Observacoes de seguranca

- Por design, a ocultacao e reversivel com a senha. Nao ha chave de recuperacao: se esquecer a senha, nao ha como mostrar a pasta pelo app.
- Nomes, tamanhos e conteudo dos arquivos continuam legiveis para quem localizar `private.hidden`; proteja a pasta do app com permissoes adequadas do Windows.
- O app nao deve ser usado em arquivos de terceiros sem permissao.
