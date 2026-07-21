using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Wpf;

namespace Regexutor.App.Services;

/// <summary>
/// Wraps a WebView2 control to drive the Pi Network JS SDK (Pi.init → Pi.authenticate),
/// then validates the returned access token against https://api.minepi.com/v2/me.
/// </summary>
public sealed class PiAuthService : IDisposable
{
    private readonly WebView2 _webView;
    private readonly HttpClient _http;

    private TaskCompletionSource<bool>? _initTcs;
    private TaskCompletionSource<PiAuthResult>? _authTcs;

    private const string PiApiBase = "https://api.minepi.com/v2";

    private static readonly JsonSerializerOptions s_json = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public PiAuthService(WebView2 webView)
    {
        _webView = webView;
        _http = new HttpClient();
        _webView.WebMessageReceived += OnWebMessageReceived;
    }

    // ───────────────────── public API ─────────────────────

    /// <summary>Ensure the hidden WebView2 is ready and the bridge page is loaded.</summary>
    public async Task EnsureReadyAsync()
    {
        if (_webView.CoreWebView2 is not null)
            return;

        await _webView.EnsureCoreWebView2Async();

        var core = _webView.CoreWebView2
            ?? throw new InvalidOperationException("WebView2 initialization failed — the WebView2 Runtime may not be installed.");

        core.NavigateToString(BridgeHtml);

        // Let the page finish loading. The JS bridge itself polls for the Pi SDK,
        // so a short delay here is enough for the DOM to be ready.
        await Task.Delay(500);
    }

    /// <summary>Call Pi.init() in the browser context.</summary>
    public async Task<bool> InitPiAsync(CancellationToken ct = default)
    {
        await EnsureReadyAsync();

        _initTcs = new TaskCompletionSource<bool>();
        using var reg = ct.Register(() => _initTcs.TrySetResult(false));

        await _webView.CoreWebView2!.ExecuteScriptAsync("window.__piInit()");
        return await _initTcs.Task;
    }

    /// <summary>Call Pi.authenticate(['username']) in the browser context.</summary>
    public async Task<PiAuthResult> AuthenticateAsync(CancellationToken ct = default)
    {
        _authTcs = new TaskCompletionSource<PiAuthResult>();
        using var reg = ct.Register(() =>
            _authTcs.TrySetResult(new PiAuthResult(false, null, null, "Cancelado")));

        await _webView.CoreWebView2!.ExecuteScriptAsync("window.__piAuthenticate()");
        return await _authTcs.Task;
    }

    /// <summary>
    /// Validate the access token by calling GET https://api.minepi.com/v2/me
    /// with an Authorization: Bearer header.  No Pi Network API key is needed for this call.
    /// </summary>
    public async Task<PiSession?> ValidateAsync(string accessToken, CancellationToken ct = default)
    {
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Get, $"{PiApiBase}/me");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
                return null;

            var body = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            var username = root.TryGetProperty("username", out var u) ? u.GetString() : null;
            var uuid = root.TryGetProperty("uuid", out var id) ? id.GetString() : null;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(uuid))
                return null;

            return new PiSession(username, accessToken, uuid);
        }
        catch
        {
            return null;
        }
    }

    // ───────────────────── WebView2 message router ─────────────────────

    private void OnWebMessageReceived(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var json = e.WebMessageAsJson;
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("action", out var actProp))
                return;

            switch (actProp.GetString())
            {
                case "init":
                    var ok = root.TryGetProperty("success", out var s) && s.GetBoolean();
                    _initTcs?.TrySetResult(ok);
                    break;

                case "authenticate":
                    var authOk = root.TryGetProperty("success", out var as2) && as2.GetBoolean();
                    if (authOk)
                    {
                        var token = root.TryGetProperty("accessToken", out var at) ? at.GetString() : null;
                        var userJson = root.TryGetProperty("user", out var u) ? u.GetRawText() : null;
                        _authTcs?.TrySetResult(new PiAuthResult(true, token, userJson, null));
                    }
                    else
                    {
                        var err = root.TryGetProperty("error", out var er) ? er.GetString() : "Error desconocido";
                        _authTcs?.TrySetResult(new PiAuthResult(false, null, null, err));
                    }
                    break;
            }
        }
        catch
        {
            // Ignore malformed messages from the bridge.
        }
    }

    // ───────────────────── Embedded HTML / JS bridge ─────────────────────

    private static string BridgeHtml => """
        <!DOCTYPE html>
        <html>
        <head><meta charset="utf-8"><title>Pi Auth Bridge</title></head>
        <body>
        <script src="https://sdk.minepi.com/pi-sdk.js"></script>
        <script>
            /* ---- helpers ---- */
            function _post(obj) {
                window.chrome.webview.postMessage(JSON.stringify(obj));
            }

            /* Poll until the Pi SDK global is available (max 15 s). */
            function _waitForPi(timeoutMs) {
                return new Promise(function(resolve, reject) {
                    if (typeof Pi !== 'undefined') { resolve(); return; }
                    var start = Date.now();
                    var timer = setInterval(function() {
                        if (typeof Pi !== 'undefined') {
                            clearInterval(timer); resolve();
                        } else if (Date.now() - start > (timeoutMs || 15000)) {
                            clearInterval(timer);
                            reject(new Error('Pi SDK no se cargó (timeout).'));
                        }
                    }, 100);
                });
            }

            /* Called from C#: Pi.init() */
            window.__piInit = async function() {
                try {
                    await _waitForPi(15000);
                    await Pi.init({ version: '2.0' });
                    _post({ action:'init', success:true });
                } catch(e) {
                    _post({ action:'init', success:false, error: e.message || String(e) });
                }
            };

            /* Called from C#: Pi.authenticate() with scope "username" */
            window.__piAuthenticate = async function() {
                try {
                    await _waitForPi(5000);
                    var auth = await Pi.authenticate(['username']);
                    _post({
                        action     : 'authenticate',
                        success    : true,
                        accessToken: auth.accessToken,
                        user       : auth.user
                    });
                } catch(e) {
                    _post({ action:'authenticate', success:false, error: e.message || String(e) });
                }
            };
        </script>
        </body>
        </html>
        """;

    public void Dispose()
    {
        _webView.WebMessageReceived -= OnWebMessageReceived;
        _http.Dispose();
    }
}

// ───────────────────── DTOs ─────────────────────

public sealed record PiAuthResult(bool Success, string? AccessToken, string? UserJson, string? Error);

public sealed record PiSession(string Username, string AccessToken, string Uuid);
