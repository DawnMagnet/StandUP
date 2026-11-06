using System.Globalization;

namespace StandUP.Services;

public static class Loc
{
    public const string Auto = "auto";
    public const string ZhCN = "zh-CN";
    public const string EnUS = "en-US";

    public static string Current { get; private set; } = EnUS;

    public static event Action? LanguageChanged;

    public static string Resolve(string setting) => setting switch
    {
        ZhCN => ZhCN,
        EnUS => EnUS,
        _ => CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? ZhCN : EnUS,
    };

    public static void Apply(string setting)
    {
        var resolved = Resolve(setting);
        var changed = resolved != Current;
        Current = resolved;
        if (changed)
        {
            LanguageChanged?.Invoke();
        }
    }

    public static string T(string key)
    {
        var table = Current == ZhCN ? Strings.Zh : Strings.En;
        return table.TryGetValue(key, out var value) ? value
            : Strings.En.TryGetValue(key, out var fallback) ? fallback
            : key;
    }

    public static string F(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, T(key), args);

    // Expands {sit}, {stand} and {snooze} in default or user-defined reminder text.
    public static string Expand(string template, AppSettings settings) => template
        .Replace("{sit}", settings.SitMinutes.ToString(CultureInfo.InvariantCulture))
        .Replace("{stand}", settings.StandMinutes.ToString(CultureInfo.InvariantCulture))
        .Replace("{snooze}", settings.SnoozeMinutes.ToString(CultureInfo.InvariantCulture));

    // Returns the user's custom text when set, otherwise the localized default for the given key.
    public static string Reminder(string? custom, string defaultKey, AppSettings settings) =>
        Expand(string.IsNullOrWhiteSpace(custom) ? T(defaultKey) : custom, settings);
}

internal static class Strings
{
    public static readonly Dictionary<string, string> En = new()
    {
        ["tip.default"] = "StandUP",
        ["tip.locked"] = "StandUP - screen locked",
        ["tip.paused"] = "StandUP - paused",
        ["tip.sitting"] = "StandUP - stand up in {0} min",
        ["tip.standing"] = "StandUP - sit down in {0} min",

        ["tray.open"] = "Open settings",
        ["tray.breakNow"] = "Take a break now",
        ["tray.restart"] = "Restart timer",
        ["tray.pause"] = "Pause",
        ["tray.resume"] = "Resume",
        ["tray.exit"] = "Exit StandUP",

        ["overlay.stand.title"] = "Time to stand up",
        ["overlay.stand.message"] = "You have been sitting for {sit} minutes. Stand up, stretch and take a {stand}-minute break.",
        ["overlay.stand.confirm"] = "I'm standing",
        ["overlay.stand.snooze"] = "Remind me in {snooze} min",
        ["overlay.sit.title"] = "Break is over, please sit down",
        ["overlay.sit.message"] = "You have been standing for {stand} minutes. You can get back to work.",
        ["overlay.sit.confirm"] = "I'm sitting",
        ["overlay.sit.snooze"] = "Stand a bit longer ({snooze} min)",

        ["status.locked"] = "Screen locked, timer paused",
        ["status.paused"] = "Paused",
        ["status.sitting"] = "Sitting - time until you are asked to stand",
        ["status.standing"] = "Standing - time until you are asked to sit",
        ["status.waiting"] = "Waiting for your confirmation on the full-screen reminder",

        ["btn.breakNow"] = "Break now",
        ["btn.restart"] = "Restart timer",
        ["btn.pause"] = "Pause",
        ["btn.resume"] = "Resume",
        ["btn.exit"] = "Exit StandUP",

        ["settings.timeHeader"] = "Timing",
        ["settings.sit"] = "Remind me to stand after sitting for (minutes)",
        ["settings.stand"] = "Remind me to sit after standing for (minutes)",
        ["settings.snooze"] = "Snooze delay (minutes)",
        ["settings.allowSnooze"] = "Allow \"snooze\" on the full-screen reminder",
        ["settings.allow"] = "Allowed",
        ["settings.disallow"] = "Not allowed",
        ["settings.autostart"] = "Start with Windows",
        ["settings.on"] = "On",
        ["settings.off"] = "Off",
        ["settings.language"] = "Language",
        ["lang.auto"] = "Follow system",
        ["lang.zh-CN"] = "中文",
        ["lang.en-US"] = "English",
        ["settings.note"] = "A locked screen counts as standing: it does not add to your sitting time. If the screen stays locked for at least the stand duration, the sitting timer restarts on unlock; otherwise it continues where it left off. Closing this window keeps StandUP running in the system tray.",

        ["custom.header"] = "Full-screen reminder text",
        ["custom.hint"] = "Leave a field empty to use the default text. You can use {sit}, {stand} and {snooze}, which are replaced with the configured minutes.",
        ["custom.standGroup"] = "Stand-up reminder",
        ["custom.sitGroup"] = "Sit-down reminder",
        ["field.title"] = "Title",
        ["field.message"] = "Message",
        ["field.confirm"] = "Confirm button",
        ["field.snooze"] = "Snooze button",
        ["custom.reset"] = "Reset to default text",
        ["custom.previewStand"] = "Preview stand-up reminder",
        ["custom.previewSit"] = "Preview sit-down reminder",
    };

    public static readonly Dictionary<string, string> Zh = new()
    {
        ["tip.default"] = "StandUP",
        ["tip.locked"] = "StandUP · 已锁屏",
        ["tip.paused"] = "StandUP · 已暂停",
        ["tip.sitting"] = "StandUP · {0} 分钟后提醒站起来",
        ["tip.standing"] = "StandUP · {0} 分钟后提醒坐下",

        ["tray.open"] = "打开设置",
        ["tray.breakNow"] = "立即休息",
        ["tray.restart"] = "重新开始计时",
        ["tray.pause"] = "暂停计时",
        ["tray.resume"] = "继续计时",
        ["tray.exit"] = "退出 StandUP",

        ["overlay.stand.title"] = "该站起来活动一下了",
        ["overlay.stand.message"] = "你已连续坐了 {sit} 分钟。站起来走一走、伸展一下，休息 {stand} 分钟。",
        ["overlay.stand.confirm"] = "我已站起来",
        ["overlay.stand.snooze"] = "稍后提醒（{snooze} 分钟）",
        ["overlay.sit.title"] = "休息结束，请坐下",
        ["overlay.sit.message"] = "你已经站了 {stand} 分钟，可以回到座位继续工作了。",
        ["overlay.sit.confirm"] = "我已坐下",
        ["overlay.sit.snooze"] = "再站一会儿（{snooze} 分钟）",

        ["status.locked"] = "已锁屏，计时暂停",
        ["status.paused"] = "已暂停",
        ["status.sitting"] = "久坐计时中，距离提醒站起来还有",
        ["status.standing"] = "站立休息中，距离提醒坐下还有",
        ["status.waiting"] = "等待你在全屏提醒上确认",

        ["btn.breakNow"] = "立即休息",
        ["btn.restart"] = "重新计时",
        ["btn.pause"] = "暂停",
        ["btn.resume"] = "继续",
        ["btn.exit"] = "退出 StandUP",

        ["settings.timeHeader"] = "时间设置",
        ["settings.sit"] = "久坐多久后提醒站起来（分钟）",
        ["settings.stand"] = "站立多久后提醒坐下（分钟）",
        ["settings.snooze"] = "稍后提醒的延迟（分钟）",
        ["settings.allowSnooze"] = "允许在全屏提醒上选择“稍后提醒”",
        ["settings.allow"] = "允许",
        ["settings.disallow"] = "不允许",
        ["settings.autostart"] = "开机自动启动",
        ["settings.on"] = "开启",
        ["settings.off"] = "关闭",
        ["settings.language"] = "语言",
        ["lang.auto"] = "跟随系统",
        ["lang.zh-CN"] = "中文",
        ["lang.en-US"] = "English",
        ["settings.note"] = "锁屏按“站立”计算：锁屏不累计久坐时间；锁屏时长达到站立时长则解锁后重新开始久坐计时，否则接着之前的进度继续。关闭此窗口后程序会继续在系统托盘运行。",

        ["custom.header"] = "全屏提醒文字",
        ["custom.hint"] = "留空则使用默认文字。可使用 {sit}、{stand}、{snooze}，会被替换为设置的分钟数。",
        ["custom.standGroup"] = "站起来提醒",
        ["custom.sitGroup"] = "坐下提醒",
        ["field.title"] = "标题",
        ["field.message"] = "正文",
        ["field.confirm"] = "确认按钮",
        ["field.snooze"] = "稍后提醒按钮",
        ["custom.reset"] = "恢复默认文字",
        ["custom.previewStand"] = "预览站起来提醒",
        ["custom.previewSit"] = "预览坐下提醒",
    };
}
