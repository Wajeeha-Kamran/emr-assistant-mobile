namespace EMRAssistant.Mobile.Controls;

public partial class WaveformLoader : ContentView
{
    private const string AnimationName = "waveform";

    public WaveformLoader()
    {
        InitializeComponent();
    }

    public static readonly BindableProperty IsRunningProperty =
        BindableProperty.Create(nameof(IsRunning), typeof(bool), typeof(WaveformLoader), false,
            propertyChanged: OnIsRunningChanged);

    public bool IsRunning
    {
        get => (bool)GetValue(IsRunningProperty);
        set => SetValue(IsRunningProperty, value);
    }

    private static void OnIsRunningChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (WaveformLoader)bindable;
        if ((bool)newValue) control.Start();
        else control.Stop();
    }

    /// <summary>
    /// Each bar scales vertically from short to tall and back, staggered so the
    /// motion travels along the row.
    ///
    /// Built with MAUI's Animation class rather than a loop of awaited calls:
    /// the animation runs on the platform's own timer and stops cleanly when the
    /// page goes away, whereas an async loop keeps running after the page has
    /// been navigated off and quietly holds it alive.
    /// </summary>
    public void Start()
    {
        var bars = new View[] { Bar1, Bar2, Bar3, Bar4, Bar5 };

        // Reset, so restarting does not begin from a half-finished pose.
        foreach (var bar in bars) bar.ScaleY = 0.35;

        var parent = new Animation();

        const double stagger = 0.08;   // delay between neighbouring bars
        const double rise = 0.30;      // fraction of the cycle spent growing
        const double fall = 0.30;      // and shrinking

        for (int i = 0; i < bars.Length; i++)
        {
            var bar = bars[i];
            double begin = i * stagger;

            parent.Add(begin, begin + rise,
                new Animation(v => bar.ScaleY = v, 0.35, 1.0, Easing.SinInOut));

            parent.Add(begin + rise, begin + rise + fall,
                new Animation(v => bar.ScaleY = v, 1.0, 0.35, Easing.SinInOut));
        }

        parent.Commit(this, AnimationName, length: 1300, repeat: () => true);
    }

    public void Stop()
    {
        this.AbortAnimation(AnimationName);
        foreach (var bar in new View[] { Bar1, Bar2, Bar3, Bar4, Bar5 })
            bar.ScaleY = 1.0;
    }
}
