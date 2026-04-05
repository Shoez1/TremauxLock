using System.Net;
using System.Text;

namespace TremauxLock
{
    internal static class DialogHtmlBuilder
    {
        public static string BuildCredential(CredentialDialogMode mode, int minimumPasswordLength)
        {
            string eyebrow;
            string title;
            string description;
            string hint;
            string primaryLabel;
            string primaryPlaceholder;
            string primaryType = "password";
            string primaryTag = "input";
            string primaryCssClass = "field-input";
            string confirmLabel = "Confirmar senha";
            string confirmPlaceholder = "Repita a mesma senha";
            bool showConfirm = false;
            string confirmType = "password";
            string actionText;

            switch (mode)
            {
                case CredentialDialogMode.CreatePassword:
                    eyebrow = "Configuracao de senha";
                    title = "Proteja o cofre com uma senha";
                    description = "Defina a senha que sera exigida para restaurar os arquivos ocultos do cofre.";
                    hint = $"Use pelo menos {minimumPasswordLength} caracteres. A chave de recuperacao sera exibida ao final.";
                    primaryLabel = "Senha";
                    primaryPlaceholder = "Digite uma senha forte";
                    showConfirm = true;
                    actionText = "Bloquear cofre";
                    break;

                case CredentialDialogMode.UnlockWithPassword:
                    eyebrow = "Acesso por senha";
                    title = "Desbloqueie com sua senha";
                    description = "Digite a senha definida no ultimo bloqueio para restaurar a pasta private.";
                    hint = "Se a senha nao estiver disponivel, voce ainda pode usar a chave de recuperacao.";
                    primaryLabel = "Senha do cofre";
                    primaryPlaceholder = "Digite sua senha";
                    actionText = "Desbloquear";
                    break;

                default:
                    eyebrow = "Chave de recuperacao";
                    title = "Restaure com a chave";
                    description = "Cole a chave de recuperacao gerada no ultimo bloqueio para restaurar o cofre.";
                    hint = "A chave deve ser informada por completo.";
                    primaryLabel = "Chave de recuperacao";
                    primaryPlaceholder = "Cole a chave completa";
                    primaryType = string.Empty;
                    primaryTag = "textarea";
                    primaryCssClass = "field-input field-textarea field-mono";
                    actionText = "Validar chave";
                    break;
            }

            string confirmField = showConfirm
                ? $$"""
<div class="field">
  <label class="field-label" for="confirm">{{Encode(confirmLabel)}}</label>
  <input id="confirm" class="field-input" type="{{Encode(confirmType)}}" placeholder="{{Encode(confirmPlaceholder)}}" autocomplete="off">
</div>
"""
                : string.Empty;

            string primaryField = primaryTag == "textarea"
                ? $$"""<textarea id="primary" class="{{primaryCssClass}}" placeholder="{{Encode(primaryPlaceholder)}}" spellcheck="false"></textarea>"""
                : $$"""<input id="primary" class="{{primaryCssClass}}" type="{{Encode(primaryType)}}" placeholder="{{Encode(primaryPlaceholder)}}" autocomplete="off">""";

            return $$"""
<!DOCTYPE html>
<html lang="pt-BR">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <style>
    :root {
      --bg: #0d1117;
      --surface: #161b22;
      --surface-soft: #11161e;
      --border: #21262d;
      --border-strong: #30363d;
      --text: #e6edf3;
      --text-muted: #8b949e;
      --text-soft: #6e7681;
      --blue: #58a6ff;
      --green: #3fb950;
      --red: #f85149;
      --font-ui: "Segoe UI Variable Text", "Segoe UI", system-ui, sans-serif;
      --font-title: "Segoe UI Variable Display", "Segoe UI Semibold", "Segoe UI", sans-serif;
      --font-mono: "Cascadia Mono", Consolas, "Courier New", monospace;
    }

    * { box-sizing: border-box; }

    html, body {
      margin: 0;
      min-height: 100%;
      background:
        radial-gradient(circle at top right, rgba(56, 139, 253, 0.08), transparent 22%),
        radial-gradient(circle at bottom left, rgba(63, 185, 80, 0.05), transparent 24%),
        var(--bg);
      color: var(--text);
      font-family: var(--font-ui);
      text-rendering: optimizeLegibility;
      -webkit-font-smoothing: antialiased;
      font-synthesis: none;
      overflow: hidden;
    }

    body {
      display: flex;
      min-height: 100vh;
    }

    button, input, textarea {
      font: inherit;
      outline: none;
    }

    .dialog-root {
      display: flex;
      flex-direction: column;
      min-height: 100vh;
      width: 100%;
      border: 1px solid var(--border);
      background: rgba(13, 17, 23, 0.96);
    }

    .dialog-titlebar {
      height: 40px;
      background: var(--surface);
      border-bottom: 1px solid var(--border);
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 0 16px;
      user-select: none;
    }

    .titlebar-left {
      display: flex;
      align-items: center;
      gap: 8px;
    }

    .appmark {
      width: 16px;
      height: 16px;
      display: flex;
      align-items: center;
      justify-content: center;
      color: var(--blue);
    }

    .appname {
      font-family: var(--font-title);
      font-size: 12px;
      font-weight: 600;
      color: var(--blue);
      letter-spacing: 0.02em;
    }

    .close-btn {
      width: 28px;
      height: 28px;
      border: 0;
      border-radius: 8px;
      background: transparent;
      color: var(--text-muted);
      cursor: pointer;
      transition: background 0.14s ease, color 0.14s ease;
    }

    .close-btn:hover {
      background: rgba(248, 81, 73, 0.12);
      color: var(--red);
    }

    .dialog-shell {
      flex: 1;
      padding: 28px;
      display: flex;
      align-items: center;
      justify-content: center;
    }

    .dialog-card {
      width: 100%;
      max-width: 560px;
      background: rgba(22, 27, 34, 0.96);
      border: 1px solid var(--border);
      border-radius: 14px;
      overflow: hidden;
      box-shadow: 0 24px 60px rgba(0, 0, 0, 0.24);
    }

    .card-header {
      padding: 22px 24px 18px;
      border-bottom: 1px solid var(--border);
    }

    .eyebrow {
      margin-bottom: 6px;
      color: var(--blue);
      font-family: var(--font-mono);
      font-size: 10px;
      letter-spacing: 0.1em;
      text-transform: uppercase;
    }

    .title {
      margin: 0 0 8px;
      color: var(--text);
      font-family: var(--font-title);
      font-size: 24px;
      font-weight: 700;
      line-height: 1.12;
      letter-spacing: -0.02em;
    }

    .description {
      margin: 0;
      color: var(--text-muted);
      font-size: 13px;
      line-height: 1.5;
    }

    .card-body {
      padding: 24px;
    }

    .error-banner {
      display: none;
      margin-bottom: 16px;
      padding: 11px 13px;
      border-radius: 10px;
      border: 1px solid rgba(248, 81, 73, 0.3);
      background: rgba(248, 81, 73, 0.1);
      color: #ffb1ac;
      font-size: 12px;
      line-height: 1.45;
    }

    .error-banner.visible {
      display: block;
    }

    .fields {
      display: grid;
      gap: 16px;
    }

    .field {
      display: grid;
      gap: 8px;
    }

    .field-label {
      color: var(--text-muted);
      font-size: 12px;
      font-weight: 500;
    }

    .field-input {
      width: 100%;
      min-height: 42px;
      padding: 0 14px;
      border-radius: 10px;
      border: 1px solid var(--border-strong);
      background: var(--surface-soft);
      color: var(--text);
      transition: border-color 0.14s ease, box-shadow 0.14s ease, background 0.14s ease;
    }

    .field-input::placeholder {
      color: #68707c;
    }

    .field-input:focus {
      border-color: rgba(88, 166, 255, 0.6);
      box-shadow: 0 0 0 3px rgba(88, 166, 255, 0.12);
      background: #101720;
    }

    .field-textarea {
      min-height: 108px;
      padding: 12px 14px;
      resize: none;
      line-height: 1.5;
    }

    .field-mono {
      font-family: var(--font-mono);
      font-size: 12px;
    }

    .hint {
      margin-top: 14px;
      color: var(--text-soft);
      font-size: 12px;
      line-height: 1.45;
    }

    .card-footer {
      display: flex;
      justify-content: flex-end;
      gap: 10px;
      padding: 18px 24px 24px;
      border-top: 1px solid var(--border);
      background: rgba(17, 22, 30, 0.55);
    }

    .btn {
      appearance: none;
      min-height: 34px;
      padding: 7px 14px;
      border-radius: 8px;
      border: 1px solid;
      cursor: pointer;
      font-size: 12px;
      font-weight: 500;
      transition: background 0.14s ease, border-color 0.14s ease, color 0.14s ease, transform 0.08s ease;
    }

    .btn:hover {
      transform: translateY(-1px);
    }

    .btn:active {
      transform: translateY(0);
    }

    .btn-secondary {
      background: transparent;
      border-color: var(--border-strong);
      color: var(--text-muted);
    }

    .btn-secondary:hover {
      border-color: var(--blue);
      background: rgba(88, 166, 255, 0.05);
      color: var(--blue);
    }

    .btn-primary {
      background: rgba(88, 166, 255, 0.12);
      border-color: rgba(88, 166, 255, 0.36);
      color: var(--blue);
    }

    .btn-primary:hover {
      background: rgba(88, 166, 255, 0.2);
    }
  </style>
</head>
<body>
  <div class="dialog-root">
    <div class="dialog-titlebar" data-action="drag-window">
      <div class="titlebar-left">
        <span class="appmark">
          <svg width="16" height="16" viewBox="0 0 16 16" fill="none">
            <rect x="2" y="5" width="12" height="9" rx="2" stroke="#58a6ff" stroke-width="1"/>
            <path d="M5 5V4a3 3 0 016 0v1" stroke="#58a6ff" stroke-width="1" stroke-linecap="round"/>
            <circle cx="8" cy="9.5" r="1.5" fill="#58a6ff"/>
            <path d="M8 11v1.5" stroke="#58a6ff" stroke-width="1" stroke-linecap="round"/>
          </svg>
        </span>
        <span class="appname">TremauxLock Vault</span>
      </div>
      <button type="button" class="close-btn" data-action="close" aria-label="Fechar">×</button>
    </div>

    <div class="dialog-shell">
      <div class="dialog-card">
        <div class="card-header">
          <div class="eyebrow">{{Encode(eyebrow)}}</div>
          <h1 class="title">{{Encode(title)}}</h1>
          <p class="description">{{Encode(description)}}</p>
        </div>

        <div class="card-body">
          <div id="error" class="error-banner"></div>

          <form id="credential-form" class="fields">
            <div class="field">
              <label class="field-label" for="primary">{{Encode(primaryLabel)}}</label>
              {{primaryField}}
            </div>

            {{confirmField}}
          </form>

          <div class="hint">{{Encode(hint)}}</div>
        </div>

        <div class="card-footer">
          <button type="button" class="btn btn-secondary" data-action="cancel">Cancelar</button>
          <button type="button" class="btn btn-primary" id="submit-btn">{{Encode(actionText)}}</button>
        </div>
      </div>
    </div>
  </div>

  <script>
    function send(message) {
      if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(message);
      }
    }

    window.applyCredentialError = function (message) {
      var errorBox = document.getElementById('error');
      errorBox.textContent = message || '';
      errorBox.classList.toggle('visible', !!message);
    };

    function submitForm() {
      window.applyCredentialError('');
      var payload = {
        action: 'submit',
        primary: document.getElementById('primary').value,
        confirm: document.getElementById('confirm') ? document.getElementById('confirm').value : ''
      };
      send(JSON.stringify(payload));
    }

    document.addEventListener('DOMContentLoaded', function () {
      document.querySelectorAll('[data-action]').forEach(function (element) {
        element.addEventListener('click', function () {
          var action = element.getAttribute('data-action');
          if (action) {
            send(action);
          }
        });
      });

      document.getElementById('submit-btn').addEventListener('click', submitForm);
      document.getElementById('credential-form').addEventListener('submit', function (event) {
        event.preventDefault();
        submitForm();
      });

      document.addEventListener('keydown', function (event) {
        if (event.key === 'Enter' && !event.shiftKey) {
          var isTextArea = document.activeElement && document.activeElement.tagName === 'TEXTAREA';
          if (!isTextArea) {
            event.preventDefault();
            submitForm();
          }
        }
      });

      var titlebar = document.querySelector('.dialog-titlebar');
      titlebar.addEventListener('mousedown', function (event) {
        if (!event.target.closest('.close-btn')) {
          send('drag-window');
        }
      });

      var primary = document.getElementById('primary');
      if (primary) {
        primary.focus();
      }
    });
  </script>
</body>
</html>
""";
        }

        public static string BuildRecovery(string recoveryKey, int fileCount, long totalBytes, string? backupWarning)
        {
            string summary = $"{fileCount} arquivo(s) foram protegidos, totalizando {VaultCrypto.FormatSize(totalBytes)}.";
            string note = backupWarning ?? "Salve a chave fora desta pasta para nao depender do executavel atual.";
            string noteClass = backupWarning == null ? "note" : "note warning";

            return $$"""
<!DOCTYPE html>
<html lang="pt-BR">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <style>
    :root {
      --bg: #0d1117;
      --surface: #161b22;
      --surface-soft: #11161e;
      --border: #21262d;
      --border-strong: #30363d;
      --text: #e6edf3;
      --text-muted: #8b949e;
      --text-soft: #6e7681;
      --blue: #58a6ff;
      --orange: #e1b25e;
      --green: #3fb950;
      --font-ui: "Segoe UI Variable Text", "Segoe UI", system-ui, sans-serif;
      --font-title: "Segoe UI Variable Display", "Segoe UI Semibold", "Segoe UI", sans-serif;
      --font-mono: "Cascadia Mono", Consolas, "Courier New", monospace;
    }

    * { box-sizing: border-box; }

    html, body {
      margin: 0;
      min-height: 100%;
      background:
        radial-gradient(circle at top right, rgba(56, 139, 253, 0.08), transparent 24%),
        radial-gradient(circle at bottom left, rgba(63, 185, 80, 0.05), transparent 24%),
        var(--bg);
      color: var(--text);
      font-family: var(--font-ui);
      text-rendering: optimizeLegibility;
      -webkit-font-smoothing: antialiased;
      font-synthesis: none;
      overflow: hidden;
    }

    body {
      display: flex;
      min-height: 100vh;
    }

    button { font: inherit; }

    .dialog-root {
      display: flex;
      flex-direction: column;
      min-height: 100vh;
      width: 100%;
      border: 1px solid var(--border);
      background: rgba(13, 17, 23, 0.96);
    }

    .dialog-titlebar {
      height: 40px;
      background: var(--surface);
      border-bottom: 1px solid var(--border);
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 0 16px;
      user-select: none;
    }

    .titlebar-left {
      display: flex;
      align-items: center;
      gap: 8px;
    }

    .appname {
      font-family: var(--font-title);
      font-size: 12px;
      font-weight: 600;
      color: var(--blue);
      letter-spacing: 0.02em;
    }

    .close-btn {
      width: 28px;
      height: 28px;
      border: 0;
      border-radius: 8px;
      background: transparent;
      color: var(--text-muted);
      cursor: pointer;
      transition: background 0.14s ease, color 0.14s ease;
    }

    .close-btn:hover {
      background: rgba(248, 81, 73, 0.12);
      color: #f85149;
    }

    .dialog-shell {
      flex: 1;
      padding: 28px;
      display: flex;
      align-items: center;
      justify-content: center;
    }

    .dialog-card {
      width: 100%;
      max-width: 620px;
      background: rgba(22, 27, 34, 0.96);
      border: 1px solid var(--border);
      border-radius: 14px;
      overflow: hidden;
      box-shadow: 0 24px 60px rgba(0, 0, 0, 0.24);
    }

    .card-header {
      padding: 22px 24px 18px;
      border-bottom: 1px solid var(--border);
    }

    .eyebrow {
      margin-bottom: 6px;
      color: var(--blue);
      font-family: var(--font-mono);
      font-size: 10px;
      letter-spacing: 0.1em;
      text-transform: uppercase;
    }

    .title {
      margin: 0 0 8px;
      color: var(--text);
      font-family: var(--font-title);
      font-size: 24px;
      font-weight: 700;
      line-height: 1.12;
      letter-spacing: -0.02em;
    }

    .summary {
      margin: 0;
      color: var(--text-muted);
      font-size: 13px;
      line-height: 1.5;
    }

    .card-body {
      padding: 24px;
      display: grid;
      gap: 16px;
    }

    .notice {
      display: none;
      padding: 11px 13px;
      border-radius: 10px;
      border: 1px solid rgba(63, 185, 80, 0.28);
      background: rgba(63, 185, 80, 0.1);
      color: #8ddf9a;
      font-size: 12px;
      line-height: 1.45;
    }

    .notice.visible {
      display: block;
    }

    .key-block {
      border: 1px solid var(--border);
      border-radius: 12px;
      overflow: hidden;
      background: var(--surface-soft);
    }

    .key-label {
      padding: 14px 16px 10px;
      color: var(--text-soft);
      font-family: var(--font-mono);
      font-size: 10px;
      letter-spacing: 0.08em;
      text-transform: uppercase;
      border-bottom: 1px solid var(--border);
    }

    .key-value {
      padding: 16px;
      color: var(--text);
      font-family: var(--font-mono);
      font-size: 13px;
      line-height: 1.6;
      word-break: break-word;
      user-select: text;
    }

    .note {
      padding: 12px 14px;
      border-radius: 10px;
      border: 1px solid rgba(88, 166, 255, 0.18);
      background: rgba(88, 166, 255, 0.07);
      color: var(--text-soft);
      font-size: 12px;
      line-height: 1.5;
    }

    .note.warning {
      border-color: rgba(225, 178, 94, 0.28);
      background: rgba(225, 178, 94, 0.08);
      color: #efc982;
    }

    .card-footer {
      display: flex;
      justify-content: flex-end;
      gap: 10px;
      padding: 18px 24px 24px;
      border-top: 1px solid var(--border);
      background: rgba(17, 22, 30, 0.55);
    }

    .btn {
      appearance: none;
      min-height: 34px;
      padding: 7px 14px;
      border-radius: 8px;
      border: 1px solid;
      cursor: pointer;
      font-size: 12px;
      font-weight: 500;
      transition: background 0.14s ease, border-color 0.14s ease, color 0.14s ease, transform 0.08s ease;
    }

    .btn:hover { transform: translateY(-1px); }
    .btn:active { transform: translateY(0); }

    .btn-secondary {
      background: transparent;
      border-color: var(--border-strong);
      color: var(--text-muted);
    }

    .btn-secondary:hover {
      border-color: var(--blue);
      background: rgba(88, 166, 255, 0.05);
      color: var(--blue);
    }

    .btn-primary {
      background: rgba(88, 166, 255, 0.12);
      border-color: rgba(88, 166, 255, 0.36);
      color: var(--blue);
    }

    .btn-primary:hover {
      background: rgba(88, 166, 255, 0.2);
    }
  </style>
</head>
<body>
  <div class="dialog-root">
    <div class="dialog-titlebar" data-action="drag-window">
      <div class="titlebar-left">
        <span class="appname">TremauxLock Vault</span>
      </div>
      <button type="button" class="close-btn" data-action="close" aria-label="Fechar">×</button>
    </div>

    <div class="dialog-shell">
      <div class="dialog-card">
        <div class="card-header">
          <div class="eyebrow">Recovery Key</div>
          <h1 class="title">Guarde esta chave antes de fechar</h1>
          <p class="summary">{{Encode(summary)}}</p>
        </div>

        <div class="card-body">
          <div id="notice" class="notice"></div>

          <div class="key-block">
            <div class="key-label">Chave de recuperacao</div>
            <div class="key-value" id="key-value">{{Encode(recoveryKey)}}</div>
          </div>

          <div class="{{noteClass}}">{{Encode(note)}}</div>
        </div>

        <div class="card-footer">
          <button type="button" class="btn btn-secondary" data-action="save">Salvar arquivo</button>
          <button type="button" class="btn btn-primary" data-action="copy">Copiar chave</button>
          <button type="button" class="btn btn-secondary" data-action="close">Fechar</button>
        </div>
      </div>
    </div>
  </div>

  <script>
    function send(action) {
      if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(action);
      }
    }

    window.applyRecoveryNotice = function (message, isWarning) {
      var notice = document.getElementById('notice');
      notice.textContent = message || '';
      notice.classList.toggle('visible', !!message);
      notice.style.borderColor = isWarning ? 'rgba(225, 178, 94, 0.28)' : 'rgba(63, 185, 80, 0.28)';
      notice.style.background = isWarning ? 'rgba(225, 178, 94, 0.08)' : 'rgba(63, 185, 80, 0.1)';
      notice.style.color = isWarning ? '#efc982' : '#8ddf9a';
    };

    document.addEventListener('DOMContentLoaded', function () {
      document.querySelectorAll('[data-action]').forEach(function (element) {
        element.addEventListener('click', function () {
          var action = element.getAttribute('data-action');
          if (action) {
            send(action);
          }
        });
      });

      var titlebar = document.querySelector('.dialog-titlebar');
      titlebar.addEventListener('mousedown', function (event) {
        if (!event.target.closest('.close-btn')) {
          send('drag-window');
        }
      });
    });
  </script>
</body>
</html>
""";
        }

        private static string Encode(string value) => WebUtility.HtmlEncode(value ?? string.Empty);
    }
}
