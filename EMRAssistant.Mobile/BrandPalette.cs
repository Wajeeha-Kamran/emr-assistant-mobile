namespace EMRAssistant.Mobile;

/// <summary>
/// Reads a colour from Brand.xaml without being able to crash the screen.
///
/// The obvious way to do this is Application.Current.Resources["TextMuted"],
/// but the ResourceDictionary indexer throws KeyNotFoundException on a missing
/// key. A typo in a resource name, or a dictionary that has not merged yet,
/// then takes down whatever the user was doing — and the code that most often
/// reads a colour at runtime is the code that displays an error message, which
/// is the worst possible moment to fail.
///
/// Every lookup here carries the literal from Brand.xaml as a fallback. If the
/// resource is missing the screen renders in a slightly wrong colour instead of
/// throwing, which is always the better failure.
/// </summary>
public static class BrandPalette
{
    // Literals mirror Resources/Styles/Brand.xaml. Used only when the lookup
    // fails, so they never diverge silently in normal operation.
    public const string TextMuted = "#9A94A8";
    public const string TextPrimary = "#241245";
    public const string BrandPurpleLight = "#8B5CD6";
    public const string Danger = "#8C1D18";
    public const string DangerBg = "#F9DEDC";
    public const string Success = "#0F5132";
    public const string SuccessBg = "#D1E7DD";

    public static Color Color(string key, string fallbackHex)
    {
        if (Application.Current?.Resources is { } resources
            && resources.TryGetValue(key, out var value)
            && value is Color colour)
        {
            return colour;
        }

        return Microsoft.Maui.Graphics.Color.FromArgb(fallbackHex);
    }

    public static Brush Brush(string key, string fallbackHex)
        => new SolidColorBrush(Color(key, fallbackHex));
}
