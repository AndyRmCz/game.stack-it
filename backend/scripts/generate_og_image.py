"""Draws the 1200x630 Open Graph preview (synthwave sky + glowing stack) as a PNG.

    uv run python scripts/generate_og_image.py

No imaging dependency: pixels are computed directly and written with zlib.
"""

import struct
import zlib
from pathlib import Path

W, H = 1200, 630
OUT = Path(__file__).resolve().parent.parent / "app" / "web" / "static" / "og-challenge.png"

SKY_TOP, SKY_MID, SKY_LOW = (20, 10, 46), (59, 16, 96), (106, 27, 110)
TOWER = [(45, 226, 230), (79, 195, 255), (124, 140, 255), (176, 108, 255), (224, 79, 216),
         (255, 61, 154), (255, 122, 107), (255, 184, 77)]  # fmt: skip


def lerp(a, b, f):
    return tuple(int(a[i] + (b[i] - a[i]) * f) for i in range(3))


def sky(y: int):
    f = y / (H - 1)
    return (
        lerp(SKY_TOP, SKY_MID, f / 0.62) if f < 0.62 else lerp(SKY_MID, SKY_LOW, (f - 0.62) / 0.38)
    )


def build() -> list[bytearray]:
    rows = [bytearray() for _ in range(H)]
    pixels = [[sky(y) for _ in range(W)] for y in range(H)]

    # Perspective grid on the "floor".
    horizon = 470
    for y in range(horizon, H):
        depth = (y - horizon) / (H - horizon)
        for x in range(W):
            on_line = (y - horizon) % int(10 + depth * 40) == 0
            vx = (x - W / 2) / max(0.15, depth)
            on_col = depth > 0.08 and abs(vx % 120) < 3 * max(0.6, depth)
            if on_line or on_col:
                pixels[y][x] = lerp(pixels[y][x], (45, 226, 230), 0.35 * (0.3 + depth))

    # Stack of glowing blocks, narrowing as it rises.
    blocks, block_h, gap = 14, 22, 5
    cx = 860
    for i in range(blocks):
        color = TOWER[i * len(TOWER) // blocks]
        half = 150 - i * 6 + (8 if i % 4 == 3 else 0)
        offset = (i % 3 - 1) * 6
        bottom = horizon - i * (block_h + gap)
        top = bottom - block_h
        for y in range(max(0, top - 18), min(H, bottom + 18)):
            for x in range(max(0, cx + offset - half - 18), min(W, cx + offset + half + 18)):
                dx = max(cx + offset - half - x, 0, x - (cx + offset + half))
                dy = max(top - y, 0, y - bottom)
                if dx == 0 and dy == 0:
                    shade = 1.0 if y - top > 3 else 1.25
                    pixels[y][x] = tuple(min(255, int(c * shade)) for c in color)
                else:
                    glow = max(0.0, 1 - (dx * dx + dy * dy) ** 0.5 / 18) ** 2 * 0.55
                    pixels[y][x] = lerp(pixels[y][x], color, glow)

    # The rival's ghost line above the tower.
    ghost_y = horizon - blocks * (block_h + gap) - 30
    for x in range(cx - 230, cx + 230):
        if (x // 14) % 2 == 0:
            for y in range(ghost_y - 2, ghost_y + 2):
                pixels[y][x] = (45, 226, 230)

    for y in range(H):
        rows[y].append(0)  # filter: none
        for r, g, b in pixels[y]:
            rows[y].extend((r, g, b))
    return rows


def write_png(rows: list[bytearray], path: Path) -> None:
    def chunk(tag: bytes, data: bytes) -> bytes:
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data))

    raw = b"".join(bytes(r) for r in rows)
    png = b"\x89PNG\r\n\x1a\n"
    png += chunk(b"IHDR", struct.pack(">IIBBBBB", W, H, 8, 2, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(raw, 9))
    png += chunk(b"IEND", b"")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(png)


if __name__ == "__main__":
    write_png(build(), OUT)
    print(f"wrote {OUT} ({OUT.stat().st_size // 1024} KiB)")
