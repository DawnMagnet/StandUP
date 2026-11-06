using Microsoft.UI.Xaml.Markup;
using StandUP.Services;

namespace StandUP.Controls;

// XAML markup extension for localized text: Text="{c:L Key=nav.home}".
// Pages are rebuilt when the language changes, so a one-time lookup is enough.
[MarkupExtensionReturnType(ReturnType = typeof(string))]
public sealed partial class L : MarkupExtension
{
    public string Key { get; set; } = string.Empty;

    protected override object ProvideValue() => Loc.T(Key);
}
