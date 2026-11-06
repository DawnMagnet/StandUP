using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using StandUP.Services;

namespace StandUP.Views.Pages;

public sealed partial class GeneralPage : Page
{
    static readonly string[] Themes = [AppTheme.System, AppTheme.Light, AppTheme.Dark];
    static readonly string[] Backdrops = [AppBackdrop.MicaAlt, AppBackdrop.Mica, AppBackdrop.Acrylic];
    static readonly string[] Languages = [Loc.Auto, Loc.ZhCN, Loc.EnUS];

    readonly SettingsStore _store = AppHost.Settings;
    bool _loading = true;

    public GeneralPage()
    {
        InitializeComponent();
        var s = _store.Current;
        Fill(ThemeBox, Themes, "theme.", s.Theme);
        Fill(BackdropBox, Backdrops, "backdrop.", s.Backdrop);
        Fill(LanguageBox, Languages, "lang.", s.Language);
        AutoStartSwitch.IsOn = s.StartWithWindows;
        VersionText.Text = Loc.F("general.version", AppHost.Version);
        AboutIcon.Source = new BitmapImage(new Uri(AppHost.IconPath));
        _loading = false;
    }

    static void Fill(ComboBox box, string[] values, string prefix, string current)
    {
        foreach (var value in values)
        {
            box.Items.Add(new ComboBoxItem { Content = Loc.T(prefix + value), Tag = value });
        }
        box.SelectedIndex = Math.Max(0, Array.IndexOf(values, current));
    }

    static string? Selected(ComboBox box) => (box.SelectedItem as ComboBoxItem)?.Tag as string;

    void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loading && Selected(ThemeBox) is { } value)
        {
            _store.Current.Theme = value;
            _store.Save();
        }
    }

    void OnBackdropChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loading && Selected(BackdropBox) is { } value)
        {
            _store.Current.Backdrop = value;
            _store.Save();
        }
    }

    void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || Selected(LanguageBox) is not { } value)
        {
            return;
        }
        _store.Current.Language = value;
        _store.Save();
        Loc.Apply(value);
    }

    void OnAutoStartToggled(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }
        _store.Current.StartWithWindows = AutoStartSwitch.IsOn;
        _store.Save();
        AutoStart.Apply(AutoStartSwitch.IsOn);
    }

    void OnOpenDataFolderClick(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(SettingsStore.DataDirectory);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{SettingsStore.DataDirectory}\"") { UseShellExecute = true });
    }

    async void OnExitClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
            RequestedTheme = ActualTheme,
            Title = Loc.T("general.exitTitle"),
            Content = Loc.T("general.exitBody"),
            PrimaryButtonText = Loc.T("dialog.exit"),
            CloseButtonText = Loc.T("dialog.cancel"),
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            AppHost.Exit();
        }
    }
}
