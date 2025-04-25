# TremauxLock
 

![screenshot](https://user-images.githubusercontent.com/placeholder/lockerapp-dark.png)

**TremauxLock** é um utilitário Windows para bloquear/desbloquear uma pasta privada com senha, proteção visual e tema escuro estilo hacker. Ideal para proteger arquivos pessoais de forma simples, portátil e visualmente estilosa.

## Recursos
- Cria automaticamente uma pasta `private` ao lado do executável.
- Bloqueia e oculta a pasta, protegendo por senha (hash seguro SHA-256).
- Permissões de acesso negadas ao grupo Everyone enquanto bloqueada.
- Interface moderna, dark, "hacker" (verde suave sobre preto, fonte monoespaçada).
- Ícone ladybug embutido no executável e na janela.
- 100% portátil: basta levar apenas o `TremauxLock.exe`.

## Como usar
1. **Primeira execução:**
   - Rode o `TremauxLock.exe`.
   - A pasta `private` será criada automaticamente.
   - Coloque os arquivos que deseja proteger dentro da pasta `private`.
2. **Bloquear:**
   - Clique em "Bloquear pasta".
   - Defina e confirme uma senha.
   - A pasta será ocultada e protegida.
3. **Desbloquear:**
   - Clique em "Desbloquear pasta".
   - Digite a senha correta.
   - A pasta será restaurada e o acesso liberado.

## Compilação
Necessário [.NET 6/7/8 SDK](https://dotnet.microsoft.com/download/dotnet) instalado.

```sh
# Clone o repositório e navegue até a pasta LockerApp
cd LockerApp
# Compile e gere um executável único
 dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeAllContentForSelfExtract=true
```
O executável estará em `bin/Release/netX.X-windows/win-x64/publish/TremauxLock.exe`.

## Personalização
- Para trocar o ícone, substitua `ladybug.ico` no projeto.
- Para alterar o tema, edite as cores no código fonte em `Program.cs`.

## Avisos
- Execute como administrador para garantir alteração de permissões.
- Se esquecer a senha, não há como recuperar o acesso pelos meios normais.
- O app é destinado a proteção casual/local, não para dados ultra-sensíveis.

---

**by Shoez3 - Hacker Edition**
