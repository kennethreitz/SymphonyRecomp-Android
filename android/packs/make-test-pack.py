#!/usr/bin/env python3
"""Create original test art. No pixels or files are extracted from a game disc."""
from pathlib import Path
import json, struct, zlib
root = Path(__file__).resolve().parent / "dist/android-title-marker"
(root / "textures").mkdir(parents=True, exist_ok=True)
width, height = 255, 144
def pixel(x, y):
    if x < 8 or y < 8 or x >= width-8 or y >= height-8: return bytes((255,255,255,255))
    if 40 <= y < 104: return bytes((0,255,255,255)) if 64 <= x < 192 else bytes((20,20,20,255))
    return bytes((255,0,200,255)) if (x//32+y//32)%2 else bytes((0,255,80,255))
raw = b"".join(bytes((0,))+b"".join(pixel(x,y) for x in range(width)) for y in range(height))
def chunk(kind, data):
    return struct.pack(">I",len(data))+kind+data+struct.pack(">I",zlib.crc32(kind+data)&0xffffffff)
png = b"\x89PNG\r\n\x1a\n"+chunk(b"IHDR",struct.pack(">IIBBBBB",width,height,8,6,0,0,0))+chunk(b"IDAT",zlib.compress(raw))+chunk(b"IEND",b"")
# Only the observed title/menu texture and palette, not a broad tpage rule.
(root/"textures/ac6d04dd968b34ca_58ea135bbf174f98.png").write_bytes(png)
(root/"pack.json").write_text(json.dumps({"formatVersion":1,"id":"android-title-marker","name":"Android asset smoke test","version":"1.0.0","author":"Kenneth Reitz","description":"Original neon checker marker replacing one observed title/menu texture. No game art is included.","priority":1000},indent=2)+"\n")
print(root)
