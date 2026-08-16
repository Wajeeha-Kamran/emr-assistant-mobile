using Microsoft.Maui.Controls.Shapes;

namespace EMRAssistant.Mobile.Controls;

public partial class IconEntry : Border
{
    public IconEntry()
    {
        InitializeComponent();
        BindingContext = this;
    }

    // -- bindable properties ------------------------------------------------

    public static readonly BindableProperty IconProperty =
        BindableProperty.Create(nameof(Icon), typeof(Geometry), typeof(IconEntry), null);

    /// <summary>Vector icon shown at the left of the field. See Brand.xaml.</summary>
    public Geometry? Icon
    {
        get => (Geometry?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public static readonly BindableProperty PlaceholderProperty =
        BindableProperty.Create(nameof(Placeholder), typeof(string), typeof(IconEntry), "");

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(IconEntry), "",
            defaultBindingMode: BindingMode.TwoWay, propertyChanged: OnTextPropertyChanged);

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly BindableProperty IsPasswordProperty =
        BindableProperty.Create(nameof(IsPassword), typeof(bool), typeof(IconEntry), false,
            propertyChanged: OnIsPasswordChanged);

    /// <summary>Masks input and shows the reveal toggle.</summary>
    public bool IsPassword
    {
        get => (bool)GetValue(IsPasswordProperty);
        set => SetValue(IsPasswordProperty, value);
    }

    public static readonly BindableProperty KeyboardTypeProperty =
        BindableProperty.Create(nameof(KeyboardType), typeof(Keyboard), typeof(IconEntry), Keyboard.Default,
            propertyChanged: OnKeyboardChanged);

    public Keyboard KeyboardType
    {
        get => (Keyboard)GetValue(KeyboardTypeProperty);
        set => SetValue(KeyboardTypeProperty, value);
    }

    /// <summary>Raised when the user presses enter or the keyboard's go key.</summary>
    public event EventHandler? Completed;

    // -- plumbing -----------------------------------------------------------

    private static void OnTextPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (IconEntry)bindable;
        var text = (string)newValue ?? "";
        if (control.Field.Text != text) control.Field.Text = text;
    }

    private static void OnIsPasswordChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (IconEntry)bindable;
        var isPassword = (bool)newValue;
        control.Field.IsPassword = isPassword;
        control.RevealToggle.IsVisible = isPassword;
    }

    private static void OnKeyboardChanged(BindableObject bindable, object oldValue, object newValue)
        => ((IconEntry)bindable).Field.Keyboard = (Keyboard)newValue;

    private void OnTextChanged(object sender, TextChangedEventArgs e) => Text = e.NewTextValue ?? "";

    private void OnCompleted(object sender, EventArgs e) => Completed?.Invoke(this, EventArgs.Empty);

    private void OnToggleReveal(object sender, EventArgs e)
    {
        Field.IsPassword = !Field.IsPassword;

        // Tint the icon to show the current state: muted when hidden, brand
        // colour when the password is visible.
        var key = Field.IsPassword ? "TextMuted" : "BrandPurpleLight";
        RevealToggle.Stroke = new SolidColorBrush((Color)Application.Current!.Resources[key]);
    }
}
