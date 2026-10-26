using System;
using Avalonia.Media;

namespace Shade
{
    /// <summary>An icon-only native Shade action with standard command and automation support.</summary>
    public sealed class IconButton : ActionButton
    {
        protected override Type StyleKeyOverride => typeof(IconButton);
        public IconButton() { }
        public IconButton(string data, double size = 18) { Icon = Geometry.Parse(data); IconSize = Look.M(size); }
        public const string Table = "M3,6 L3.01,6 M8,6 L21,6 M3,12 L3.01,12 M8,12 L21,12 M3,18 L3.01,18 M8,18 L21,18";
        public const string Undo = "M9,14 L5,10 L9,6 M5,10 L16,10 A4,4 0 0 1 16,18 L15,18";
        public const string Export = "M14,3 L7,3 L5,5 L5,19 L7,21 L17,21 L19,19 L19,8 L14,3 L14,8 L19,8 M12,10.5 L12,17 M9.2,14.4 L12,17.2 L14.8,14.4";
    }
}
