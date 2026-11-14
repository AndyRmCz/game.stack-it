"""Bakes Blocky for the store art: backend/scripts/blocky/<pose>.png and slab.png.

Run from unity/ with numpy, Pillow and the .NET SDK: python Tools/blocky_stills.py. Each still is
the glass body (blocky_glass.py) with a face exactly as the game draws it: DotNetTests/FaceDump
prints FaceDrawing's quads, coloured here with BlockyFace's fills. slab.png is the gold slab
Blocky wears, seen the same way. Both are PER_BLOCK pixels per block width; backend/scripts/art.py
places them by their foot, FOOT block widths above the bottom of the image.
"""
import re
import subprocess
from pathlib import Path

import numpy as np
from PIL import Image

from blocky_glass import FOOT, bake, mix, rbox, rgb, sm

OUT = Path('../backend/scripts/blocky')
PER_BLOCK, SS = 640, 3
FACE = 'Assets/_Project/Scripts/UI/BlockyFace.cs'
SLAB_HEIGHT, SLAB_TOP = 0.2, 0.15  # the front face, and the top face seen above it


def fills():
    source = Path(FACE).read_text(encoding='utf8')
    found = re.findall(r'FacePart\.(\w+)\] = new Color32\(0x(\w\w), 0x(\w\w), 0x(\w\w), (\d+)\)', source)
    return {m[0]: (int(m[1], 16) / 255, int(m[2], 16) / 255, int(m[3], 16) / 255, int(m[4]) / 255) for m in found}


def faces():
    """{pose: [(part, [x, y, alpha] * 4)]} from the game's own face drawing."""
    text = subprocess.run(['dotnet', 'run', '-v', 'q', '--project', 'DotNetTests/FaceDump'],
                          capture_output=True, text=True, check=True).stdout
    poses, current = {}, None
    for line in text.splitlines():
        if line.startswith('# '):
            current = poses.setdefault(line[2:].strip(), [])
        elif current is not None and len(line.split()) == 13:
            part, *values = line.split()
            current.append((part, [float(v) for v in values]))
    return poses


def triangle(img, colour, p0, p1, p2):
    """Fills a triangle, its alpha blended between the corners'."""
    (x0, y0, a0), (x1, y1, a1), (x2, y2, a2) = p0, p1, p2
    area = (x1 - x0) * (y2 - y0) - (x2 - x0) * (y1 - y0)
    if abs(area) < 1e-9:
        return
    h, w = img.shape[:2]
    ax, bx = max(int(min(x0, x1, x2)), 0), min(int(max(x0, x1, x2)) + 2, w)
    ay, by = max(int(min(y0, y1, y2)), 0), min(int(max(y0, y1, y2)) + 2, h)
    if ax >= bx or ay >= by:
        return
    px, py = np.meshgrid(np.arange(ax, bx) + 0.5, np.arange(ay, by) + 0.5)
    w0 = ((x1 - px) * (y2 - py) - (x2 - px) * (y1 - py)) / area
    w1 = ((x2 - px) * (y0 - py) - (x0 - px) * (y2 - py)) / area
    w2 = 1 - w0 - w1
    alpha = (w0 * a0 + w1 * a1 + w2 * a2) * colour[3] * ((w0 >= 0) & (w1 >= 0) & (w2 >= 0))
    region = img[ay:by, ax:bx]
    region[..., :3] = region[..., :3] * (1 - alpha[..., None]) + np.array(colour[:3]) * alpha[..., None]


def still(glass, quads, colours):
    img = glass.copy()
    P = PER_BLOCK * SS
    h, w = img.shape[:2]
    for part, v in quads:
        corners = [(w / 2 + v[i] * P, h + FOOT * P - v[i + 1] * P, v[i + 2]) for i in (0, 3, 6, 9)]
        triangle(img, colours[part], corners[0], corners[1], corners[2])
        triangle(img, colours[part], corners[0], corners[2], corners[3])
    return small(img)


def small(img):
    image = Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8), 'RGBA')
    return image.resize((round(image.width / SS), round(image.height / SS)), Image.LANCZOS)


def slab():
    """The gold slab: front face square on, its top foreshortened above it like Blocky's."""
    P = PER_BLOCK * SS
    w, h = round(1.04 * P), round((SLAB_HEIGHT + SLAB_TOP + 0.04) * P)
    # y = 0 at the slab's foot, which sits FOOT above the bottom of the image like Blocky's
    x, y = np.meshgrid(-0.52 + (np.arange(w) + 0.5) / P, FOOT + (h - np.arange(h) - 0.5) / P)
    front = rbox(x, y, 0, SLAB_HEIGHT / 2, 0.5, SLAB_HEIGHT / 2, 0.06)
    top = rbox(x * (1 + (y - SLAB_HEIGHT) * 0.34), y, 0, SLAB_HEIGHT + SLAB_TOP / 2 - 0.04, 0.5, SLAB_TOP / 2 + 0.04, 0.06)
    aa = 1.2 / PER_BLOCK
    fm, tm = sm(aa, -aa, front), sm(aa, -aa, top)
    t = np.clip((y - SLAB_HEIGHT + 0.04) / (SLAB_TOP + 0.04), 0, 1)
    plane = rgb('#FFD45A') * (1 - t[..., None]) + rgb('#FFF3A6') * t[..., None]
    v = np.clip(y / SLAB_HEIGHT, 0, 1)[..., None] * 0.35
    body = rgb('#FFB321') * (1 - v) + rgb('#FFD45A') * v
    body = mix(body, rgb('#C7650C'), 0.35 * sm(0.0, 0.5, x))  # the far side turns from the light
    img = np.ones((h, w, 3)) * rgb('#7A3306')
    img = mix(img, plane, tm)
    img = mix(img, body, fm)
    img = mix(img, rgb('#7A3306'), 0.8 * sm(0.012, 0.0, np.abs(np.minimum(front, top) + 0.005)))
    seam = sm(0.01, 0.0, np.abs(top + 0.012)) * tm * (front > 0)
    img = mix(img, rgb('#FFF9D9'), 0.85 * seam * np.clip(0.3 + np.exp(-np.abs(x + 0.3) / 0.25), 0, 1))
    glint = np.exp(-(((x + 0.4) / 0.05) ** 2 + ((y - SLAB_HEIGHT + 0.035) / 0.016) ** 2))
    img = mix(img, [1, 1, 1], 0.8 * glint * fm)
    return small(np.dstack([np.clip(img, 0, 1), np.maximum(fm, tm)]))


if __name__ == '__main__':
    OUT.mkdir(exist_ok=True)
    colours = fills()
    body = np.asarray(bake(PER_BLOCK * SS, 1)).astype(np.float64) / 255
    for name, quads in faces().items():
        path = OUT / (re.sub(r'(?<=.)([A-Z])', r'-\1', name).lower() + '.png')  # SmugRight: smug-right
        still(body, quads, colours).save(path, optimize=True)
        print(path)
    slab().save(OUT / 'slab.png', optimize=True)
    print(OUT / 'slab.png')
