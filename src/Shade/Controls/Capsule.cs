using System;
using Avalonia.Media;

namespace Shade;

/// <summary>A compact native Shade action with an accent label and icon.</summary>
public sealed class Capsule : ActionButton
{
    public static readonly Geometry Plus = Icons.Plus, List = Icons.List, Bookmark = Icons.Bookmark;
    protected override Type StyleKeyOverride => typeof(Capsule);
    public Capsule() { }
    public Capsule(string text, Geometry glyph) { Content = text; Icon = glyph; }
}
