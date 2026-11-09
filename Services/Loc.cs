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

        ["tray.open"] = "Open StandUP",
        ["tray.breakNow"] = "Take a break now",
        ["tray.restart"] = "Restart timer",
        ["tray.pause"] = "Pause",
        ["tray.resume"] = "Resume",
        ["tray.exit"] = "Exit StandUP",

        ["overlay.stand.title"] = "Time to stand up",
        ["overlay.stand.message"] = "You have been sitting for {sit} minutes. Stand up, stretch and take a {stand}-minute break.",
        ["overlay.stand.confirm"] = "I'm standing",
        ["overlay.stand.snooze"] = "Remind me in {snooze} min",
        ["overlay.stand.chip"] = "Sat for {sit} min",
        ["overlay.sit.title"] = "Break is over, please sit down",
        ["overlay.sit.message"] = "You have been standing for {stand} minutes. You can get back to work.",
        ["overlay.sit.confirm"] = "I'm sitting",
        ["overlay.sit.snooze"] = "Stand a bit longer ({snooze} min)",
        ["overlay.sit.chip"] = "Stood for {stand} min",
        ["overlay.preview"] = "Preview - the timer is not affected",
        ["overlay.confirmed.stand"] = "Enjoy your break",
        ["overlay.confirmed.sit"] = "Ready when you are",
        ["overlay.snoozed"] = "We’ll remind you later",

        ["btn.breakNow"] = "Break now",
        ["btn.restart"] = "Restart",
        ["btn.pause"] = "Pause",
        ["btn.resume"] = "Resume",
        ["btn.exit"] = "Exit StandUP",

        ["nav.home"] = "Home",
        ["nav.schedule"] = "Schedule",
        ["nav.reminders"] = "Reminders",
        ["nav.general"] = "Settings",
        ["title.subtitle"] = "Sit less, move more",

        ["pill.sitting"] = "Sitting",
        ["pill.standing"] = "Standing",
        ["pill.paused"] = "Paused",
        ["pill.locked"] = "Locked",
        ["pill.prompt"] = "Reminder",

        ["home.morning"] = "Good morning",
        ["home.afternoon"] = "Good afternoon",
        ["home.evening"] = "Good evening",
        ["home.remaining"] = "remaining",
        ["home.nextStand"] = "Stand up at {0}",
        ["home.nextSit"] = "Sit down at {0}",
        ["home.pausedTitle"] = "Timer paused",
        ["home.lockedTitle"] = "Screen locked",
        ["home.promptTitle"] = "Reminder on screen",
        ["home.desc.sitting"] = "Keep working. A full-screen reminder asks you to stand up when the timer runs out.",
        ["home.desc.standing"] = "Enjoy the break: stretch, walk around and drink some water.",
        ["home.desc.paused"] = "You will not get reminders until you resume.",
        ["home.desc.prompt"] = "Confirm the full-screen reminder to continue.",
        ["home.today"] = "Today",
        ["mascot.action"] = "Play mascot animation",
        ["home.feedback.paused"] = "Timer paused. Take your time.",
        ["home.feedback.resumed"] = "Timer resumed. We’ll remind you when it’s time.",
        ["home.feedback.restarted"] = "A fresh sitting session has started.",
        ["home.feedback.standing"] = "Break started. Stretch, walk, or get some water.",
        ["home.feedback.sitting"] = "Welcome back. Your next sitting session is ready.",
        ["home.infoTitle"] = "Screen lock",
        ["stats.sitting"] = "Sitting",
        ["stats.standing"] = "Standing",
        ["stats.breaks"] = "Breaks taken",
        ["fmt.hm"] = "{0} h {1} min",
        ["fmt.m"] = "{0} min",
        ["unit.h"] = "h",
        ["unit.min"] = "min",
        ["home.until.stand"] = "until you stand up",
        ["home.until.sit"] = "until you sit down",
        ["home.next.sitting"] = "At {0}, a full-screen reminder asks you to move for {1} min",
        ["home.next.standing"] = "At {0}, you will be reminded to get back to your seat",
        ["home.progress.sat"] = "Sat for {0}",
        ["home.progress.stood"] = "Stood for {0}",
        ["home.progress.total"] = "{0} min in total",
        ["home.standShare"] = "Standing share",

        ["schedule.presets"] = "Presets",
        ["preset.classic"] = "Classic",
        ["preset.balanced"] = "Balanced",
        ["preset.light"] = "Short breaks",
        ["preset.pomodoro"] = "Pomodoro",
        ["schedule.cycle"] = "One cycle takes {0} min",
        ["schedule.timing"] = "Timing",
        ["schedule.sit"] = "Sitting time",
        ["schedule.sitDesc"] = "Show the stand-up reminder after sitting this long (minutes)",
        ["schedule.stand"] = "Standing time",
        ["schedule.standDesc"] = "Show the sit-down reminder after standing this long (minutes)",
        ["schedule.snooze"] = "Snooze length",
        ["schedule.snoozeDesc"] = "How long \"remind me later\" postpones a reminder (minutes)",
        ["schedule.allowSnooze"] = "Allow snoozing",
        ["schedule.allowSnoozeDesc"] = "Show a \"remind me later\" button on the full-screen reminder",

        ["reminders.stand"] = "Stand-up reminder",
        ["reminders.sit"] = "Sit-down reminder",
        ["reminders.preview"] = "Live preview",
        ["reminders.text"] = "Text",
        ["reminders.previewFull"] = "Preview full screen",
        ["reminders.resetTitle"] = "Reset reminder text?",
        ["reminders.resetBody"] = "All custom titles, messages and button labels of both reminders are cleared.",
        ["custom.hint"] = "Leave a field empty to use the default text. {sit}, {stand} and {snooze} are replaced with the configured minutes.",
        ["custom.reset"] = "Reset to default",
        ["field.title"] = "Title",
        ["field.message"] = "Message",
        ["field.confirm"] = "Confirm button",
        ["field.snooze"] = "Snooze button",
        ["dialog.reset"] = "Reset",
        ["dialog.cancel"] = "Cancel",
        ["dialog.exit"] = "Exit",

        ["general.appearance"] = "Appearance",
        ["general.theme"] = "App theme",
        ["general.themeDesc"] = "Use light or dark colors, or follow Windows",
        ["theme.system"] = "Use system setting",
        ["theme.light"] = "Light",
        ["theme.dark"] = "Dark",
        ["general.backdrop"] = "Window material",
        ["general.backdropDesc"] = "The translucent material behind the window",
        ["backdrop.mica"] = "Mica",
        ["backdrop.micaAlt"] = "Mica Alt",
        ["backdrop.acrylic"] = "Acrylic",
        ["general.language"] = "Language",
        ["general.languageDesc"] = "Language of the app and its reminders",
        ["lang.auto"] = "Use system setting",
        ["lang.zh-CN"] = "中文",
        ["lang.en-US"] = "English",
        ["general.startup"] = "Startup",
        ["general.autostart"] = "Start with Windows",
        ["general.autostartDesc"] = "Run StandUP in the tray when you sign in",
        ["general.about"] = "About",
        ["general.aboutDesc"] = "A gentle full-screen reminder to alternate between sitting and standing.",
        ["general.version"] = "Version {0}",
        ["general.dataFolder"] = "Open data folder",
        ["general.exitDesc"] = "Closing the window keeps StandUP in the tray. Exiting stops all reminders.",
        ["general.exitTitle"] = "Exit StandUP?",
        ["general.exitBody"] = "You will not get any reminders until you start StandUP again.",
        ["lock.header"] = "When the screen is locked",
        ["lock.standing"] = "Count as standing",
        ["lock.standing.desc"] = "Locking counts as a standing break. A lock at least as long as the standing time restarts the sitting timer; a shorter one continues where it left off.",
        ["lock.standing.short"] = "Locked time counts as standing.",
        ["lock.sitting"] = "Count as sitting",
        ["lock.sitting.desc"] = "Locked time adds to your sitting time, as if you were still at your desk. Locking during a standing break ends the break.",
        ["lock.sitting.short"] = "Locked time counts as sitting.",
        ["lock.continue"] = "Keep the timer running",
        ["lock.continue.desc"] = "The current phase keeps counting while locked. A reminder that comes due appears after you unlock.",
        ["lock.continue.short"] = "The timer keeps running while locked.",
        ["lock.pause"] = "Pause the timer",
        ["lock.pause.desc"] = "The timer stops while locked and continues where it left off after you unlock.",
        ["lock.pause.short"] = "The timer is paused while locked.",
        ["lock.reset"] = "Restart after unlocking",
        ["lock.reset.desc"] = "Every unlock starts a fresh sitting period.",
        ["lock.reset.short"] = "The sitting timer restarts when you unlock.",
        ["home.trayNote"] = "Closing the window keeps StandUP running in the tray.",
    };

    public static readonly Dictionary<string, string> Zh = new()
    {
        ["tip.default"] = "StandUP",
        ["tip.locked"] = "StandUP · 已锁屏",
        ["tip.paused"] = "StandUP · 已暂停",
        ["tip.sitting"] = "StandUP · {0} 分钟后提醒站起来",
        ["tip.standing"] = "StandUP · {0} 分钟后提醒坐下",

        ["tray.open"] = "打开 StandUP",
        ["tray.breakNow"] = "立即休息",
        ["tray.restart"] = "重新开始计时",
        ["tray.pause"] = "暂停计时",
        ["tray.resume"] = "继续计时",
        ["tray.exit"] = "退出 StandUP",

        ["overlay.stand.title"] = "该站起来活动一下了",
        ["overlay.stand.message"] = "你已连续坐了 {sit} 分钟。站起来走一走、伸展一下，休息 {stand} 分钟。",
        ["overlay.stand.confirm"] = "我已站起来",
        ["overlay.stand.snooze"] = "稍后提醒（{snooze} 分钟）",
        ["overlay.stand.chip"] = "已坐 {sit} 分钟",
        ["overlay.sit.title"] = "休息结束，请坐下",
        ["overlay.sit.message"] = "你已经站了 {stand} 分钟，可以回到座位继续工作了。",
        ["overlay.sit.confirm"] = "我已坐下",
        ["overlay.sit.snooze"] = "再站一会儿（{snooze} 分钟）",
        ["overlay.sit.chip"] = "已站 {stand} 分钟",
        ["overlay.preview"] = "预览 · 不影响计时",
        ["overlay.confirmed.stand"] = "享受这段休息吧",
        ["overlay.confirmed.sit"] = "准备好，再继续",
        ["overlay.snoozed"] = "稍后再提醒你",

        ["btn.breakNow"] = "立即休息",
        ["btn.restart"] = "重新计时",
        ["btn.pause"] = "暂停",
        ["btn.resume"] = "继续",
        ["btn.exit"] = "退出 StandUP",

        ["nav.home"] = "概览",
        ["nav.schedule"] = "作息",
        ["nav.reminders"] = "提醒",
        ["nav.general"] = "设置",
        ["title.subtitle"] = "少坐一点，多动一点",

        ["pill.sitting"] = "久坐中",
        ["pill.standing"] = "站立中",
        ["pill.paused"] = "已暂停",
        ["pill.locked"] = "已锁屏",
        ["pill.prompt"] = "提醒中",

        ["home.morning"] = "早上好",
        ["home.afternoon"] = "下午好",
        ["home.evening"] = "晚上好",
        ["home.remaining"] = "剩余",
        ["home.nextStand"] = "{0} 提醒你站起来",
        ["home.nextSit"] = "{0} 提醒你坐下",
        ["home.pausedTitle"] = "计时已暂停",
        ["home.lockedTitle"] = "屏幕已锁定",
        ["home.promptTitle"] = "正在全屏提醒",
        ["home.desc.sitting"] = "专心工作吧，计时结束时会全屏提醒你站起来。",
        ["home.desc.standing"] = "享受休息：伸展一下、走一走、喝点水。",
        ["home.desc.paused"] = "继续计时之前不会提醒你。",
        ["home.desc.prompt"] = "请在全屏提醒上确认后继续。",
        ["home.today"] = "今日",
        ["mascot.action"] = "与小人互动",
        ["home.feedback.paused"] = "计时已暂停，按自己的节奏来。",
        ["home.feedback.resumed"] = "计时已继续，到点会提醒你。",
        ["home.feedback.restarted"] = "新一轮久坐计时已开始。",
        ["home.feedback.standing"] = "休息开始了，伸展一下、走一走或喝点水。",
        ["home.feedback.sitting"] = "欢迎回来，新一轮久坐计时已开始。",
        ["home.infoTitle"] = "锁屏",
        ["stats.sitting"] = "久坐",
        ["stats.standing"] = "站立",
        ["stats.breaks"] = "休息次数",
        ["fmt.hm"] = "{0} 小时 {1} 分",
        ["fmt.m"] = "{0} 分钟",
        ["unit.h"] = "小时",
        ["unit.min"] = "分钟",
        ["home.until.stand"] = "后提醒站起来",
        ["home.until.sit"] = "后提醒坐下",
        ["home.next.sitting"] = "{0} 会全屏提醒你起身活动 {1} 分钟",
        ["home.next.standing"] = "{0} 提醒你回到座位继续工作",
        ["home.progress.sat"] = "已坐 {0}",
        ["home.progress.stood"] = "已站 {0}",
        ["home.progress.total"] = "共 {0} 分钟",
        ["home.standShare"] = "站立占比",

        ["schedule.presets"] = "预设",
        ["preset.classic"] = "经典",
        ["preset.balanced"] = "均衡",
        ["preset.light"] = "短休息",
        ["preset.pomodoro"] = "番茄钟",
        ["schedule.cycle"] = "一个周期 {0} 分钟",
        ["schedule.timing"] = "时长",
        ["schedule.sit"] = "久坐时长",
        ["schedule.sitDesc"] = "坐满这么久后提醒你站起来（分钟）",
        ["schedule.stand"] = "站立时长",
        ["schedule.standDesc"] = "站满这么久后提醒你坐下（分钟）",
        ["schedule.snooze"] = "稍后提醒时长",
        ["schedule.snoozeDesc"] = "“稍后提醒”推迟的时间（分钟）",
        ["schedule.allowSnooze"] = "允许稍后提醒",
        ["schedule.allowSnoozeDesc"] = "在全屏提醒上显示“稍后提醒”按钮",

        ["reminders.stand"] = "站起来提醒",
        ["reminders.sit"] = "坐下提醒",
        ["reminders.preview"] = "实时预览",
        ["reminders.text"] = "文字",
        ["reminders.previewFull"] = "全屏预览",
        ["reminders.resetTitle"] = "恢复默认文字？",
        ["reminders.resetBody"] = "两种提醒的自定义标题、正文和按钮文字都会被清除。",
        ["custom.hint"] = "留空则使用默认文字。{sit}、{stand}、{snooze} 会被替换为设置的分钟数。",
        ["custom.reset"] = "恢复默认",
        ["field.title"] = "标题",
        ["field.message"] = "正文",
        ["field.confirm"] = "确认按钮",
        ["field.snooze"] = "稍后提醒按钮",
        ["dialog.reset"] = "恢复",
        ["dialog.cancel"] = "取消",
        ["dialog.exit"] = "退出",

        ["general.appearance"] = "外观",
        ["general.theme"] = "应用主题",
        ["general.themeDesc"] = "使用浅色、深色或跟随 Windows",
        ["theme.system"] = "跟随系统",
        ["theme.light"] = "浅色",
        ["theme.dark"] = "深色",
        ["general.backdrop"] = "窗口材质",
        ["general.backdropDesc"] = "窗口背后的半透明材质",
        ["backdrop.mica"] = "云母",
        ["backdrop.micaAlt"] = "云母（强调）",
        ["backdrop.acrylic"] = "亚克力",
        ["general.language"] = "语言",
        ["general.languageDesc"] = "界面和提醒使用的语言",
        ["lang.auto"] = "跟随系统",
        ["lang.zh-CN"] = "中文",
        ["lang.en-US"] = "English",
        ["general.startup"] = "启动",
        ["general.autostart"] = "开机自动启动",
        ["general.autostartDesc"] = "登录 Windows 后在托盘中运行 StandUP",
        ["general.about"] = "关于",
        ["general.aboutDesc"] = "温和的全屏提醒，帮你在坐与站之间切换。",
        ["general.version"] = "版本 {0}",
        ["general.dataFolder"] = "打开数据文件夹",
        ["general.exitDesc"] = "关闭窗口后 StandUP 会继续在托盘运行；退出会停止所有提醒。",
        ["general.exitTitle"] = "退出 StandUP？",
        ["general.exitBody"] = "再次启动 StandUP 之前不会收到任何提醒。",
        ["lock.header"] = "锁屏时",
        ["lock.standing"] = "算作站立",
        ["lock.standing.desc"] = "锁屏按站立休息计算。锁屏时长达到站立时长时，解锁后重新开始久坐计时；否则接着之前的进度继续。",
        ["lock.standing.short"] = "锁屏时间按站立计算。",
        ["lock.sitting"] = "算作坐着",
        ["lock.sitting.desc"] = "锁屏时间计入久坐时间，就像你仍坐在桌前；站立休息时锁屏会结束这次休息。",
        ["lock.sitting.short"] = "锁屏时间计入久坐。",
        ["lock.continue"] = "继续计时",
        ["lock.continue.desc"] = "锁屏期间当前阶段照常计时，到点的提醒会在解锁后显示。",
        ["lock.continue.short"] = "锁屏期间继续计时。",
        ["lock.pause"] = "暂停计时",
        ["lock.pause.desc"] = "锁屏期间暂停计时，解锁后从原来的进度继续。",
        ["lock.pause.short"] = "锁屏期间暂停计时。",
        ["lock.reset"] = "解锁后重新计时",
        ["lock.reset.desc"] = "每次解锁都重新开始久坐计时。",
        ["lock.reset.short"] = "解锁后重新开始久坐计时。",
        ["home.trayNote"] = "关闭窗口后 StandUP 会继续在托盘运行。",
    };
}
