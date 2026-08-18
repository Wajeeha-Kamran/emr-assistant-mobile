using Microsoft.Maui.Controls.Shapes;

// Path is ambiguous: MAUI's vector shape and System.IO.Path, which the implicit
// usings bring in. This file draws icons and touches no files, so the alias
// points at the shape. RecordPage does the opposite - it uses System.IO.Path
// and no shapes - which is why it needs no alias.
using Path = Microsoft.Maui.Controls.Shapes.Path;

using EMRAssistant.Mobile.Services;

namespace EMRAssistant.Mobile.Views;

/// <summary>
/// Screen 7. The SOAP note and the transcript behind it.
///
/// The note is editable while it is a draft; the transcript never is. That
/// split is not a shortcut — UC-03 provides for editing the note's sections,
/// and nothing in the SRS provides for editing a transcript. Keeping the
/// machine's raw output as it was produced is also what makes the note's
/// traceability meaningful.
/// </summary>
[QueryProperty(nameof(SessionId), "sessionId")]
public partial class ReviewPage : ContentPage
{
    private readonly ApiClient _api;

    private SoapNote? _note;
    private Transcript? _transcript;
    private bool _showingNote = true;

    public ReviewPage(ApiClient api)
    {
        InitializeComponent();
        _api = api;
    }

    public string SessionId { get; set; } = "";

    private int Session => int.TryParse(SessionId, out var id) ? id : 0;

    private bool IsSigned => _note?.Status == SoapNoteStatuses.Signed;

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        LoadingView.IsVisible = true;
        SectionsHost.IsVisible = false;
        TranscriptHost.IsVisible = false;
        MessageLabel.IsVisible = false;

        try
        {
            _note = await _api.GetSoapNoteAsync(Session);
        }
        catch (Exception ex)
        {
            LoadingView.IsVisible = false;
            MessageLabel.Text = ex is ApiException
                ? ex.Message
                : "The note could not be opened.";
            MessageLabel.IsVisible = true;
            return;
        }

        try
        {
            _transcript = await _api.GetTranscriptAsync(Session);
        }
        catch
        {
            // The transcript tab can fail on its own without taking the note
            // with it. The note is what matters here.
            _transcript = null;
        }

        StatusChip.Text = IsSigned ? "Signed" : "Draft";
        HeaderMeta.Text = _transcript is null
            ? "Note ready"
            : $"{_transcript.Segments.Count} exchanges";

        BuildSections();
        BuildTranscript();

        LoadingView.IsVisible = false;
        ShowTab(note: true);
    }

    // -- tabs ---------------------------------------------------------------

    private void OnNoteTabTapped(object sender, EventArgs e) => ShowTab(note: true);
    private void OnTranscriptTabTapped(object sender, EventArgs e) => ShowTab(note: false);

    private void ShowTab(bool note)
    {
        _showingNote = note;

        SectionsHost.IsVisible = note;
        TranscriptHost.IsVisible = !note;

        // Colour, not IsVisible. Hiding a child changes the column's desired
        // width, both star columns re-measure to their content, and the Border
        // spanning them collapses with them - which is what shrank the white
        // tab card the moment either tab was tapped. Recolouring changes
        // nothing about the layout.
        var rule = BrandPalette.Color("BrandPurple", BrandPalette.BrandPurpleLight);
        NoteTabRule.Color = note ? rule : Colors.Transparent;
        TranscriptTabRule.Color = note ? Colors.Transparent : rule;

        var active = BrandPalette.Color("BrandPurple", BrandPalette.BrandPurpleLight);
        var idle = BrandPalette.Color("TextSecondary", "#6B6480");

        // Colour and the underline carry the active state. The font family is
        // deliberately NOT changed: swapping InterSemiBold for InterMedium
        // changes the label's measured width, the two-star columns re-measure to
        // fit their content, and the Border spanning them shrinks with them - so
        // the white tab card collapsed to half width the first time either tab
        // was tapped.
        NoteTabLabel.TextColor = note ? active : idle;
        TranscriptTabLabel.TextColor = note ? idle : active;
    }

    // -- the note -----------------------------------------------------------

    private void BuildSections()
    {
        SectionsHost.Clear();
        if (_note is null) return;

        // S, O, A, P. The API returns them in whatever order the database
        // produced; a doctor reads them in one order only.
        foreach (var type in SoapSectionTypes.InOrder)
        {
            var section = _note.Sections.FirstOrDefault(s => s.SectionType == type);
            if (section is null) continue;
            SectionsHost.Add(BuildSectionCard(section));
        }
    }

    private View BuildSectionCard(SoapSection section)
    {
        var reader = new Label
        {
            Text = section.Content,
            FontFamily = "InterRegular",
            FontSize = 13.5,
            LineHeight = 1.5,
            TextColor = BrandPalette.Color("TextPrimary", BrandPalette.TextPrimary),
        };

        var editor = new Editor
        {
            Text = section.Content,
            FontFamily = "InterRegular",
            FontSize = 13.5,
            AutoSize = EditorAutoSizeOption.TextChanges,
            BackgroundColor = BrandPalette.Color("SurfaceMuted", "#F4F2F9"),
            TextColor = BrandPalette.Color("TextPrimary", BrandPalette.TextPrimary),
            IsVisible = false,
            Margin = new Thickness(0, 6, 0, 0),
        };

        var save = new Button
        {
            Text = "Save",
            Style = BrandPalette.LookupStyle("GhostButton"),
            IsVisible = false,
            HorizontalOptions = LayoutOptions.End,
        };
        var cancel = new Button
        {
            Text = "Cancel",
            Style = BrandPalette.LookupStyle("GhostButton"),
            TextColor = BrandPalette.Color("TextMuted", BrandPalette.TextMuted),
            IsVisible = false,
            HorizontalOptions = LayoutOptions.End,
        };

        var pencil = new Path
        {
            Data = Geometry("IconPen"),
            Stroke = BrandPalette.Brush("BrandPurpleLight", BrandPalette.BrandPurpleLight),
            StrokeThickness = 1.6,
            StrokeLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Fill = Brush.Transparent,
            Aspect = Stretch.Uniform,
            WidthRequest = 18,
            HeightRequest = 18,
            VerticalOptions = LayoutOptions.Center,
            // Signing is what makes the note a record. After it, there is
            // nothing to offer here.
            IsVisible = !IsSigned,
        };

        void StartEditing()
        {
            editor.Text = reader.Text;
            reader.IsVisible = false;
            editor.IsVisible = true;
            save.IsVisible = true;
            cancel.IsVisible = true;
            pencil.IsVisible = false;
        }

        void StopEditing()
        {
            reader.IsVisible = true;
            editor.IsVisible = false;
            save.IsVisible = false;
            cancel.IsVisible = false;
            pencil.IsVisible = !IsSigned;
        }

        pencil.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(StartEditing) });
        cancel.Clicked += (_, _) => StopEditing();

        save.Clicked += async (_, _) =>
        {
            var text = editor.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(text))
            {
                await DisplayAlert("Nothing to save",
                    "A section cannot be left empty. Cancel instead if nothing needs changing.", "OK");
                return;
            }

            save.IsEnabled = false;
            try
            {
                _note = await _api.UpdateSectionAsync(_note!.Id, section.Id, text);
                reader.Text = text;
                StopEditing();
            }
            catch (ApiException ex)
            {
                await DisplayAlert("Not saved", ex.Message, "OK");
            }
            finally
            {
                save.IsEnabled = true;
            }
        };

        var letter = new Border
        {
            WidthRequest = 34,
            HeightRequest = 34,
            Padding = 0,
            BackgroundColor = SectionTint(section.SectionType),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 10 },
            VerticalOptions = LayoutOptions.Start,
            Content = new Label
            {
                Text = SoapSectionTypes.Letter(section.SectionType),
                FontFamily = "PlayfairSemiBold",
                FontSize = 17,
                TextColor = SectionInk(section.SectionType),
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
            },
        };

        var head = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
            },
            ColumnSpacing = 12,
        };
        head.Add(letter, 0);
        head.Add(new Label
        {
            Text = SoapSectionTypes.Title(section.SectionType),
            FontFamily = "PlayfairSemiBold",
            FontSize = 18,
            TextColor = BrandPalette.Color("TextPrimary", BrandPalette.TextPrimary),
            VerticalOptions = LayoutOptions.Center,
        }, 1);
        head.Add(pencil, 2);

        var buttons = new HorizontalStackLayout
        {
            Spacing = 4,
            HorizontalOptions = LayoutOptions.End,
            Children = { cancel, save },
        };

        var content = new VerticalStackLayout
        {
            Spacing = 0,
            Margin = new Thickness(16, 16),
            Children = { head, Spacer(10), reader, editor, buttons },
        };

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
        card.Add(content);
        return card;
    }

    private static View Spacer(double height) => new BoxView
    {
        HeightRequest = height,
        Color = Colors.Transparent,
    };

    private static Color SectionTint(string type) => type switch
    {
        SoapSectionTypes.Subjective => Color.FromArgb("#EDE7FB"),
        SoapSectionTypes.Objective => Color.FromArgb("#E4EEFB"),
        SoapSectionTypes.Assessment => Color.FromArgb("#E2F4F1"),
        _ => Color.FromArgb("#FFF0E3"),
    };

    private static Color SectionInk(string type) => type switch
    {
        SoapSectionTypes.Subjective => Color.FromArgb("#5B2E9D"),
        SoapSectionTypes.Objective => Color.FromArgb("#2C5C9E"),
        SoapSectionTypes.Assessment => Color.FromArgb("#0F6E62"),
        _ => Color.FromArgb("#B4611F"),
    };

    // -- the transcript -----------------------------------------------------

    private void BuildTranscript()
    {
        TranscriptHost.Clear();

        if (_transcript is null || _transcript.Segments.Count == 0)
        {
            TranscriptHost.Add(new Label
            {
                Text = "The transcript could not be loaded.",
                Style = BrandPalette.LookupStyle("Subtitle"),
            });
            return;
        }

        // Honest about what the measurement showed, and it does not promise a
        // correction the API cannot perform: speaker labels are not editable.
        var notice = new Grid { HeightRequest = 64, Margin = new Thickness(0, 0, 0, 14) };
        notice.Add(new Border
        {
            BackgroundColor = BrandPalette.Color("TileNotice", "#EFEBFB"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            InputTransparent = true,
        });
        notice.Add(new Label
        {
            Text = "Speaker labels are produced automatically and can be wrong. " +
                   "Correct anything that matters in the note.",
            FontFamily = "InterRegular",
            FontSize = 11.5,
            LineHeight = 1.35,
            TextColor = BrandPalette.Color("BrandPurple", BrandPalette.BrandPurpleLight),
            Margin = new Thickness(16, 0),
            VerticalOptions = LayoutOptions.Center,
            InputTransparent = true,
        });
        TranscriptHost.Add(notice);

        var card = new Grid();
        card.Add(new Border
        {
            BackgroundColor = BrandPalette.Color("Surface", "#FFFFFF"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 20 },
            InputTransparent = true,
        });

        var rows = new VerticalStackLayout { Spacing = 0, Margin = new Thickness(16, 6) };

        for (var i = 0; i < _transcript.Segments.Count; i++)
        {
            var segment = _transcript.Segments[i];
            var isDoctor = segment.SpeakerRole.Equals("DOCTOR", StringComparison.OrdinalIgnoreCase);

            var chip = new Border
            {
                BackgroundColor = Color.FromArgb(isDoctor ? "#EDE7FB" : "#E2F4F1"),
                Padding = new Thickness(9, 4),
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 8 },
                HorizontalOptions = LayoutOptions.Start,
                Content = new Label
                {
                    Text = isDoctor ? "DOCTOR" : "PATIENT",
                    FontFamily = "InterSemiBold",
                    FontSize = 9.5,
                    CharacterSpacing = 1,
                    TextColor = Color.FromArgb(isDoctor ? "#5B2E9D" : "#0F6E62"),
                },
            };

            var head = new HorizontalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    chip,
                    new Label
                    {
                        Text = Stamp(segment.StartTime),
                        Style = BrandPalette.LookupStyle("RowMeta"),
                        VerticalOptions = LayoutOptions.Center,
                    },
                },
            };

            var row = new VerticalStackLayout
            {
                Spacing = 0,
                Margin = new Thickness(0, 12),
                Children =
                {
                    head,
                    new Label
                    {
                        // Inter, never the display serif. This is text read for
                        // accuracy, at a size where a serif is measurably
                        // slower to scan.
                        Text = segment.Text,
                        FontFamily = "InterRegular",
                        FontSize = 13.5,
                        LineHeight = 1.45,
                        TextColor = BrandPalette.Color("TextPrimary", BrandPalette.TextPrimary),
                        Margin = new Thickness(0, 7, 0, 0),
                    },
                },
            };

            rows.Add(row);

            if (i < _transcript.Segments.Count - 1)
                rows.Add(new BoxView { HeightRequest = 1, Color = Color.FromArgb("#EFECF5") });
        }

        card.Add(rows);
        TranscriptHost.Add(card);
    }

    private static string Stamp(double? seconds)
    {
        if (seconds is not { } s) return "";
        return $"{(int)(s / 60):00}:{(int)(s % 60):00}";
    }

    private static Geometry? Geometry(string key)
        => Application.Current?.Resources is { } resources
           && resources.TryGetValue(key, out var value)
            ? value as Geometry
            : null;

    // -- actions ------------------------------------------------------------

    private async void OnContinueTapped(object sender, EventArgs e)
    {
        if (_note is null) return;
        await Shell.Current.GoToAsync(
            $"{nameof(CodesPage)}?sessionId={Session}&noteId={_note.Id}");
    }

    private async void OnBackTapped(object sender, EventArgs e)
        => await Shell.Current.GoToAsync("//HomePage");
}
