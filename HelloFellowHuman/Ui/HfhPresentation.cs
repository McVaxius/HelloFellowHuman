using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Numerics;
using AethertekUI;
using Dalamud.Bindings.ImGui;

namespace HelloFellowHuman.Ui;

internal enum UiFontRole { Body, BodyStrong, Title, PaneHeading, CompactTitle, CompactPaneHeading }

internal static class HfhPresentation
{
    // Approved regular PNG: 1505x1045; window (17,17)-(1488,1027), header90,
    // tab row70, panes (34,192)-(319,992) and (334,192)-(1473,994), gap15.
    // Compact PNG: 1506x1045; window (28,216)-(1478,779), header78, tabs68,
    // panes (42,372)-(322,763) and (337,372)-(1463,763), gap15.
    // Title30/28 semibold, subtitle16, pane headings22/20; native chrome is host-owned.
    internal const uint ReferenceAccent = 0xFEB995;
    internal static readonly float[] FontSizes = [16, 16, 30, 22, 28, 20];
    internal static readonly string[] FontFiles = ["segoeui.ttf", "seguisb.ttf", "seguisb.ttf", "seguisb.ttf", "seguisb.ttf", "seguisb.ttf"];
    internal static float AtlasHeight(UiFontRole role) => FontSizes[(int)role] * 4 / 3;
    internal static bool Compact => MaterialTheme.Current.Density == MaterialDensity.Compact;
    internal static float HeaderHeight => Compact ? 68 : 82;
    internal static float Gap => Compact ? 10 : 15;
    internal static float ControlHeight => Compact ? 32 : 40;
    internal static Vector4 Rgb(uint rgb) => new(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1);

    internal static MaterialTheme Theme(uint accent)
    {
        accent &= 0xFFFFFF;
        var selected = Rgb(accent);
        var reference = Rgb(ReferenceAccent);
        var seed = MaterialColor.LabToLch(MaterialColor.SrgbToOklab(new(selected.X, selected.Y, selected.Z)));
        var original = MaterialColor.LabToLch(MaterialColor.SrgbToOklab(new(reference.X, reference.Y, reference.Z)));
        var hue = seed.Y < .001f ? 0 : seed.Z - original.Z;
        var chroma = seed.Y < .001f ? 0 : seed.Y / original.Y;
        Vector4 Relative(uint rgb)
        {
            var color = Rgb(rgb);
            if (accent == ReferenceAccent) return color;
            var lch = MaterialColor.LabToLch(MaterialColor.SrgbToOklab(new(color.X, color.Y, color.Z)));
            return new(MaterialColor.GamutMap(lch.X, lch.Y * chroma, lch.Z + hue), 1);
        }
        var background = Relative(0x161F26);
        var foreground = Relative(0xF4F6F8);
        var primary = Relative(ReferenceAccent);
        var palette = new OklchPaletteGenerator().Generate(new(selected.X, selected.Y, selected.Z));
        var colors = new MaterialColorScheme(palette)
        {
            Background = background, OnBackground = foreground,
            Surface = Relative(0x19232B), OnSurface = foreground,
            SurfaceContainerLowest = Relative(0x131D25), SurfaceContainerLow = Relative(0x19232B),
            SurfaceContainer = Relative(0x1C262E), SurfaceContainerHigh = Relative(0x242E37), SurfaceContainerHighest = Relative(0x242E37),
            SurfaceVariant = Relative(0x303C46), OnSurfaceVariant = Relative(0xBDC7D1),
            Outline = Relative(0x71808F), OutlineVariant = Relative(0x303942),
            Primary = primary, OnPrimary = MaterialColor.Contrast(primary, background) >= MaterialColor.Contrast(primary, foreground) ? background : foreground,
            PrimaryContainer = Relative(0x493B35), OnPrimaryContainer = foreground,
            Secondary = Relative(0xEEC2A8), OnSecondary = background, SecondaryContainer = Relative(0x303C46), OnSecondaryContainer = foreground,
            Tertiary = Relative(0xC5CBD1), OnTertiary = background, TertiaryContainer = Relative(0x35424D), OnTertiaryContainer = foreground,
            InverseSurface = foreground, InverseOnSurface = background, InversePrimary = Relative(0x926348),
        };
        return new(colors, MaterialDensity.Standard) { SurfaceOpacity = 1 };
    }

    internal static MaterialControlMetrics Controls(float height = 0)
    {
        if (height <= 0) height = ControlHeight;
        var s = MaterialTheme.Metrics.Scale;
        return new() { Height = height * s, Padding = new(12 * s, Math.Max(0, (height * s - ImGui.GetTextLineHeight()) * .5f)),
            Gap = 8 * s, IconSize = 22 * s, Rounding = 4 * s, ItemSpacing = new(10 * s, 6 * s), CellPadding = new(12 * s, 6 * s) };
    }

    internal static void Surface(Vector2 min, Vector2 max)
    {
        var c = MaterialTheme.Current.Colors;
        MaterialCanvas.Surface(min, max, c.SurfaceContainerHigh, c.Surface, 4 * MaterialTheme.Metrics.Scale);
        ImGui.GetWindowDrawList().AddRect(min, max, MaterialCanvas.Color(c.OutlineVariant), 4 * MaterialTheme.Metrics.Scale);
    }

    internal static void Brand(Vector2 origin, float size)
    {
        var dl = ImGui.GetWindowDrawList(); var ink = MaterialCanvas.Color(MaterialTheme.Current.Colors.Primary);
        dl.AddCircleFilled(origin + new Vector2(.30f, .20f) * size, size * .18f, ink, 24);
        dl.AddCircleFilled(origin + new Vector2(.74f, .32f) * size, size * .14f, ink, 24);
        dl.AddRectFilled(origin + new Vector2(.02f, .48f) * size, origin + new Vector2(.58f, .92f) * size, ink, size * .22f, ImDrawFlags.RoundCornersTop);
        dl.AddRectFilled(origin + new Vector2(.52f, .62f) * size, origin + new Vector2(.98f, .92f) * size, ink, size * .18f, ImDrawFlags.RoundCornersTop);
    }
}
