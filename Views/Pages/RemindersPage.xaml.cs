using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using StandUP.Controls;
using StandUP.Services;

namespace StandUP.Views.Pages;

public sealed partial class RemindersPage : Page
{
    readonly SettingsStore _store = AppHost.Settings;
    Phase _phase = Phase.StandPrompt;
    bool _loading;

    public RemindersPage()
    {
        InitializeComponent();
        Load();
        PreviewCard.SizeChanged += (_, _) => PreviewActions.Orientation =
            PreviewCard.ActualWidth < 620 ? Orientation.Vertical : Orientation.Horizontal;
        Unloaded += (_, _) => Save();
    }

    AppSettings S => _store.Current;

    void Load()
    {
        _loading = true;
        var stand = _phase == Phase.StandPrompt;
        TitleBox.Text = stand ? S.StandTitle : S.SitTitle;
        MessageBox.Text = stand ? S.StandMessage : S.SitMessage;
        ConfirmBox.Text = stand ? S.StandConfirm : S.SitConfirm;
        SnoozeBox.Text = stand ? S.StandSnooze : S.SitSnooze;
        TitleBox.PlaceholderText = Loc.Expand(Loc.T(stand ? "overlay.stand.title" : "overlay.sit.title"), S);
        MessageBox.PlaceholderText = Loc.Expand(Loc.T(stand ? "overlay.stand.message" : "overlay.sit.message"), S);
        ConfirmBox.PlaceholderText = Loc.Expand(Loc.T(stand ? "overlay.stand.confirm" : "overlay.sit.confirm"), S);
        SnoozeBox.PlaceholderText = Loc.Expand(Loc.T(stand ? "overlay.stand.snooze" : "overlay.sit.snooze"), S);
        _loading = false;

        var palette = stand ? Palette.Prompt : Palette.Stand;
        PreviewGlow.Background = Palette.Gradient(palette, 0.35, 0.05);
        PreviewOrbA.Fill = Orb(palette.From);
        PreviewOrbB.Fill = Orb(palette.To);
        PreviewPrimary.Background = Palette.Gradient(palette);
        PreviewMascot.State = stand ? MascotState.Prompt : MascotState.SitPrompt;
        UpdatePreview();
        if (IsLoaded)
        {
            UiMotion.Reveal(PreviewContent, 12);
        }
    }

    static RadialGradientBrush Orb(Windows.UI.Color color)
    {
        var brush = new RadialGradientBrush();
        brush.GradientStops.Add(new GradientStop { Color = Palette.WithAlpha(color, 0.55), Offset = 0 });
        brush.GradientStops.Add(new GradientStop { Color = Palette.WithAlpha(color, 0), Offset = 1 });
        return brush;
    }

    static string Pick(TextBox box) => string.IsNullOrWhiteSpace(box.Text) ? box.PlaceholderText : box.Text;

    void UpdatePreview()
    {
        PreviewTitle.Text = Loc.Expand(Pick(TitleBox), S);
        PreviewMessage.Text = Loc.Expand(Pick(MessageBox), S);
        PreviewPrimaryText.Text = Loc.Expand(Pick(ConfirmBox), S);
        PreviewSecondaryText.Text = Loc.Expand(Pick(SnoozeBox), S);
        PreviewSecondary.Visibility = S.AllowSnooze ? Visibility.Visible : Visibility.Collapsed;
    }

    void Save()
    {
        var title = TitleBox.Text.Trim();
        var message = MessageBox.Text.Trim();
        var confirm = ConfirmBox.Text.Trim();
        var snooze = SnoozeBox.Text.Trim();
        if (_phase == Phase.StandPrompt)
        {
            if ((S.StandTitle, S.StandMessage, S.StandConfirm, S.StandSnooze) == (title, message, confirm, snooze))
            {
                return;
            }
            (S.StandTitle, S.StandMessage, S.StandConfirm, S.StandSnooze) = (title, message, confirm, snooze);
        }
        else
        {
            if ((S.SitTitle, S.SitMessage, S.SitConfirm, S.SitSnooze) == (title, message, confirm, snooze))
            {
                return;
            }
            (S.SitTitle, S.SitMessage, S.SitConfirm, S.SitSnooze) = (title, message, confirm, snooze);
        }
        _store.Save();
    }

    void OnPhaseSelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        var phase = ReferenceEquals(sender.SelectedItem, SitTab) ? Phase.SitPrompt : Phase.StandPrompt;
        if (phase == _phase)
        {
            return;
        }
        Save();
        _phase = phase;
        Load();
    }

    void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loading)
        {
            UpdatePreview();
        }
    }

    void OnFieldLostFocus(object sender, RoutedEventArgs e) => Save();

    void OnPreviewClick(object sender, RoutedEventArgs e)
    {
        Save();
        AppHost.Preview(_phase);
    }

    async void OnResetClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
            RequestedTheme = ActualTheme,
            Title = Loc.T("reminders.resetTitle"),
            Content = Loc.T("reminders.resetBody"),
            PrimaryButtonText = Loc.T("dialog.reset"),
            CloseButtonText = Loc.T("dialog.cancel"),
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }
        S.StandTitle = S.StandMessage = S.StandConfirm = S.StandSnooze = string.Empty;
        S.SitTitle = S.SitMessage = S.SitConfirm = S.SitSnooze = string.Empty;
        _store.Save();
        Load();
    }
}
