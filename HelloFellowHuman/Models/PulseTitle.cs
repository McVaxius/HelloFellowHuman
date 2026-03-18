using System.Numerics;
using Dalamud.Game.Text.SeStringHandling;
using SeString = Dalamud.Game.Text.SeStringHandling.SeString;
using SeStringBuilder = Lumina.Text.SeStringBuilder;

namespace HelloFellowHuman.Models
{
    /// <summary>
    /// Simplified title generator based on Caraxi's CustomTitle pattern
    /// Used for pulse animation with color and glow effects
    /// </summary>
    public class PulseTitle
    {
        public string Title { get; set; } = string.Empty;
        public string Emoji { get; set; } = string.Empty;
        public string Style { get; set; } = "emoji";
        public bool IsPrefix { get; set; } = true; // Always use prefix as requested
        public Vector3? Color { get; set; } = null; // Main color (RGB 0-1)
        public Vector3? Glow { get; set; } = null; // Glow/edge color (RGB 0-1)
        public bool IncludeQuotes { get; set; } = false; // No quotes for pulse animation

        /// <summary>
        /// Generate SeString with color and glow effects (based on Caraxi's ToSeString)
        /// </summary>
        public SeString ToSeString(bool includeColor = true, bool animate = true)
        {
            var displayText = string.IsNullOrEmpty(Emoji) ? Title : Emoji;
            if (string.IsNullOrEmpty(displayText)) return SeString.Empty;
            
            var builder = new SeStringBuilder();
            
            // Add quotes if enabled (not used for pulse animation)
            if (IncludeQuotes) builder.Append("《");
            
            // Add main color
            if (includeColor && Color != null) 
                builder.PushColorRgba(new Vector4(Color.Value, 1));
            
            // Add title with optional glow
            if (Glow != null)
            {
                builder.PushEdgeColorRgba(new Vector4(Glow.Value, 1));
                builder.Append(displayText);
                builder.PopEdgeColor();
            }
            else
            {
                builder.Append(displayText);
            }
            
            // Close main color
            if (includeColor && Color != null) 
                builder.PopColor();
            
            // Close quotes
            if (IncludeQuotes) builder.Append("》");
            
            return SeString.Parse(builder.GetViewAsSpan());
        }
        
        /// <summary>
        /// Convert Vector3 color to hex string (based on Caraxi's pattern)
        /// </summary>
        public static string ColorToHex(Vector3? color)
        {
            if (!color.HasValue) return "No Colour";
            var c = color.Value;
            return $"#{(byte)(c.X * 255):X2}{(byte)(c.Y * 255):X2}{(byte)(c.Z * 255):X2}";
        }
        
        /// <summary>
        /// Convert hex string to Vector3 color
        /// </summary>
        public static Vector3? HexToColor(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex) || !hex.StartsWith("#") || hex.Length != 7)
                return null;
                
            try
            {
                var r = byte.Parse(hex.Substring(1, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
                var g = byte.Parse(hex.Substring(3, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
                var b = byte.Parse(hex.Substring(5, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
                return new Vector3(r, g, b);
            }
            catch
            {
                return null;
            }
        }
    }
}
