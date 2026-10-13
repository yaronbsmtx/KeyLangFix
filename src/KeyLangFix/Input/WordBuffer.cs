using System;
using KeyLangFix.Detection;

namespace KeyLangFix.Input;

/// <summary>
/// Keys typed since the last reset. One entry per character inserted into the app, so the entry
/// count is exactly the number of Backspaces needed to erase them. Holds, per entry, what the key
/// produced on the current layout and on each candidate layout. Lives in RAM only.
/// </summary>
internal sealed class WordBuffer
{
    public const int Capacity = 48;
    public const int MaxAlts = 3;

    private readonly char[] _cur = new char[Capacity];                 // scoring text, ' ' = separator
    private readonly char[][] _alt = new char[MaxAlts][];              // scoring text per candidate
    private readonly char[][] _altRaw = new char[MaxAlts][];           // real chars per candidate
    private readonly ushort[] _scan = new ushort[Capacity];            // scan code, bit 8 = extended
    private readonly bool[] _shift = new bool[Capacity];
    private int _len;

    public WordBuffer()
    {
        for (int i = 0; i < MaxAlts; i++)
        {
            _alt[i] = new char[Capacity];
            _altRaw[i] = new char[Capacity];
        }
    }

    public int Length => _len;
    public ReadOnlySpan<char> Cur => _cur.AsSpan(0, _len);
    public ReadOnlySpan<char> Alt(int i) => _alt[i].AsSpan(0, _len);
    public ReadOnlySpan<char> AltRaw(int i) => _altRaw[i].AsSpan(0, _len);
    public ushort Scan(int i) => _scan[i];
    public bool Shift(int i) => _shift[i];

    public void Clear() => _len = 0;

    public void Backspace()
    {
        if (_len > 0) _len--;
    }

    /// <summary>
    /// Appends a typed key. <paramref name="alts"/> are the chars the same key gives on each candidate
    /// layout, <paramref name="altModels"/> the matching models. Chars that are letters on no layout
    /// (digits, most punctuation, space) are word separators.
    /// </summary>
    public void Add(char cur, NgramModel curModel, ReadOnlySpan<char> alts, ReadOnlySpan<NgramModel> altModels,
        ushort scan = 0, bool shift = false)
    {
        if (_len == 0 && char.IsWhiteSpace(cur)) return;
        if (_len == Capacity) DropOldest();

        bool letter = curModel.IsLetter(cur);
        for (int i = 0; i < alts.Length && !letter; i++) letter = altModels[i].IsLetter(alts[i]);

        _scan[_len] = scan;
        _shift[_len] = shift;
        _cur[_len] = letter ? cur : ' ';
        for (int i = 0; i < alts.Length; i++)
        {
            _altRaw[i][_len] = alts[i];
            _alt[i][_len] = letter ? alts[i] : ' ';
        }
        _len++;
    }

    private void DropOldest()
    {
        const int drop = Capacity / 3;
        Array.Copy(_cur, drop, _cur, 0, _len - drop);
        Array.Copy(_scan, drop, _scan, 0, _len - drop);
        Array.Copy(_shift, drop, _shift, 0, _len - drop);
        for (int i = 0; i < MaxAlts; i++)
        {
            Array.Copy(_alt[i], drop, _alt[i], 0, _len - drop);
            Array.Copy(_altRaw[i], drop, _altRaw[i], 0, _len - drop);
        }
        _len -= drop;
    }
}
