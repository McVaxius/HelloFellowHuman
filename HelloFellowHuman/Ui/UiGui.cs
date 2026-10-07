using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Numerics;
using AethertekUI;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace HelloFellowHuman.Ui;

// Native widgets receive their original English labels/IDs. Only their visible label is painted in the selected locale.
// This also preserves English-derived helper IDs and existing saved window identities.
internal static class UiGui
{
    internal static float ButtonWidth(string label, MaterialIcon icon = MaterialIcon.None) => MaterialText.Measure(UiText.T(label.Split("##", 2)[0])).X + 2 * ImGui.GetStyle().FramePadding.X + (icon == MaterialIcon.None ? 0 : 28 * MaterialTheme.Metrics.Scale);
    private static bool ToolbarSwitch(string label) => label is "Enabled" or "DTR ON" or "Krangle";
    internal static float CheckboxWidth(string label) => ToolbarSwitch(label)
        ? MaterialText.Measure(UiText.T(label)).X + 75 * MaterialTheme.Metrics.Scale
        : MathF.Ceiling(ImGui.GetFrameHeight()) + (label.Split("##", 2)[0].Length == 0 ? 0
            : ImGui.GetStyle().ItemInnerSpacing.X + MaterialText.Measure(UiText.T(label.Split("##", 2)[0])).X);
    internal static void SameLineIfFits(float width)
    {
        var edge = ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X;
        if (ImGui.GetItemRectMax().X + ImGui.GetStyle().ItemSpacing.X + width <= edge) ImGui.SameLine();
    }
    internal static bool BeginCombo(string label, string preview, bool translatePreview = true)
    {
        var shown = translatePreview ? UiText.T(preview) : preview;
        using var height = MaterialText.PushLineHeight(MaterialText.RequiresShaping(shown) || MaterialText.RequiresShaping(label) ? "" : UiText.T(label.Split("##", 2)[0]));
        var width = FitField(label, Math.Max(80 * MaterialTheme.Metrics.Scale, MaterialText.Measure(shown).X + ImGui.GetFrameHeight() + 2 * ImGui.GetStyle().FramePadding.X));
        var origin = ImGui.GetCursorScreenPos();
        var parent = ImGuiP.GetCurrentWindow(); var previousMax = parent.DC.CursorMaxPos;
        var parentDrawList = ImGui.GetWindowDrawList();
        var clipped = ClipFieldLabel(label, origin, width, parentDrawList);
        bool open;
        try { open = MaterialText.BeginCombo(label, shown); }
        finally { if (clipped) parentDrawList.PopClipRect(); }
        // BeginCombo enters the popup on success; keep its label on the parent's draw list.
        try { FieldLabel(label, origin, width, parentDrawList, parent, previousMax); }
        catch { if (open) ImGui.EndCombo(); throw; }
        return open;
    }
    internal static bool BeginTabItem(string label, ref bool open, ImGuiTabItemFlags flags)
    {
        var style = ImGui.GetStyle();
        var padding = new Vector2(48, HfhPresentation.Compact ? 10 : 14) * MaterialTheme.Metrics.Scale;
        if (MaterialText.RequiresShaping(UiText.T(label)))
            padding.Y += Math.Max(0, MaterialText.Measure(UiText.T(label)).Y - ImGui.GetTextLineHeight()) * .5f;
        ImGui.SetNextItemWidth(MathF.Ceiling(Math.Max(MaterialText.Measure(label).X, MaterialText.Measure(UiText.T(label)).X) + 2 * padding.X
            + ImGui.GetFontSize() + style.ItemInnerSpacing.X));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, padding);
        var colors = MaterialTheme.Current.Colors;
        var hoverColor = MaterialColor.Layer(colors.Primary, colors.OnPrimary, .08f);
        ImGui.PushStyleColor(ImGuiCol.TabActive, colors.Primary);
        ImGui.PushStyleColor(ImGuiCol.TabUnfocusedActive, colors.Primary);
        ImGui.PushStyleColor(ImGuiCol.TabHovered, hoverColor);
        var selected = ImGui.BeginTabItem(label, ref open, flags); ImGui.PopStyleVar();
        ImGui.PopStyleColor(3);
        var hovered = ImGui.IsItemHovered();
        var color = hovered ? hoverColor : selected ? colors.Primary : style.Colors[(int)ImGuiCol.Tab];
        var foreground = selected || hovered ? colors.OnPrimary : style.Colors[(int)ImGuiCol.Text];
        try
        {
            Label(label, ImGui.GetItemRectMin() + padding, color,
                foreground, repaint: true);
            var iconOrigin = ImGui.GetItemRectMin() + padding - new Vector2(30 * MaterialTheme.Metrics.Scale, 0);
            MaterialIcons.Draw(label == "Presets" ? MaterialIcon.List : MaterialIcon.Settings, iconOrigin, 22 * MaterialTheme.Metrics.Scale,
                foreground);
        }
        catch { if (selected) ImGui.EndTabItem(); throw; }
        return selected;
    }
    internal static bool Combo(string label, ref int index, string zeroSeparated)
    {
        var options = zeroSeparated.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        return Combo(label, ref index, options, options.Length);
    }
    internal static bool DragFloat(string label, ref float value, float speed, float min, float max, string format)
    {
        using var height = MaterialText.PushLineHeight(UiText.T(label.Split("##", 2)[0]));
        var minimum = Math.Max(80 * MaterialTheme.Metrics.Scale, MaterialText.Measure("-00000.0").X + 2 * ImGui.GetStyle().FramePadding.X);
        var width = FitField(label, minimum); var origin = ImGui.GetCursorScreenPos(); var parent = ImGuiP.GetCurrentWindow();
        var previousMax = parent.DC.CursorMaxPos; var drawing = ImGui.GetWindowDrawList();
        var clipped = ClipFieldLabel(label, origin, width, drawing);
        bool changed;
        try { changed = ImGui.DragFloat(label, ref value, speed, min, max, format); }
        finally { if (clipped) drawing.PopClipRect(); }
        if (!ImGui.IsItemActive())
        {
            var rectMin = ImGui.GetItemRectMin();
            var rectMax = new Vector2(rectMin.X + width, ImGui.GetItemRectMax().Y);
            var style = ImGui.GetStyle(); var dl = ImGui.GetWindowDrawList();
            var background = style.Colors[(int)(ImGui.IsItemHovered() ? ImGuiCol.FrameBgHovered : ImGuiCol.FrameBg)];
            var translated = value.ToString("0.0", UiText.Current.Culture);
            dl.PushClipRect(rectMin, rectMax, true);
            try
            {
                dl.AddRectFilled(rectMin + new Vector2(3, 1), rectMax - new Vector2(3, 1), MaterialCanvas.Color(background));
                MaterialText.AddText(dl,rectMin + (rectMax - rectMin - MaterialText.Measure(translated)) * .5f, MaterialCanvas.Color(style.Colors[(int)ImGuiCol.Text]), translated);
            }
            finally { dl.PopClipRect(); }
        }
        FieldLabel(label, origin, width, drawing, parent, previousMax); return changed;
    }
    internal static bool ColorEdit3(string label, ref Vector3 value)
    {
        using var height = MaterialText.PushLineHeight(UiText.T(label.Split("##", 2)[0]));
        var minimum = 3 * (MaterialText.Measure("0.000").X + 2 * ImGui.GetStyle().FramePadding.X + ImGui.GetStyle().ItemInnerSpacing.X) + ImGui.GetFrameHeight();
        var width = FitField(label, minimum); var min = ImGui.GetCursorScreenPos(); var parent = ImGuiP.GetCurrentWindow();
        var previousMax = parent.DC.CursorMaxPos; var drawing = ImGui.GetWindowDrawList();
        var clipped = ClipFieldLabel(label, min, width, drawing);
        bool changed;
        try { changed = ImGui.ColorEdit3(label, ref value); }
        finally { if (clipped) drawing.PopClipRect(); }
        FieldLabel(label, min, width, drawing, parent, previousMax); return changed;
    }
    internal static bool Selectable(string raw, bool selected, string? display = null)
    {
        var translated = display ?? UiText.T(raw.Split("##", 2)[0]);
        using var height = MaterialText.PushLineHeight(translated);
        var size = new Vector2(Math.Max(ImGui.GetContentRegionAvail().X, MaterialText.Measure(translated).X), MaterialText.RequiresShaping(translated) ? MaterialText.Measure(translated).Y : 0);
        ImGui.PushStyleColor(ImGuiCol.Text, Vector4.Zero);
        var clicked = ImGui.Selectable(raw, selected, ImGuiSelectableFlags.None, size);
        ImGui.PopStyleColor();
        var min = ImGui.GetItemRectMin(); var max = ImGui.GetItemRectMax(); var dl = ImGui.GetWindowDrawList();
        dl.PushClipRect(min, max, true);
        try { MaterialText.AddText(dl,min, MaterialCanvas.Color(ImGui.GetStyle().Colors[(int)ImGuiCol.Text]), translated); }
        finally { dl.PopClipRect(); }
        return clicked;
    }
    private static float FitField(string label, float? minimumPixels = null)
    {
        var requested = ImGui.CalcItemWidth(); var visible = label.Split("##", 2)[0];
        var minimum = minimumPixels ?? 80 * MaterialTheme.Metrics.Scale;
        var labelWidth = visible.Length == 0 ? 0 : MaterialText.Measure(UiText.T(visible)).X + ImGui.GetStyle().ItemInnerSpacing.X;
        var total = MaterialLayout.FitNextItemWidth(requested + labelWidth, minimum + labelWidth);
        var width = MathF.Ceiling(Math.Max(minimum, total - labelWidth));
        ImGui.SetNextItemWidth(width); return width;
    }
    internal static void TextDisabled(string text)
    {
        ImGui.PushTextWrapPos(0);
        try { MaterialText.TextDisabled(UiText.T(text)); }
        finally { ImGui.PopTextWrapPos(); }
    }
    private static bool ClipFieldLabel(string original, Vector2 min, float width, ImDrawListPtr drawing)
    {
        var visible = original.Split("##", 2)[0];
        if (UiText.T(visible) == visible && !MaterialText.RequiresShaping(visible)) return false;
        var window = ImGui.GetWindowPos();
        drawing.PushClipRect(new Vector2(min.X, window.Y), new Vector2(min.X + width, window.Y + ImGui.GetWindowSize().Y), true);
        return true;
    }
    private static void FieldLabel(string original, Vector2 min, float width, ImDrawListPtr drawing, ImGuiWindowPtr window, Vector2 previousMax)
    {
        var visible = original.Split("##", 2)[0]; var translated = UiText.T(visible);
        if (visible.Length == 0 || translated == visible && !MaterialText.RequiresShaping(translated)) return;
        var position = min + new Vector2(width + ImGui.GetStyle().ItemInnerSpacing.X, ImGui.GetStyle().FramePadding.Y);
        MaterialText.AddText(drawing,position, ImGui.GetColorU32(ImGuiCol.Text), translated);
        var right = position.X + MaterialText.Measure(translated).X;
        window.DC.CursorMaxPos = new Vector2(Math.Max(previousMax.X, right), window.DC.CursorMaxPos.Y);
        window.DC.CursorPosPrevLine = new Vector2(right, window.DC.CursorPosPrevLine.Y);
    }
    internal static bool InputText(string label,ref string value,int length,ImGuiInputTextFlags flags=ImGuiInputTextFlags.None)
    {
        using var height = MaterialText.PushLineHeight(value, UiText.T(label.Split("##", 2)[0]));
        var width = FitField(label); var min = ImGui.GetCursorScreenPos(); var parent = ImGuiP.GetCurrentWindow();
        var previousMax = parent.DC.CursorMaxPos; var drawing = ImGui.GetWindowDrawList();
        var clipped = ClipFieldLabel(label, min, width, drawing);
        bool changed;
        try { changed = MaterialShapedInput.SingleLine(label, "", ref value, length, flags); }
        finally { if (clipped) drawing.PopClipRect(); }
        FieldLabel(label, min, width, drawing, parent, previousMax); return changed;
    }
    internal static bool InputInt(string label, ref int value, int step = 1, int fastStep = 100)
    {
        using var height = MaterialText.PushLineHeight(UiText.T(label.Split("##", 2)[0]));
        var width = FitField(label); var min = ImGui.GetCursorScreenPos(); var parent = ImGuiP.GetCurrentWindow();
        var previousMax = parent.DC.CursorMaxPos; var drawing = ImGui.GetWindowDrawList();
        var clipped = ClipFieldLabel(label, min, width, drawing);
        bool changed;
        try { changed = ImGui.InputInt(label, ref value, step, fastStep); }
        finally { if (clipped) drawing.PopClipRect(); }
        FieldLabel(label, min, width, drawing, parent, previousMax); return changed;
    }
    internal static bool RadioButton(string label,bool active)
    {
        var visible = label.Split("##", 2)[0]; var translated = UiText.T(visible);
        using var height = MaterialText.PushLineHeight(translated);
        var gap = ImGui.GetStyle().ItemInnerSpacing;
        MaterialLayout.FitNextItemWidth(0, ImGui.GetFrameHeight() + gap.X + MaterialText.Measure(translated).X);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemInnerSpacing, new Vector2(gap.X + MaterialText.Measure(translated).X - MaterialText.Measure(visible).X, gap.Y));
        ImGui.PushStyleColor(ImGuiCol.Text, Vector4.Zero);
        var changed=ImGui.RadioButton(label,active);
        ImGui.PopStyleColor();
        ImGui.PopStyleVar();
        MaterialText.AddText(ImGui.GetWindowDrawList(),ImGui.GetItemRectMin()+new Vector2(ImGui.GetFrameHeight()+gap.X,ImGui.GetStyle().FramePadding.Y),MaterialCanvas.Color(ImGui.GetStyle().Colors[(int)ImGuiCol.Text]),translated);
        return changed;
    }
    internal static bool Combo(string label,ref int index,string[] options,int count,string[]? displays=null)
    {
        var preview = index >= 0 && index < count ? displays?[index] ?? UiText.T(options[index]) : string.Empty;
        if (!BeginCombo(label, preview, translatePreview: false)) return false;
        var changed = false;
        for (var option = 0; option < count; option++)
        {
            // Native Combo uses an index scope plus the original option label for each item.
            ImGui.PushID(option);
            var selected = index == option;
            if (Selectable(options[option], selected, displays?[option])) { index = option; changed = true; }
            if (selected) ImGui.SetItemDefaultFocus();
            ImGui.PopID();
        }
        ImGui.EndCombo();
        return changed;
    }
    internal static void TextUnformatted(string text) => MaterialText.Text(UiText.T(text));
    internal static void TextWrapped(string text) => MaterialText.TextWrapped(UiText.T(text));
    internal static void Text(string text)
    {
        ImGui.PushTextWrapPos(0);
        try { MaterialText.Text(UiText.T(text)); }
        finally { ImGui.PopTextWrapPos(); }
    }
    internal static void TextColored(Vector4 color, string text)
    {
        ImGui.PushTextWrapPos(0);
        try { MaterialText.TextColored(color, UiText.T(text)); }
        finally { ImGui.PopTextWrapPos(); }
    }
    internal static void SetTooltip(string text) => MaterialText.SetTooltip(UiText.T(text));
    private static void Label(string original,Vector2 position,Vector4 background,Vector4 foreground,Vector2? clip=null,string? display=null,ImDrawListPtr? drawList=null,bool repaint=false)
    {
        var visible=original.Split("##",2)[0];
        var translated=display ?? UiText.T(visible);
        if(translated==visible && !repaint && !MaterialText.RequiresShaping(translated)) return;
        var dl=drawList ?? ImGui.GetWindowDrawList();
        var width=Math.Max(MaterialText.Measure(visible).X,MaterialText.Measure(translated).X);
        if(clip is { } max) dl.PushClipRect(position,max,true);
        try
        {
            dl.AddRectFilled(position,position+new Vector2(width,Math.Max(ImGui.GetTextLineHeight(), MaterialText.Measure(translated).Y)),ImGui.ColorConvertFloat4ToU32(background));
            foreground.W*=ImGui.GetStyle().Alpha;
            MaterialText.AddText(dl,position,ImGui.ColorConvertFloat4ToU32(foreground),translated);
        }
        finally { if(clip.HasValue) dl.PopClipRect(); }
    }
    internal static bool Button(string label,string? display=null,MaterialIcon icon=MaterialIcon.None)
    {
        var translated=display ?? UiText.T(label.Split("##",2)[0]);
        using var height = MaterialText.PushLineHeight(translated);
        var iconWidth=icon==MaterialIcon.None?0:28*MaterialTheme.Metrics.Scale;
        var width=MaterialLayout.FitNextItemWidth(0,MaterialText.Measure(translated).X+2*ImGui.GetStyle().FramePadding.X+iconWidth);
        var foreground=ImGui.GetStyle().Colors[(int)ImGuiCol.Text];
        ImGui.PushStyleColor(ImGuiCol.Text,Vector4.Zero);
        var clicked=ImGui.Button(label,new Vector2(width,0));
        ImGui.PopStyleColor();
        var min=ImGui.GetItemRectMin(); var max=ImGui.GetItemRectMax();
        foreground.W*=ImGui.GetStyle().Alpha;
        ImGui.GetWindowDrawList().PushClipRect(min,max,true);
        try
        {
        var origin=min+(max-min-MaterialText.Measure(translated)-new Vector2(iconWidth,0))*.5f;
        if(icon!=MaterialIcon.None) MaterialIcons.Draw(icon,origin+new Vector2(0,(ImGui.GetTextLineHeight()-22*MaterialTheme.Metrics.Scale)*.5f),22*MaterialTheme.Metrics.Scale,
            icon==MaterialIcon.Heart?MaterialTheme.Current.Colors.Primary:ImGui.GetStyle().Colors[(int)ImGuiCol.Text]);
        MaterialText.AddText(ImGui.GetWindowDrawList(),origin+new Vector2(iconWidth,0),ImGui.ColorConvertFloat4ToU32(foreground),translated);
        }
        finally { ImGui.GetWindowDrawList().PopClipRect(); }
        return clicked;
    }
    internal static bool Button(string label, Vector2 pixels)
    {
        var translated = UiText.T(label.Split("##", 2)[0]);
        using var height = MaterialText.PushLineHeight(translated);
        if (MaterialText.RequiresShaping(translated) && pixels.Y > 0)
            pixels.Y = Math.Max(pixels.Y, MaterialText.Measure(translated).Y + ImGui.GetStyle().FramePadding.Y * 2);
        var minimum = MaterialText.Measure(translated).X + 2 * ImGui.GetStyle().FramePadding.X;
        pixels.X = MaterialLayout.FitNextItemWidth(pixels.X, pixels.X > 0 ? Math.Max(pixels.X, minimum) : minimum);
        var color = ImGui.GetStyle().Colors[(int)ImGuiCol.Text];
        ImGui.PushStyleColor(ImGuiCol.Text, Vector4.Zero);
        var clicked = ImGui.Button(label, pixels);
        ImGui.PopStyleColor();
        var min = ImGui.GetItemRectMin(); var max = ImGui.GetItemRectMax();
        var dl = ImGui.GetWindowDrawList();
        dl.PushClipRect(min, max, true);
        try { MaterialText.AddText(dl,min + (max - min - MaterialText.Measure(translated)) * .5f, MaterialCanvas.Color(color), translated); }
        finally { dl.PopClipRect(); }
        return clicked;
    }
    internal static bool SmallButton(string label,string? display=null)
    {
        // Native small buttons use the same ID and behavior with zero vertical padding.
        using var padding = new MaterialStyleScope();
        padding.Style(ImGuiStyleVar.FramePadding,new Vector2(ImGui.GetStyle().FramePadding.X,0));
        return Button(label,display);
    }
    internal static bool Checkbox(string label,ref bool value)
    {
        if (ToolbarSwitch(label)) return DrawToolbarSwitch(label, ref value);
        var visible=label.Split("##",2)[0];
        var translated=UiText.T(visible);
        using var height = MaterialText.PushLineHeight(translated);
        var foreground=ImGui.GetStyle().Colors[(int)ImGuiCol.Text];
        var gap=ImGui.GetStyle().ItemInnerSpacing;
        // Native Checkbox sizes its hit area from the original label. Adjust that size for the
        // translated ink while keeping the native widget and its original ID.
        MaterialLayout.FitNextItemWidth(0, CheckboxWidth(label));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemInnerSpacing,new Vector2(gap.X+MaterialText.Measure(translated).X-MaterialText.Measure(visible).X,gap.Y));
        ImGui.PushStyleColor(ImGuiCol.Text,Vector4.Zero);
        var changed=ImGui.Checkbox(label,ref value);
        ImGui.PopStyleColor();
        ImGui.PopStyleVar();
        var p=ImGui.GetItemRectMin()+new Vector2(ImGui.GetFrameHeight()+gap.X,ImGui.GetStyle().FramePadding.Y);
        foreground.W*=ImGui.GetStyle().Alpha;
        MaterialText.AddText(ImGui.GetWindowDrawList(),p,ImGui.ColorConvertFloat4ToU32(foreground),translated);
        return changed;
    }
    private static bool DrawToolbarSwitch(string label, ref bool value)
    {
        using var line = MaterialText.PushLineHeight(UiText.T(label));
        var scale = MaterialTheme.Metrics.Scale; var style = ImGui.GetStyle();
        var width = CheckboxWidth(label);
        MaterialLayout.FitNextItemWidth(0, width);
        // Keep the original native Checkbox ID, keyboard navigation and hit area.
        ImGui.PushStyleVar(ImGuiStyleVar.ItemInnerSpacing, new Vector2(width - ImGui.GetFrameHeight() - MaterialText.Measure(label).X, style.ItemInnerSpacing.Y));
        var changed = ImGui.Checkbox(label, ref value);
        ImGui.PopStyleVar();
        var min = ImGui.GetItemRectMin(); var max = ImGui.GetItemRectMax();
        var colors = MaterialTheme.Current.Colors; var dl = ImGui.GetWindowDrawList();
        dl.AddRectFilled(min, max, MaterialCanvas.Color(ImGui.IsItemHovered() ? colors.SurfaceContainerHighest : colors.SurfaceContainer), 4 * scale);
        dl.AddRect(min, max, MaterialCanvas.Color(ImGui.IsItemFocused() ? colors.Primary : colors.OutlineVariant), 4 * scale);
        var text = UiText.T(label);
        MaterialText.AddText(dl,new Vector2(min.X + 10 * scale, min.Y + (max.Y - min.Y - MaterialText.Measure(text).Y) * .5f), MaterialCanvas.Color(colors.OnSurface), text);
        var trackMin = new Vector2(max.X - 51 * scale, min.Y + (max.Y - min.Y - 22 * scale) * .5f);
        var trackMax = trackMin + new Vector2(41, 22) * scale;
        dl.AddRectFilled(trackMin, trackMax, MaterialCanvas.Color(value ? colors.Primary : colors.SurfaceVariant), 11 * scale);
        dl.AddCircleFilled(new Vector2(value ? trackMax.X - 11 * scale : trackMin.X + 11 * scale, trackMin.Y + 11 * scale), 8.5f * scale, MaterialCanvas.Color(colors.OnSurface), 20);
        return changed;
    }
    internal static void Title(string original,string translated)
        => TitleWithButtons(original, translated, null);

    internal static void TitleWithButtons(string original,string translated, Window? owner)
    {
        var s=ImGui.GetStyle(); var size=ImGui.GetFontSize();var height=ImGui.GetFrameHeight();
        var flags=ImGuiP.GetCurrentWindow().Flags;
        var collapseOnLeft=(flags & (ImGuiWindowFlags.NoCollapse|ImGuiWindowFlags.Modal))==0 && s.WindowMenuButtonPosition==ImGuiDir.Left;
        var position=ImGui.GetWindowPos()+new Vector2(s.FramePadding.X+(collapseOnLeft?size+s.ItemInnerSpacing.X:0),s.FramePadding.Y);
        var originalWidth=MaterialText.Measure(original).X;
        using var font=UiText.Font(UiFontRole.Body);
        float translatedWidth;
        if (MaterialText.RequiresShaping(translated))
        {
            var callerScale = ImGuiP.GetCurrentWindow().FontWindowScale;
            var textScale = size / ImGui.GetFontSize();
            ImGui.SetWindowFontScale(callerScale * textScale);
            try { translatedWidth = MaterialText.Measure(translated).X; }
            finally { ImGui.SetWindowFontScale(callerScale); }
        }
        else translatedWidth = MaterialText.Measure(translated).X*size/ImGui.GetFontSize();
        var dl=ImGui.GetWindowDrawList();
        var reserved = 2 * height;
        if (owner is not null)
        {
            var count = owner.TitleBarButtons.Count(button => !owner.IsClickthrough || button.AvailableClickthrough);
            if (owner.AllowPinning || owner.AllowClickthrough || owner.AllowBackgroundBlur) count++;
            reserved = size + 2 * s.FramePadding.X + count * (size + s.ItemInnerSpacing.X);
            if ((flags & ImGuiWindowFlags.NoCollapse) == 0 && s.WindowMenuButtonPosition == ImGuiDir.Right)
                reserved += size + s.ItemInnerSpacing.X;
        }
        dl.PushClipRect(ImGui.GetWindowPos(),ImGui.GetWindowPos()+new Vector2(Math.Max(0,ImGui.GetWindowSize().X-reserved),height),false);
        try
        {
        var bg=s.Colors[(int)(ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows)?ImGuiCol.TitleBgActive:ImGuiCol.TitleBg)];
        dl.AddRectFilled(position,position+new Vector2(Math.Max(originalWidth,translatedWidth),height-s.FramePadding.Y),ImGui.ColorConvertFloat4ToU32(bg));
        MaterialText.AddText(dl,ImGui.GetFont(),size,position,ImGui.ColorConvertFloat4ToU32(s.Colors[(int)ImGuiCol.Text]),translated);
        }
        finally { dl.PopClipRect(); }
    }
}
