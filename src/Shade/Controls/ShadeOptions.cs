using Avalonia.Media;
using System.Globalization;
namespace Shade;
/// <summary>Application-wide defaults, applied before building views. Stored settings belong to the consuming app.</summary>
public sealed class ShadeOptions
{
    public double TextScale { get; init; } = 1;
    public double Density { get; init; } = 1;
    public bool ReducedMotion { get; init; }
    public FontFamily? UiFont { get; init; }
    public FontFamily? MonoFont { get; init; }
    public CultureInfo? Culture { get; init; }
}
