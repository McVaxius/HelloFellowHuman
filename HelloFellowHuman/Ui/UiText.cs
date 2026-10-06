using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Collections;
using System.Globalization;
using System.Resources;
using AethertekUI;

namespace HelloFellowHuman.Ui;

internal sealed class UiText : IDisposable
{
    [ThreadStatic] private static UiText? current;
    internal static UiText Current => current ?? throw new InvalidOperationException("Enter the HFH UI frame before drawing.");
    internal static readonly (string Code, string Name)[] Languages = [("en", "English"), ("de", "Deutsch"), ("fr", "Français"),
        ("es", "Español"), ("it", "Italiano"), ("ru", "Русский"), ("ja", "日本語"), ("ko", "한국어"), ("zh-Hans", "简体中文"),
        ("vi", "Tiếng Việt"), ("pt-BR", "Português (Brasil)"), ("id", "Bahasa Indonesia"), ("pl", "Polski"), ("tr", "Türkçe"), ("hi", "हिन्दी")];
    internal const string NativeSymbols = "♡⚫—…·+?";
    internal static IEnumerable<string> CjkLanguages(string selected) => new[] { "ja", "ko", "zh-Hans" }.OrderBy(code => code == selected ? 0 : 1);
    private readonly ResourceManager manager;
    internal ResourceSet Resources { get; }
    internal IReadOnlyList<string> RequiredText { get; }
    internal CultureInfo Culture { get; }
    internal string Language { get; }
    private readonly Func<UiFontRole, IDisposable> pushFont;
    internal UiText(string language, Func<UiFontRole, IDisposable> pushFont)
    {
        Language = Languages.Any(l => l.Code == language) ? language : "en";
        Culture = CultureInfo.GetCultureInfo(Language);
        manager = new ResourceManager("HelloFellowHuman.Localization.Strings_" + Language.Replace('-', '_'), typeof(UiText).Assembly);
        Resources = manager.GetResourceSet(CultureInfo.InvariantCulture, true, false) ?? throw new MissingManifestResourceException(Language);
        this.pushFont = pushFont;
        var english = new ResourceManager("HelloFellowHuman.Localization.Strings_en", typeof(UiText).Assembly);
        try
        {
            var fallback = english.GetResourceSet(CultureInfo.InvariantCulture, true, false) ?? throw new MissingManifestResourceException("en");
            RequiredText = Values(Resources).Concat(Values(fallback)).Concat(Languages.Select(l => l.Name)).Append(NativeSymbols).Distinct().ToArray();
        }
        finally { english.ReleaseAllResources(); }
    }
    internal static string T(string english) => Current.Resources.GetString(english, true) ?? english;
    internal static string F(string english, params object?[] args) => string.Format(Current.Culture, T(english), args);
    internal static string Interpolated(FormattableString text) => F(text.Format, text.GetArguments());
    internal static IDisposable Font(UiFontRole role) => Current.pushFont(role);
    internal Scope Enter() => new(this);
    internal readonly struct Scope : IDisposable
    {
        private readonly UiText? previous;
        internal Scope(UiText value) { previous = current; current = value; }
        public void Dispose() => current = previous;
    }
    internal ushort[] GlyphRanges()
    {
        var chars = RequiredText.SelectMany(MaterialText.NativeGlyphText).Where(c => !char.IsControl(c))
            .Concat(Enumerable.Range(0x20, 0x024F - 0x20 + 1).Select(i => (char)i))
            .Concat(Enumerable.Range(0x0400, 0x052F - 0x0400 + 1).Select(i => (char)i)).Distinct().Order().ToArray();
        var result = new List<ushort>();
        for (var index = 0; index < chars.Length; index++)
        {
            var first = chars[index]; var last = first;
            while (index + 1 < chars.Length && chars[index + 1] == last + 1) last = chars[++index];
            result.Add(first); result.Add(last);
        }
        result.Add(0); return result.ToArray();
    }
    private static IEnumerable<string> Values(ResourceSet set) => set.Cast<DictionaryEntry>().Select(e => (string)e.Value!);
    public void Dispose() => manager.ReleaseAllResources();
}
