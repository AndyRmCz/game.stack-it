"""Bakes Blocky's glass body: Assets/_Project/Resources/StackIt/BlockyGlass.png.

Run from unity/ with numpy and Pillow: python Tools/blocky_glass.py. The face isn't in the image:
FaceDrawing draws it on top, so it can move. The numbers that place the image are BlockyGlass's.
Tools/blocky_stills.py bakes it larger, with faces, for the store art.
"""
import numpy as np
from PIL import Image

OUT = 'Assets/_Project/Resources/StackIt/BlockyGlass.png'
WIDTH, HEIGHT = 520 / 480, 568 / 480  # in block widths
FOOT = -0.04
BUBBLES = [(0.3, 0.13, 0.011), (0.355, 0.2, 0.006), (0.25, 0.09, 0.005), (-0.36, 0.34, 0.006), (0.385, 0.48, 0.005),
           (-0.31, 0.11, 0.008), (0.05, 0.075, 0.004)]


def rgb(h):
    h = h.lstrip('#')
    return np.array([int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)])


def sm(a, b, v):
    t = np.clip((v - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


def noise(seed, beta, shape):
    H, W = shape
    f = np.fft.fft2(np.random.default_rng(seed).normal(size=(H, W)))
    r = np.hypot(np.fft.fftfreq(H)[:, None], np.fft.fftfreq(W)[None, :])
    r[0, 0] = 1e-6
    n = np.fft.ifft2(f * r ** (-beta / 2)).real
    return (n - n.mean()) / n.std()


def rbox(x, y, cx, cy, hw, hh, r):
    qx, qy = np.abs(x - cx) - hw + r, np.abs(y - cy) - hh + r
    return np.hypot(np.maximum(qx, 0), np.maximum(qy, 0)) + np.minimum(np.maximum(qx, qy), 0) - r


def mix(a, colour, m):
    return a * (1 - m[..., None]) + np.asarray(colour) * m[..., None]


def ramp(t, stops):
    t = np.clip(t, 0, 1)
    pos = [s[0] for s in stops]
    return np.stack([np.interp(t, pos, [rgb(s[1])[c] for s in stops]) for c in range(3)], -1)


def bake(per_block=480, ss=3):
    """The body at per_block pixels per block width, drawn ss times larger and scaled down."""
    P = per_block * ss
    W, H = round(WIDTH * P), round(HEIGHT * P)
    GX, GY = np.meshgrid(-WIDTH / 2 + (np.arange(W) + 0.5) / P, FOOT + HEIGHT - (np.arange(H) + 0.5) / P)

    x = GX + noise(41, 4.6, GX.shape) * 0.0028
    y = GY + noise(42, 4.6, GX.shape) * 0.0028
    front = rbox(x, y, 0, 0.45, 0.5, 0.45, 0.11)
    top = rbox(x * (1 + (y - 0.82) * 0.34), y, 0, 0.9, 0.5, 0.2, 0.09)
    aa = 1.2 / per_block
    fm, tm = sm(aa, -aa, front), sm(aa, -aa, top)

    # Light goes in at the upper left and pools low on the far side.
    pool = np.exp(-(((x - 0.14) / 0.36) ** 2 + ((y - 0.03) / 0.2) ** 2))
    upper = np.exp(-(((x + 0.3) / 0.42) ** 2 + ((y - 0.72) / 0.32) ** 2))
    v = 0.46 + 0.62 * pool - 0.16 * upper + 0.05 * noise(43, 4.2, GX.shape)
    body = ramp(v, [(0, '#3419B0'), (0.32, '#5230E2'), (0.62, '#7A55FA'), (0.86, '#AD8CFF'), (1, '#E2A6FF')])
    body = mix(body, rgb('#27127F'), 0.5 * sm(0.09, 0.0, -front))  # thin glass looks darker and richer

    # The block's far edges, seen through it.
    back = rbox(x, y, 0.045, 0.49, 0.41, 0.375, 0.09)
    body = mix(body, rgb('#B9A2FF'), 0.22 * sm(0.012, 0.0, np.abs(back)) * sm(-0.2, 0.34, x + y - 0.62))

    t = np.clip((y - 0.78) / 0.34, 0, 1) * (1 - 0.18 * np.abs(x))
    plane = ramp(t + 0.03 * noise(44, 4.2, GX.shape), [(0, '#3F24C4'), (0.5, '#6E4CF0'), (1, '#BBA6FF')])
    img = np.ones(GX.shape + (3,)) * rgb('#27127F')
    img = mix(img, plane, tm)
    img = mix(img, body, fm)

    for bx, by, br in BUBBLES:
        d = np.hypot(x - bx, y - by)
        img = mix(img, rgb('#D9CCFF'), 0.42 * sm(br * 0.34, 0.0, np.abs(d - br)))
        img = mix(img, [1, 1, 1], 0.7 * sm(br * 0.4, br * 0.18, np.hypot(x - bx + br * 0.35, y - by - br * 0.35)))

    # A soft window reflection, curved with the bevel.
    c, s = np.cos(-0.22), np.sin(-0.22)
    rx, ry = (x + 0.25) * c - (y - 0.72) * s, (x + 0.25) * s + (y - 0.72) * c
    window = sm(0.04, -0.04, rbox(rx, ry + 1.5 * rx * rx, 0, 0, 0.16, 0.085, 0.05))
    img = mix(img, rgb('#EFE9FF'), 0.17 * window * sm(-0.1, 0.09, ry) * fm)

    # Edges: dark where the glass turns away, bright only where the bevel meets the light.
    img = mix(img, rgb('#1C0D63'), 0.72 * sm(0.014, 0.0, np.abs(np.minimum(front, top) + 0.005)))
    near_light = np.exp(-np.hypot(x + 0.42, y - 0.82) / 0.2)
    far_corner = np.exp(-np.hypot(x - 0.4, y - 0.08) / 0.15)
    bevel = sm(0.013, 0.0, np.abs(front + 0.022)) * np.clip(0.04 + 1.25 * near_light + 0.75 * far_corner + 0.22 * sm(0.5, 0.9, y), 0, 1)
    img = mix(img, rgb('#E9E1FF'), 0.9 * bevel)
    seam = sm(0.012, 0.0, np.abs(top + 0.02)) * tm * (front > 0) * np.clip(0.2 + np.exp(-np.abs(x + 0.3) / 0.25), 0, 1)
    img = mix(img, rgb('#E9E1FF'), 0.7 * seam)
    img = mix(img, [1, 1, 1], 0.9 * np.exp(-(((x + 0.385) / 0.034) ** 2 + ((y - 0.795) / 0.02) ** 2)) * fm)

    out = np.dstack([np.clip(img, 0, 1), np.maximum(fm, tm)])
    size = (round(WIDTH * per_block), round(HEIGHT * per_block))
    return Image.fromarray((out * 255).astype(np.uint8), 'RGBA').resize(size, Image.LANCZOS)


if __name__ == '__main__':
    bake().save(OUT, optimize=True)
    print(OUT)
