"""Dev-time only: builds the offline language models embedded in KeyLangFix.

Needs `pip install wordfreq`. The shipped app never touches the network; the
generated .ngm files are embedded as resources.

Model file (little endian):
  "NGM1" | u16 primaryLangId | u8 alphabetLen | alphabet (UTF-16) |
  int16[(A+1)^3] logP*256 (index 0 = word boundary) |
  u32 wordCount | u32[wordCount] sorted FNV-1a hashes of normalised words |
  u32 prefixCount | u32[prefixCount] sorted hashes of proper prefixes (len >= 3) of those words

Also writes held-out words (not used for training) for the unit tests.
"""
import math
import os
import struct
import sys
from collections import defaultdict

from wordfreq import top_n_list, word_frequency

ROOT = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(ROOT, "..", "src", "KeyLangFix", "Resources")
TEST_OUT = os.path.join(ROOT, "..", "tests", "KeyLangFix.Tests", "data")

FINALS = {"ך": "כ", "ם": "מ", "ן": "נ", "ף": "פ", "ץ": "צ"}

LANGS = {
    "en": dict(langid=0x09, alphabet="abcdefghijklmnopqrstuvwxyz"),
    "he": dict(langid=0x0D, alphabet="אבגדהוזחטיכלמנסעפצקרשת"),
}

TRAIN_N = 40000
TOTAL_N = 70000
WORDSET_N = 40000
MIN_PREFIX = 3
ALPHA = 0.1  # additive smoothing (weights are normalised to mean 1)


# wordfreq is built from web text and contains many abbreviations and junk tokens ("kl", "xz", ...).
# English: two-letter entries only from this list of real words, longer ones need a vowel.
EN_TWO_LETTER = set("am an as at be by do go he hi if in is it me my no of oh ok on or so to up us we".split())


def acceptable(lang, n):
    if lang != "en":
        return True
    if len(n) == 2:
        return n in EN_TWO_LETTER
    return any(c in "aeiouy" for c in n)


def normalise(word):
    word = word.lower()
    return "".join(FINALS.get(c, c) for c in word)


def fnv1a(word):
    h = 0x811C9DC5
    data = word.encode("utf-16-le")
    for i in range(0, len(data), 2):
        for b in (data[i], data[i + 1]):
            h ^= b
            h = (h * 0x01000193) & 0xFFFFFFFF
    return h


def build(lang, cfg):
    alphabet = cfg["alphabet"]
    idx = {c: i + 1 for i, c in enumerate(alphabet)}
    a1 = len(alphabet) + 1
    words = []
    for w in top_n_list(lang, TOTAL_N):
        n = normalise(w)
        if len(n) >= 2 and all(c in idx for c in n) and acceptable(lang, n):
            words.append((w, n))
    train, heldout = words[:TRAIN_N], words[TRAIN_N:]

    counts = defaultdict(float)
    ctx = defaultdict(float)
    weights = [word_frequency(raw, lang) ** 0.5 for raw, _ in train]
    mean = sum(weights) / len(weights)
    for (raw, n), weight in zip(train, weights):
        weight /= mean
        seq = [0, 0] + [idx[c] for c in n] + [0]
        for i in range(2, len(seq)):
            key = (seq[i - 2], seq[i - 1], seq[i])
            counts[key] += weight
            ctx[(seq[i - 2], seq[i - 1])] += weight

    table = []
    for x in range(a1):
        for y in range(a1):
            total = ctx.get((x, y), 0.0)
            for z in range(a1):
                if x == 0 and y == 0 and z == 0:
                    p = 1e-6
                elif total:
                    p = (counts.get((x, y, z), 0.0) + ALPHA) / (total + ALPHA * a1)
                else:
                    p = 1.0 / a1
                lp = max(math.log(max(p, 1e-9)), -127.0)
                table.append(int(round(lp * 256)))

    kept = [n for _, n in train[:WORDSET_N] if len(n) >= 2]
    wordset = sorted({fnv1a(n) for n in kept})
    prefixes = sorted({fnv1a(n[:k]) for n in kept for k in range(MIN_PREFIX, len(n))})

    path = os.path.join(OUT, f"{lang}.ngm")
    os.makedirs(OUT, exist_ok=True)
    with open(path, "wb") as f:
        f.write(b"NGM1")
        f.write(struct.pack("<HB", cfg["langid"], len(alphabet)))
        f.write(alphabet.encode("utf-16-le"))
        f.write(struct.pack(f"<{len(table)}h", *table))
        f.write(struct.pack("<I", len(wordset)))
        f.write(struct.pack(f"<{len(wordset)}I", *wordset))
        f.write(struct.pack("<I", len(prefixes)))
        f.write(struct.pack(f"<{len(prefixes)}I", *prefixes))
    print(lang, "model", os.path.getsize(path), "bytes;", len(train), "train words,", len(heldout), "heldout")

    os.makedirs(TEST_OUT, exist_ok=True)
    with open(os.path.join(TEST_OUT, f"train.{lang}.txt"), "w", encoding="utf-8") as f:
        f.write("\n".join(raw for raw, _ in train[:5000]))
    with open(os.path.join(TEST_OUT, f"heldout.{lang}.txt"), "w", encoding="utf-8") as f:
        f.write("\n".join(raw for raw, _ in heldout[:8000]))


if __name__ == "__main__":
    for lang, cfg in LANGS.items():
        build(lang, cfg)
