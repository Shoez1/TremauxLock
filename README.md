# TremauxLock

TremauxLock e um cofre local para Windows. Ele protege arquivos colocados na pasta `private` usando criptografia autenticada, senha e chave de recuperacao. O foco e ser simples, reversivel e claro para o usuario.

## O que ele faz

- Cria uma pasta `private` ao lado do executavel.
- Bloqueia o conteudo em `private.locked` com AES-GCM.
- Protege a chave mestra com PBKDF2-SHA256.
- Gera uma chave de recuperacao separada a cada bloqueio.
- Restaura os arquivos com senha ou chave de recuperacao.
- Mantem o escopo limitado a pasta do cofre, sem varrer o PC inteiro.

## Melhorias recentes

- Interface principal redesenhada com visual Windows moderno, melhor hierarquia e acoes mais claras.
- Criptografia de arquivos em blocos (`TMX3`), evitando carregar arquivos grandes inteiros na memoria.
- Compatibilidade para desbloquear arquivos antigos no formato `TMX2`.
- Enumeracao mais segura da pasta `private`, ignorando pontos de reparse para evitar seguir atalhos/junctions.
- Validacao de integridade adicional ao restaurar o cofre.
- Validacao rigorosa dos metadados do cofre antes de derivar chaves.
- Senhas novas preservam hifens, espacos e maiusculas/minusculas exatamente como digitadas.
- Arquivos temporarios em claro passam por sobrescrita simples antes da exclusao, quando o sistema permite.
- A chave copiada para a area de transferencia e limpa automaticamente se continuar igual apos 60 segundos.
- Botoes sem timers permanentes, reduzindo trabalho desnecessario da UI.

## Estrutura do cofre

- `private`: pasta de trabalho em claro quando o cofre esta aberto.
- `private.locked`: conteudo criptografado quando o cofre esta bloqueado.
- `private.vault.json`: metadados do cofre, salts e chaves mestras protegidas.

## Fluxo de uso

1. Rode `TremauxLock.exe`.
2. Coloque arquivos dentro da pasta `private`.
3. Clique em `Bloquear cofre` e defina uma senha forte.
4. Guarde a chave de recuperacao fora do computador.
5. Para restaurar, use `Desbloquear` ou `Chave`.

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

- O projeto e recuperavel por design. Ele nao tenta criar bloqueio irreversivel.
- A chave de recuperacao precisa ficar fora da pasta do app.
- A remocao de arquivos em claro tenta sobrescrever os dados antes de excluir, mas SSDs, snapshots, cache e journaling do sistema podem manter copias fora do controle do app.
- Nomes e tamanhos dos arquivos ainda podem aparecer nos metadados/estrutura local do cofre; proteja a pasta do app com permissoes adequadas do Windows.
- O app nao deve ser usado em arquivos de terceiros sem permissao.
