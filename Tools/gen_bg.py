"""Procedural winter background art generator for Snow Boarder.

Run:  python Tools/gen_bg.py [preview_dir]
Outputs RGBA PNGs to Assets/Art/Generated/BG/.
All horizontal layers tile seamlessly (features placed modulo width, periodic functions,
wrap-padded downscale).
"""
import os
import sys
import math
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageChops

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Art", "Generated", "BG")
SS = 4  # supersampling factor


# ----------------------------------------------------------------------------- helpers
def lerp(a, b, t):
    return a + (b - a) * t


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def mix_col(c1, c2, t):
    return tuple(int(round(lerp(c1[i], c2[i], t))) for i in range(3))


def periodic_noise(x, W, rng, kmin=1, kmax=24, beta=1.2):
    """Sum of sines with integer harmonics -> exactly periodic over W."""
    x = np.asarray(x, dtype=np.float64)
    out = np.zeros_like(x)
    for k in range(kmin, kmax + 1):
        a = rng.normal() / (k ** beta)
        ph = rng.uniform(0, 2 * math.pi)
        out += a * np.sin(2 * math.pi * k * x / W + ph)
    m = np.max(np.abs(out)) or 1.0
    return out / m


def md_line(p0, p1, rough, depth, rng):
    """Midpoint displacement polyline between p0 and p1 (perpendicular jitter)."""
    pts = [np.array(p0, float), np.array(p1, float)]
    d = np.linalg.norm(pts[1] - pts[0])
    disp = rough * d
    for _ in range(depth):
        new = [pts[0]]
        for a, b in zip(pts[:-1], pts[1:]):
            v = b - a
            n = np.array([-v[1], v[0]])
            nl = np.linalg.norm(n) or 1.0
            m = (a + b) / 2 + n / nl * rng.uniform(-1, 1) * disp
            new += [m, b]
        pts = new
        disp *= 0.52
    return [tuple(p) for p in pts]


def downscale_wrap(img, W, H, wrap=True):
    """Downscale an SSx RGBA image to (W,H) with LANCZOS, premultiplied, wrap-padded."""
    if wrap:
        pad = 16 * SS
        a = np.array(img)
        a = np.concatenate([a[:, -pad:], a, a[:, :pad]], axis=1)
        big = Image.fromarray(a, "RGBA").convert("RGBa")
        small = big.resize((W + 32, H), Image.LANCZOS).convert("RGBA")
        return small.crop((16, 0, 16 + W, H))
    return img.convert("RGBa").resize((W, H), Image.LANCZOS).convert("RGBA")


def bleed_transparent(img):
    """Copy the colour of the first visible pixel upward into transparent pixels in each
    column so bilinear filtering in Unity never pulls in black fringes."""
    a = np.array(img).astype(np.uint8)
    alpha = a[:, :, 3]
    H, W = alpha.shape
    vis = alpha > 8
    first = np.where(vis.any(axis=0), vis.argmax(axis=0), H - 1)
    rows = np.arange(H)[:, None]
    src = a[first, np.arange(W)][:, :3]  # W x 3
    mask = (rows < first[None, :]) & (alpha < 250)
    rgb = a[:, :, :3]
    # only replace colour of (nearly) transparent pixels above the ridge
    m2 = mask & (alpha < 4)
    rgb[m2] = np.broadcast_to(src[None, :, :], rgb.shape)[m2]
    a[:, :, :3] = rgb
    return Image.fromarray(a, "RGBA")


def save(img, name):
    p = os.path.join(OUT, name)
    img.save(p, optimize=True)
    print("wrote", p, img.size)


# ----------------------------------------------------------------------------- sky
SKY_STOPS = {
    "Day": [(0.0, (0x2E, 0x7B, 0xD6)), (0.18, (0x3D, 0x8B, 0xE0)), (0.55, (0x86, 0xBE, 0xEE)),
            (0.82, (0xC8, 0xE6, 0xF8)), (1.0, (0xF0, 0xF8, 0xFF))],
    "Dusk": [(0.0, (0x2A, 0x24, 0x5E)), (0.25, (0x4E, 0x3C, 0x8A)), (0.50, (0xA8, 0x5E, 0x9E)),
             (0.68, (0xEE, 0x8E, 0x92)), (0.84, (0xFA, 0xB4, 0x86)), (1.0, (0xFF, 0xD6, 0x8A))],
    "Storm": [(0.0, (0x2F, 0x38, 0x52)), (0.3, (0x4A, 0x55, 0x72)), (0.65, (0x87, 0x8D, 0xA6)),
              (1.0, (0xC9, 0xC8, 0xD8))],
}


def sky_column(stops, H):
    t = np.linspace(0, 1, H)
    col = np.zeros((H, 3))
    for i in range(3):
        col[:, i] = np.interp(t, [s[0] for s in stops], [s[1][i] for s in stops])
    return col


def make_sky(name, W=32, H=512, seed=1):
    rng = np.random.default_rng(seed)
    col = sky_column(SKY_STOPS[name], H)
    img = np.repeat(col[:, None, :], W, axis=1)
    img += rng.uniform(-0.7, 0.7, img.shape)  # dithering
    out = np.dstack([np.clip(np.round(img), 0, 255), np.full((H, W), 255)]).astype(np.uint8)
    return Image.fromarray(out, "RGBA")


# ----------------------------------------------------------------------------- sun
def make_sun(S=512):
    y, x = np.mgrid[0:S, 0:S].astype(np.float64)
    c = (S - 1) / 2
    r = np.hypot(x - c, y - c) / (S / 2)
    disc_r = 0.17
    disc = 1 - smoothstep(disc_r - 0.012, disc_r + 0.004, r)
    glow = np.exp(-((r / 0.30) ** 2)) * 0.55 + np.exp(-((r / 0.55) ** 2)) * 0.32
    glow *= 1 - smoothstep(0.6, 1.0, r)
    alpha = np.clip(disc + glow * (1 - disc), 0, 1)
    # colour: white core, warm halo
    core = np.array([255, 253, 244.0])
    halo = np.array([255, 236, 196.0])
    t = smoothstep(disc_r, 0.7, r)[..., None]
    rgb = core * (1 - t) + halo * t
    out = np.dstack([rgb, alpha * 255]).clip(0, 255).astype(np.uint8)
    return Image.fromarray(out, "RGBA")


# ----------------------------------------------------------------------------- mountains
def draw_peak(rng, cx, top, base, wl, wr, pal, opts):
    """Return (local RGB image, mask, origin x, origin y) at SS resolution.
    All coordinates already in SS pixels."""
    rough = opts.get("rough", 0.07)
    depth = opts.get("depth", 6)
    left = md_line((cx - wl, base), (cx, top), rough, depth, rng)
    right = md_line((cx, top), (cx + wr, base), rough, depth, rng)
    outline = left + right[1:]
    xs = [p[0] for p in outline]
    ys = [p[1] for p in outline]
    x0, x1 = int(min(xs)) - 4, int(max(xs)) + 4
    y0, y1 = int(min(ys)) - 4, int(base) + 4
    w, h = x1 - x0, y1 - y0

    def loc(pts):
        return [(px - x0, py - y0) for px, py in pts]

    M = Image.new("L", (w, h), 0)
    ImageDraw.Draw(M).polygon(loc(outline), fill=255)

    # main light/shadow ridge from the summit downwards (lit from upper-left)
    rx_end = cx + wr * rng.uniform(0.05, 0.35)
    ridge = md_line((cx, top), (rx_end, base), opts.get("ridge_rough", 0.09), depth, rng)
    L = Image.new("L", (w, h), 0)
    ImageDraw.Draw(L).polygon(loc(ridge + [(x0 - 10, base + 10), (x0 - 10, top - 10)]), fill=255)

    # secondary facet on the shadow side (mid tone band)
    F = Image.new("L", (w, h), 0)
    if opts.get("facets", True):
        sx = cx + wr * rng.uniform(0.25, 0.45)
        sy = lerp(top, base, rng.uniform(0.18, 0.35))
        # start somewhere on right edge
        k = min(range(len(right)), key=lambda i: abs(right[i][1] - sy))
        p_start = right[k]
        f2 = md_line(p_start, (rx_end + wr * rng.uniform(0.35, 0.6), base), 0.08, depth - 1, rng)
        ImageDraw.Draw(F).polygon(loc(ridge + list(reversed(f2)) + right[k::-1][::-1][:0] + [p_start]), fill=0)
        # region between main ridge and f2 => mid-shadow tone
        poly = ridge + [(x1 + 10, base + 10)] + list(reversed(f2))
        ImageDraw.Draw(F).polygon(loc([(px, py) for px, py in ridge] + list(reversed(f2))), fill=255)
        # also a lit sub-facet on left side (slightly darker lit tone)
        lx = cx - wl * rng.uniform(0.35, 0.6)
        ly = lerp(top, base, rng.uniform(0.3, 0.5))
        kk = min(range(len(left)), key=lambda i: abs(left[i][1] - ly))
        f3 = md_line(left[kk], (cx - wl * rng.uniform(0.0, 0.3), base), 0.08, depth - 1, rng)
        F2 = Image.new("L", (w, h), 0)
        ImageDraw.Draw(F2).polygon(loc(left[:kk + 1] + f3[1:] + [(x0 - 10, base + 10)]), fill=255)
    else:
        F2 = Image.new("L", (w, h), 0)

    # snow cap: region above a jagged snowline that is lower under the summit
    snow_frac = opts.get("snow", 0.4)
    ys0 = top + (base - top) * snow_frac * rng.uniform(0.75, 1.15)
    n = opts.get("snow_n", 26)
    sxs = np.linspace(cx - wl - 20, cx + wr + 20, n)
    width = max(wl, wr)
    sys_ = ys0 - (np.abs(sxs - cx) / width) * (ys0 - top) * opts.get("cap_slope", 0.55)
    sys_ += rng.normal(0, 1, n) * (base - top) * 0.012
    # downward tongues (snow gullies): alternate low/high points -> saw-tooth snowline
    for i in range(1, n - 1):
        if i % 2 == 0 and rng.random() < opts.get("tongues", 0.35):
            sys_[i] += rng.uniform(0.03, 0.09) * (base - top)
        elif i % 2 == 1:
            sys_[i] -= rng.uniform(0.0, 0.03) * (base - top)
    spts = []
    for i in range(n - 1):
        spts.append((sxs[i], sys_[i]))
        spts.append(((sxs[i] + sxs[i + 1]) / 2 + rng.uniform(-0.15, 0.15) * (sxs[1] - sxs[0]),
                     (sys_[i] + sys_[i + 1]) / 2 + rng.uniform(-0.01, 0.01) * (base - top)))
    spts.append((sxs[-1], sys_[-1]))
    S_ = Image.new("L", (w, h), 0)
    ImageDraw.Draw(S_).polygon(loc(spts + [(cx + wr + 40, top - 40), (cx - wl - 40, top - 40)]), fill=255)

    # thin snow streaks in couloirs below the cap
    dS = ImageDraw.Draw(S_)
    for _ in range(opts.get("streaks", 4)):
        sx0 = rng.uniform(cx - wl * 0.6, cx + wr * 0.6)
        syy = np.interp(sx0, sxs, sys_) - 4
        ln = (base - top) * rng.uniform(0.04, 0.09)
        wd = (base - top) * rng.uniform(0.03, 0.05)
        tip = (sx0 + rng.uniform(-0.25, 0.25) * ln, syy + ln)
        dS.polygon(loc([(sx0 - wd, syy), (sx0 + wd, syy), tip]), fill=255)

    # dark rock outcrops poking through the snow
    R = Image.new("L", (w, h), 0)
    dR = ImageDraw.Draw(R)
    for _ in range(opts.get("rocks", 0)):
        side = rng.choice([-1, 1])
        rx = cx + side * rng.uniform(0.1, 0.6) * (wl if side < 0 else wr)
        ry = np.interp(rx, sxs, sys_) + (base - top) * rng.uniform(0.0, 0.05)
        ln = (base - top) * rng.uniform(0.15, 0.4)
        ang = side * rng.uniform(0.35, 0.6)
        ex, ey = rx + ln * ang, ry + ln
        wd = (base - top) * rng.uniform(0.01, 0.022)
        a1 = md_line((rx - wd, ry), (ex, ey), 0.05, 3, rng)
        a2 = md_line((ex, ey), (rx + wd, ry), 0.05, 3, rng)
        dR.polygon(loc(a1 + a2[1:]), fill=255)

    # compose local colour image
    img = Image.new("RGB", (w, h), pal["rock_shadow"])
    img.paste(pal["rock_mid"], (0, 0), F)
    img.paste(pal["rock_lit"], (0, 0), L)
    img.paste(pal["rock_lit2"], (0, 0), ImageChops.multiply(F2, L))
    img.paste(pal["snow_shadow"], (0, 0), S_)
    img.paste(pal["snow_mid"], (0, 0), ImageChops.multiply(S_, F))
    img.paste(pal["snow_lit"], (0, 0), ImageChops.multiply(S_, L))
    if opts.get("rocks", 0):
        Rr = ImageChops.multiply(R, ImageChops.invert(S_))
        img.paste(pal["rock_dark"], (0, 0), ImageChops.multiply(Rr, ImageChops.invert(L)))
        img.paste(pal["rock_mid"], (0, 0), ImageChops.multiply(Rr, L))
    return img, M, x0, y0


def tint_pal(pal, haze, t):
    return {k: mix_col(v, haze, t) for k, v in pal.items()}


def make_mountains(W, H, seed, rows, pal, haze, foot, vhaze):
    """rows: list of dicts (back to front) {count, top:(a,b), slope:(a,b), haze_t, opts}"""
    rng = np.random.default_rng(seed)
    Ws, Hs = W * SS, H * SS
    canvas = Image.new("RGBA", (Ws, Hs), (0, 0, 0, 0))
    base = Hs + 20 * SS
    for row in rows:
        cnt = row["count"]
        offs = rng.uniform(0, 1)
        rpal = tint_pal(pal, haze, row["haze_t"])
        order = list(range(cnt))
        rng.shuffle(order)
        for i in order:
            cx = ((i + offs + rng.uniform(-0.3, 0.3)) / cnt) * Ws
            top = rng.uniform(*row["top"]) * Hs
            hgt = base - top
            wl = hgt * rng.uniform(*row["slope"])
            wr = hgt * rng.uniform(*row["slope"])
            ppal = tint_pal(rpal, haze, rng.uniform(0, row.get("jitter", 0.06)))
            img, M, x0, y0 = draw_peak(rng, cx, top, base, wl, wr, ppal, row["opts"])
            for dx in (-Ws, 0, Ws):
                canvas.paste(img, (int(x0 + dx), int(y0)), M)
    # foothill band in front (periodic)
    if foot:
        xs = np.arange(Ws)
        fy = (foot["y"] + foot["amp"] * periodic_noise(xs, Ws, rng, 2, 40, 1.1)) * Hs
        fy = np.round(fy).astype(int)
        a = np.array(canvas)
        rows_ = np.arange(Hs)[:, None]
        m = rows_ >= fy[None, :]
        # vertical gradient inside the foothills
        g = smoothstep(0, 1, (rows_ - fy[None, :]) / (0.25 * Hs))
        for c in range(3):
            colc = foot["c1"][c] + (foot["c2"][c] - foot["c1"][c]) * g
            a[:, :, c] = np.where(m, colc, a[:, :, c])
        a[:, :, 3] = np.where(m, 255, a[:, :, 3])
        canvas = Image.fromarray(a, "RGBA")
        if foot.get("trees"):
            d = ImageDraw.Draw(canvas)
            ntr = foot["trees"]
            for _ in range(ntr):
                x = rng.uniform(0, Ws)
                yb = fy[int(x) % Ws] + rng.uniform(-0.002, 0.03) * Hs
                h = rng.uniform(*foot["tree_h"]) * SS
                w = h * rng.uniform(0.32, 0.42)
                col = mix_col(foot["tc"], foot["c1"], rng.uniform(0, 0.35))
                for dx in (-Ws, 0, Ws):
                    xx = x + dx
                    if -w < xx < Ws + w:
                        d.polygon([(xx, yb - h), (xx - w / 2, yb), (xx + w / 2, yb)], fill=col)
                        d.polygon([(xx, yb - h), (xx - w * 0.16, yb - h * 0.62), (xx + w * 0.05, yb - h * 0.7)],
                                  fill=foot["c2"])
    small = downscale_wrap(canvas, W, H)
    # aerial haze toward the bottom
    a = np.array(small).astype(np.float64)
    yy = np.arange(H)[:, None] / H
    t = (smoothstep(vhaze[0], vhaze[1], yy) * vhaze[2])[..., None]
    a[:, :, :3] = a[:, :, :3] * (1 - t) + np.array(haze)[None, None, :] * t
    a[-3:, :, 3] = 255
    small = Image.fromarray(a.clip(0, 255).astype(np.uint8), "RGBA")
    return bleed_transparent(small)


FAR_PAL = dict(
    rock_lit=(204, 210, 236), rock_lit2=(196, 203, 232), rock_mid=(186, 192, 226),
    rock_shadow=(176, 183, 220), rock_dark=(164, 172, 212),
    snow_lit=(246, 248, 255), snow_mid=(228, 232, 248), snow_shadow=(214, 220, 242))
MID_PAL = dict(
    rock_lit=(140, 166, 206), rock_lit2=(128, 154, 198), rock_mid=(104, 128, 178),
    rock_shadow=(88, 110, 160), rock_dark=(64, 82, 130),
    snow_lit=(252, 253, 255), snow_mid=(218, 228, 246), snow_shadow=(182, 200, 234))


def make_far():
    rows = [
        dict(count=10, top=(0.08, 0.28), slope=(0.9, 1.5), haze_t=0.35,
             opts=dict(snow=0.40, tongues=0.4, streaks=2, rocks=0, rough=0.07, snow_n=30)),
        dict(count=8, top=(0.28, 0.46), slope=(1.0, 1.7), haze_t=0.12,
             opts=dict(snow=0.36, tongues=0.4, streaks=2, rocks=0, rough=0.07, snow_n=30)),
    ]
    foot = dict(y=0.78, amp=0.035, c1=(212, 220, 244), c2=(226, 232, 250))
    return make_mountains(2048, 640, 11, rows, FAR_PAL, (228, 234, 250), foot, (0.40, 1.0, 0.6))


def make_mid():
    rows = [
        dict(count=4, top=(0.05, 0.18), slope=(0.8, 1.2), haze_t=0.12,
             opts=dict(snow=0.30, tongues=0.5, streaks=4, rocks=7, rough=0.08, cap_slope=0.4)),
        dict(count=4, top=(0.30, 0.44), slope=(0.95, 1.5), haze_t=0.0,
             opts=dict(snow=0.27, tongues=0.5, streaks=3, rocks=5, rough=0.08, cap_slope=0.4)),
    ]
    foot = dict(y=0.84, amp=0.03, c1=(176, 194, 226), c2=(200, 214, 238),
                trees=900, tree_h=(8, 18), tc=(108, 136, 182))
    return make_mountains(2048, 640, 23, rows, MID_PAL, (214, 225, 244), foot, (0.55, 1.0, 0.45))


# ----------------------------------------------------------------------------- hills
def draw_pine(d, x, yb, h, col_l, col_r, snow, snow_sh, trunk, rng):
    """Stylised pine: stacked tiers, left half lit, snow on the tier tops."""
    w = h * rng.uniform(0.36, 0.46)
    tw = max(2, h * 0.06)
    d.rectangle([x - tw / 2, yb - h * 0.12, x + tw / 2, yb + h * 0.02], fill=trunk)
    tiers = 4 if h > 30 * SS else 3
    top = yb - h
    bot = yb - h * 0.08
    for t in range(tiers):
        f0 = t / tiers
        f1 = (t + 1) / tiers
        ty = lerp(top, bot, f0 * 0.85)
        by = lerp(top, bot, f1)
        hw = w * (0.35 + 0.65 * f1) / 2
        # right (shadow) side then left (lit)
        d.polygon([(x, ty), (x + hw, by), (x, by - h * 0.03)], fill=col_r)
        d.polygon([(x, ty), (x - hw, by), (x, by - h * 0.03)], fill=col_l)
        # snow tip: small cap on top of each tier, lit on left
        sh = (by - ty) * 0.42
        sw = hw * 0.42
        d.polygon([(x, ty), (x - sw, ty + sh), (x - sw * 0.3, ty + sh * 0.8),
                   (x + sw * 0.2, ty + sh * 0.95), (x + sw * 0.7, ty + sh * 0.85)], fill=snow)
        d.polygon([(x, ty), (x + sw * 0.7, ty + sh * 0.85), (x + sw * 0.2, ty + sh * 0.95),
                   (x + sw * 0.05, ty + sh * 0.3)], fill=snow_sh)
        # snow on the lower tips of the tier
        d.polygon([(x - hw, by), (x - hw * 0.62, by - (by - ty) * 0.16),
                   (x - hw * 0.45, by - h * 0.012)], fill=snow)


def hill_layer(W, H, h, c_top, c_bot, depth_px, lit_k, rim=None):
    """Analytic AA hill: h is ridge y per column (float px). Returns float RGBA."""
    yy = np.arange(H)[:, None].astype(np.float64)
    alpha = np.clip(yy - h[None, :] + 0.5, 0, 1)
    dh = (np.roll(h, -1) - np.roll(h, 1)) / 2.0  # periodic derivative
    # height rising to the right (dh<0 in image coords) => faces upper-left => lit
    lit = np.clip(-dh * lit_k, -1, 1)
    g = smoothstep(0, 1, (yy - h[None, :]) / depth_px)
    rgb = np.zeros((H, W, 3))
    for c in range(3):
        base = c_top[c] + (c_bot[c] - c_top[c]) * g
        rgb[:, :, c] = base + lit[None, :] * 10 * (1 - g * 0.5)
    if rim is not None:
        r = np.exp(-((yy - h[None, :]) / rim[1]) ** 2) * (yy >= h[None, :])
        for c in range(3):
            rgb[:, :, c] = rgb[:, :, c] * (1 - r * rim[2]) + rim[0][c] * r * rim[2]
    return np.dstack([rgb, alpha])


def over(dst, src):
    """Alpha composite float RGBA (0-255 rgb, 0-1 a) src over dst."""
    sa = src[:, :, 3:4]
    da = dst[:, :, 3:4]
    oa = sa + da * (1 - sa)
    rgb = (src[:, :, :3] * sa + dst[:, :, :3] * da * (1 - sa)) / np.maximum(oa, 1e-6)
    return np.dstack([rgb, oa[:, :, 0]])


def make_hills(W=2048, H=512, seed=5):
    rng = np.random.default_rng(seed)
    xs = np.arange(W).astype(np.float64)
    hb = (0.40 + 0.075 * periodic_noise(xs, W, rng, 2, 16, 1.25)) * H   # back hills (forest)
    hm = (0.58 + 0.06 * periodic_noise(xs, W, rng, 1, 12, 1.3)) * H     # middle hills
    hf = (0.76 + 0.05 * periodic_noise(xs, W, rng, 1, 8, 1.4)) * H      # front drifts
    Ws, Hs = W * SS, H * SS

    def tree_band(h_line, h_next, n_try, size, depth_rng, dens_k, cols, ph):
        tl = Image.new("RGBA", (Ws, Hs), (0, 0, 0, 0))
        d = ImageDraw.Draw(tl)
        trees = []
        for _ in range(n_try):
            x = rng.uniform(0, W)
            dens = 0.5 + 0.5 * math.sin(2 * math.pi * dens_k[0] * x / W + ph) *                 math.cos(2 * math.pi * dens_k[1] * x / W + ph * 0.7)
            if rng.random() > 0.15 + 0.85 * dens:
                continue
            depth = rng.uniform(*depth_rng)
            yb = np.interp(x, xs, h_line) + depth
            if yb > np.interp(x, xs, h_next) + 4:
                continue
            hgt = rng.uniform(*size) * (0.85 + depth / (depth_rng[1] * 3 + 1))
            trees.append((yb, x, hgt))
        trees.sort()
        for yb, x, hgt in trees:
            tseed = int(rng.integers(1 << 30))
            t = np.clip((yb - h_line.min()) / (0.25 * H), 0, 1)
            col_l = mix_col(cols[0], cols[1], t * 0.5)
            col_r = mix_col(cols[2], cols[3], t * 0.5)
            for dx in (-W, 0, W):
                if -80 < x + dx < W + 80:
                    draw_pine(d, (x + dx) * SS, yb * SS, hgt * SS, col_l, col_r, (240, 246, 253),
                              (208, 222, 242), (96, 108, 138), np.random.default_rng(tseed))
        ta = np.array(downscale_wrap(tl, W, H)).astype(np.float64)
        ta[:, :, 3] /= 255.0
        return ta

    out = np.zeros((H, W, 4))
    out = over(out, hill_layer(W, H, hb, (222, 232, 248), (192, 208, 234), 0.30 * H, 3.0,
                               rim=((244, 248, 255), 5.0, 0.6)))
    out = over(out, tree_band(hb, hm, 900, (22, 40), (-3, 60), (3, 5),
                              ((132, 164, 198), (120, 154, 192), (100, 136, 178), (90, 126, 170)), 1.3))
    out = over(out, hill_layer(W, H, hm, (236, 243, 252), (206, 219, 240), 0.30 * H, 3.5,
                               rim=((252, 254, 255), 5.0, 0.7)))
    out = over(out, tree_band(hm, hf, 260, (34, 62), (-2, 40), (2, 7),
                              ((112, 148, 188), (104, 140, 182), (82, 120, 166), (74, 112, 158)), 4.1))
    out = over(out, hill_layer(W, H, hf, (248, 251, 255), (220, 230, 246), 0.30 * H, 4.0,
                               rim=((255, 255, 255), 4.0, 0.8)))
    out[-2:, :, 3] = 1.0
    res = np.dstack([out[:, :, :3].clip(0, 255), out[:, :, 3:4] * 255]).astype(np.uint8)
    return bleed_transparent(Image.fromarray(res, "RGBA"))


# ----------------------------------------------------------------------------- clouds
def make_cloud(seed, W=768, H=320, kind="puffy"):
    """Cartoon cloud: individually shaded overlapping puffs (lit from upper-left),
    flat rounded base with soft blue-grey underside."""
    rng = np.random.default_rng(seed)
    Ws, Hs = W * SS, H * SS
    yb = 0.86 * Hs
    if kind == "puffy":
        pattern, rmax = [0.42, 0.66, 1.0, 0.78, 0.6, 0.4], 0.40 * Hs
    elif kind == "tall":
        pattern, rmax = [0.45, 0.8, 1.0, 0.55], 0.48 * Hs
    else:
        pattern, rmax = [0.4, 0.62, 0.5, 0.9, 0.7, 1.0, 0.55, 0.42], 0.30 * Hs
    radii = [p * rmax * rng.uniform(0.9, 1.1) for p in pattern]
    gaps = [(radii[k] + radii[k + 1]) * rng.uniform(0.62, 0.78) for k in range(len(radii) - 1)]
    total = sum(gaps) + radii[0] + radii[-1]
    sc = min(1.0, 0.92 * Ws / total)
    radii = [r * sc for r in radii]
    gaps = [g * sc for g in gaps]
    total *= sc
    x = (Ws - total) / 2 + radii[0]
    main = []
    for k, r in enumerate(radii):
        drop = 1.0 if k in (0, len(radii) - 1) else rng.uniform(0.55, 0.8)
        main.append((x, yb - r * drop, r))
        if k < len(gaps):
            x += gaps[k]
    bumps = []
    for _ in range(len(radii) // 2 + 1):
        cx, cy, cr = main[int(rng.integers(1, len(main) - 1))]
        r = cr * rng.uniform(0.5, 0.72)
        y = max(cy - cr * rng.uniform(0.35, 0.6), 0.03 * Hs + r)
        bumps.append((cx + rng.uniform(-0.5, 0.5) * cr, y, r))
    circles = bumps + main  # back (top bumps) first, front row last

    c_lit = np.array([255, 255, 255.0])
    c_mid = np.array([238, 243, 251.0])
    c_sh = np.array([214, 224, 241.0])
    rgb = np.empty((Hs, Ws, 3))
    rgb[:] = (226, 234, 247)  # edge colour for the soft alpha fringe (no dark outline)
    cov = np.zeros((Hs, Ws))
    for (cx, cy, r) in circles:
        x0, x1 = int(max(0, cx - r - 2)), int(min(Ws, cx + r + 2))
        y0, y1 = int(max(0, cy - r - 2)), int(min(Hs, cy + r + 2))
        yy, xx = np.mgrid[y0:y1, x0:x1].astype(np.float64)
        d = np.hypot(xx - cx, yy - cy)
        m = np.clip(r - d + 0.5, 0, 1) * (yy <= yb)
        # light centre offset toward upper-left
        dl = np.hypot(xx - (cx - 0.35 * r), yy - (cy - 0.45 * r)) / (1.45 * r)
        t = smoothstep(0.45, 1.12, dl)[..., None]
        col = np.where(t < 0.5, c_lit + (c_mid - c_lit) * t * 2, c_mid + (c_sh - c_mid) * (t - 0.5) * 2)
        sub = rgb[y0:y1, x0:x1]
        rgb[y0:y1, x0:x1] = sub * (1 - m[..., None]) + col * m[..., None]
        cov[y0:y1, x0:x1] = np.maximum(cov[y0:y1, x0:x1], m)
    # fill gaps at the flat base
    bx0, bx1 = main[0][0], main[-1][0]
    base = Image.new("L", (Ws, Hs), 0)
    eh = 0.05 * Hs
    ImageDraw.Draw(base).rounded_rectangle([bx0, yb - 2.2 * eh, bx1, yb], radius=eh, fill=255)
    bm = np.array(base).astype(np.float64) / 255 * (1 - cov)
    rgb = rgb * (1 - bm[..., None]) + c_sh * bm[..., None]
    cov = np.maximum(cov, np.array(base).astype(np.float64) / 255)
    # underside tint
    yyf = np.arange(Hs)[:, None] / Hs
    under = smoothstep(yb / Hs - 0.18, yb / Hs, yyf)[..., None] * 0.45
    rgb = rgb * (1 - under) + np.array([196, 210, 232.0]) * under
    alpha = np.array(Image.fromarray((cov * 255).astype(np.uint8)).filter(
        ImageFilter.GaussianBlur(1.2 * SS))).astype(np.float64)
    img = np.dstack([rgb, alpha]).clip(0, 255).astype(np.uint8)
    small = downscale_wrap(Image.fromarray(img, "RGBA"), W, H, wrap=False)
    s = np.array(small)
    s[s[:, :, 3] < 3, 0:3] = (230, 238, 248)
    return Image.fromarray(s, "RGBA")


# ----------------------------------------------------------------------------- compositing
def to_f(img):
    a = np.array(img.convert("RGBA")).astype(np.float64)
    a[:, :, 3] /= 255
    return a


def paste_over(dst, src_img, x, y, mul=(1, 1, 1), alpha_mul=1.0):
    """dst float RGBA HxWx4; src PIL; composites with clipping."""
    s = to_f(src_img)
    s[:, :, :3] *= np.array(mul)[None, None, :]
    s[:, :, 3] *= alpha_mul
    H, W = dst.shape[:2]
    h, w = s.shape[:2]
    x0, y0 = max(0, x), max(0, y)
    x1, y1 = min(W, x + w), min(H, y + h)
    if x1 <= x0 or y1 <= y0:
        return dst
    sub = s[y0 - y:y1 - y, x0 - x:x1 - x]
    dst[y0:y1, x0:x1] = over(dst[y0:y1, x0:x1], sub)
    return dst


def tile_h(img, total_w, offset):
    """Tile image horizontally starting at -offset to cover total_w."""
    W, H = img.size
    out = Image.new("RGBA", (total_w, H), (0, 0, 0, 0))
    x = -offset
    while x < total_w:
        out.paste(img, (x, 0))
        x += W
    return out


def finish(dst):
    return Image.fromarray(np.dstack([dst[:, :, :3], dst[:, :, 3:4] * 255]).clip(0, 255).astype(np.uint8), "RGBA")


def preview_scene(layers, sky, mul, path):
    """Parallax test: layers tiled so a seam sits at screen centre (x=960).
    Each opaque layer is extended downward by repeating its last row (as in-game it
    sits low enough / is overlapped by the next layer)."""
    Wp, Hp = 1920, 1080
    dst = to_f(sky.resize((Wp, Hp), Image.BICUBIC))
    dst = paste_over(dst, layers["Sun"].resize((420, 420), Image.LANCZOS), 1360, 30, (1, 1, 1))
    for name, x, y, s in (("Clouds_A", 120, 70, 0.6), ("Clouds_B", 1000, 170, 0.5),
                          ("Clouds_C", 560, 20, 0.5)):
        c = layers[name]
        c = c.resize((int(c.width * s), int(c.height * s)), Image.LANCZOS)
        dst = paste_over(dst, c, x, y, mul)
    for name, top, sc in (("MountainsFar", 230, 0.6), ("MountainsMid", 360, 0.6), ("Hills", 640, 0.75)):
        L = layers[name]
        Ls = L.resize((int(L.width * sc), int(L.height * sc)), Image.LANCZOS)
        off = Ls.width - (960 % Ls.width)
        t = np.array(tile_h(Ls, Wp, off))
        ext = max(0, Hp - top - t.shape[0])
        if ext:
            t = np.concatenate([t, np.repeat(t[-1:], ext, axis=0)], axis=0)
        dst = paste_over(dst, Image.fromarray(t, "RGBA"), 0, top, mul)
    finish(dst).convert("RGB").save(path)
    print("preview", path)


def seam_sheet(layers, path):
    """1:1 crops across the wrap seam (right edge followed by left edge) for each layer."""
    rows = []
    for name in ("MountainsFar", "MountainsMid", "Hills"):
        a = np.array(layers[name])
        strip = np.concatenate([a[:, -300:], a[:, :300]], axis=1)
        bg = np.zeros_like(strip)
        bg[:] = (120, 170, 230, 255)
        im = Image.fromarray(bg, "RGBA")
        im.alpha_composite(Image.fromarray(strip, "RGBA"))
        rows.append(im)
    H = sum(r.height for r in rows)
    sheet = Image.new("RGB", (600, H))
    y = 0
    for r in rows:
        sheet.paste(r.convert("RGB"), (0, y))
        y += r.height
    sheet.save(path)


def make_menu(layers, path_out, preview_path=None):
    Wp, Hp = 1920, 1080
    rng = np.random.default_rng(77)
    dst = to_f(layers["Sky_Day"].resize((Wp, Hp), Image.BICUBIC))
    dst = paste_over(dst, layers["Sun"].resize((600, 600), Image.LANCZOS), 1250, -90)
    for name, x, y, s, am in (("Clouds_A", 40, 110, 0.8, 0.95), ("Clouds_C", 660, 30, 0.65, 0.85),
                              ("Clouds_B", 1100, 230, 0.55, 0.9), ("Clouds_C", 1560, 320, 0.4, 0.75),
                              ("Clouds_B", 420, 300, 0.32, 0.7)):
        c = layers[name]
        c = c.resize((int(c.width * s), int(c.height * s)), Image.LANCZOS)
        dst = paste_over(dst, c, x, y, (1, 1, 1), am)
    horizon = np.array([236, 244, 252.0])

    def aerial(img, t, sc=1.0):
        if sc != 1.0:
            img = img.resize((int(img.width * sc), int(img.height * sc)), Image.LANCZOS)
        a = to_f(img)
        a[:, :, :3] = a[:, :, :3] * (1 - t) + horizon * t
        return finish(a)

    dst = paste_over(dst, tile_h(aerial(layers["MountainsFar"], 0.22, 0.85), Wp, 300), 0, 340)
    dst = paste_over(dst, tile_h(aerial(layers["MountainsMid"], 0.06, 0.95), Wp, 900), 0, 450)
    dst = paste_over(dst, tile_h(layers["Hills"], Wp, 500), 0, 640)

    # big foreground slope on the right half: rises from lower-middle to a rounded crest,
    # then sweeps down toward the lower right.
    yy, xx = np.mgrid[0:Hp, 0:Wp].astype(np.float64)
    cx = np.array([0.24, 0.32, 0.40, 0.47, 0.53, 0.59, 0.66, 0.76, 0.87, 1.0, 1.1])
    cy = np.array([1.14, 0.98, 0.82, 0.68, 0.595, 0.565, 0.575, 0.63, 0.70, 0.765, 0.80])
    xn = np.arange(Wp) / Wp
    h = np.interp(xn, cx, cy)
    k = np.exp(-np.linspace(-3, 3, 121) ** 2)
    k /= k.sum()
    h = np.convolve(np.pad(h, 60, mode="edge"), k, mode="valid") * Hp
    a = np.clip(yy - h[None, :] + 0.5, 0, 1)
    dh = np.gradient(h)
    # sun is upper-right: faces descending to the right are lit, left shoulder in shadow
    lit = (0.25 + 0.75 * smoothstep(-2.5, 0.8, dh))[None, :]
    depth = np.clip((yy - h[None, :]) / (0.5 * Hp), 0, 1)
    top_col = np.array([252, 253, 255.0])
    low_col = np.array([220, 232, 248.0])
    sh_top = np.array([196, 214, 242.0])
    sh_low = np.array([170, 192, 230.0])
    lit_rgb = top_col + (low_col - top_col) * depth[..., None]
    sh_rgb = sh_top + (sh_low - sh_top) * depth[..., None]
    rgb = sh_rgb + (lit_rgb - sh_rgb) * lit[..., None]
    # wind ripples / carved tracks following the slope
    for oy, wdt, st, amp in ((0.05, 2.5, 0.20, 0.25), (0.11, 2.0, 0.18, 0.2), (0.19, 3.0, 0.15, 0.18)):
        ty = h[None, :] + (oy + 0.025 * np.sin(xn * 8 + oy * 40))[None, :] * Hp
        g = np.exp(-((yy - ty) / wdt) ** 2) * smoothstep(0.55, 0.7, xx / Wp) * amp
        rgb = rgb * (1 - g[..., None]) + np.array([176, 198, 234.0]) * g[..., None]
    rim = np.exp(-((yy - h[None, :]) / 3.5) ** 2) * (yy >= h[None, :] - 1)
    rgb = rgb * (1 - rim[..., None] * 0.6) + 255 * rim[..., None] * 0.6
    # soft shadow the slope casts onto the hills on its left
    shadow = np.zeros((Hp, Wp))
    for sx in range(1, 40, 3):
        shadow = np.maximum(shadow, np.roll(a, -sx, axis=1) * (1 - sx / 40))
    shadow *= (1 - a)
    dst[:, :, :3] *= (1 - 0.12 * shadow[..., None])
    # subtle contour lines on the shaded shoulder
    for oy in (0.03, 0.07, 0.12):
        ty = h[None, :] + oy * Hp
        g = np.exp(-((yy - ty) / 2.2) ** 2) * (1 - lit) * 0.18 * (xx / Wp < 0.6)
        rgb = rgb * (1 - g[..., None]) + np.array([160, 184, 224.0]) * g[..., None]
    dst = over(dst, np.dstack([rgb, a]))
    xs, ys = xn, h / Hp
    Ws, Hs = Wp * SS, Hp * SS

    # pines on the slope
    tl = Image.new("RGBA", (Ws, Hs), (0, 0, 0, 0))
    td = ImageDraw.Draw(tl)
    pines = []
    for _ in range(22):
        x = rng.uniform(0.66, 1.0)
        y = np.interp(x, xs, ys) + rng.uniform(0.005, 0.04)
        pines.append((y, x, rng.uniform(50, 110) * (0.7 + (x - 0.6))))
    pines += [(1.0, 0.955, 360), (1.03, 0.90, 260), (1.04, 1.0, 230)]
    for x, hh in ((0.505, 70), (0.515, 92), (0.528, 64), (0.47, 58)):
        pines.append((np.interp(x, xs, ys) + 0.012, x, hh))
    pines.sort()
    for y, x, h in pines:
        draw_pine(td, x * Ws, y * Hs, h * SS, (96, 140, 184), (70, 110, 156), (244, 248, 255),
                  (206, 222, 244), (80, 90, 118), rng)
    ta = to_f(tl.convert("RGBa").resize((Wp, Hp), Image.LANCZOS).convert("RGBA"))
    dst = over(dst, ta)

    # falling snow
    sn = Image.new("RGBA", (Wp, Hp), (255, 255, 255, 0))
    sd = ImageDraw.Draw(sn)
    for _ in range(420):
        x, y = rng.uniform(0, Wp), rng.uniform(0, Hp)
        r = rng.choice([1.0, 1.4, 1.8, 2.4, 3.2], p=[0.35, 0.3, 0.18, 0.12, 0.05])
        al = int(rng.uniform(120, 230))
        sd.ellipse([x - r, y - r, x + r, y + r], fill=(255, 255, 255, al))
    sn = sn.filter(ImageFilter.GaussianBlur(0.6))
    big = Image.new("RGBA", (Wp, Hp), (255, 255, 255, 0))
    bd = ImageDraw.Draw(big)
    for _ in range(26):
        x, y = rng.uniform(0, Wp), rng.uniform(0, Hp)
        r = rng.uniform(4, 8)
        bd.ellipse([x - r, y - r, x + r, y + r], fill=(255, 255, 255, int(rng.uniform(90, 160))))
    big = big.filter(ImageFilter.GaussianBlur(2.2))
    dst = over(dst, to_f(sn))
    dst = over(dst, to_f(big))
    # gentle vignette
    v = 1 - 0.10 * smoothstep(0.55, 1.1, np.hypot((xx - Wp / 2) / (Wp / 2), (yy - Hp / 2) / (Hp / 2)))
    dst[:, :, :3] *= v[..., None]
    dst[:, :, 3] = 1
    out = finish(dst)
    save(out, "MenuBackdrop.png")
    if preview_path:
        out.convert("RGB").resize((1280, 720), Image.LANCZOS).save(preview_path)


def seam_check(img):
    a = np.array(img).astype(np.float64)
    a[:, :, :3] *= a[:, :, 3:4] / 255.0  # premultiplied: ignore colour of invisible pixels
    seam = np.abs(a[:, 0] - a[:, -1]).mean()
    inner = np.abs(a[:, 1:] - a[:, :-1]).mean()
    return seam, inner


def main():
    prev = sys.argv[1] if len(sys.argv) > 1 else None
    os.makedirs(OUT, exist_ok=True)
    L = {}
    for n, s in (("Day", 1), ("Dusk", 2), ("Storm", 3)):
        L["Sky_" + n] = make_sky(n, seed=s)
        save(L["Sky_" + n], "Sky_%s.png" % n)
    L["Sun"] = make_sun()
    save(L["Sun"], "Sun.png")
    for n, s, k in (("A", 3, "puffy"), ("B", 8, "tall"), ("C", 14, "long")):
        L["Clouds_" + n] = make_cloud(s, kind=k)
        save(L["Clouds_" + n], "Clouds_%s.png" % n)
    L["MountainsFar"] = make_far()
    save(L["MountainsFar"], "MountainsFar.png")
    L["MountainsMid"] = make_mid()
    save(L["MountainsMid"], "MountainsMid.png")
    L["Hills"] = make_hills()
    save(L["Hills"], "Hills.png")
    for n in ("MountainsFar", "MountainsMid", "Hills"):
        print("seam", n, "edge diff %.2f vs mean adjacent diff %.2f" % seam_check(L[n]))
    if prev:
        os.makedirs(prev, exist_ok=True)
        preview_scene(L, L["Sky_Day"], (1, 1, 1), os.path.join(prev, "bg_preview_day.png"))
        preview_scene(L, L["Sky_Dusk"], (1, 0.93, 0.92), os.path.join(prev, "bg_preview_dusk.png"))
        preview_scene(L, L["Sky_Storm"], (0.75, 0.8, 0.9), os.path.join(prev, "bg_preview_storm.png"))
        seam_sheet(L, os.path.join(prev, "bg_preview_seams.png"))
        make_menu(L, None, os.path.join(prev, "bg_preview_menu.png"))
    else:
        make_menu(L, None)


if __name__ == "__main__":
    main()
