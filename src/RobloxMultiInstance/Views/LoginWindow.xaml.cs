using System.IO;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace RobloxMultiInstance.Views;

public partial class LoginWindow : Window
{
    public string? CapturedCookie { get; private set; }
    public string StartUrl { get; set; } = "https://www.roblox.com/login";
    public string Heading { get; set; } = "Sign in to Roblox";
    public string SubHeading { get; set; } = "Log into the account you want to add. We'll capture the session cookie automatically and close this window. The browser session uses a fresh temp profile that's discarded after.";

    public string? PrefillUsername { get; set; }
    public string? PrefillPassword { get; set; }
    public int? PrefillBirthMonth { get; set; }
    public int? PrefillBirthDay { get; set; }
    public int? PrefillBirthYear { get; set; }
    public string? PrefillGender { get; set; }

    private bool _captured;
    private bool _prefillApplied;
    private string? _profileFolder;

    public LoginWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _profileFolder = Path.Combine(Path.GetTempPath(), $"RMI_Login_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_profileFolder);

            var env = await CoreWebView2Environment.CreateAsync(null, _profileFolder);
            await Web.EnsureCoreWebView2Async(env);

            Web.CoreWebView2.NavigationCompleted += OnNavCompleted;
            Web.CoreWebView2.NavigationStarting += OnNavStarting;
            HeadingText.Text = Heading;
            SubHeadingText.Text = SubHeading;
            Web.CoreWebView2.Navigate(StartUrl);
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not start the embedded browser.\n\n{ex.Message}\n\nMake sure the Microsoft Edge WebView2 Runtime is installed: https://go.microsoft.com/fwlink/p/?LinkId=2124703",
                "WebView2 unavailable",
                MessageBoxButton.OK, MessageBoxImage.Error);
            DialogResult = false;
            Close();
        }
    }

    private void OnNavStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(() => StatusText.Text = $"Loading: {e.Uri}"));
    }

    private async void OnNavCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (_captured) return;
        try
        {
            var url = Web.CoreWebView2.Source ?? "";
            Dispatcher.BeginInvoke(new Action(() => StatusText.Text = $"Checking session..."));

            // Auto-fill the signup form once when we land on /CreateAccount.
            if (!_prefillApplied && HasPrefill() &&
                url.Contains("/CreateAccount", StringComparison.OrdinalIgnoreCase))
            {
                _prefillApplied = true;
                try { await Web.CoreWebView2.ExecuteScriptAsync(BuildPrefillScript()); } catch { }
            }

            // If we landed somewhere that means we're logged in
            bool likelyLoggedIn =
                url.Contains("roblox.com/home", StringComparison.OrdinalIgnoreCase) ||
                url.Contains("roblox.com/discover", StringComparison.OrdinalIgnoreCase) ||
                url.Contains("roblox.com/games", StringComparison.OrdinalIgnoreCase) ||
                url.Contains("roblox.com/users/", StringComparison.OrdinalIgnoreCase);

            var cookies = await Web.CoreWebView2.CookieManager.GetCookiesAsync("https://www.roblox.com");
            var auth = cookies?.FirstOrDefault(c => c.Name == ".ROBLOSECURITY");

            if (auth != null && !string.IsNullOrEmpty(auth.Value) && likelyLoggedIn)
            {
                _captured = true;
                CapturedCookie = auth.Value;
                Dispatcher.BeginInvoke(new Action(() => StatusText.Text = "Captured. Closing..."));
                DialogResult = true;
                Close();
            }
            else if (auth != null && !string.IsNullOrEmpty(auth.Value))
            {
                Dispatcher.BeginInvoke(new Action(() =>
                    StatusText.Text = "Cookie found — navigate to /home or any post-login page to confirm."));
            }
            else
            {
                Dispatcher.BeginInvoke(new Action(() => StatusText.Text = "Waiting for login..."));
            }
        }
        catch
        {
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private bool HasPrefill()
        => !string.IsNullOrEmpty(PrefillUsername) || !string.IsNullOrEmpty(PrefillPassword)
           || PrefillBirthMonth.HasValue || PrefillBirthDay.HasValue || PrefillBirthYear.HasValue;

    private string BuildPrefillScript()
    {
        string Q(string? s) => System.Text.Json.JsonSerializer.Serialize(s ?? "");
        var monthStr = PrefillBirthMonth?.ToString("D2") ?? "";
        var dayStr = PrefillBirthDay?.ToString("D2") ?? "";
        var yearStr = PrefillBirthYear?.ToString() ?? "";

        return $@"
(function() {{
    function setValueRaw(el, val) {{
        if (!el) return false;
        const proto = el.tagName === 'SELECT' ? window.HTMLSelectElement.prototype : window.HTMLInputElement.prototype;
        const desc = Object.getOwnPropertyDescriptor(proto, 'value');
        if (desc && desc.set) desc.set.call(el, val); else el.value = val;
        el.dispatchEvent(new Event('input', {{ bubbles: true }}));
        el.dispatchEvent(new Event('change', {{ bubbles: true }}));
        el.dispatchEvent(new Event('blur', {{ bubbles: true }}));
        return true;
    }}
    function fillFirst(selectors, val) {{
        if (!val) return false;
        for (const s of selectors) {{
            const el = document.querySelector(s);
            if (el) return setValueRaw(el, val);
        }}
        return false;
    }}
    fillFirst(['#signup-username','input[name=username]','input[placeholder*=ame i]'], {Q(PrefillUsername)});
    fillFirst(['#signup-password','input[name=password]','input[type=password]'], {Q(PrefillPassword)});
    fillFirst(['#MonthDropdown','select[name=Month]','select[id*=month i]'], {Q(monthStr)});
    fillFirst(['#DayDropdown','select[name=Day]','select[id*=day i]'], {Q(dayStr)});
    fillFirst(['#YearDropdown','select[name=Year]','select[id*=year i]'], {Q(yearStr)});
    if ({Q(PrefillGender)}) fillFirst(['select[name=Gender]'], {Q(PrefillGender)});
}})();
";
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        try { Web?.Dispose(); } catch { }
        if (_profileFolder != null)
        {
            try { Directory.Delete(_profileFolder, recursive: true); } catch { }
        }
    }
}
