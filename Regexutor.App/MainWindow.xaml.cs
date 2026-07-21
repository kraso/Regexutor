using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Shell;
using Regexutor.App.Services;

namespace Regexutor.App;

public partial class MainWindow : Window
{
    private AdditionalInfoWindow? _additionalInfoWindow;
    private PiAuthService? _piAuth;

    public MainWindow()
    {
        InitializeComponent();

        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            CaptionHeight = 44,
            CornerRadius = new CornerRadius(12),
            GlassFrameThickness = new Thickness(0),
            ResizeBorderThickness = new Thickness(6),
            UseAeroCaptionButtons = false,
        });

        DataContext = new MainViewModel();
    }

    // ───────────────────── Pi Network auth ─────────────────────

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var vm = (MainViewModel)DataContext;
        await RunAuthFlowAsync(vm);
    }

    private async void SignIn_Click(object sender, RoutedEventArgs e)
    {
        var vm = (MainViewModel)DataContext;
        if (vm.IsAuthenticating || vm.IsAuthenticated)
            return;
        await RunAuthFlowAsync(vm);
    }

    private async System.Threading.Tasks.Task RunAuthFlowAsync(MainViewModel vm)
    {
        try
        {
            _piAuth ??= new PiAuthService(PiWebView);

            // 1. Pi.init() — awaited fully before authenticate
            vm.SetAuthenticating(true, "Inicializando Pi Network…");
            var initOk = await _piAuth.InitPiAsync();
            if (!initOk)
            {
                vm.SetAuthenticating(false, "Pi Network no disponible (init falló).");
                return;
            }

            // 2. Pi.authenticate({ scopes: ['username'] })
            vm.SetAuthenticating(true, "Autenticando con Pi…");
            var auth = await _piAuth.AuthenticateAsync();

            if (!auth.Success || auth.AccessToken is null)
            {
                vm.SetAuthenticating(false, auth.Error ?? "Autenticación cancelada.");
                return;
            }

            // 3. Validate via GET https://api.minepi.com/v2/me
            vm.SetAuthenticating(true, "Validando token…");
            var session = await _piAuth.ValidateAsync(auth.AccessToken);

            if (session is null)
            {
                vm.SetAuthenticating(false, "Token inválido (validación fallida).");
                return;
            }

            vm.SetAuthenticated(session);
        }
        catch (Exception ex)
        {
            vm.SetAuthenticating(false, $"Error: {ex.Message}");
        }
    }

    private void SignOut_Click(object sender, RoutedEventArgs e)
    {
        ((MainViewModel)DataContext).ClearSession();
    }

    // ───────────────────── existing code ─────────────────────

    private void AdditionalInfo_Click(object sender, RoutedEventArgs e)
    {
        if (_additionalInfoWindow is null)
        {
            _additionalInfoWindow = new AdditionalInfoWindow
            {
                Owner = this,
                DataContext = DataContext,
            };
            _additionalInfoWindow.Closed += (_, _) => _additionalInfoWindow = null;
            _additionalInfoWindow.Show();
            return;
        }

        if (_additionalInfoWindow.WindowState == WindowState.Minimized)
            _additionalInfoWindow.WindowState = WindowState.Normal;
        _additionalInfoWindow.Activate();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximizeRestore();
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeRestore_Click(object sender, RoutedEventArgs e) => ToggleMaximizeRestore();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void ToggleMaximizeRestore()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }
}