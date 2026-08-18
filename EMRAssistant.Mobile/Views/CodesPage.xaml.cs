using Microsoft.Maui.Controls.Shapes;

// Same ambiguity as ReviewPage: this file draws vector shapes and touches no
// files, so Path means the MAUI shape.
using Path = Microsoft.Maui.Controls.Shapes.Path;

using EMRAssistant.Mobile.Services;

namespace EMRAssistant.Mobile.Views;

/// <summary>
/// Screen 8. The ranked ICD-10 and CPT suggestions, and the doctor's decision
/// about each.
///
/// Two things about this screen are deliberate and easy to undo by accident.
///
/// The similarity score is never shown. It is a cosine distance between the
/// note text and a code description, not a probability, and a doctor shown
/// "0.83" will read it as a confidence the number does not carry. The ranking
/// is expressed as position in the list and nothing else.
///
/// Nothing arrives accepted. UC-08 leaves the final decision with the doctor,
/// and a pre-ticked list invites someone to tap straight past it.
/// </summary>
[QueryProperty(nameof(SessionId), "sessionId")]
[QueryProperty(nameof(NoteId), "noteId")]
public partial class CodesPage : ContentPage
{
    private readonly ApiClient _api;

    private List<CodeSuggestion> _suggestions = new();
    private bool _signed;

    public CodesPage(ApiClient api)
    {
        InitializeComponent();
        _api = api;
    }

    public string SessionId { get; set; } = "";
    public string NoteId { get; set; } = "";

    private int Session => int.TryParse(SessionId, out var id) ? id : 0;
    private int Note => int.TryParse(NoteId, out var id) ? id : 0;

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_suggestions.Count == 0) await LoadAsync();
    }

    // -- loading ------------------------------------------------------------

    private async Task LoadAsync()
    {
        ShowWorking("Finding matching codes");

        // Whether the note is signed changes what this screen offers, so it is
        // worth one extra call rather than a guess.
        try
        {
            var note = await _api.GetSoapNoteAsync(Session);
            _signed = note.Status == SoapNoteStatuses.Signed;
        }
        catch
        {
            _signed = false;
        }

        try
        {
            var existing = await _api.GetCodeSuggestionsAsync(Note);

            // Generating is the slow part, so only do it when there is nothing
            // to show. Re-entering the screen should be instant.
            _suggestions = existing.Count > 0
                ? existing.ToList()
                : (await _api.GenerateCodeSuggestionsAsync(Note)).ToList();
        }
        catch (Exception ex)
        {
            ShowEmpty(
                title: "Codes couldn't be suggested",
                body: ex is ApiException ? ex.Message
                                         : "The note is unaffected and can still be signed.",
                offerRetry: true);
            return;
        }

        if (_suggestions.Count == 0)
        {
            ShowEmpty(
                title: "No codes matched this note",
                body: "Nothing in the reference set was close enough to suggest.",
                offerRetry: false);
            return;
        }

        BuildGroups();
        ShowList();
    }

    // -- states -------------------------------------------------------------

    private void ShowWorking(string message)
    {
        LoadingLabel.Text = message;
        LoadingView.IsVisible = true;
        GroupsHost.IsVisible = false;
        EmptyView.IsVisible = false;
        NoticeStrip.IsVisible = false;
        CountLabel.IsVisible = false;
        ContinueButton.IsVisible = false;
    }

    private void ShowList()
    {
        LoadingView.IsVisible = false;
        EmptyView.IsVisible = false;
        GroupsHost.IsVisible = true;
        NoticeStrip.IsVisible = true;
        CountLabel.IsVisible = true;
        ContinueButton.IsVisible = true;

        HeaderMeta.Text = _signed
            ? "Part of the signed record"
            : $"Suggested from the note · {_suggestions.Count} to review";

        ContinueLabel.Text = _signed ? "Back to dashboard" : "Continue to signing";
        UpdateCount();
    }

    private void ShowEmpty(string title, string body, bool offerRetry)
    {
        LoadingView.IsVisible = false;
        GroupsHost.IsVisible = false;
        NoticeStrip.IsVisible = false;
        EmptyView.IsVisible = true;

        EmptyTitle.Text = title;
        EmptyBody.Text = body;
        RetryButton.IsVisible = offerRetry;

        EmptyTile.BackgroundColor = offerRetry
            ? BrandPalette.Color("TileWarning", "#FFF0E3")
            : BrandPalette.Color("SurfaceMuted", "#F4F2F9");
        EmptyIcon.Stroke = offerRetry
            ? BrandPalette.Brush("InkWarning", "#E07B39")
            : BrandPalette.Brush("TextMuted", BrandPalette.TextMuted);

        HeaderMeta.Text = "Suggested from the note";
        CountLabel.IsVisible = false;

        // Always a way forward. A note stranded here because its codes failed is
        // exactly what the dashboard's attention list then has to rescue.
        ContinueButton.IsVisible = true;
        ContinueLabel.Text = _signed ? "Back to dashboard" : "Continue to signing anyway";
    }

    // -- the list -----------------------------------------------------------

    private void BuildGroups()
    {
        GroupsHost.Clear();

        foreach (var type in new[] { CodeTypes.Icd10, CodeTypes.Cpt })
        {
            var rows = _suggestions
                .Where(s => string.Equals(s.CodeType, type, StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s.Rank)
                .ToList();

            if (rows.Count == 0) continue;
            GroupsHost.Add(BuildGroup(type, rows));
        }
    }

    private View BuildGroup(string type, IReadOnlyList<CodeSuggestion> rows)
    {
        var head = new VerticalStackLayout
        {
            Spacing = 2,
            Margin = new Thickness(4, 0, 4, 10),
            Children =
            {
                new Label
                {
                    Text = CodeTypes.Title(type),
                    FontFamily = "PlayfairSemiBold",
                    FontSize = 19,
                    TextColor = BrandPalette.Color("TextPrimary", BrandPalette.TextPrimary),
                },
                new Label
                {
                    Text = CodeTypes.Source(type),
                    FontFamily = "InterRegular",
                    FontSize = 11.5,
                    TextColor = BrandPalette.Color("TextMuted", BrandPalette.TextMuted),
                },
            },
        };

        var list = new VerticalStackLayout { Spacing = 0, Margin = new Thickness(0, 4) };
        for (var i = 0; i < rows.Count; i++)
        {
            list.Add(BuildRow(rows[i], type));
            if (i < rows.Count - 1)
                list.Add(new BoxView
                {
                    HeightRequest = 1,
                    Color = Color.FromArgb("#EFECF5"),
                    Margin = new Thickness(16, 0),
                });
        }

        var card = new Grid();
        card.Add(new Border
        {
            BackgroundColor = BrandPalette.Color("Surface", "#FFFFFF"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 20 },
            InputTransparent = true,
            Shadow = new Shadow
            {
                Brush = Brush.Black,
                Offset = new Point(0, 5),
                Radius = 14,
                Opacity = 0.06f,
            },
        });
        card.Add(list);

        return new VerticalStackLayout { Spacing = 0, Children = { head, card } };
    }

    /// <summary>
    /// One suggestion.
    ///
    /// The server stores a plain boolean, so there are two states here and not
    /// three: accepted, or not. "Not accepted" is also the state every row
    /// starts in, which is why it is drawn as ordinary rather than as a
    /// rejection - a strikethrough on ten untouched rows would read as though
    /// the system had already refused them.
    /// </summary>
    /// <summary>
    /// One suggestion.
    ///
    /// The server stores a plain boolean, so there are two states here and not
    /// three: accepted, or not. "Not accepted" is also the state every row
    /// starts in, which is why it is drawn as ordinary rather than as a
    /// rejection - a strikethrough on ten untouched rows would read as though
    /// the system had already refused them.
    ///
    /// LAYOUT: the description sits on its own line, not in a star column
    /// beside the chip. A wrapping Label inside a star column is measured at
    /// unbounded width by the surrounding stack layout, reports the width of a
    /// single line, and the row then overflows the card and carries its own
    /// controls outside the parent's bounds - where they stop receiving taps.
    /// The top row here holds only fixed-size children, so nothing can wrap
    /// inside it. Real CPT descriptions run to a dozen words, so they need the
    /// full width anyway.
    /// </summary>
    private View BuildRow(CodeSuggestion suggestion, string type)
    {
        var isIcd = string.Equals(type, CodeTypes.Icd10, StringComparison.OrdinalIgnoreCase);
        var accepted = suggestion.Accepted;

        var tint = new Border
        {
            BackgroundColor = Colors.Transparent,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 14 },
            InputTransparent = true,
        };

        var chip = new Border
        {
            BackgroundColor = Color.FromArgb(isIcd ? "#EDE7FB" : "#E2F4F1"),
            Padding = new Thickness(10, 6),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 9 },
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Center,
            Content = new Label
            {
                Text = suggestion.Code,
                FontFamily = "InterSemiBold",
                FontSize = 12.5,
                TextColor = Color.FromArgb(isIcd ? "#5B2E9D" : "#0F6E62"),
            },
        };

        var description = new Label
        {
            Text = suggestion.Description,
            FontFamily = "InterRegular",
            FontSize = 12.5,
            LineHeight = 1.35,
            LineBreakMode = LineBreakMode.WordWrap,
            TextColor = BrandPalette.Color("TextPrimary", BrandPalette.TextPrimary),
            Margin = new Thickness(0, 8, 0, 0),
        };

        // Remove. Enabled only when there is something to remove, which is the
        // honest reading of a boolean flag.
        var removeGlyph = new Path
        {
            Data = Icon("IconClose"),
            Stroke = BrandPalette.Brush("TextMuted", BrandPalette.TextMuted),
            StrokeThickness = 1.8,
            StrokeLineCap = PenLineCap.Round,
            Fill = Brush.Transparent,
            Aspect = Stretch.Uniform,
            WidthRequest = 14,
            HeightRequest = 14,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            InputTransparent = true,
        };

        // The explicit transparent background matters: a layout with no
        // background of its own is not reliably hit-tested, so the tap would
        // fall through to the page.
        var remove = new Grid
        {
            WidthRequest = 38,
            HeightRequest = 38,
            BackgroundColor = Colors.Transparent,
        };
        remove.Add(new Border
        {
            BackgroundColor = Colors.Transparent,
            Stroke = new SolidColorBrush(Color.FromArgb("#E4E0EC")),
            StrokeThickness = 1.4,
            StrokeShape = new RoundRectangle { CornerRadius = 19 },
            InputTransparent = true,
        });
        remove.Add(removeGlyph);

        var acceptLabel = new Label
        {
            FontFamily = "InterSemiBold",
            FontSize = 12.5,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            InputTransparent = true,
        };
        var acceptFill = new Border
        {
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            InputTransparent = true,
        };
        var accept = new Grid
        {
            HeightRequest = 38,
            WidthRequest = 96,
            BackgroundColor = Colors.Transparent,
        };
        accept.Add(acceptFill);
        accept.Add(acceptLabel);

        void Paint(bool isAccepted)
        {
            tint.BackgroundColor = isAccepted
                ? BrandPalette.Color("TileSecure", "#E9F7F0")
                : Colors.Transparent;

            acceptFill.BackgroundColor = isAccepted
                ? BrandPalette.Color("SecureGreen", "#1F8A5B")
                : BrandPalette.Color("TileSecure", "#E9F7F0");
            acceptLabel.Text = isAccepted ? "Accepted" : "Accept";
            acceptLabel.TextColor = isAccepted
                ? Colors.White
                : BrandPalette.Color("SecureGreen", "#1F8A5B");

            // Nothing to remove until something has been accepted.
            remove.Opacity = isAccepted ? 1 : 0.35;
            remove.IsEnabled = isAccepted;
        }

        Paint(accepted);

        if (!_signed)
        {
            accept.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(async () => await SetAsync(suggestion, true, Paint)),
            });
            remove.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(async () => await SetAsync(suggestion, false, Paint)),
            });
        }
        else
        {
            // Signed: the decision is part of the record. Hide the controls
            // rather than disable them - a disabled control still invites a tap.
            accept.IsVisible = accepted;
            accept.InputTransparent = true;
            remove.IsVisible = false;
        }

        var controls = new HorizontalStackLayout
        {
            Spacing = 10,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Center,
            Children = { remove, accept },
        };

        // Chip left, controls right, a star column between them holding nothing
        // that can wrap.
        var head = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
            },
            ColumnSpacing = 10,
        };
        head.Add(chip, 0);
        head.Add(controls, 2);

        var content = new VerticalStackLayout
        {
            Spacing = 0,
            Margin = new Thickness(14, 14),
            Children = { head, description },
        };

        var row = new Grid();
        row.Add(tint);
        row.Add(content);
        return row;
    }

    private async Task SetAsync(CodeSuggestion suggestion, bool accepted, Action<bool> paint)
    {
        // Paint first. The call is fast and the tap must not feel laggy; if it
        // fails the row is put back and the reason shown.
        paint(accepted);

        try
        {
            var updated = await _api.UpdateCodeSuggestionAsync(Note, suggestion.Id, accepted);

            var index = _suggestions.FindIndex(s => s.Id == suggestion.Id);
            if (index >= 0) _suggestions[index] = updated;

            paint(updated.Accepted);
            UpdateCount();
        }
        catch (Exception ex)
        {
            paint(suggestion.Accepted);
            UpdateCount();
            await DisplayAlert("Not saved",
                ex is ApiException ? ex.Message : "That change could not be saved.", "OK");
        }
    }

    private void UpdateCount()
    {
        var accepted = _suggestions.Count(s => s.Accepted);
        var rest = _suggestions.Count - accepted;

        CountLabel.Text = _signed
            ? $"{accepted} accepted"
            : $"{accepted} accepted · {rest} not accepted";
    }

    // -- actions ------------------------------------------------------------

    private async void OnRetryTapped(object sender, EventArgs e)
    {
        _suggestions.Clear();
        await LoadAsync();
    }

    private async void OnContinueTapped(object sender, EventArgs e)
    {
        if (_signed)
        {
            await Shell.Current.GoToAsync("//HomePage");
            return;
        }

        await Shell.Current.GoToAsync(
            $"{nameof(SignPage)}?sessionId={Session}&noteId={Note}");
    }

    private async void OnBackTapped(object sender, EventArgs e)
        => await Shell.Current.GoToAsync("..");

    /// <summary>
    /// A named PathGeometry from Brand.xaml. TryGetValue, never the indexer -
    /// the indexer does not search merged dictionaries and throws.
    /// </summary>
    private static Geometry? Icon(string key)
        => Application.Current?.Resources is { } resources
           && resources.TryGetValue(key, out var value)
            ? value as Geometry
            : null;
}
