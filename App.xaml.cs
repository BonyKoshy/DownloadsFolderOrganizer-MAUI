using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;

#if WINDOWS
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using WinRT.Interop;
#endif

namespace DownloadsFolderOrganizer;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        MainPage = new MainPage();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = base.CreateWindow(activationState);

#if WINDOWS
    window.HandlerChanged += (s, e) =>
    {
        var handler = window.Handler;
        if (handler?.PlatformView is not Microsoft.UI.Xaml.Window winUIWindow)
            return;

        IntPtr hWnd = WindowNative.GetWindowHandle(winUIWindow);
        WindowId wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
        AppWindow appWindow = AppWindow.GetFromWindowId(wndId);

        appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        appWindow.TitleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
        appWindow.TitleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
        appWindow.TitleBar.ButtonForegroundColor = Microsoft.UI.Colors.White;

        appWindow.Resize(new SizeInt32(450, 950));
        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = true;
        }
    };
#endif

        return window;
    }

}
