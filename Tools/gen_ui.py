"""Procedural UI art generator for Snow Boarder (Unity 2D).

Draws every asset at 4x supersampling with numpy + Pillow, then downsamples
with LANCZOS (premultiplied alpha) for clean anti-aliased edges.

Usage:  python Tools/gen_ui.py [preview_path]
Outputs: Assets/Art/Generated/UI/*.png
"""
import math
import os
import random
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

SS = 4
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Art", "Generated", "UI")
FONTS = "C:/Windows/Fonts"


def hexc(h):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)], np.float32) / 255.0


WHITE = hexc("FFFFFF")
ICE = hexc("BFE6FF")
SKY = hexc("7CC8F5")
NAVY = hexc("14284F")
NAVY2 = hexc("1E3A73")
NAVY_D = hexc("0A1530")
ORANGE = hexc("FF9A3C")
GOLD = hexc("FFD24A")


# ----------------------------------------------------------------------------
# Canvas (premultiplied float RGBA at SS resolution)
# ----------------------------------------------------------------------------
class Canvas:
    def __init__(self, w, h):
        self.w, self.h = w, h
        self.W, self.H = w * SS, h * SS
        self.rgb = np.zeros((self.H, self.W, 3), np.float32)
        self.a = np.zeros((self.H, self.W), np.float32)

    def over(self, mask, color, alpha=1.0):
        m = np.clip(mask, 0, 1).astype(np.float32) * alpha
        col = np.asarray(color, np.float32)
        if col.ndim == 1:
            col = col[None, None, :]
        m3 = m[..., None]
        self.rgb = col * m3 + self.rgb * (1 - m3)
        self.a = m + self.a * (1 - m)

    def image(self):
        def down(arr):
            return np.asarray(Image.fromarray(arr.astype(np.float32), "F")
                              .resize((self.w, self.h), Image.LANCZOS), np.float32)
        a = np.clip(down(self.a), 0, 1)
        rgb = np.stack([down(self.rgb[..., i]) for i in range(3)], -1)
        safe = np.where(a > 1e-4, a, 1)[..., None]
        rgb = np.clip(rgb / safe, 0, 1)
        rgb[a <= 1e-4] = 0
        out = np.dstack([rgb, a[..., None]])
        return Image.fromarray((out * 255 + 0.5).astype(np.uint8), "RGBA")


# ----------------------------------------------------------------------------
# Mask helpers (coordinates in FINAL pixels, scaled internally)
# ----------------------------------------------------------------------------
def _img(c):
    return Image.new("L", (c.W, c.H), 0)


def _arr(img):
    return np.asarray(img, np.float32) / 255.0


def S(v):
    return v * SS


def m_circle(c, cx, cy, r):
    im = _img(c)
    ImageDraw.Draw(im).ellipse([S(cx - r), S(cy - r), S(cx + r) - 1, S(cy + r) - 1], fill=255)
    return _arr(im)


def m_ellipse(c, x0, y0, x1, y1):
    im = _img(c)
    ImageDraw.Draw(im).ellipse([S(x0), S(y0), S(x1) - 1, S(y1) - 1], fill=255)
    return _arr(im)


def m_rrect(c, x0, y0, x1, y1, r):
    im = _img(c)
    ImageDraw.Draw(im).rounded_rectangle([S(x0), S(y0), S(x1) - 1, S(y1) - 1], radius=S(r), fill=255)
    return _arr(im)


def m_poly(c, pts):
    im = _img(c)
    ImageDraw.Draw(im).polygon([(S(x), S(y)) for x, y in pts], fill=255)
    return _arr(im)


def m_line(c, pts, w, caps=True):
    im = _img(c)
    d = ImageDraw.Draw(im)
    sp = [(S(x), S(y)) for x, y in pts]
    d.line(sp, fill=255, width=int(S(w)), joint="curve")
    if caps:
        r = S(w) / 2
        for x, y in (sp[0], sp[-1]):
            d.ellipse([x - r, y - r, x + r, y + r], fill=255)
    return _arr(im)


def m_pieslice(c, cx, cy, r, a0, a1):
    im = _img(c)
    ImageDraw.Draw(im).pieslice([S(cx - r), S(cy - r), S(cx + r), S(cy + r)], a0, a1, fill=255)
    return _arr(im)


def m_arc(c, cx, cy, r, a0, a1, w):
    """Thick arc with round caps."""
    im = _img(c)
    d = ImageDraw.Draw(im)
    ro = r + w / 2
    d.arc([S(cx - ro), S(cy - ro), S(cx + ro), S(cy + ro)], a0, a1, fill=255, width=int(S(w)))
    for a in (a0, a1):
        x = cx + r * math.cos(math.radians(a))
        y = cy + r * math.sin(math.radians(a))
        rr = S(w) / 2
        d.ellipse([S(x) - rr, S(y) - rr, S(x) + rr, S(y) + rr], fill=255)
    return _arr(im)


def m_text(c, x, y, txt, font, size, anchor="mm"):
    im = _img(c)
    f = ImageFont.truetype(os.path.join(FONTS, font), int(S(size)))
    ImageDraw.Draw(im).text((S(x), S(y)), txt, font=f, fill=255, anchor=anchor)
    return _arr(im)


def union(*ms):
    out = ms[0]
    for m in ms[1:]:
        out = np.maximum(out, m)
    return out


def sub(a, b):
    return np.clip(a - b, 0, 1)


def inter(a, b):
    return np.minimum(a, b)


def shift(m, dx, dy):
    """Shift by final-pixel offsets (zero fill)."""
    dx, dy = int(round(S(dx))), int(round(S(dy)))
    out = np.zeros_like(m)
    H, W = m.shape[:2]
    ys0, ys1 = max(0, -dy), min(H, H - dy)
    xs0, xs1 = max(0, -dx), min(W, W - dx)
    out[ys0 + dy:ys1 + dy, xs0 + dx:xs1 + dx] = m[ys0:ys1, xs0:xs1]
    return out


def blur(m, r):
    im = Image.fromarray((np.clip(m, 0, 1) * 255).astype(np.uint8), "L")
    return _arr(im.filter(ImageFilter.GaussianBlur(S(r))))


def _shift_px(a, k, axis):
    """result[i] = a[i + k] along axis (zero fill), k in raw pixels."""
    if k == 0:
        return a
    out = np.zeros_like(a)
    n = a.shape[axis]
    if abs(k) >= n:
        return out
    src = [slice(None)] * a.ndim
    dst = [slice(None)] * a.ndim
    if k > 0:
        src[axis] = slice(k, n)
        dst[axis] = slice(0, n - k)
    else:
        src[axis] = slice(0, n + k)
        dst[axis] = slice(-k, n)
    out[tuple(dst)] = a[tuple(src)]
    return out


def win_max(a, lo, hi, axis):
    """result[i] = max(a[i+lo .. i+hi]) along axis (raw pixels)."""
    n = hi - lo + 1
    if n <= 1:
        return _shift_px(a, lo, axis)
    m = a.copy()
    s = 1
    while s * 2 <= n:
        m = np.maximum(m, _shift_px(m, s, axis))
        s *= 2
    if s < n:
        m = np.maximum(m, _shift_px(m, n - s, axis))
    return _shift_px(m, lo, axis)


def dilate(m, r, k=10):
    """Disc dilation (radius r final px) approximated by a union of rectangles."""
    R = S(r)
    P = int(R) + 2
    b = np.pad((m > 0.5).astype(np.uint8), P)  # pad so edge shifts never lose data
    out = np.zeros_like(b)
    for i in range(k):
        t = (math.pi / 2) * i / (k - 1)
        ax = int(round(R * math.cos(t)))
        ay = int(round(R * math.sin(t)))
        tmp = win_max(win_max(b, -ax, ax, 1), -ay, ay, 0)
        out = np.maximum(out, tmp)
    return out[P:-P, P:-P].astype(np.float32)


def erode(m, r):
    return 1.0 - dilate(1.0 - (m > 0.5), r)


def smooth(m, r):
    """Blur + threshold: rounds shapes and merges blobs."""
    return (blur(m, r) > 0.5).astype(np.float32)


def vgrad(c, y0, y1, stops):
    """Vertical gradient (H,1,3). stops: list of (t, color)."""
    ys = (np.arange(c.H, dtype=np.float32) + 0.5) / SS
    t = np.clip((ys - y0) / max(1e-6, (y1 - y0)), 0, 1)
    out = np.zeros((c.H, 3), np.float32)
    ts = [s[0] for s in stops]
    for ch in range(3):
        out[:, ch] = np.interp(t, ts, [s[1][ch] for s in stops])
    return out[:, None, :]


def outline_shadow(c, m, ow, sh=(0, 3), sh_blur=2.0, sh_alpha=0.35, color=NAVY):
    """Dilated outline with soft drop shadow; returns outline mask."""
    o = dilate(m, ow)
    if sh_alpha > 0:
        c.over(blur(shift(o, *sh), sh_blur), NAVY_D, sh_alpha)
    c.over(o, color)
    return o


def save(img, name):
    p = os.path.join(OUT, name)
    img.save(p)
    print("wrote", name, img.size)
    return img


# ----------------------------------------------------------------------------
# Small shapes
# ----------------------------------------------------------------------------
def star_pts(cx, cy, R, ri, n=5, rot=-90):
    pts = []
    for i in range(n * 2):
        r = R if i % 2 == 0 else ri
        a = math.radians(rot + i * 180 / n)
        pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return pts


def sparkle_pts(cx, cy, R, ri, rot=-90):
    return star_pts(cx, cy, R, ri, n=4, rot=rot)


def snowflake_mask(c, cx, cy, L, w, branch=True, hexr=None):
    parts = []
    for i in range(6):
        a = math.radians(-90 + i * 60)
        ex, ey = cx + L * math.cos(a), cy + L * math.sin(a)
        parts.append(m_line(c, [(cx, cy), (ex, ey)], w))
        if branch:
            bx, by = cx + L * 0.58 * math.cos(a), cy + L * 0.58 * math.sin(a)
            for s in (-1, 1):
                b = a + s * math.radians(55)
                bl = L * 0.36
                parts.append(m_line(c, [(bx, by), (bx + bl * math.cos(b), by + bl * math.sin(b))], w * 0.8))
    if hexr:
        parts.append(m_poly(c, [(cx + hexr * math.cos(math.radians(-90 + i * 60)),
                                 cy + hexr * math.sin(math.radians(-90 + i * 60))) for i in range(6)]))
    return union(*parts)


# ----------------------------------------------------------------------------
# 1. Logo
# ----------------------------------------------------------------------------
def make_logo():
    W, H = 1600, 440
    c = Canvas(W, H)
    rng = random.Random(7)
    text = "SNOW BOARDER"
    fpath = os.path.join(FONTS, "ariblk.ttf")
    track = 6
    target = 1430
    f100 = ImageFont.truetype(fpath, 100)
    base = sum(f100.getlength(ch) for ch in text)
    size = (target - track * (len(text) - 1)) / base * 100
    font = ImageFont.truetype(fpath, int(S(size)))
    capH = font.getbbox("H")[3] - font.getbbox("H")[1]  # raw px
    total = sum(font.getlength(ch) for ch in text) + S(track) * (len(text) - 1)

    letters = np.zeros((c.H, c.W), np.float32)
    caps = np.zeros((c.H, c.W), np.float32)
    cap_top_y = S(150)  # top of cap height in raw px (before arc)
    x = (c.W - total) / 2
    arcA = S(16)
    pad = int(S(40))
    Hb = font.getbbox("H")
    for ch in text:
        adv = font.getlength(ch)
        if ch == " ":
            x += adv + S(track)
            continue
        bb = font.getbbox(ch)
        lw, lh = bb[2] - bb[0], bb[3] - bb[1]
        li = Image.new("L", (int(lw + 2 * pad), int(bb[3] - Hb[1] + 2 * pad)), 0)
        # draw so that cap-top sits at y=pad
        ImageDraw.Draw(li).text((pad - bb[0], pad - Hb[1]), ch, font=font, fill=255)
        lm = np.asarray(li, np.float32) / 255.0
        cap = letter_snowcap(lm, capH, pad, rng)
        # arc placement
        cxl = x + bb[0] + lw / 2
        t = cxl / c.W
        yoff = -arcA * 4 * t * (1 - t)
        slope = -arcA * 4 * (1 - 2 * t) / c.W
        ang = -math.degrees(math.atan(slope))
        li2 = Image.fromarray((lm * 255).astype(np.uint8)).rotate(ang, Image.BICUBIC, expand=True)
        ci2 = Image.fromarray((cap * 255).astype(np.uint8)).rotate(ang, Image.BICUBIC, expand=True)
        # center of original image on canvas
        ocx = x + bb[0] - pad + li.width / 2
        ocy = cap_top_y + arcA * 0.0 - pad + li.height / 2 + yoff + arcA
        px0 = int(round(ocx - li2.width / 2))
        py0 = int(round(ocy - li2.height / 2))
        for src, dst in ((li2, letters), (ci2, caps)):
            a = np.asarray(src, np.float32) / 255.0
            dst[py0:py0 + a.shape[0], px0:px0 + a.shape[1]] = np.maximum(
                dst[py0:py0 + a.shape[0], px0:px0 + a.shape[1]], a)
        x += adv + S(track)

    letters = (letters > 0.5).astype(np.float32)
    caps = (caps > 0.5).astype(np.float32)
    rows = np.where(letters.max(1) > 0)[0]
    ty, by = rows[0] / SS, rows[-1] / SS  # final px

    # ---- mountains behind
    mx0, mx1 = 300, 1300
    mpts = [(mx0, by - 30), (430, 150), (500, 110), (560, 132), (660, 70), (760, 28),
            (850, 80), (905, 66), (1010, 112), (1090, 92), (1180, 150), (mx1, by - 30)]
    mount = m_poly(c, mpts)
    snow_pts = [(612, 100), (660, 70), (760, 28), (850, 80), (905, 66), (950, 92),
                (912, 104), (880, 96), (840, 110), (800, 92), (760, 116), (720, 96), (680, 112), (640, 100)]
    msnow = inter(m_poly(c, snow_pts), mount)
    snow2 = [(470, 132), (500, 110), (560, 132), (532, 140), (512, 132), (492, 144)]
    snow3 = [(1040, 104), (1090, 92), (1140, 124), (1116, 124), (1096, 116), (1074, 126), (1056, 114)]
    msnow = union(msnow, inter(m_poly(c, snow2), mount), inter(m_poly(c, snow3), mount))

    # ---- swoosh (board trail) under text
    sw_top, sw_bot = [], []
    n = 80
    for i in range(n + 1):
        t = i / n
        xx = 150 + t * 1300
        yc = by + 40 - 30 * math.sin(math.pi * t) + 14 * t
        th = 5 + 42 * math.sin(math.pi * t ** 0.8) ** 1.2
        sw_top.append((xx, yc - th / 2))
        sw_bot.append((xx, yc + th / 2))
    swoosh = m_poly(c, sw_top + sw_bot[::-1])
    hl_top = [(x_, y_ + 6) for x_, y_ in sw_top[16:72]]
    hl_bot = [(x_, y_ + 12) for x_, y_ in sw_top[16:72]]
    sw_hl = inter(m_poly(c, hl_top + hl_bot[::-1]), swoosh)

    # ---- outlines
    txt_all = union(letters, caps)
    txt_out = dilate(txt_all, 14)
    m_out = dilate(mount, 8)
    s_out = dilate(swoosh, 8)

    # sparkles + flakes
    spark_specs = [(118, 150, 30), (1488, 120, 34), (1530, 335, 22), (66, 330, 20), (240, 405, 13), (1370, 410, 14), (1395, 60, 16), (210, 62, 14)]
    sparks = [sparkle_pts(x_, y_, r, r * 0.3) for x_, y_, r in spark_specs]
    spark_m = union(*[m_poly(c, p) for p in sparks])
    spark_m = smooth(spark_m, 0.6)
    spark_o = dilate(spark_m, 5)
    flakes = [(40, 220, 16), (1565, 210, 15), (300, 380, 0), (1320, 395, 0)]
    flake_m = union(*[snowflake_mask(c, x_, y_, r, 5, branch=True) for x_, y_, r in flakes if r > 0])
    flake_o = dilate(flake_m, 4)

    # ---- global drop shadow
    allo = union(txt_out, m_out, s_out)
    c.over(blur(shift(allo, 7, 12), 5), NAVY_D, 0.45)
    c.over(blur(shift(union(spark_o, flake_o), 3, 5), 2), NAVY_D, 0.35)

    # mountains
    c.over(m_out, NAVY)
    c.over(mount, vgrad(c, 28, by, [(0, hexc("9FD3F7")), (0.6, hexc("5AA9E0")), (1, hexc("3D7FC4"))]))
    # light face on left side of each peak for depth
    lit = m_poly(c, [(760, 28), (720, 96), (690, 200), (560, 260), (430, 150), (500, 110), (560, 132), (660, 70)])
    c.over(inter(lit, sub(mount, msnow)), WHITE, 0.18)
    c.over(msnow, vgrad(c, 28, 120, [(0, WHITE), (1, hexc("DDF0FF"))]))

    # swoosh
    c.over(s_out, NAVY)
    c.over(swoosh, vgrad(c, by - 20, by + 50, [(0, hexc("FFC163")), (0.5, ORANGE), (1, hexc("EE6F1C"))]))
    c.over(sw_hl, hexc("FFE7A8"), 0.85)

    # text extrude + outline
    extr = win_max(txt_out.astype(np.uint8), -int(S(9)), 0, 0).astype(np.float32)
    c.over(extr, NAVY_D)
    c.over(txt_out, NAVY)
    # letter body: dark rim then gradient core
    c.over(letters, hexc("4C9BD8"))
    core = erode(letters, 3.2)
    c.over(core, vgrad(c, ty, by, [(0, WHITE), (0.35, hexc("EEF8FF")), (0.62, ICE), (1, SKY)]))
    # bottom inner gloss line
    c.over(inter(core, sub(shift(core, 0, -5), shift(core, 0, -10))), WHITE, 0.0)
    # snow cap shading on the letter face
    capsh = inter(sub(shift(caps, 0, 5), caps), letters)
    c.over(blur(capsh, 0.8), hexc("3F7FBF"), 0.55)
    c.over(caps, vgrad(c, ty - 15, ty + 40, [(0, WHITE), (0.7, WHITE), (1, hexc("DCEFFF"))]))
    # cap top sparkle highlights
    caphl = inter(sub(caps, shift(caps, 0, 4)), caps)
    c.over(caphl, WHITE, 1)

    # sparkles + flakes
    c.over(spark_o, NAVY)
    c.over(spark_m, vgrad(c, 40, 360, [(0, hexc("FFF3B0")), (1, GOLD)]))
    for x_, y_, r in spark_specs:
        c.over(m_circle(c, x_, y_, r * 0.16), WHITE, 0.95)
    c.over(flake_o, NAVY)
    c.over(flake_m, WHITE)
    # tiny dot sparkles
    for x_, y_, r in [(1450, 30, 4), (1585, 280, 4), (130, 400, 4), (1470, 415, 4)]:
        c.over(m_circle(c, x_, y_, r + 3), NAVY)
        c.over(m_circle(c, x_, y_, r), WHITE)

    return save(c.image(), "Logo.png")


def letter_snowcap(lm, capH, pad, rng):
    """Build a lumpy snow cap + drips for one letter mask (raw SS pixels)."""
    h, w = lm.shape
    b = lm > 0.5
    has = b.any(0)
    ty = np.where(has, b.argmax(0), h * 10)
    eligible = ty <= pad + 0.28 * capH
    cap = np.zeros_like(lm)
    if not eligible.any():
        return cap
    yy = np.arange(h)[:, None]
    # smooth noisy depth
    xs = np.arange(w)
    ph = rng.random() * 10
    depth = capH * (0.15 + 0.035 * np.sin(xs / (S(9)) + ph) + 0.02 * np.sin(xs / S(4.3) + ph * 2))
    band = (yy < (ty + depth)[None, :]) & b & eligible[None, :]
    cap = band.astype(np.float32)
    im = Image.fromarray((cap * 255).astype(np.uint8))
    d = ImageDraw.Draw(im)
    cols = np.where(eligible)[0]
    # split eligible columns into runs
    runs = np.split(cols, np.where(np.diff(cols) > 1)[0] + 1)
    for run in runs:
        if len(run) < S(6):
            continue
        x0, x1 = run[0], run[-1]
        span = x1 - x0
        step = S(15)
        nb = max(2, int(span / step) + 1)
        for i in range(nb):
            xx = x0 + span * (i / (nb - 1))
            r = S(rng.uniform(10.5, 15.5)) if span > S(30) else S(rng.uniform(8, 11))
            # keep end blobs only slightly overhanging
            xx = min(max(xx, x0 + r - S(5)), x1 - r + S(5))
            col = int(min(max(xx, x0), x1))
            yc = ty[col] + r * 0.62
            d.ellipse([xx - r, yc - r, xx + r, yc + r], fill=255)
        # drips
        ndrip = 1 if span < S(70) else 2
        cands = list(range(int(x0 + S(12)), int(x1 - S(12)), int(S(4))))
        rng.shuffle(cands)
        made = 0
        for xc in cands:
            if made >= ndrip:
                break
            dw = S(rng.uniform(9, 13))
            dl = S(rng.uniform(16, 32))
            ys = int(ty[xc] + depth[xc] - S(3))
            xa, xb = int(xc - dw / 2 - S(4)), int(xc + dw / 2 + S(4))
            if xa < 0 or xb >= w or ys + dl + S(6) >= h:
                continue
            if not b[ys:int(ys + dl + S(6)), xa:xb].all():
                continue
            d.rounded_rectangle([xc - dw / 2, ys - S(4), xc + dw / 2, ys + dl], radius=dw / 2, fill=255)
            made += 1
    m = np.asarray(im, np.float32) / 255.0
    m = (np.asarray(Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(S(2.2))),
                    np.float32) / 255.0 > 0.5).astype(np.float32)
    # drips must stay on the letter face; lumps may rise above the letter top
    above = (yy < ty[None, :] + S(2))
    m = np.where(above, m, np.minimum(m, b.astype(np.float32)))
    return m


# ----------------------------------------------------------------------------
# 2-4. Tintable 9-slice sprites
# ----------------------------------------------------------------------------
def make_button():
    c = Canvas(256, 112)
    x0, y0, x1, y1, r = 6, 5, 250, 101, 36
    body = m_rrect(c, x0, y0, x1, y1, r)
    c.over(blur(shift(body, 0, 4), 2.6), NAVY_D, 0.38)
    c.over(body, hexc("98A6BA"))  # rim
    inner = m_rrect(c, x0 + 3, y0 + 3, x1 - 3, y1 - 3, r - 3)
    c.over(inner, vgrad(c, 86, 98, [(0, hexc("C4CEDC")), (1, hexc("A9B6C8"))]))  # bevel band
    face = m_rrect(c, x0 + 3, y0 + 3, x1 - 3, 86, r - 3)
    c.over(face, vgrad(c, 8, 48, [(0, hexc("F4F7FB")), (1, hexc("E3E9F2"))]))
    gl = m_rrect(c, x0 + 12, y0 + 6, x1 - 12, 46, 24)
    fade = vgrad(c, y0 + 6, 46, [(0, np.ones(3)), (1, np.full(3, 0.15))])[..., 0]
    c.over(gl * np.broadcast_to(fade, gl.shape), WHITE, 0.95)
    return save(c.image(), "Button.png")


def make_panel():
    c = Canvas(256, 256)
    x0, y0, x1, y1, r = 12, 10, 244, 242, 44
    body = m_rrect(c, x0, y0, x1, y1, r)
    c.over(blur(shift(body, 0, 4), 6), NAVY_D, 0.30)
    c.over(body, hexc("C3CEDD"))
    fill = m_rrect(c, x0 + 2, y0 + 2, x1 - 2, y1 - 2, r - 2)
    c.over(fill, vgrad(c, y0, y0 + 40, [(0, WHITE), (1, hexc("F7F9FC"))]))
    ib = 10
    ring = sub(m_rrect(c, x0 + ib, y0 + ib, x1 - ib, y1 - ib, r - ib),
               m_rrect(c, x0 + ib + 2, y0 + ib + 2, x1 - ib - 2, y1 - ib - 2, r - ib - 2))
    c.over(ring, hexc("D7DFEA"))
    return save(c.image(), "Panel.png")


def make_panel_hud():
    c = Canvas(192, 192)
    x0, y0, x1, y1, r = 3, 3, 189, 189, 36
    body = m_rrect(c, x0, y0, x1, y1, r)
    fill = m_rrect(c, x0 + 3, y0 + 3, x1 - 3, y1 - 3, r - 3)
    c.over(sub(body, fill), WHITE, 1.0)
    c.over(fill, hexc("F2F6FB"), 0.85)
    inl = sub(fill, m_rrect(c, x0 + 4, y0 + 4, x1 - 4, y1 - 4, r - 4))
    # subtle 1px inner line for definition (kept within the fill alpha)
    c.over(inl, hexc("D3DCE8"), 0.85)
    return save(c.image(), "PanelHUD.png")


# ----------------------------------------------------------------------------
# 5. Icons (128x128)
# ----------------------------------------------------------------------------
OW = 5


def gloss(c, mask, x0, y0, x1, y1, alpha=0.45, inset=4):
    g = inter(m_ellipse(c, x0, y0, x1, y1), erode(mask, inset))
    c.over(g, WHITE, alpha)


def glyph(c, m, ow=3.5, sh=True):
    o = dilate(m, ow)
    if sh:
        c.over(blur(shift(o, 0, 2), 1.2), NAVY_D, 0.30)
    c.over(o, NAVY)
    c.over(m, vgrad(c, 20, 110, [(0, WHITE), (1, hexc("E3EEF8"))]))


def star_mask(c, cx=64, cy=67, R=46, k=0.47, rnd=4):
    return smooth(dilate(m_poly(c, star_pts(cx, cy, R - rnd, (R - rnd) * k)), rnd), 0.5)


def icon_star(empty=False):
    c = Canvas(128, 128)
    m = star_mask(c)
    if empty:
        o = dilate(m, OW)
        c.over(blur(shift(o, 0, 3), 2), NAVY_D, 0.25)
        c.over(o, NAVY)
        c.over(m, hexc("3A5288"), 0.55)
        inner = smooth(dilate(m_poly(c, star_pts(64, 67, 24, 24 * 0.47)), 3), 0.5)
        c.over(inner, hexc("6F86B8"), 0.30)
        return save(c.image(), "Icon_StarEmpty.png")
    outline_shadow(c, m, OW)
    c.over(m, vgrad(c, 18, 112, [(0, hexc("FFF0A0")), (0.45, GOLD), (1, hexc("F29A1E"))]))
    inner = smooth(dilate(m_poly(c, star_pts(64, 67, 22, 22 * 0.47)), 3), 0.5)
    c.over(inner, hexc("FFF4B8"), 0.55)
    # lower rim shade
    c.over(inter(m, sub(m, shift(m, 0, -4))), hexc("D9770F"), 0.55)
    gloss(c, m, 30, 22, 74, 58, 0.5, 5)
    return save(c.image(), "Icon_Star.png")


def icon_speed():
    c = Canvas(128, 128)
    cx, cy, R = 64, 66, 50
    body = m_circle(c, cx, cy, R)
    outline_shadow(c, body, OW)
    c.over(body, vgrad(c, 16, 116, [(0, ICE), (1, hexc("5EAEE6"))]))
    face = m_circle(c, cx, cy, 41)
    c.over(face, vgrad(c, 25, 107, [(0, WHITE), (1, hexc("E6F2FB"))]))
    # coloured scale band
    band_o, band_i = 37, 28
    segs = [(150, 225, "4CD07A"), (225, 290, "FFD24A"), (290, 340, "FF9A3C"), (340, 390, "FF5B5B")]
    inner_c = m_circle(c, cx, cy, band_i)
    for a0, a1, col in segs:
        c.over(sub(m_pieslice(c, cx, cy, band_o, a0, a1), inner_c), hexc(col))
    # ticks
    for i in range(9):
        a = math.radians(150 + i * 30)
        p0 = (cx + 24 * math.cos(a), cy + 24 * math.sin(a))
        p1 = (cx + 19 * math.cos(a), cy + 19 * math.sin(a))
        c.over(m_line(c, [p0, p1], 3), NAVY2)
    # needle
    a = math.radians(318)
    tip = (cx + 36 * math.cos(a), cy + 36 * math.sin(a))
    pa = (cx + 6 * math.cos(a + math.pi / 2), cy + 6 * math.sin(a + math.pi / 2))
    pb = (cx + 6 * math.cos(a - math.pi / 2), cy + 6 * math.sin(a - math.pi / 2))
    needle = smooth(m_poly(c, [tip, pa, pb]), 0.4)
    no = dilate(needle, 2)
    c.over(blur(shift(no, 1, 2), 1), NAVY_D, 0.3)
    c.over(no, NAVY)
    c.over(needle, hexc("FF6A3D"))
    hub = m_circle(c, cx, cy, 9)
    c.over(hub, NAVY)
    c.over(m_circle(c, cx - 2, cy - 2, 3), hexc("7CC8F5"))
    # little label bar at the bottom
    c.over(m_rrect(c, cx - 13, cy + 22, cx + 13, cy + 30, 4), NAVY2, 0.85)
    gloss(c, body, 26, 20, 90, 52, 0.35, 6)
    return save(c.image(), "Icon_Speed.png")


def icon_timer():
    c = Canvas(128, 128)
    cx, cy, R = 64, 72, 44
    body = m_circle(c, cx, cy, R)
    crown = m_rrect(c, 52, 12, 76, 24, 4)
    neck = m_rrect(c, 58, 20, 70, 32, 2)
    # side button at ~45deg
    a = math.radians(-45)
    bx, by = cx + (R + 3) * math.cos(a), cy + (R + 3) * math.sin(a)
    side = m_line(c, [(bx - 5 * math.cos(a), by - 5 * math.sin(a)), (bx + 4 * math.cos(a), by + 4 * math.sin(a))], 11)
    allm = union(body, crown, neck, side)
    outline_shadow(c, allm, OW)
    c.over(union(crown, side), vgrad(c, 12, 40, [(0, hexc("FFC27A")), (1, hexc("F07F22"))]))
    c.over(neck, hexc("D0DCEA"))
    c.over(body, vgrad(c, 28, 116, [(0, ICE), (1, hexc("5EAEE6"))]))
    face = m_circle(c, cx, cy, 35)
    c.over(face, vgrad(c, 37, 107, [(0, WHITE), (1, hexc("E6F2FB"))]))
    c.over(m_pieslice(c, cx, cy, 30, -90, 40), hexc("FFB25C"), 0.85)
    for i in range(12):
        aa = math.radians(i * 30)
        ln = 6 if i % 3 == 0 else 3.5
        p0 = (cx + 31 * math.cos(aa), cy + 31 * math.sin(aa))
        p1 = (cx + (31 - ln) * math.cos(aa), cy + (31 - ln) * math.sin(aa))
        c.over(m_line(c, [p0, p1], 3 if i % 3 == 0 else 2.2), NAVY2)
    ha = math.radians(40)
    c.over(m_line(c, [(cx, cy), (cx + 24 * math.cos(ha), cy + 24 * math.sin(ha))], 5), NAVY)
    c.over(m_line(c, [(cx, cy), (cx, cy - 26)], 4), NAVY)
    c.over(m_circle(c, cx, cy, 6), NAVY)
    c.over(m_circle(c, cx, cy, 2.5), hexc("FF6A3D"))
    gloss(c, body, 32, 32, 90, 60, 0.35, 6)
    return save(c.image(), "Icon_Timer.png")


def icon_trophy():
    c = Canvas(128, 128)
    bowl = m_rrect(c, 32, 18, 96, 40, 3)
    bowl = union(bowl, inter(m_ellipse(c, 32, -6, 96, 78), m_rrect(c, 0, 30, 128, 128, 0)))
    hl = sub(m_circle(c, 31, 42, 15), m_circle(c, 31, 42, 8))
    hr = sub(m_circle(c, 97, 42, 15), m_circle(c, 97, 42, 8))
    stem = m_poly(c, [(56, 74), (72, 74), (69, 92), (59, 92)])
    base1 = m_rrect(c, 42, 90, 86, 100, 3)
    base2 = m_rrect(c, 34, 98, 94, 114, 4)
    allm = union(bowl, hl, hr, stem, base1, base2)
    outline_shadow(c, allm, OW)
    gold = vgrad(c, 18, 100, [(0, hexc("FFF0A0")), (0.45, GOLD), (1, hexc("E8901A"))])
    c.over(union(hl, hr), hexc("F2B231"))
    c.over(union(bowl, stem, base1), gold)
    c.over(base2, vgrad(c, 98, 114, [(0, hexc("4A7FC8")), (1, NAVY2)]))
    c.over(m_rrect(c, 52, 103, 76, 109, 2), hexc("FFD24A"), 0.95)
    # rim / opening
    c.over(m_rrect(c, 32, 18, 96, 24, 3), hexc("FFF6C8"))
    c.over(m_rrect(c, 32, 24, 96, 27, 0), hexc("E2981E"), 0.5)
    # star emboss
    st = smooth(dilate(m_poly(c, star_pts(64, 50, 12, 12 * 0.47)), 1.5), 0.4)
    c.over(shift(st, 0, 1.5), hexc("C9790E"), 0.6)
    c.over(st, hexc("FFF4B8"))
    # gloss stripe
    gs = inter(m_rrect(c, 40, 29, 47, 66, 3.5), erode(bowl, 3))
    c.over(gs, WHITE, 0.6)
    return save(c.image(), "Icon_Trophy.png")


def circle_icon(c, col_top, col_mid, col_bot):
    cx, cy, R = 64, 62, 52
    body = m_circle(c, cx, cy, R)
    outline_shadow(c, body, OW)
    c.over(body, vgrad(c, 10, 114, [(0, col_top), (0.5, col_mid), (1, col_bot)]))
    # bottom inner rim shade
    c.over(sub(body, m_circle(c, cx, cy - 3, R - 1)), col_bot * 0.85, 0.6)
    return body


def top_gloss(c, body):
    g = inter(m_ellipse(c, 28, 16, 100, 56), erode(body, 5))
    fade = vgrad(c, 16, 56, [(0, np.ones(3)), (1, np.zeros(3))])[..., 0]
    c.over(g * np.broadcast_to(fade, g.shape), WHITE, 0.55)


def icon_play():
    c = Canvas(128, 128)
    body = circle_icon(c, hexc("8BEBAA"), hexc("4CD07A"), hexc("27A957"))
    top_gloss(c, body)
    tri = smooth(dilate(m_poly(c, [(54, 42), (54, 82), (86, 62)]), 5), 0.4)
    glyph(c, tri)
    return save(c.image(), "Icon_Play.png")


def icon_gear():
    c = Canvas(128, 128)
    cx, cy = 64, 64
    parts = [m_circle(c, cx, cy, 39)]
    for i in range(8):
        a = math.radians(i * 45 + 22.5)
        ca, sa = math.cos(a), math.sin(a)
        px, py = -sa, ca
        r0, r1, w0, w1 = 30, 51, 12, 8.5
        parts.append(m_poly(c, [(cx + r0 * ca + w0 * px, cy + r0 * sa + w0 * py),
                                (cx + r1 * ca + w1 * px, cy + r1 * sa + w1 * py),
                                (cx + r1 * ca - w1 * px, cy + r1 * sa - w1 * py),
                                (cx + r0 * ca - w0 * px, cy + r0 * sa - w0 * py)]))
    g = union(*parts)
    g = erode(dilate(g, 3), 3)        # round inner corners
    g = dilate(erode(g, 3), 3)        # round outer corners
    g = smooth(g, 0.5)
    hole = m_circle(c, cx, cy, 15)
    g = sub(g, hole)
    o = outline_shadow(c, g, OW)
    c.over(g, vgrad(c, 12, 116, [(0, hexc("D6F0FF")), (0.45, SKY), (1, hexc("3F93D3"))]))
    ring = sub(m_circle(c, cx, cy, 29), m_circle(c, cx, cy, 25.5))
    c.over(ring, hexc("2F78B8"), 0.45)
    c.over(inter(m_ellipse(c, 30, 14, 98, 50), erode(g, 4)), WHITE, 0.35)
    return save(c.image(), "Icon_Gear.png")


def icon_help():
    c = Canvas(128, 128)
    body = circle_icon(c, hexc("D6F0FF"), SKY, hexc("3F93D3"))
    top_gloss(c, body)
    q = m_text(c, 65, 64, "?", "ariblk.ttf", 78)
    glyph(c, q, 4)
    return save(c.image(), "Icon_Help.png")


def icon_quit():
    c = Canvas(128, 128)
    body = circle_icon(c, hexc("FFA3A3"), hexc("FF6B6B"), hexc("DE4559"))
    top_gloss(c, body)
    cx, cy = 64, 66
    arc = m_arc(c, cx, cy, 23, 305, 235, 10)
    bar = m_line(c, [(cx, 34), (cx, 62)], 10)
    glyph(c, union(arc, bar), 3.5)
    return save(c.image(), "Icon_Quit.png")


def icon_arrow():
    c = Canvas(128, 128)
    ch = m_line(c, [(80, 26), (44, 63), (80, 100)], 28)
    outline_shadow(c, ch, OW)
    c.over(ch, vgrad(c, 14, 112, [(0, WHITE), (1, hexc("CFE5F7"))]))
    c.over(inter(m_line(c, [(76, 26), (44, 59)], 6), erode(ch, 6)), WHITE, 0.0)
    left = c.image()
    save(left, "Icon_ArrowLeft.png")
    save(left.transpose(Image.FLIP_LEFT_RIGHT), "Icon_ArrowRight.png")


def icon_snowflake():
    c = Canvas(128, 128)
    m = snowflake_mask(c, 64, 64, 47, 10, branch=True, hexr=14)
    outline_shadow(c, m, OW)
    c.over(m, vgrad(c, 12, 116, [(0, WHITE), (1, hexc("BFE3FA"))]))
    core = snowflake_mask(c, 64, 64, 40, 3, branch=False)
    c.over(core, hexc("8CCBF2"), 0.55)
    c.over(m_circle(c, 64, 64, 5.5), hexc("7CC8F5"))
    c.over(m_circle(c, 64, 64, 2.5), WHITE)
    return save(c.image(), "Icon_Snowflake.png")


def icon_heart():
    c = Canvas(128, 128)
    pts = []
    for i in range(360):
        t = 2 * math.pi * i / 360
        x = 16 * math.sin(t) ** 3
        y = 13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t)
        pts.append((64 + x * 3.15, 60 - y * 3.15))
    m = m_poly(c, pts)
    outline_shadow(c, m, OW)
    c.over(m, vgrad(c, 14, 112, [(0, hexc("FF8FA0")), (0.5, hexc("FF4D6D")), (1, hexc("CF2445"))]))
    c.over(sub(m, shift(m, 0, -4)), hexc("A81A38"), 0.45)
    # glossy highlight (rotated ellipse)
    gp = []
    for i in range(60):
        t = 2 * math.pi * i / 60
        ex, ey = 13 * math.cos(t), 7.5 * math.sin(t)
        a = math.radians(-38)
        gp.append((40 + ex * math.cos(a) - ey * math.sin(a), 38 + ex * math.sin(a) + ey * math.cos(a)))
    c.over(inter(m_poly(c, gp), erode(m, 4)), WHITE, 0.75)
    c.over(m_circle(c, 56, 31, 3.2), WHITE, 0.7)
    return save(c.image(), "Icon_Heart.png")


# ----------------------------------------------------------------------------
# 6. Divider
# ----------------------------------------------------------------------------
def make_divider():
    c = Canvas(512, 16)
    cx, cy = 256, 8
    xs = (np.arange(c.W, dtype=np.float32) + 0.5) / SS
    d = np.abs(xs - cx)
    fade = np.clip((240 - d) / 200, 0, 1)
    fade = fade * fade * (3 - 2 * fade)
    gap = np.clip((d - 17) / 3, 0, 1)
    prof = (fade * gap)[None, :]
    line = m_rrect(c, 8, cy - 1.5, 504, cy + 1.5, 1.5)
    glow = blur(m_rrect(c, 8, cy - 2, 504, cy + 2, 2), 1.5)
    c.over(glow * prof, WHITE, 0.35)
    c.over(line * prof, WHITE, 0.95)
    dia = m_poly(c, [(cx, cy - 7), (cx + 7, cy), (cx, cy + 7), (cx - 7, cy)])
    c.over(blur(dia, 1.2), WHITE, 0.4)
    c.over(dia, WHITE)
    for s in (-1, 1):
        c.over(m_poly(c, [(cx + s * 12, cy - 2.6), (cx + s * 14.6, cy), (cx + s * 12, cy + 2.6), (cx + s * 9.4, cy)]), WHITE, 0.9)
    return save(c.image(), "Divider.png")


# ----------------------------------------------------------------------------
# Preview contact sheet
# ----------------------------------------------------------------------------
BORDERS = {  # left, bottom, right, top
    "Button.png": (46, 60, 46, 48),
    "Panel.png": (64, 64, 64, 64),
    "PanelHUD.png": (44, 44, 44, 44),
}


def nine_slice(img, border, w, h):
    L, B, R, T = border
    iw, ih = img.size
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    xs_src = [0, L, iw - R, iw]
    ys_src = [0, T, ih - B, ih]
    xs_dst = [0, L, w - R, w]
    ys_dst = [0, T, h - B, h]
    for i in range(3):
        for j in range(3):
            sb = (xs_src[i], ys_src[j], xs_src[i + 1], ys_src[j + 1])
            db = (xs_dst[i], ys_dst[j], xs_dst[i + 1], ys_dst[j + 1])
            dw, dh = db[2] - db[0], db[3] - db[1]
            if dw <= 0 or dh <= 0:
                continue
            piece = img.crop(sb).resize((dw, dh), Image.BILINEAR)
            out.paste(piece, db[:2])
    return out


def tint(img, col):
    a = np.asarray(img, np.float32) / 255.0
    t = hexc(col)
    a[..., :3] *= t
    return Image.fromarray((a * 255 + 0.5).astype(np.uint8), "RGBA")


def label(draw, xy, txt, col):
    f = ImageFont.truetype(os.path.join(FONTS, "segoeuib.ttf"), 18)
    draw.text(xy, txt, font=f, fill=col)


def section(assets, bg, txtcol):
    Wd, Hd = 1800, 1560
    im = Image.new("RGBA", (Wd, Hd), bg)
    d = ImageDraw.Draw(im)
    logo = assets["Logo.png"]
    im.alpha_composite(logo, (100, 20))
    im.alpha_composite(logo.resize((800, 220), Image.LANCZOS), (500, 470))
    label(d, (100, 470), "Logo @ 50%", txtcol)
    y = 720
    btn = assets["Button.png"]
    x = 60
    for col, name in (("4CD07A", "green"), ("FFFFFF", "white"), ("FF7FA8", "pink"), ("7CC8F5", "sky")):
        im.alpha_composite(tint(btn, col), (x, y))
        label(d, (x + 8, y + 116), "tint " + name, txtcol)
        x += 280
    sb = nine_slice(tint(btn, "FFB347"), BORDERS["Button.png"], 520, 112)
    im.alpha_composite(sb, (x, y))
    f = ImageFont.truetype(os.path.join(FONTS, "ariblk.ttf"), 40)
    d.text((x + 260, y + 50), "PLAY", font=f, fill=(255, 255, 255, 255), anchor="mm",
           stroke_width=4, stroke_fill=(20, 40, 79, 255))
    label(d, (x + 8, y + 116), "9-sliced 520x112 tint orange", txtcol)
    y = 880
    pnl = nine_slice(assets["Panel.png"], BORDERS["Panel.png"], 600, 400)
    im.alpha_composite(pnl, (60, y))
    im.alpha_composite(assets["Divider.png"].resize((512, 16)), (104, y + 110))
    im.alpha_composite(tint(assets["Divider.png"], "7CC8F5"), (104, y + 300))
    label(d, (60, y + 404), "Panel 9-sliced 600x400 (+ Divider white / tinted)", txtcol)
    im.alpha_composite(assets["Panel.png"], (700, y))
    hud = nine_slice(tint(assets["PanelHUD.png"], "1E3A73"), BORDERS["PanelHUD.png"], 340, 110)
    im.alpha_composite(hud, (1000, y))
    im.alpha_composite(assets["Icon_Snowflake.png"].resize((80, 80), Image.LANCZOS), (1020, y + 15))
    d.text((1120, y + 55), "x 128", font=f, fill=(255, 255, 255, 255), anchor="lm")
    hud2 = nine_slice(assets["PanelHUD.png"], BORDERS["PanelHUD.png"], 340, 110)
    im.alpha_composite(hud2, (1000, y + 130))
    im.alpha_composite(assets["Icon_Timer.png"].resize((80, 80), Image.LANCZOS), (1020, y + 145))
    d.text((1120, y + 185), "01:23", font=f, fill=(20, 40, 79, 255), anchor="lm")
    im.alpha_composite(assets["PanelHUD.png"], (1400, y))
    label(d, (1000, y + 250), "PanelHUD sliced 340x110 (navy tint / white) + raw", txtcol)
    for k in range(3):
        im.alpha_composite(assets["Icon_Star.png" if k < 2 else "Icon_StarEmpty.png"].resize((96, 96), Image.LANCZOS),
                           (1000 + k * 100, y + 290))
    im.alpha_composite(assets["Icon_Heart.png"].resize((64, 64), Image.LANCZOS), (1320, y + 306))
    im.alpha_composite(assets["Icon_Heart.png"].resize((32, 32), Image.LANCZOS), (1400, y + 322))
    y = 1310
    names = ["Icon_Star", "Icon_StarEmpty", "Icon_Speed", "Icon_Timer", "Icon_Trophy", "Icon_Play", "Icon_Gear",
             "Icon_Help", "Icon_Quit", "Icon_ArrowLeft", "Icon_ArrowRight", "Icon_Snowflake", "Icon_Heart"]
    x = 30
    for n in names:
        im.alpha_composite(assets[n + ".png"], (x, y))
        x += 134
    label(d, (30, y + 130), "Icons 128x128 (native)", txtcol)
    return im


def preview(path):
    assets = {f: Image.open(os.path.join(OUT, f)).convert("RGBA") for f in os.listdir(OUT) if f.endswith(".png")}
    a = section(assets, (61, 111, 181, 255), (255, 255, 255, 255))
    b = section(assets, (255, 255, 255, 255), (20, 40, 79, 255))
    sheet = Image.new("RGBA", (1800, a.height * 2), (0, 0, 0, 255))
    sheet.paste(a, (0, 0))
    sheet.paste(b, (0, a.height))
    sheet.convert("RGB").save(path)
    print("preview", path)


def main():
    os.makedirs(OUT, exist_ok=True)
    only = os.environ.get("ONLY")
    jobs = {
        "logo": make_logo, "button": make_button, "panel": make_panel, "hud": make_panel_hud,
        "star": lambda: (icon_star(False), icon_star(True)), "speed": icon_speed, "timer": icon_timer,
        "trophy": icon_trophy, "play": icon_play, "gear": icon_gear, "help": icon_help, "quit": icon_quit,
        "arrow": icon_arrow, "flake": icon_snowflake, "heart": icon_heart, "divider": make_divider,
    }
    for k, fn in jobs.items():
        if only and k not in only.split(","):
            continue
        fn()
    if len(sys.argv) > 1:
        preview(sys.argv[1])


if __name__ == "__main__":
    main()
