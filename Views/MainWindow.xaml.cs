using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using StandUP.Services;
using StandUP.Views.Pages;
using Windows.Graphics;
using WinRT.Interop;

namespace StandUP.Views;

public sealed partial class MainWindow : Window
{
    static readonly Dictionary<string, Type> PageTypes = new()
    {
        ["home"] = typeof(HomePage),
        ["schedule"] = typeof(SchedulePage),
        ["reminders"] = typeof(RemindersPage),
        ["general"] = typeof(GeneralPage),
    };

    readonly ReminderEngine _engine;
    readonly SettingsStore _store;
    bool _syncingSelection;
    string _appliedBackdrop = string.Empty;

    public bool AllowClose { get; set; }

    public MainWindow()
    {
        InitializeComponent();
        _engine = AppHost.Engine;
        _store = AppHost.Settings;

        Title = "StandUP";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon(AppHost.IconPath);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        TitleIcon.ImageSource = new BitmapImage(new Uri(AppHost.IconPath));

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            var minScale = Scale;
            presenter.PreferredMinimumWidth = (int)(560 * minScale);
            presenter.PreferredMinimumHeight = (int)(560 * minScale);
        }
        PlaceWindow();

        AppWindow.Closing += (_, args) =>
        {
            if (!AllowClose)
            {
                args.Cancel = true;
                AppWindow.Hide();
            }
        };

        ApplyAppearance();
        Localize();
        Nav.SelectedItem = HomeItem;

        _engine.Changed += RefreshStatus;
        _store.Saved += ApplyAppearance;
        Loc.LanguageChanged += () => DispatcherQueue.TryEnqueue(Relocalize);
        RefreshStatus();
    }

    double Scale => NativeMethods.GetDpiForWindow(WindowNative.GetWindowHandle(this)) / 96.0;

    void PlaceWindow()
    {
        var scale = Scale;
        var work = DisplayArea.Primary.WorkArea;
        var width = Math.Min((int)(1120 * scale), work.Width - (int)(48 * scale));
        var height = Math.Min((int)(800 * scale), work.Height - (int)(48 * scale));
        AppWindow.MoveAndResize(new RectInt32(
            work.X + (work.Width - width) / 2,
            work.Y + (work.Height - height) / 2,
            width,
            height));
    }

    public void ShowAndActivate()
    {
        AppWindow.Show();
        Activate();
        NativeMethods.SetForegroundWindow(WindowNative.GetWindowHandle(this));
    }

    void ApplyAppearance()
    {
        var s = _store.Current;
        var theme = s.Theme switch
        {
            AppTheme.Light => ElementTheme.Light,
            AppTheme.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
        if (RootGrid.RequestedTheme != theme)
        {
            RootGrid.RequestedTheme = theme;
            AppWindow.TitleBar.PreferredTheme = theme switch
            {
                ElementTheme.Light => TitleBarTheme.Light,
                ElementTheme.Dark => TitleBarTheme.Dark,
                _ => TitleBarTheme.UseDefaultAppMode,
            };
        }

        if (s.Backdrop != _appliedBackdrop)
        {
            _appliedBackdrop = s.Backdrop;
            SystemBackdrop = s.Backdrop switch
            {
                AppBackdrop.Mica => new MicaBackdrop { Kind = MicaKind.Base },
                AppBackdrop.Acrylic => new DesktopAcrylicBackdrop(),
                _ => new MicaBackdrop { Kind = MicaKind.BaseAlt },
            };
        }
    }

    void Localize()
    {
        AppTitleBar.Subtitle = Loc.T("title.subtitle");
        HomeItem.Content = Loc.T("nav.home");
        ScheduleItem.Content = Loc.T("nav.schedule");
        RemindersItem.Content = Loc.T("nav.reminders");
        GeneralItem.Content = Loc.T("nav.general");
    }

    // Pages read their text once, so rebuild the visible page after a language change.
    void Relocalize()
    {
        Localize();
        RefreshStatus();
        if (ContentFrame.SourcePageType is { } type)
        {
            ContentFrame.Navigate(type, null, new SuppressNavigationTransitionInfo());
            if (ContentFrame.BackStackDepth > 0)
            {
                ContentFrame.BackStack.RemoveAt(ContentFrame.BackStackDepth - 1);
            }
            AppTitleBar.IsBackButtonVisible = ContentFrame.CanGoBack;
        }
    }

    void RefreshStatus()
    {
        var (from, _) = Palette.For(_engine);
        StatusDot.Fill = Palette.Solid(from);
        StatusPillText.Text = Loc.T(_engine switch
        {
            { IsLocked: true } => "pill.locked",
            { IsPaused: true } => "pill.paused",
            { IsPrompting: true } => "pill.prompt",
            { Phase: Phase.Standing } => "pill.standing",
            _ => "pill.sitting",
        });
        var showTime = !_engine.IsPrompting && !_engine.IsLocked;
        StatusPillTime.Visibility = showTime ? Visibility.Visible : Visibility.Collapsed;
        if (showTime)
        {
            StatusPillTime.Text = HomePage.FormatSpan(_engine.Remaining);
        }
        HomeBadge.Visibility = _engine.IsPaused || _engine.IsLocked ? Visibility.Visible : Visibility.Collapsed;
    }

    public void NavigateTo(string tag)
    {
        var item = Nav.MenuItems.Concat(Nav.FooterMenuItems)
            .OfType<NavigationViewItem>()
            .FirstOrDefault(i => (string)i.Tag == tag);
        if (item is not null)
        {
            Nav.SelectedItem = item;
        }
    }

    void OnNavSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_syncingSelection || args.SelectedItemContainer?.Tag is not string tag || !PageTypes.TryGetValue(tag, out var type))
        {
            return;
        }
        if (ContentFrame.SourcePageType != type)
        {
            ContentFrame.Navigate(type, null, args.RecommendedNavigationTransitionInfo);
        }
    }

    void OnFrameNavigated(object sender, NavigationEventArgs e)
    {
        AppTitleBar.IsBackButtonVisible = ContentFrame.CanGoBack;
        var tag = PageTypes.FirstOrDefault(p => p.Value == e.SourcePageType).Key;
        var item = Nav.MenuItems.Concat(Nav.FooterMenuItems)
            .OfType<NavigationViewItem>()
            .FirstOrDefault(i => (string)i.Tag == tag);
        if (item is not null && !ReferenceEquals(Nav.SelectedItem, item))
        {
            _syncingSelection = true;
            Nav.SelectedItem = item;
            _syncingSelection = false;
        }
    }

    void OnTitleBarBackRequested(TitleBar sender, object args)
    {
        if (ContentFrame.CanGoBack)
        {
            ContentFrame.GoBack();
        }
    }

    void OnTitleBarPaneToggleRequested(TitleBar sender, object args) => Nav.IsPaneOpen = !Nav.IsPaneOpen;
}
