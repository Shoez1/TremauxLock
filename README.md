# TremauxLock

TremauxLock foi reescrito como um cofre local recuperavel para Windows com interface visual hacker aprimorada. Em vez de esconder a pasta com ACL e atributo de sistema, ele agora protege os arquivos com criptografia autenticada e fluxo de recuperacao.

## 🎨 Melhorias Visuais Implementadas

- **Tema Hacker Cyberpunk**: Interface completamente redesenhada com cores neon (ciano, verde, magenta)
- **Animações Dinâmicas**: Efeitos de brilho, varreduras e pulsações em toda a interface
- **Gradientes Animados**: Fundos com gradientes radiais animados e efeitos de transparência
- **Botões com Efeitos**: Hover states com brilho, animações suaves e sombras coloridas
- **Status Pills**: Indicadores de estado com animações de pulsação e varredura
- **File Rows**: Lista de arquivos com efeitos hover e animações de entrada
- **Scrollbar Customizada**: Scrollbar com tema hacker e efeitos de brilho
- **Icones Vetoriais**: Icones atualizados com cores do tema hacker

## 🔒 Melhorias de Segurança

- **UI nativa WinForms**: Sem motor de browser embutido (sem WebView2 / sem perfil Edge ao lado do exe)
- **Memory Management**: Melhor gerenciamento de memória criptográfica
- **Input Validation**: Validação reforçada de entradas de usuário
- **Error Handling**: Tratamento de exceções mais robusto

## ⚡ Otimizações de Performance

- **Thread-Safe Rendering**: Operações de UI thread-safe com Invoke pattern
- **Buffered File I/O**: Leitura de arquivos com buffers otimizados para grandes arquivos
- **Memory Efficiency**: Redução de alocações desnecessárias de memória

## O que mudou

- O bloqueio antigo por permissao e ocultacao foi removido.
- A pasta `private` passa a ser criptografada de verdade em `private.locked`.
- `private.locked` e `private.vault.json` ficam ocultos enquanto o cofre estiver bloqueado.
- Cada arquivo usa `AES-GCM` com nonce aleatorio e validacao de integridade.
- A senha protege uma chave mestra aleatoria usando `PBKDF2-SHA256`.
- Cada bloqueio gera uma chave de recuperacao separada.
- O desbloqueio aceita senha ou chave de recuperacao.
- Cinco tentativas invalidas seguidas ativam uma pausa temporaria.
- A interface foi redesenhada com visual moderno hacker e foco em clareza operacional.

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

Saida esperada (script gera um unico exe na pasta `dist`):

`dist\win-x64\TremauxLock.exe`

## Build manual

```powershell
dotnet restore -r win-x64
dotnet publish .\LockerApp.csproj -c Release -r win-x64 --self-contained true --no-restore /p:PublishSingleFile=true /p:EnableCompressionInSingleFile=true /p:DebugType=None /p:DebugSymbols=false
```

## Observacoes de seguranca

- O projeto foi desenhado para ser recuperavel. Ele nao tenta criar bloqueio irreversivel.
- A chave de recuperacao precisa ser guardada fora da pasta do app.
- A remocao de arquivos em claro usa exclusao normal do sistema, nao limpeza criptografica do disco.
- Arquivos muito grandes sao processados de forma mais eficiente com buffers otimizados.

## 🎯 Características Técnicas

- **Framework**: .NET 9.0 Windows Forms
- **Criptografia**: AES-GCM com autenticação integrada
- **Derivação de Chave**: PBKDF2-SHA256 com 210.000 iterações
- **Interface**: Windows Forms compacta (tema hacker, sem dependencia de Edge/WebView2)
- **Temas**: Sistema de cores hacker customizável
- **Performance**: Otimizado para grandes volumes de dados

## 🚀 Versão

**TremauxLock v2.0** - Interface Hacker Enhanced

Melhorias significativas de visual, segurança e performance mantendo a robustez da criptografia original.
