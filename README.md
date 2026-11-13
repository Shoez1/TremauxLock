# TremauxLock

Cofre local para Windows que protege os arquivos da pasta `private` com criptografia autenticada, senha e chave de recuperação. O objetivo é ser simples, reversível e transparente: o conteúdo fica ilegível enquanto o cofre está bloqueado e volta ao normal quando você desbloqueia.

> Versão atual: **3.2.0** — AES-GCM, PBKDF2-HMAC-SHA256 com 600.000 iterações, recuperação automática de operações interrompidas e 46 testes automatizados.

## O que ele faz

- Cria a pasta `private` ao lado do executável.
- Bloqueia o conteúdo em `private.locked` usando AES-256-GCM.
- Protege a chave mestra com PBKDF2-HMAC-SHA256 e salts aleatórios.
- Gera uma chave de recuperação nova a cada bloqueio.
- Restaura os arquivos com a senha ou com a chave de recuperação.
- Mantém o escopo restrito à pasta do cofre, sem varrer o restante do PC.
- Recupera automaticamente operações interrompidas por queda de energia, travamento ou fechamento forçado.

## Segurança

- **Criptografia autenticada:** AES-256-GCM detecta alterações e arquivos truncados.
- **Arquivos grandes em blocos:** o formato `TMX3` processa o conteúdo em blocos de 4 MiB, sem carregar o arquivo inteiro na memória. Arquivos antigos no formato `TMX2` continuam legíveis.
- **Derivação de chave:** PBKDF2-HMAC-SHA256 com 600.000 iterações por padrão (OWASP). Cofres antigos com 210.000 iterações ou mais continuam abrindo.
- **Metadados versão 3.2:** compatível com cofres 3.0, 3.1 e 3.2.
- **Chave de recuperação:** 128 bits aleatórios, exibida em grupos de 4 caracteres. Ela muda a cada bloqueio e só funciona para o bloqueio atual.
- **Validação de caminhos:** nomes relativos são normalizados e validados para impedir escapes da pasta do cofre (`..`, caminhos absolutos e junctions/reparse points).
- **Limpeza de temporários:** pastas em claro de operações interrompidas são sobrescritas e removidas quando possível.
- **Área de transferência:** a chave copiada é limpa após 60 segundos se continuar igual.
- **Senhas:** qualquer senha não vazia é aceita, inclusive curtas como `1234`. O medidor de força é apenas informativo e não bloqueia o bloqueio do cofre.

## Estrutura do cofre

| Caminho | Função |
| --- | --- |
| `private/` | Pasta de trabalho em claro quando o cofre está aberto. |
| `private.locked/` | Conteúdo criptografado quando o cofre está bloqueado. |
| `private.vault.json` | Metadados: versão, contagem, tamanho, salts, iterações e chave mestra protegida. |
| `private.plain.backup*` | Cópia temporária em claro criada durante o bloqueio. |
| `private.locked.encrypted.backup*` | Cópia temporária dos dados criptografados durante o desbloqueio. |
| `*.pending` | Pastas/arquivos intermediários de uma operação em andamento. |

Os backups usam nomes únicos e existem apenas durante a operação. Se o app for interrompido, a próxima execução decide o que restaurar ou limpar e mostra um aviso na interface.

## Fluxo de uso

1. Execute `TremauxLock.exe`.
2. Coloque os arquivos dentro da pasta `private`.
3. Clique em **Bloquear cofre**, defina uma senha e confirme.
4. Guarde a chave de recuperação fora do computador (copie ou salve em arquivo).
5. Para restaurar, use **Desbloquear** com a senha ou com a chave de recuperação.

## Recuperação de interrupções

Na inicialização, o app verifica o estado das pastas e metadados:

- **Bloqueio interrompido:** se `private` sumiu e existe `private.plain.backup*`, a cópia em claro é restaurada.
- **Desbloqueio interrompido:** se `private.pending` existe e há metadados válidos com backup criptografado, a pasta temporária é promovida a `private`.
- **Metadados obsoletos:** se `private` existe com arquivos e `private.locked` não existe, o cofre é tratado como aberto e os metadados serão sobrescritos no próximo bloqueio.
- **Pastas em conflito:** estados ambíguos são exibidos como inconsistentes, sem apagar dados automaticamente.

## Requisitos

- Windows 10/11 x64.
- Para compilar: .NET 9 SDK.
- O executável publicado é autocontido e não exige .NET instalado.

## Build

Na raiz do projeto:

```bat
build-exe.bat
```

Saída esperada:

```text
dist\win-x64\TremauxLock.exe
```

Build manual:

```powershell
dotnet restore -r win-x64
dotnet publish .\TremauxLock.csproj -c Release -r win-x64 --self-contained true --no-restore /p:PublishSingleFile=true /p:EnableCompressionInSingleFile=true /p:DebugType=None /p:DebugSymbols=false
```

## Testes

A solução inclui o projeto `tests\TremauxLock.Tests` com 46 testes xUnit. A paralelização está desabilitada porque os testes usam arquivos e diretórios temporários locais.

```powershell
dotnet test .\TremauxLock.sln -c Release
```

Cobertura atual:

- Criptografia `TMX2` e `TMX3`, arquivos grandes e normalização de caminhos.
- Detecção de adulteração, truncamento e dados extras.
- KDF, salts, senha errada e compatibilidade de metadados.
- Bloqueio, desbloqueio com senha e com chave de recuperação.
- Recuperação de bloqueio/desbloqueio interrompido e metadados obsoletos.
- Heurística de força de senha (informativa, sem bloqueio).
- Aceitação de senhas curtas/fracas, como `1234`, no bloqueio e no desbloqueio.

## Estrutura do projeto

| Arquivo | Responsabilidade |
| --- | --- |
| `Program.cs` | Inicialização, DPI, tratamento global de erros e log técnico sem segredos. |
| `MainForm.cs` | Interface principal, atalhos, estado do cofre e avisos. |
| `CredentialDialog.cs` | Definição/desbloqueio com senha, medidor de força e opção de mostrar senhas. |
| `RecoveryKeyDialog.cs` | Exibição, cópia e salvamento da chave de recuperação. |
| `PasswordStrength.cs` | Heurística de força apenas informativa. |
| `VaultService.cs` | Fluxo do cofre, metadados, recuperação e limpeza segura. |
| `VaultCrypto.cs` | AES-GCM, PBKDF2, formatos `TMX2`/`TMX3` e tamanhos. |
| `VaultMetadata.cs` | Modelo e versões dos metadados. |
| `VaultSecurity.cs` | Restrição de ACLs e atributos das pastas do cofre. |
| `AppTheme.cs` e controles visuais | Identidade visual existente. |
| `Build-Exe.ps1` / `build-exe.bat` | Publicação single-file em `dist\win-x64`. |

## Limitações conhecidas

- Senhas curtas ou previsíveis são aceitas por escolha do usuário e reduzem a resistência a ataques de força bruta.
- A sobrescrita de arquivos em claro antes da exclusão é uma mitigação simples. SSDs, snapshots, cache do sistema e journaling podem manter cópias fora do controle do app.
- Os **nomes** dos arquivos aparecem dentro de `private.locked` (acrescidos de `.tmx`), embora o conteúdo esteja criptografado. Contagem, tamanho total e datas ficam nos metadados.
- A chave de recuperação vale apenas para o bloqueio atual. Se você bloqueou de novo, use a chave mais recente.
- Não há sincronização em nuvem nem controle de versões do cofre.
- A pasta do aplicativo deve ser protegida por permissões do Windows; o app não gerencia ACLs de todo o diretório.
- Não use o app em arquivos de terceiros sem permissão.
