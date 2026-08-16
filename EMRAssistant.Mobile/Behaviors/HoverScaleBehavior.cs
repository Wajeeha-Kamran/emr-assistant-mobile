namespace EMRAssistant.Mobile.Behaviors;

/// <summary>
/// Smooth scale feedback on hover and press.
///
/// WHY NOT THE VISUAL STATE MANAGER
/// Setting Scale from a VisualState changes the value instantly: the button
/// snaps to the new size with no transition, which reads as a glitch rather
/// than as feedback. ScaleTo animates over a duration with an easing curve, so
/// the button eases into the change.
///
/// The values are deliberately small. A button inside a padded card will visibly
/// overflow its container at 1.03; 1.015 is enough to feel responsive while
/// staying inside its bounds.
/// </summary>
public class HoverScaleBehavior : Behavior<Button>
{
    /// <summary>
    /// Scale when the pointer is over the control.
    ///
    /// Kept very small deliberately. A full-width button inside a padded card
    /// has only a few pixels of room either side, so even 1.5% growth pushes it
    /// visibly past its container. At 1.008 the movement is felt rather than
    /// seen, which is what hover feedback is for.
    /// </summary>
    public double HoverScale { get; set; } = 1.008;

    /// <summary>Scale while pressed. Below 1 so the control feels pushed in.</summary>
    public double PressedScale { get; set; } = 0.992;

    /// <summary>
    /// Milliseconds. The first version used 160ms, which still read as a snap:
    /// below roughly 200ms the eye registers the end state rather than the
    /// movement. 280ms with an ease-in-out curve reads as a glide.
    /// </summary>
    public uint Duration { get; set; } = 280;

    private Button? _button;
    private PointerGestureRecognizer? _pointer;

    protected override void OnAttachedTo(Button button)
    {
        base.OnAttachedTo(button);
        _button = button;

        button.Pressed += OnPressed;
        button.Released += OnReleased;

        // Pointer events only exist where there is a pointer, so this is a
        // desktop refinement. On a phone, Pressed and Released do the work.
        _pointer = new PointerGestureRecognizer();
        _pointer.PointerEntered += OnPointerEntered;
        _pointer.PointerExited += OnPointerExited;
        button.GestureRecognizers.Add(_pointer);
    }

    protected override void OnDetachingFrom(Button button)
    {
        button.Pressed -= OnPressed;
        button.Released -= OnReleased;

        if (_pointer is not null)
        {
            _pointer.PointerEntered -= OnPointerEntered;
            _pointer.PointerExited -= OnPointerExited;
            button.GestureRecognizers.Remove(_pointer);
        }

        _button = null;
        base.OnDetachingFrom(button);
    }

    private async void OnPointerEntered(object? sender, PointerEventArgs e)
    {
        if (_button is not null) await _button.ScaleTo(HoverScale, Duration, Easing.CubicInOut);
    }

    private async void OnPointerExited(object? sender, PointerEventArgs e)
    {
        if (_button is not null) await _button.ScaleTo(1.0, Duration, Easing.CubicInOut);
    }

    private async void OnPressed(object? sender, EventArgs e)
    {
        if (_button is not null) await _button.ScaleTo(PressedScale, 140, Easing.CubicInOut);
    }

    private async void OnReleased(object? sender, EventArgs e)
    {
        if (_button is not null) await _button.ScaleTo(HoverScale, 200, Easing.CubicInOut);
    }
}
