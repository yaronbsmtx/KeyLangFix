using System.Collections.Generic;
using System.Reflection;

namespace KeyLangFix.Detection;

/// <summary>Language models embedded in the exe, keyed by primary language id.</summary>
internal static class Models
{
    private static readonly string[] Files = { "en.ngm", "he.ngm" };
    private static Dictionary<ushort, NgramModel>? _byLang;

    public static IReadOnlyDictionary<ushort, NgramModel> All => _byLang ??= LoadAll();

    public static NgramModel? Get(ushort primaryLang) => All.TryGetValue(primaryLang, out var m) ? m : null;

    private static Dictionary<ushort, NgramModel> LoadAll()
    {
        var map = new Dictionary<ushort, NgramModel>();
        var asm = typeof(Models).Assembly;
        foreach (var name in Files)
        {
            using var s = asm.GetManifestResourceStream(name);
            if (s == null) continue;
            var m = NgramModel.Load(s);
            map[m.LangId] = m;
        }
        return map;
    }
}
