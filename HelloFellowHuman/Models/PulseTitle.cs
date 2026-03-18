using System.Numerics;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
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
        /// Convert to SeString with color and glow effects
        /// </summary>
        public SeString ToSeString()
        {
            var seString = new SeString();
            
            // Add color payload if color is specified
            if (Color.HasValue)
            {
                seString.Append(new UIForegroundPayload((ushort)ColorToUInt(Color.Value)));
            }
            
            // Add glow payload if glow is specified
            if (Glow.HasValue)
            {
                seString.Append(new UIGlowPayload((ushort)ColorToUInt(Glow.Value)));
            }
            
            // Add the emoji/icon
            seString.Append(new TextPayload(Emoji));
            
            // Close glow and color payloads in reverse order
            if (Glow.HasValue)
            {
                seString.Append(new UIGlowPayload(0));
            }
            if (Color.HasValue)
            {
                seString.Append(new UIForegroundPayload(0));
            }
            
            return seString;
        }
        
        /// <summary>
        /// Convert Vector3 color to uint for SeString payloads (FFXIV standard format)
        /// </summary>
        private static uint ColorToUInt(Vector3 color)
        {
            // FFXIV uses standard RGB format with values 0-255
            // Format: 0x00RRGGBB (alpha is always 0 for text colors)
            var r = (uint)(color.X * 255);
            var g = (uint)(color.Y * 255);
            var b = (uint)(color.Z * 255);
            return (r << 16) | (g << 8) | b;
        }
        
        /// <summary>
        /// Get raw byte array for the heart symbol to avoid SeString encoding issues
        /// </summary>
        public byte[] GetRawBytes()
        {
            // Return UTF-8 bytes for heart symbol
            return System.Text.Encoding.UTF8.GetBytes("♥");
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
