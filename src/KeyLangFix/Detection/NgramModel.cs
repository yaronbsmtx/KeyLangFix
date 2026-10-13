using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace KeyLangFix.Detection;

/// <summary>
/// Offline character-trigram language model plus a hashed word list.
/// Files are produced by tools/build_ngrams.py.
/// </summary>
internal sealed class NgramModel
{
    private readonly short[] _table;      // log P * 256, index = (a*(A+1)+b)*(A+1)+c, 0 = boundary
    private readonly Dictionary<char, int> _index = new();
    private readonly uint[] _words;       // sorted FNV-1a hashes
    private readonly uint[] _prefixes;    // sorted hashes of proper word prefixes (len >= 3)
    private readonly int _a1;

    public ushort LangId { get; }
    public string Alphabet { get; }

    private NgramModel(ushort langId, string alphabet, short[] table, uint[] words, uint[] prefixes)
    {
        LangId = langId;
        Alphabet = alphabet;
        _table = table;
        _words = words;
        _prefixes = prefixes;
        _a1 = alphabet.Length + 1;
        for (int i = 0; i < alphabet.Length; i++) _index[alphabet[i]] = i + 1;
    }

    public static NgramModel Load(Stream stream)
    {
        using var r = new BinaryReader(stream, Encoding.Unicode);
        if (r.ReadUInt32() != 0x314D474E) throw new InvalidDataException("bad model"); // "NGM1"
        ushort langId = r.ReadUInt16();
        int alen = r.ReadByte();
        var chars = new char[alen];
        for (int i = 0; i < alen; i++) chars[i] = (char)r.ReadUInt16();
        int a1 = alen + 1;
        var table = new short[a1 * a1 * a1];
        for (int i = 0; i < table.Length; i++) table[i] = r.ReadInt16();
        int wc = (int)r.ReadUInt32();
        var words = new uint[wc];
        for (int i = 0; i < wc; i++) words[i] = r.ReadUInt32();
        var prefixes = Array.Empty<uint>();
        if (r.BaseStream.Position < r.BaseStream.Length)
        {
            int pc = (int)r.ReadUInt32();
            prefixes = new uint[pc];
            for (int i = 0; i < pc; i++) prefixes[i] = r.ReadUInt32();
        }
        return new NgramModel(langId, new string(chars), table, words, prefixes);
    }

    public static char Normalize(char c)
    {
        c = char.ToLowerInvariant(c);
        return c switch
        {
            'ך' => 'כ', // final kaf
            'ם' => 'מ', // final mem
            'ן' => 'נ', // final nun
            'ף' => 'פ', // final pe
            'ץ' => 'צ', // final tsadi
            _ => c,
        };
    }

    public bool IsLetter(char c) => _index.ContainsKey(Normalize(c));

    public int CountLetters(ReadOnlySpan<char> text)
    {
        int n = 0;
        foreach (char c in text) if (_index.ContainsKey(Normalize(c))) n++;
        return n;
    }

    /// <summary>
    /// Sum of log-probabilities (nats) of <paramref name="text"/>. Non-alphabet chars break the word
    /// and are not scored. <paramref name="count"/> is the number of scored events (letters plus
    /// word-end events). The final word gets an end event only if <paramref name="endOfLast"/>.
    /// </summary>
    public double Score(ReadOnlySpan<char> text, bool endOfLast, out int count, out int letters)
    {
        int a = 0, b = 0;
        long sum = 0;
        count = 0;
        letters = 0;
        foreach (char raw in text)
        {
            if (_index.TryGetValue(Normalize(raw), out int c))
            {
                sum += _table[(a * _a1 + b) * _a1 + c];
                count++;
                letters++;
                a = b;
                b = c;
            }
            else if (b != 0)
            {
                sum += _table[(a * _a1 + b) * _a1];
                count++;
                a = b = 0;
            }
        }
        if (endOfLast && b != 0)
        {
            sum += _table[(a * _a1 + b) * _a1];
            count++;
        }
        return sum / 256.0;
    }

    /// <summary>
    /// True if a Hebrew final-form letter (ך ם ן ף ץ) is followed by another letter in the same word.
    /// Those forms only end a word, so such a text cannot be a Hebrew word ("e.g" typed on the
    /// English layout reads as "קצע", with ץ in the middle).
    /// </summary>
    public bool HasMisplacedFinal(ReadOnlySpan<char> word)
    {
        for (int i = 0; i + 1 < word.Length; i++)
        {
            if (word[i] is 'ך' or 'ם' or 'ן' or 'ף' or 'ץ' && _index.ContainsKey(Normalize(word[i + 1])))
                return true;
        }
        return false;
    }

    /// <summary>One-letter words that are real words on their own (English "I", "a").</summary>
    public bool IsSingleLetterWord(char c) => LangId == 0x09 && char.ToLowerInvariant(c) is 'i' or 'a';

    public bool IsWord(ReadOnlySpan<char> word) => Hash(word, out uint h) && Array.BinarySearch(_words, h) >= 0;

    /// <summary>True if the text is a word, or the beginning of a longer word, in this language.</summary>
    public bool IsWordOrPrefix(ReadOnlySpan<char> text) =>
        Hash(text, out uint h) && (Array.BinarySearch(_words, h) >= 0 || Array.BinarySearch(_prefixes, h) >= 0);

    private bool Hash(ReadOnlySpan<char> word, out uint h)
    {
        h = 0x811C9DC5;
        foreach (char raw in word)
        {
            char c = Normalize(raw);
            if (!_index.ContainsKey(c)) return false;
            h = (h ^ (byte)c) * 0x01000193;
            h = (h ^ (byte)(c >> 8)) * 0x01000193;
        }
        return true;
    }
}
