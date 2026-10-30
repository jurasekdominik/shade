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
    public string Kind { get; init; } = PaperStructureKind.Text;
    public PaperBounds Bounds { get; init; } = new(10, 10, 50, 8);
    public string FieldKey { get; init; } = "";
    public string Label { get; init; } = "";
    public string Title { get; init; } = "";
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
    /// <summary>An image structure's picture: a PNG, JPEG or SVG data URI ("data:image/png;base64,..."), stored in the layout.
    /// An SVG may not contain scripts, entity declarations or links to other files.</summary>
    public string? Image { get; init; }
    /// <summary>An image keeps its picture's proportions: resizing changes both sides, and the picture is never stretched.</summary>
    public bool KeepAspectRatio { get; init; } = true;
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
    /// <summary>Where page numbers print: in the top or bottom margin, at its left, centre or right.</summary>
    public PaperPageNumberPosition PageNumberPosition { get; init; }
    public PaperPageNumberFormat PageNumberFormat { get; init; }
    internal bool PageNumbersOnTop => PageNumberPosition is PaperPageNumberPosition.TopLeft or PaperPageNumberPosition.TopCenter or PaperPageNumberPosition.TopRight;
    internal TextAlignment PageNumberAlignment => PageNumberPosition is PaperPageNumberPosition.BottomLeft or PaperPageNumberPosition.TopLeft ? TextAlignment.Left
        : PageNumberPosition is PaperPageNumberPosition.BottomRight or PaperPageNumberPosition.TopRight ? TextAlignment.Right : TextAlignment.Center;
    internal string PageNumberText(int page, int pages) => PageNumberFormat switch
    {
        PaperPageNumberFormat.NumberOfTotal => Look.T("{0} / {1}", page, pages),
        PaperPageNumberFormat.Number => page.ToString(CultureInfo.InvariantCulture),
        PaperPageNumberFormat.PageNumber => Look.T("Page {0}", page),
        PaperPageNumberFormat.Dashes => Look.T("– {0} –", page),
        _ => Look.T("Page {0} of {1}", page, pages),
    };
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
    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, Converters = { new JsonStringEnumConverter() } };
    internal static bool Positive(double n) => double.IsFinite(n) && n > 0;
    internal static bool Nonnegative(double n) => double.IsFinite(n) && n >= 0;
    internal static bool ColorValid(string? color) => color == null || Color.TryParse(color, out _);
    internal static string CssColor(string? color, string fallback)
    {
        if (color == null) return fallback;
        var c = Color.Parse(color); return $"rgba({c.R},{c.G},{c.B},{Number(c.A / 255d)})";
    }
    internal const int MaxImageBytes = 10_000_000;
    /// <summary>The bytes of a PNG, JPEG or SVG data URI, or null when it is anything else.</summary>
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<string, object> checkedImages = new();
    /// <summary>Validate decodes each picture once, not on every edit.</summary>
    static bool ImageValid(string uri) => ImagePixels(uri) != null;
    /// <summary>A picture's pixel size from its PNG or JPEG header (cached per data URI), or null when it is neither.</summary>
    internal static Size? ImagePixels(string uri)
    {
        if (checkedImages.TryGetValue(uri, out var known)) return (Size)known;
        if (ImageBytes(uri) is not { } b || HeaderSize(b) is not { } size) return null;
        checkedImages.AddOrUpdate(uri, size); return size;
    }
    internal static bool IsSvg(byte[] b) { int i = b.Length > 2 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF ? 3 : 0; while (i < b.Length && b[i] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n') i++; return i < b.Length && b[i] == '<'; }
    // shortcut: a pattern check, not a full SVG sanitizer; it refuses anything that could run or fetch (scripts, events, entities, outside links)
    static readonly Regex UnsafeSvg = new(@"<!ENTITY|<script|<foreignObject|@import|\son[a-z]+\s*=|(?:xlink:)?href\s*=\s*[""'](?!#|data:image/)|url\(\s*[""']?(?!#|data:image/)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    static Size? SvgSize(byte[] b)
    {
        string text = System.Text.Encoding.UTF8.GetString(b);
        if (UnsafeSvg.IsMatch(text) || Regex.Match(text, @"<svg\b[^>]*>", RegexOptions.IgnoreCase) is not { Success: true } root) return null;
        double Attr(string name) => Regex.Match(root.Value, name + @"\s*=\s*[""']\s*([0-9.]+)\s*(px)?\s*[""']") is { Success: true } m && double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : 0;
        if (Regex.Match(root.Value, @"viewBox\s*=\s*[""']\s*[-0-9.]+[\s,]+[-0-9.]+[\s,]+([0-9.]+)[\s,]+([0-9.]+)") is { Success: true } box
            && double.TryParse(box.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double w) && double.TryParse(box.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double h) && w > 0 && h > 0)
            return new Size(w, h);
        return Attr("width") > 0 && Attr("height") > 0 ? new Size(Attr("width"), Attr("height")) : null;
    }
    static Size? HeaderSize(byte[] b)
    {
        if (IsSvg(b)) return SvgSize(b);
        int Big(int at, int n) { int v = 0; for (int i = 0; i < n; i++) v = v << 8 | b[at + i]; return v; }
        if (b.Length >= 24 && b[0] == 0x89 && b[1] == 'P' && b[2] == 'N' && b[3] == 'G' && b[12] == 'I' && b[13] == 'H' && b[14] == 'D' && b[15] == 'R')
            return Big(16, 4) > 0 && Big(20, 4) > 0 ? new Size(Big(16, 4), Big(20, 4)) : null;
        if (b.Length < 4 || b[0] != 0xFF || b[1] != 0xD8) return null;
        for (int i = 2; i + 9 < b.Length;)
        {
            if (b[i] != 0xFF) return null;
            int marker = b[i + 1];
            if (marker is >= 0xC0 and <= 0xCF and not (0xC4 or 0xC8 or 0xCC)) return Big(i + 7, 2) > 0 && Big(i + 5, 2) > 0 ? new Size(Big(i + 7, 2), Big(i + 5, 2)) : null;  // start of frame: height, then width
            i += 2 + Big(i + 2, 2);
        }
        return null;
    }
    internal static byte[]? ImageBytes(string uri)
    {
        int comma = uri.IndexOf(',');
        if (comma < 0 || uri.Length > MaxImageBytes * 4 / 3 + 64 || uri[..comma] is not ("data:image/png;base64" or "data:image/jpeg;base64" or "data:image/svg+xml;base64")) return null;
        var bytes = new byte[(uri.Length - comma) * 3 / 4 + 3];
        return Convert.TryFromBase64String(uri[(comma + 1)..], bytes, out int n) ? bytes[..n] : null;
    }
    internal static string Number(double n) => n.ToString("0.######", CultureInfo.InvariantCulture);
    internal const double DipsPerMm = 96 / 25.4;

    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<string, object> checkedDateFormats = new();
    internal static bool DateFormatValid(string? format)
    {
        if (format == null || format.Length > 64) return false;
        return (bool)checkedDateFormats.GetValue(format, static text =>
        {
            var tokens = Regex.Matches(text, "yyyy|MM|M|dd|d").Select(m => m.Value).ToArray();
            return tokens.Length == 3 && tokens.Count(t => t == "yyyy") == 1 && tokens.Count(t => t[0] == 'M') == 1 && tokens.Count(t => t[0] == 'd') == 1
                && Regex.IsMatch(Regex.Replace(text, "yyyy|MM|M|dd|d", ""), @"^[ ./-]*$");
        });
    }
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<PaperFormDefinition, object> validatedDefinitions = new();
    internal void ValidateForAssignment() => validatedDefinitions.GetValue(this, static definition => { definition.Validate(); return new object(); });
    static void Require(bool ok, string rule) { if (!ok) throw new ArgumentException(rule); }
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<PaperStructure, object> validatedStructures = new();
    static object ValidateStructure(PaperStructure s)
    {
        string at = $"Structure '{s.Id}': ";
        Require(!string.IsNullOrWhiteSpace(s.Kind) && s.FieldKey != null && s.Label != null && s.Title != null && s.Subtitle != null && s.CellRanges != null && s.Properties != null && s.Bounds != null, at + "Kind, texts, ranges, properties and bounds are required.");
        Require(double.IsFinite(s.Bounds!.X) && double.IsFinite(s.Bounds.Y) && Math.Abs(s.Bounds.X) <= 10000 && Math.Abs(s.Bounds.Y) <= 10000, at + "the position is out of range.");
        Require(Positive(s.Bounds.Width) && s.Bounds.Width <= 2000 && Positive(s.Bounds.Height) && s.Bounds.Height <= 2000, at + "the size must be positive and at most 2000 mm.");
        Require(Positive(s.CellWidth) && s.CellWidth <= 2000 && s.CellCount is >= 1 and <= 1000, at + "cells need a positive width and a count from 1 to 1000.");
        Require(Positive(s.FontSize) && s.FontSize <= 256, at + "the font size must be positive and at most 256.");
        Require(Nonnegative(s.BorderWidth) && s.BorderWidth <= 2000 && Nonnegative(s.TextPadding) && s.TextPadding <= 100, at + "the border width or text padding is out of range.");
        Require(Enum.IsDefined(s.LineOrientation) && Enum.IsDefined(s.Alignment) && s.GridAlignment is TextAlignment.Left or TextAlignment.Center or TextAlignment.Right, at + "the orientation or alignment is not supported.");
        Require(DateFormatValid(s.DateFormat), at + "DateFormat needs one yyyy, one M or MM and one d or dd, separated by spaces, dots, slashes or hyphens.");
        Require(ColorValid(s.Background) && ColorValid(s.Foreground) && ColorValid(s.BorderColor), at + "a colour is not valid.");
        Require(s.Image == null || ImageValid(s.Image), at + "Image must be a PNG, JPEG or self-contained SVG data URI of at most 10 MB.");
        Require(!PaperStructureKind.HasValue(s.Kind) || !string.IsNullOrWhiteSpace(s.FieldKey), at + "an input needs a FieldKey.");
        Require(s.CellRanges!.All(r => r != null && r.Start >= 0 && r.Length >= 1 && ColorValid(r.Background) && ColorValid(r.Foreground)), at + "a cell range is not valid.");
        return new object();
    }
    /// <summary>Reject malformed definitions, duplicate identities and unsupported schema versions. The message names the rule and structure. Placement problems are reported by the view.</summary>
    public void Validate()
    {
        Require(Version == 1, $"Unsupported schema version {Version}; expected 1.");
        Require(Enum.IsDefined(PaperFormat) && PaperFormat != EditableTable.PaperKind.Screen, "PaperFormat must be a paper size, not Screen.");
        Require(Positive(CustomWidth) && Positive(CustomHeight) && PageSize.Width <= 2000 && PageSize.Height <= 2000, "Page size must be positive and at most 2000 mm.");
        Require(Nonnegative(MarginLeft) && Nonnegative(MarginRight) && Nonnegative(MarginTop) && Nonnegative(MarginBottom), "Margins must be zero or positive.");
        Require(MarginLeft + MarginRight < PageSize.Width && MarginTop + MarginBottom < PageSize.Height, "Margins must leave a printable area.");
        Require(Enum.IsDefined(PageNumberPosition) && Enum.IsDefined(PageNumberFormat), "PageNumberPosition or PageNumberFormat is not supported.");
        Require(!ShowPageNumbers || (PageNumbersOnTop ? MarginTop : MarginBottom) >= 4, "Page numbers need a margin of at least 4 mm on their edge.");
        Require(ColorValid(Background) && ColorValid(Foreground), "Background and Foreground must be colours.");
        Require(Pages is { Count: >= 1 and <= 100 }, "A form has 1 to 100 pages.");
        var ids = new HashSet<string>(StringComparer.Ordinal); var keys = new HashSet<string>(StringComparer.Ordinal);
        int count = 0;
        void Check(IReadOnlyList<PaperStructure> structures, int depth)
        {
            Require(structures != null && depth <= 16, "Structures nest at most 16 levels deep.");
            foreach (var s in structures!)
            {
                Require(s != null && ++count <= 10000, "A form has at most 10000 structures, none of them null.");
                Require(!string.IsNullOrWhiteSpace(s!.Id) && ids.Add(s.Id), $"Structure ID '{s.Id}' is empty or used twice.");
                validatedStructures.GetValue(s!, ValidateStructure);
                if (s!.FieldKey.Length > 0 && !keys.Add(s.FieldKey)) throw new ArgumentException($"Structure '{s.Id}': field key '{s.FieldKey}' is used twice.");
                Check(s.Children, depth + 1);
            }
        }
        var guideIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var page in Pages!)
        {
            Require(page != null && page.Guides != null && page.Guides.Count <= 1000, "A page is missing or has more than 1000 guides.");
            foreach (var guide in page!.Guides!)
                Require(guide != null && !string.IsNullOrWhiteSpace(guide.Id) && guideIds.Add(guide.Id) && Enum.IsDefined(guide.Orientation) && Nonnegative(guide.Position) && guide.Position <= 2000, $"Guide '{guide?.Id}' is not valid or used twice.");
            Check(page.Structures, 0);
        }
    }
}

/// <summary>The built-in structure kinds. A registered <see cref="PaperStructureTemplate"/> adds its own.</summary>
public static class PaperStructureKind
{
    public const string Characters = "characters", Text = "text", Multiline = "multiline", Caption = "caption", Check = "check", Date = "date", Line = "line", Label = "label", Group = "group", Image = "image";
    internal static bool IsBuiltIn(string kind) => kind is Characters or Text or Multiline or Caption or Check or Date or Line or Label or Group or Image;
    /// <summary>The kinds that hold a value and so need a FieldKey.</summary>
    internal static bool HasValue(string kind) => kind is Characters or Text or Multiline or Caption or Check or Date;
}

/// <summary>One separately stored input value. Text is never parsed or truncated. A plain class: build it on any thread; once a
/// view shows it, change it on the UI thread (the inputs bind to it).</summary>
public sealed class PaperFormValue : INotifyPropertyChanged, INotifyDataErrorInfo
{
    string text = ""; bool? isChecked = false; string? error;
    public PaperFormValue(string key) { ArgumentException.ThrowIfNullOrWhiteSpace(key); Key = key; }
    public string Key { get; }
    public string Text { get => text; set => Set(ref text, value ?? ""); }
    public bool? Checked { get => isChecked; set => Set(ref isChecked, value); }
    public string? Error
    {
        get => error;
        set { if (!Set(ref error, value)) return; ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(nameof(Text))); ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(nameof(Checked))); }
    }
    public bool HasErrors => Error != null;
    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;
    public System.Collections.IEnumerable GetErrors(string? propertyName) => Error == null ? Array.Empty<string>() : new[] { Error };
    bool Set<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string name = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)); return true;
    }
}

public enum PaperFormMode { Design, Fill, Preview }
public enum PaperPageNumberPosition { BottomCenter, BottomLeft, BottomRight, TopLeft, TopCenter, TopRight }
/// <summary>"Page 1 of 3", "1 / 3", "1", "Page 1" or "– 1 –"; the words go through Look.Translate.</summary>
public enum PaperPageNumberFormat { PageOfTotal, NumberOfTotal, Number, PageNumber, Dashes }
/// <summary>A problem on the page. Warnings (an overlap, a structure outside the printable area) still allow export; errors do not.</summary>
public sealed record PaperFormProblem(string StructureId, string Message, bool IsWarning = false);
