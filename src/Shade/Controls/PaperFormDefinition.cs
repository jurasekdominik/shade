using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Layout;
using System.Text.RegularExpressions;

namespace Shade;

/// <summary>Document coordinates in millimetres, independent of display scale and zoom.</summary>
public sealed record PaperBounds(double X, double Y, double Width, double Height)
{
    internal Rect Rect => new(X, Y, Width, Height);
}

/// <summary>A fixed range of character cells. Colours are optional HTML colour strings, owned by the document.</summary>
public sealed record PaperCellRange(int Start, int Length, string? Background = null, string? Foreground = null);

/// <summary>An immutable layout snapshot. Children of groups use coordinates relative to their group.</summary>
public sealed record PaperStructure
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Kind { get; init; } = "text";
    public PaperBounds Bounds { get; init; } = new(10, 10, 50, 8);
    public string FieldKey { get; init; } = "";
    public string Label { get; init; } = "";
    public string Caption { get; init; } = "";
    public string Subtitle { get; init; } = "";
    public bool IsReadOnly { get; init; }
    public bool SnapToGrid { get; init; } = true;
    /// <summary>Permit an editable multiline draft to scroll. Export still requires the complete value to fit.</summary>
    public bool AllowMultilineScrolling { get; init; }
    public Orientation LineOrientation { get; init; }
    public int CellCount { get; init; } = 16;
    /// <summary>Fixed cell width in millimetres; resizing the structure never stretches its characters.</summary>
    public double CellWidth { get; init; } = 4;
    public double FontSize { get; init; } = 12;
    /// <summary>Inset of ordinary text and multiline values, in millimetres. Character cells keep their own fixed geometry.</summary>
    public double TextPadding { get; init; } = 1;
    public string? FontFamily { get; init; }
    public TextAlignment Alignment { get; init; }
    /// <summary>Alignment of the complete character grid inside its allocated bounds, independently of glyph alignment.</summary>
    public TextAlignment GridAlignment { get; init; }
    public string DateFormat { get; init; } = "yyyy-MM-dd";
    public string? Background { get; init; }
    public string? Foreground { get; init; }
    public string? BorderColor { get; init; }
    public double BorderWidth { get; init; } = .2;
    public IReadOnlyList<PaperCellRange> CellRanges { get; init; } = Array.Empty<PaperCellRange>();
    public IReadOnlyList<PaperStructure> Children { get; init; } = Array.Empty<PaperStructure>();
    /// <summary>JSON properties for registered custom structure types; Shade does not interpret these.</summary>
    public IReadOnlyDictionary<string, string> Properties { get; init; } = new Dictionary<string, string>();
}

public enum PaperGuideOrientation { Horizontal, Vertical }
public sealed record PaperGuide(string Id, PaperGuideOrientation Orientation, double Position);

public sealed record PaperPage
{
    public IReadOnlyList<PaperGuide> Guides { get; init; } = Array.Empty<PaperGuide>();
    public IReadOnlyList<PaperStructure> Structures { get; init; } = Array.Empty<PaperStructure>();
}

/// <summary>Versioned, storage-independent layout. Values are saved separately. Treat all collections as snapshots.</summary>
public sealed record PaperFormDefinition
{
    public int Version { get; init; } = 1;
    public EditableTable.PaperKind PaperFormat { get; init; } = EditableTable.PaperKind.A4;
    public bool Landscape { get; init; }
    public bool ShowPageNumbers { get; init; }
    public double CustomWidth { get; init; } = 210;
    public double CustomHeight { get; init; } = 297;
    public double MarginLeft { get; init; } = 10;
    public double MarginTop { get; init; } = 10;
    public double MarginRight { get; init; } = 10;
    public double MarginBottom { get; init; } = 10;
    public string? Background { get; init; }
    public string? Foreground { get; init; }
    public IReadOnlyList<PaperPage> Pages { get; init; } = new[] { new PaperPage() };
    [JsonIgnore]
    public Size PageSize
    {
        get
        {
            var size = PaperFormat switch
            {
                EditableTable.PaperKind.A5 => new Size(148, 210),
                EditableTable.PaperKind.A4 => new Size(210, 297),
                EditableTable.PaperKind.A3 => new Size(297, 420),
                EditableTable.PaperKind.Letter => new Size(215.9, 279.4),
                EditableTable.PaperKind.Legal => new Size(215.9, 355.6),
                _ => new Size(CustomWidth, CustomHeight)
            };
            return Landscape ? new Size(size.Height, size.Width) : size;
        }
    }
    public string ToJson() { Validate(); return JsonSerializer.Serialize(this, JsonOptions); }
    public static PaperFormDefinition FromJson(string json)
    {
        var result = JsonSerializer.Deserialize<PaperFormDefinition>(json, JsonOptions) ?? throw new ArgumentException("Missing paper form.", nameof(json));
        result.Validate(); return result;
    }
    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    internal static bool Positive(double n) => double.IsFinite(n) && n > 0;
    internal static bool Nonnegative(double n) => double.IsFinite(n) && n >= 0;
    internal static bool ColorValid(string? color) => color == null || Color.TryParse(color, out _);
    internal static string CssColor(string? color, string fallback)
    {
        if (color == null) return fallback;
        var c = Color.Parse(color); return $"rgba({c.R},{c.G},{c.B},{Number(c.A / 255d)})";
    }
    internal static string Number(double n) => n.ToString("0.######", CultureInfo.InvariantCulture);
    internal const double DipsPerMm = 96 / 25.4;

    internal static bool DateFormatValid(string? format)
    {
        if (format == null || format.Length > 64) return false;
        var tokens = Regex.Matches(format, "yyyy|MM|M|dd|d").Select(m => m.Value).ToArray();
        return tokens.Length == 3 && tokens.Count(t => t == "yyyy") == 1 && tokens.Count(t => t[0] == 'M') == 1 && tokens.Count(t => t[0] == 'd') == 1
            && Regex.IsMatch(Regex.Replace(format, "yyyy|MM|M|dd|d", ""), @"^[ ./-]*$");
    }
    /// <summary>Reject malformed definitions, duplicate identities and unsupported schema versions. Placement problems are reported by the view.</summary>
    public void Validate()
    {
        if (Version != 1 || !Enum.IsDefined(PaperFormat) || PaperFormat == EditableTable.PaperKind.Screen || !Positive(CustomWidth) || !Positive(CustomHeight) ||
            PageSize.Width > 2000 || PageSize.Height > 2000 || !Nonnegative(MarginLeft) || !Nonnegative(MarginRight) || !Nonnegative(MarginTop) || !Nonnegative(MarginBottom) ||
            ShowPageNumbers && MarginBottom < 4 || MarginLeft + MarginRight >= PageSize.Width || MarginTop + MarginBottom >= PageSize.Height || !ColorValid(Background) || !ColorValid(Foreground) || Pages == null || Pages.Count is < 1 or > 100)
            throw new ArgumentException("Invalid paper settings or schema version.");
        var ids = new HashSet<string>(StringComparer.Ordinal); var keys = new HashSet<string>(StringComparer.Ordinal);
        int count = 0;
        void Check(IReadOnlyList<PaperStructure> structures, int depth)
        {
            if (structures == null || depth > 16) throw new ArgumentException("Invalid structure hierarchy.");
            foreach (var s in structures)
            {
                if (s == null || ++count > 10000 || string.IsNullOrWhiteSpace(s.Id) || !ids.Add(s.Id) || string.IsNullOrWhiteSpace(s.Kind) || s.FieldKey == null ||
                    s.Label == null || s.Caption == null || s.Subtitle == null || s.Bounds == null || !double.IsFinite(s.Bounds.X) || !double.IsFinite(s.Bounds.Y) ||
                    Math.Abs(s.Bounds.X) > 10000 || Math.Abs(s.Bounds.Y) > 10000 || !Positive(s.Bounds.Width) || s.Bounds.Width > 2000 || !Positive(s.Bounds.Height) || s.Bounds.Height > 2000 || !Positive(s.CellWidth) || s.CellWidth > 2000 || s.CellCount is < 1 or > 1000 || !Positive(s.FontSize) || s.FontSize > 256 ||
                    !Nonnegative(s.BorderWidth) || s.BorderWidth > 2000 || !Nonnegative(s.TextPadding) || s.TextPadding > 100 || !Enum.IsDefined(s.LineOrientation) || !Enum.IsDefined(s.GridAlignment) || s.GridAlignment is not (TextAlignment.Left or TextAlignment.Center or TextAlignment.Right) || !DateFormatValid(s.DateFormat) || !Enum.IsDefined(s.Alignment) || !ColorValid(s.Background) || !ColorValid(s.Foreground) || !ColorValid(s.BorderColor) ||
                    s.CellRanges == null || s.Properties == null || (s.Kind is "characters" or "text" or "multiline" or "caption" or "check" or "date") && string.IsNullOrWhiteSpace(s.FieldKey) || s.FieldKey.Length > 0 && !keys.Add(s.FieldKey))
                    throw new ArgumentException("Invalid structure, duplicate ID or duplicate field key.");
                if (s.CellRanges.Any(r => r == null || r.Start < 0 || r.Length < 1 || !ColorValid(r.Background) || !ColorValid(r.Foreground))) throw new ArgumentException("Invalid cell decoration.");
                Check(s.Children, depth + 1);
            }
        }
        var guideIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var page in Pages)
        {
            if (page == null || page.Guides == null || page.Guides.Count > 1000) throw new ArgumentException("Missing page or invalid guides.");
            foreach (var guide in page.Guides)
                if (guide == null || string.IsNullOrWhiteSpace(guide.Id) || !guideIds.Add(guide.Id) || !Enum.IsDefined(guide.Orientation) || !Nonnegative(guide.Position) || guide.Position > 2000) throw new ArgumentException("Invalid guide.");
            Check(page.Structures, 0);
        }
    }
}

/// <summary>One separately stored input value. Text is never parsed or truncated.</summary>
public sealed class PaperFormValue : AvaloniaObject, INotifyDataErrorInfo
{
    public string Key { get; }
    public PaperFormValue(string key) { ArgumentException.ThrowIfNullOrWhiteSpace(key); Key = key; }
    public static readonly StyledProperty<string> TextProperty = AvaloniaProperty.Register<PaperFormValue, string>(nameof(Text), "", defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
    public static readonly StyledProperty<bool?> CheckedProperty = AvaloniaProperty.Register<PaperFormValue, bool?>(nameof(Checked), false, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
    public static readonly StyledProperty<string?> ErrorProperty = AvaloniaProperty.Register<PaperFormValue, string?>(nameof(Error));
    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public bool? Checked { get => GetValue(CheckedProperty); set => SetValue(CheckedProperty, value); }
    public string? Error { get => GetValue(ErrorProperty); set => SetValue(ErrorProperty, value); }
    public bool HasErrors => Error != null;
    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;
    public System.Collections.IEnumerable GetErrors(string? propertyName) => Error == null ? Array.Empty<string>() : new[] { Error };
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ErrorProperty) { ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(nameof(Text))); ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(nameof(Checked))); }
    }
}

public enum PaperFormMode { Design, Fill, Preview }
public sealed record PaperFormProblem(string StructureId, string Message);
