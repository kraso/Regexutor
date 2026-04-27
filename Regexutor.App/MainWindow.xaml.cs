using System.Windows;
using System.Windows.Input;
using System.Windows.Shell;

namespace Regexutor.App;

public partial class MainWindow : Window
{
    private AdditionalInfoWindow? _additionalInfoWindow;

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