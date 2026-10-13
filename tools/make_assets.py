"""Dev-time only: generates icons and the switch sound (needs Pillow)."""
import io
import math
import os
import struct
import wave

from PIL import Image, ImageDraw, ImageFont

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src", "KeyLangFix", "Resources")
os.makedirs(OUT, exist_ok=True)


def render(size, bg, fg):
    s = 8
    big = size * s
    img = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([0, 0, big - 1, big - 1], radius=big // 5, fill=bg)
    font = ImageFont.truetype("C:/Windows/Fonts/arialbd.ttf", int(big * 0.46))
    # Latin A top-left, Hebrew alef bottom-right: reads as "language switch".
    d.text((big * 0.06, big * 0.02), "A", font=font, fill=fg)
    d.text((big * 0.50, big * 0.40), "א", font=font, fill=fg)
    return img.resize((size, size), Image.LANCZOS)


def dib(img):
    """Icon-resource DIB (BITMAPINFOHEADER + BGRA bottom-up + AND mask)."""
    w, h = img.size
    px = img.load()
    xor = bytearray()
    for y in range(h - 1, -1, -1):
        for x in range(w):
            r, g, b, a = px[x, y]
            xor += bytes((b, g, r, a))
    row = ((w + 31) // 32) * 4
    and_mask = bytes(row * h)
    header = struct.pack("<IiiHHIIiiII", 40, w, h * 2, 1, 32, 0, len(xor) + len(and_mask), 0, 0, 0, 0)
    return header + bytes(xor) + and_mask


ON = ((37, 99, 235, 255), (255, 255, 255, 255))
OFF = ((120, 120, 120, 255), (225, 225, 225, 255))

for name, (bg, fg) in (("icon_on", ON), ("icon_off", OFF)):
    with open(os.path.join(OUT, name + ".bin"), "wb") as f:
        f.write(dib(render(32, bg, fg)))

# app.ico (exe icon): PNG-compressed 16/32/48/256
sizes = [16, 32, 48, 256]
pngs = []
for sz in sizes:
    buf = io.BytesIO()
    render(sz, *ON).save(buf, "PNG")
    pngs.append(buf.getvalue())
offset = 6 + 16 * len(sizes)
ico = struct.pack("<HHH", 0, 1, len(sizes))
for sz, data in zip(sizes, pngs):
    ico += struct.pack("<BBBBHHII", sz % 256, sz % 256, 0, 0, 1, 32, len(data), offset)
    offset += len(data)
ico += b"".join(pngs)
with open(os.path.join(OUT, "app.ico"), "wb") as f:
    f.write(ico)

# short, soft two-note "pop" (~140 ms)
rate = 22050
samples = []
for freq, dur in ((880.0, 0.06), (1320.0, 0.08)):
    n = int(rate * dur)
    for i in range(n):
        env = math.sin(math.pi * i / n) ** 1.5
        samples.append(int(9000 * env * math.sin(2 * math.pi * freq * i / rate)))
with wave.open(os.path.join(OUT, "switch.wav"), "wb") as w:
    w.setnchannels(1)
    w.setsampwidth(2)
    w.setframerate(rate)
    w.writeframes(b"".join(struct.pack("<h", s) for s in samples))
print("assets ok")
