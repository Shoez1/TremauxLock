using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace TremauxLock
{
    internal sealed class VaultRenderState
    {
        public string AppName { get; init; } = string.Empty;
        public string Eyebrow { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Subtitle { get; init; } = string.Empty;
        public string StatusText { get; init; } = string.Empty;
        public string StatusTone { get; init; } = "info";
        public string FilesValue { get; init; } = string.Empty;
        public string SizeValue { get; init; } = string.Empty;
        public string VisibilityText { get; init; } = string.Empty;
        public string VisibilityTone { get; init; } = "info";
        public string PanelLabel { get; init; } = string.Empty;
        public string PanelTitle { get; init; } = string.Empty;
        public string PathLabel { get; init; } = string.Empty;
        public string PathValue { get; init; } = string.Empty;
        public string PathHint { get; init; } = string.Empty;
        public string SecondaryText { get; init; } = string.Empty;
        public string SecondaryAction { get; init; } = string.Empty;
        public bool SecondaryEnabled { get; init; }
        public string PrimaryText { get; init; } = string.Empty;
        public string PrimaryAction { get; init; } = string.Empty;
        public string PrimaryKind { get; init; } = "ghost";
        public bool PrimaryEnabled { get; init; }
        public string? TertiaryText { get; init; }
        public string? TertiaryAction { get; init; }
        public bool TertiaryEnabled { get; init; }
        public string? NoticeText { get; init; }
        public int NoticePercent { get; init; }
        public string NoticeTone { get; init; } = "info";
        public string ContentHtml { get; init; } = string.Empty;
        public string FooterLeft { get; init; } = string.Empty;
        public string FooterCenter { get; init; } = string.Empty;
        public string FooterRight { get; init; } = string.Empty;
    }

    internal sealed class VaultFileRow
    {
        public string Name { get; init; } = string.Empty;
        public string Meta { get; init; } = string.Empty;
        public string IconText { get; init; } = "?";
    }

    internal static class VaultHtmlBuilder
    {
        private const string Template = """
<!DOCTYPE html>
<html lang="pt-BR">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <style>
    :root {
      --bg: #0d1117;
      --surface: #161b22;
      --border: #21262d;
      --border-strong: #30363d;
      --text: #e6edf3;
      --text-muted: #8b949e;
      --text-soft: #484f58;
      --blue: #58a6ff;
      --green: #3fb950;
      --orange: #e1b25e;
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
        radial-gradient(circle at top right, rgba(56, 139, 253, 0.09), transparent 24%),
        radial-gradient(circle at bottom left, rgba(63, 185, 80, 0.06), transparent 22%),
        var(--bg);
      color: var(--text);
      font-family: var(--font-ui);
      overflow: hidden;
      text-rendering: optimizeLegibility;
      -webkit-font-smoothing: antialiased;
      font-synthesis: none;
    }

    button {
      font: inherit;
      outline: none;
    }

    .vault-root {
      min-height: 100vh;
      display: flex;
      flex-direction: column;
      background: rgba(13, 17, 23, 0.96);
      border: 1px solid var(--border);
    }

    .vault-titlebar {
      background: var(--surface);
      border-bottom: 1px solid var(--border);
      padding: 0 16px;
      height: 40px;
      display: flex;
      align-items: center;
      justify-content: space-between;
      user-select: none;
    }

    .vault-titlebar-left {
      display: flex;
      align-items: center;
      gap: 8px;
    }

    .vault-icon {
      width: 16px;
      height: 16px;
      flex: 0 0 auto;
    }

    .vault-appname {
      font-family: var(--font-title);
      font-size: 12px;
      font-weight: 600;
      color: var(--blue);
      letter-spacing: 0.02em;
    }

    .titlebar-controls {
      display: flex;
      gap: 6px;
    }

    .ctrl-btn {
      width: 12px;
      height: 12px;
      border-radius: 999px;
      border: 0;
      padding: 0;
      cursor: pointer;
      opacity: 0.95;
      transition: transform 0.12s ease, opacity 0.12s ease;
    }

    .ctrl-btn:hover {
      transform: scale(1.05);
      opacity: 1;
    }

    .ctrl-btn:active {
      transform: scale(0.95);
    }

    .ctrl-close { background: #ff5f57; }
    .ctrl-min { background: #febc2e; }
    .ctrl-max { background: #28c840; }

    .vault-header {
      background: rgba(22, 27, 34, 0.94);
      border-bottom: 1px solid var(--border);
      padding: 22px 28px;
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 20px;
    }

    .vault-label {
      font-family: var(--font-mono);
      font-size: 10px;
      font-weight: 500;
      color: var(--blue);
      letter-spacing: 0.1em;
      text-transform: uppercase;
      margin-bottom: 4px;
    }

    .vault-title {
      font-family: var(--font-title);
      font-size: 25px;
      font-weight: 700;
      color: var(--text);
      margin: 0 0 8px;
      line-height: 1.12;
      letter-spacing: -0.02em;
    }

    .vault-subtitle {
      margin: 0;
      font-size: 13px;
      line-height: 1.45;
      color: #7d8590;
      max-width: 540px;
    }

    .status-pill {
      display: inline-flex;
      align-items: center;
      gap: 8px;
      border-radius: 999px;
      padding: 8px 15px;
      border: 1px solid;
      flex-shrink: 0;
    }

    .status-pill.success {
      background: rgba(35, 134, 54, 0.15);
      border-color: rgba(35, 134, 54, 0.4);
    }

    .status-pill.warning {
      background: rgba(225, 178, 94, 0.12);
      border-color: rgba(225, 178, 94, 0.34);
    }

    .status-pill.info {
      background: rgba(88, 166, 255, 0.1);
      border-color: rgba(88, 166, 255, 0.32);
    }

    .status-pill.danger {
      background: rgba(248, 81, 73, 0.12);
      border-color: rgba(248, 81, 73, 0.35);
    }

    .status-dot {
      width: 7px;
      height: 7px;
      border-radius: 50%;
    }

    .status-pill.success .status-dot {
      background: var(--green);
      box-shadow: 0 0 0 2px rgba(63, 185, 80, 0.25);
    }

    .status-pill.warning .status-dot {
      background: var(--orange);
      box-shadow: 0 0 0 2px rgba(225, 178, 94, 0.24);
    }

    .status-pill.info .status-dot {
      background: var(--blue);
      box-shadow: 0 0 0 2px rgba(88, 166, 255, 0.24);
    }

    .status-pill.danger .status-dot {
      background: var(--red);
      box-shadow: 0 0 0 2px rgba(248, 81, 73, 0.24);
    }

    .status-text {
      font-family: var(--font-mono);
      font-size: 11px;
      font-weight: 500;
      letter-spacing: 0.04em;
      text-transform: uppercase;
    }

    .status-pill.success .status-text { color: var(--green); }
    .status-pill.warning .status-text { color: var(--orange); }
    .status-pill.info .status-text { color: var(--blue); }
    .status-pill.danger .status-text { color: var(--red); }

    .vault-body {
      flex: 1;
      min-height: 0;
      padding: 24px 28px;
      display: grid;
      grid-template-columns: 220px 1fr;
      gap: 20px;
    }

    .stats-card,
    .main-panel {
      background: rgba(22, 27, 34, 0.95);
      border: 1px solid var(--border);
      border-radius: 10px;
      min-height: 0;
    }

    .stats-card {
      padding: 16px;
    }

    .stat-row {
      margin-bottom: 14px;
    }

    .stat-row:last-child {
      margin-bottom: 0;
    }

    .stat-label,
    .panel-label,
    .path-label {
      font-family: var(--font-mono);
      font-size: 10px;
      font-weight: 500;
      color: var(--text-soft);
      letter-spacing: 0.08em;
      text-transform: uppercase;
      margin-bottom: 4px;
    }

    .stat-value {
      font-family: var(--font-title);
      font-size: 21px;
      font-weight: 700;
      color: var(--text);
      line-height: 1;
      letter-spacing: -0.01em;
    }

    .stat-value.accent { color: var(--blue); }
    .stat-value.small {
      font-size: 14px;
      font-weight: 500;
      color: #7d8590;
      font-family: var(--font-mono);
    }

    .stat-badge {
      display: inline-flex;
      align-items: center;
      gap: 5px;
      border-radius: 6px;
      padding: 4px 8px;
      border: 1px solid;
      font-family: var(--font-mono);
      font-size: 11px;
      font-weight: 500;
    }

    .stat-badge.success {
      color: var(--green);
      background: rgba(63, 185, 80, 0.1);
      border-color: rgba(63, 185, 80, 0.25);
    }

    .stat-badge.warning {
      color: var(--orange);
      background: rgba(225, 178, 94, 0.1);
      border-color: rgba(225, 178, 94, 0.25);
    }

    .stat-badge.info {
      color: var(--blue);
      background: rgba(88, 166, 255, 0.08);
      border-color: rgba(88, 166, 255, 0.2);
    }

    .stat-badge.danger {
      color: var(--red);
      background: rgba(248, 81, 73, 0.1);
      border-color: rgba(248, 81, 73, 0.25);
    }

    .stat-badge-dot {
      width: 5px;
      height: 5px;
      border-radius: 999px;
      background: currentColor;
    }

    .divider {
      height: 1px;
      background: var(--border);
      margin: 14px 0;
    }

    .main-panel {
      display: flex;
      flex-direction: column;
      overflow: hidden;
    }

    .main-panel-header {
      padding: 16px 20px;
      border-bottom: 1px solid var(--border);
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 16px;
    }

    .panel-title {
      font-family: var(--font-title);
      font-size: 15px;
      font-weight: 600;
      color: var(--text);
      margin: 0;
    }

    .btn-group {
      display: flex;
      align-items: center;
      gap: 8px;
      flex-wrap: wrap;
      justify-content: flex-end;
    }

    .btn {
      appearance: none;
      border-radius: 8px;
      padding: 7px 14px;
      min-height: 32px;
      border: 1px solid;
      cursor: pointer;
      transition: background 0.14s ease, border-color 0.14s ease, color 0.14s ease, transform 0.08s ease;
      font-size: 12px;
      font-weight: 500;
      letter-spacing: 0;
      line-height: 1;
      white-space: nowrap;
    }

    .btn:hover { transform: translateY(-1px); }
    .btn:active { transform: translateY(0); }

    .btn[disabled] {
      opacity: 0.42;
      cursor: default;
      transform: none;
    }

    .btn-ghost {
      background: transparent;
      border-color: var(--border-strong);
      color: var(--text-muted);
    }

    .btn-ghost:not([disabled]):hover {
      border-color: var(--blue);
      color: var(--blue);
      background: rgba(88, 166, 255, 0.05);
    }

    .btn-danger {
      background: rgba(248, 81, 73, 0.1);
      border-color: rgba(248, 81, 73, 0.4);
      color: var(--red);
    }

    .btn-danger:not([disabled]):hover {
      background: rgba(248, 81, 73, 0.18);
    }

    .btn-primary {
      background: rgba(88, 166, 255, 0.12);
      border-color: rgba(88, 166, 255, 0.38);
      color: var(--blue);
    }

    .btn-primary:not([disabled]):hover {
      background: rgba(88, 166, 255, 0.18);
    }

    .path-display {
      padding: 16px 20px;
      border-bottom: 1px solid var(--border);
    }

    .path-box {
      background: var(--bg);
      border: 1px solid var(--border);
      border-radius: 8px;
      padding: 10px 14px;
      display: flex;
      align-items: center;
      gap: 8px;
    }

    .path-icon {
      color: var(--text-soft);
      display: flex;
      align-items: center;
      justify-content: center;
      flex: 0 0 auto;
    }

    .path-text {
      font-family: var(--font-mono);
      font-size: 11px;
      color: var(--text-muted);
      line-height: 1.5;
      display: block;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
      width: 100%;
    }

    .path-text span { color: var(--blue); }

    .path-hint {
      font-size: 12px;
      color: var(--text-soft);
      margin-top: 8px;
      display: flex;
      align-items: center;
      gap: 6px;
    }

    .path-hint-icon {
      flex: 0 0 auto;
      display: inline-flex;
    }

    .notice-strip {
      padding: 14px 20px 12px;
      border-bottom: 1px solid var(--border);
    }

    .notice-strip.info { background: rgba(88, 166, 255, 0.03); }
    .notice-strip.warning { background: rgba(225, 178, 94, 0.05); }
    .notice-strip.danger { background: rgba(248, 81, 73, 0.05); }

    .notice-text {
      font-family: var(--font-mono);
      font-size: 11px;
      color: var(--text-muted);
      margin-bottom: 8px;
      letter-spacing: 0.03em;
      text-transform: uppercase;
    }

    .notice-bar {
      height: 4px;
      width: 100%;
      background: var(--border);
      border-radius: 999px;
      overflow: hidden;
    }

    .notice-fill {
      height: 100%;
      width: 0;
      border-radius: 999px;
      background: var(--blue);
    }

    .notice-strip.warning .notice-fill { background: var(--orange); }
    .notice-strip.danger .notice-fill { background: var(--red); }

    .content-scroll {
      flex: 1;
      min-height: 0;
      overflow: auto;
    }

    .content-scroll::-webkit-scrollbar {
      width: 10px;
      height: 10px;
    }

    .content-scroll::-webkit-scrollbar-thumb {
      background: #1f2630;
      border: 2px solid var(--surface);
      border-radius: 999px;
    }

    .empty-state {
      padding: 40px 20px;
      min-height: 240px;
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: 10px;
    }

    .empty-icon {
      width: 40px;
      height: 40px;
      border-radius: 10px;
      display: flex;
      align-items: center;
      justify-content: center;
      margin-bottom: 4px;
      border: 1px solid rgba(88, 166, 255, 0.2);
      background: rgba(88, 166, 255, 0.08);
    }

    .empty-icon.warning {
      border-color: rgba(225, 178, 94, 0.2);
      background: rgba(225, 178, 94, 0.08);
    }

    .empty-icon.danger {
      border-color: rgba(248, 81, 73, 0.2);
      background: rgba(248, 81, 73, 0.08);
    }

    .empty-title {
      font-family: var(--font-title);
      font-size: 14px;
      font-weight: 600;
      color: #6e7681;
    }

    .empty-desc {
      max-width: 260px;
      text-align: center;
      font-size: 12px;
      line-height: 1.5;
      color: var(--text-soft);
    }

    .file-row {
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 12px 20px;
      border-bottom: 1px solid var(--border);
      transition: background 0.12s ease;
    }

    .file-row:hover {
      background: rgba(88, 166, 255, 0.04);
    }

    .file-icon-wrap {
      width: 32px;
      height: 32px;
      border-radius: 8px;
      background: rgba(88, 166, 255, 0.08);
      border: 1px solid rgba(88, 166, 255, 0.2);
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
      color: var(--blue);
      font-family: var(--font-mono);
      font-size: 12px;
      font-weight: 500;
    }

    .file-copy {
      min-width: 0;
      flex: 1;
    }

    .file-name {
      font-size: 13px;
      font-weight: 500;
      color: var(--text);
      margin-bottom: 2px;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .file-meta {
      font-family: var(--font-mono);
      font-size: 11px;
      color: var(--text-soft);
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .vault-footer {
      padding: 12px 28px;
      border-top: 1px solid var(--border);
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 20px;
      background: rgba(13, 17, 23, 0.96);
    }

    .footer-info {
      font-family: var(--font-mono);
      font-size: 10px;
      color: var(--text-soft);
      letter-spacing: 0.04em;
      white-space: nowrap;
    }

    @media (max-width: 920px) {
      .vault-body {
        grid-template-columns: 1fr;
      }

      .vault-footer {
        flex-wrap: wrap;
      }
    }
  </style>
</head>
<body>
  <div class="vault-root">
    <div class="vault-titlebar" data-action="drag-window">
      <div class="vault-titlebar-left">
        <svg class="vault-icon" viewBox="0 0 16 16" fill="none">
          <rect x="2" y="5" width="12" height="9" rx="2" stroke="#58a6ff" stroke-width="1"/>
          <path d="M5 5V4a3 3 0 016 0v1" stroke="#58a6ff" stroke-width="1" stroke-linecap="round"/>
          <circle cx="8" cy="9.5" r="1.5" fill="#58a6ff"/>
          <path d="M8 11v1.5" stroke="#58a6ff" stroke-width="1" stroke-linecap="round"/>
        </svg>
        <span class="vault-appname">__APP_NAME__</span>
      </div>
      <div class="titlebar-controls">
        <button type="button" class="ctrl-btn ctrl-min" data-action="minimize" aria-label="Minimizar"></button>
        <button type="button" class="ctrl-btn ctrl-max" data-action="toggle-maximize" aria-label="Maximizar"></button>
        <button type="button" class="ctrl-btn ctrl-close" data-action="close" aria-label="Fechar"></button>
      </div>
    </div>

    <div class="vault-header">
      <div class="vault-header-left">
        <div class="vault-label">__EYEBROW__</div>
        <h1 class="vault-title">__TITLE__</h1>
        <p class="vault-subtitle">__SUBTITLE__</p>
      </div>
      <div class="status-pill __STATUS_TONE__">
        <div class="status-dot"></div>
        <span class="status-text">__STATUS_TEXT__</span>
      </div>
    </div>

    <div class="vault-body">
      <div class="stats-card">
        <div class="stat-row">
          <div class="stat-label">Arquivos</div>
          <div class="stat-value accent">__FILES_VALUE__</div>
        </div>
        <div class="divider"></div>
        <div class="stat-row">
          <div class="stat-label">Tamanho</div>
          <div class="stat-value small">__SIZE_VALUE__</div>
        </div>
        <div class="divider"></div>
        <div class="stat-row">
          <div class="stat-label">Visibilidade</div>
          <div class="stat-badge __VISIBILITY_TONE__">
            <div class="stat-badge-dot"></div>
            <span>__VISIBILITY_TEXT__</span>
          </div>
        </div>
      </div>

      <div class="main-panel">
        <div class="main-panel-header">
          <div class="panel-title-group">
            <div class="panel-label">__PANEL_LABEL__</div>
            <p class="panel-title">__PANEL_TITLE__</p>
          </div>
          <div class="btn-group">
            __TERTIARY_BUTTON__
            <button type="button" class="btn btn-ghost" data-action="__SECONDARY_ACTION__" __SECONDARY_DISABLED__>__SECONDARY_TEXT__</button>
            <button type="button" class="btn btn-__PRIMARY_KIND__" data-action="__PRIMARY_ACTION__" __PRIMARY_DISABLED__>__PRIMARY_TEXT__</button>
          </div>
        </div>

        <div class="path-display">
          <div class="path-label">__PATH_LABEL__</div>
          <div class="path-box" title="__PATH_TITLE__">
            <span class="path-icon">
              <svg width="14" height="14" viewBox="0 0 14 14" fill="none">
                <path d="M1.5 4.5a1 1 0 011-1h2.586a1 1 0 01.707.293L6.5 5h6a1 1 0 011 1v5a1 1 0 01-1 1h-10a1 1 0 01-1-1V4.5z" stroke="#484f58" stroke-width="1"/>
              </svg>
            </span>
            <span class="path-text">__PATH_HTML__</span>
          </div>
          <div class="path-hint">
            <span class="path-hint-icon">
              <svg width="10" height="10" viewBox="0 0 10 10" fill="none">
                <circle cx="5" cy="5" r="4" stroke="#484f58" stroke-width="1"/>
                <path d="M5 4.5v3M5 3h.01" stroke="#484f58" stroke-width="1" stroke-linecap="round"/>
              </svg>
            </span>
            __PATH_HINT__
          </div>
        </div>

        __NOTICE_SECTION__

        <div class="content-scroll">
          __CONTENT_HTML__
        </div>
      </div>
    </div>

    <div class="vault-footer">
      <span class="footer-info">__FOOTER_LEFT__</span>
      <span class="footer-info">__FOOTER_CENTER__</span>
      <span class="footer-info">__FOOTER_RIGHT__</span>
    </div>
  </div>

  <script>
    function send(action) {
      if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(action);
      }
    }

    document.addEventListener('DOMContentLoaded', function () {
      document.querySelectorAll('[data-action]').forEach(function (element) {
        element.addEventListener('click', function () {
          if (element.hasAttribute('disabled')) {
            return;
          }

          var action = element.getAttribute('data-action');
          if (action) {
            send(action);
          }
        });
      });

      var titlebar = document.querySelector('.vault-titlebar');
      if (titlebar) {
        titlebar.addEventListener('mousedown', function (event) {
          if (!event.target.closest('.ctrl-btn')) {
            send('drag-window');
          }
        });

        titlebar.addEventListener('dblclick', function (event) {
          if (!event.target.closest('.ctrl-btn')) {
            send('toggle-maximize');
          }
        });
      }
    });
  </script>
</body>
</html>
""";

        public static string Build(VaultRenderState state)
        {
            string tertiaryButton = string.IsNullOrWhiteSpace(state.TertiaryText) || string.IsNullOrWhiteSpace(state.TertiaryAction)
                ? string.Empty
                : $"""<button type="button" class="btn btn-ghost" data-action="{Encode(state.TertiaryAction)}" {(state.TertiaryEnabled ? string.Empty : "disabled")}>{Encode(state.TertiaryText)}</button>""";

            string noticeSection = string.IsNullOrWhiteSpace(state.NoticeText)
                ? string.Empty
                : $"""
<div class="notice-strip {Encode(state.NoticeTone)}">
  <div class="notice-text">{Encode(state.NoticeText)}</div>
  <div class="notice-bar"><div class="notice-fill" style="width:{state.NoticePercent}%"></div></div>
</div>
""";

            return Template
                .Replace("__APP_NAME__", Encode(state.AppName))
                .Replace("__EYEBROW__", Encode(state.Eyebrow))
                .Replace("__TITLE__", Encode(state.Title))
                .Replace("__SUBTITLE__", Encode(state.Subtitle))
                .Replace("__STATUS_TONE__", Encode(state.StatusTone))
                .Replace("__STATUS_TEXT__", Encode(state.StatusText))
                .Replace("__FILES_VALUE__", Encode(state.FilesValue))
                .Replace("__SIZE_VALUE__", Encode(state.SizeValue))
                .Replace("__VISIBILITY_TONE__", Encode(state.VisibilityTone))
                .Replace("__VISIBILITY_TEXT__", Encode(state.VisibilityText))
                .Replace("__PANEL_LABEL__", Encode(state.PanelLabel))
                .Replace("__PANEL_TITLE__", Encode(state.PanelTitle))
                .Replace("__TERTIARY_BUTTON__", tertiaryButton)
                .Replace("__SECONDARY_ACTION__", Encode(state.SecondaryAction))
                .Replace("__SECONDARY_DISABLED__", state.SecondaryEnabled ? string.Empty : "disabled")
                .Replace("__SECONDARY_TEXT__", Encode(state.SecondaryText))
                .Replace("__PRIMARY_KIND__", Encode(state.PrimaryKind))
                .Replace("__PRIMARY_ACTION__", Encode(state.PrimaryAction))
                .Replace("__PRIMARY_DISABLED__", state.PrimaryEnabled ? string.Empty : "disabled")
                .Replace("__PRIMARY_TEXT__", Encode(state.PrimaryText))
                .Replace("__PATH_LABEL__", Encode(state.PathLabel))
                .Replace("__PATH_TITLE__", Encode(state.PathValue))
                .Replace("__PATH_HTML__", HighlightPath(state.PathValue))
                .Replace("__PATH_HINT__", Encode(state.PathHint))
                .Replace("__NOTICE_SECTION__", noticeSection)
                .Replace("__CONTENT_HTML__", state.ContentHtml)
                .Replace("__FOOTER_LEFT__", Encode(state.FooterLeft))
                .Replace("__FOOTER_CENTER__", Encode(state.FooterCenter))
                .Replace("__FOOTER_RIGHT__", Encode(state.FooterRight));
        }

        public static string BuildFileRows(IEnumerable<VaultFileRow> rows)
        {
            StringBuilder builder = new StringBuilder();

            foreach (VaultFileRow row in rows)
            {
                builder.Append(
                    $"""
<div class="file-row">
  <div class="file-icon-wrap">{Encode(row.IconText)}</div>
  <div class="file-copy">
    <div class="file-name">{Encode(row.Name)}</div>
    <div class="file-meta">{Encode(row.Meta)}</div>
  </div>
</div>
""");
            }

            return builder.ToString();
        }

        public static string BuildEmptyState(string title, string description, string tone)
        {
            string iconClass = tone switch
            {
                "warning" => "empty-icon warning",
                "danger" => "empty-icon danger",
                _ => "empty-icon"
            };

            string stroke = tone switch
            {
                "warning" => "#e1b25e",
                "danger" => "#f85149",
                _ => "#58a6ff"
            };

            return
                $"""
<div class="empty-state">
  <div class="{iconClass}">
    <svg width="18" height="18" viewBox="0 0 18 18" fill="none">
      <path d="M3 4.5A1.5 1.5 0 014.5 3h2.836a1.5 1.5 0 011.06.44l.914.914A1.5 1.5 0 0010.37 5H13.5A1.5 1.5 0 0115 6.5v7A1.5 1.5 0 0113.5 15h-9A1.5 1.5 0 013 13.5v-9z" stroke="{stroke}" stroke-width="1"/>
    </svg>
  </div>
  <div class="empty-title">{Encode(title)}</div>
  <div class="empty-desc">{Encode(description)}</div>
</div>
""";
        }

        private static string HighlightPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            string normalized = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            int lastSeparator = normalized.LastIndexOf(Path.DirectorySeparatorChar);
            if (lastSeparator < 0 || lastSeparator >= normalized.Length - 1)
            {
                return $"<span>{Encode(normalized)}</span>";
            }

            string prefix = normalized[..(lastSeparator + 1)];
            string leaf = normalized[(lastSeparator + 1)..];
            return Encode(prefix) + "<span>" + Encode(leaf) + "</span>";
        }

        private static string Encode(string value) => WebUtility.HtmlEncode(value ?? string.Empty);
    }
}
