#!/usr/bin/env python3
"""openmod UI mockups — palette-driven (themes + accent tokens).

    python3 ui/mockup.py           # writes all SVGs
    rsvg-convert -z 2 ui/mockup-main-dark.svg -o ui/mockup-main-dark.png   # etc.

Outputs:
  ui/mockup-main-{dark,light,amoled}.svg
  ui/mockup-settings.svg          (theme + accent picker page)
  ui/mockup-themes.svg            (all palettes on one sheet)
"""

import math

W, H = 1280, 800
SANS = "Cantarell, DejaVu Sans, sans-serif"
MONO = "DejaVu Sans Mono, monospace"

# --- palettes ---------------------------------------------------------------
# Every screen is drawn from these tokens only — no per-screen overrides.
THEMES = {
    "dark": dict(
        label="Dark", light=False, glow=True,
        bg="#23272F", panel="#1B1F26", card="#2C323C", deep="#262B34",
        track="#3A4250", border="#333B47",
        text="#ECEFF4", soft="#AEB8C6", muted="#6E7A8A", faint="#4E5764",
        on_accent="#2A0E1A", knob="#FFFFFF",
    ),
    "light": dict(
        label="Light", light=True, glow=False,
        bg="#F2F5F9", panel="#FFFFFF", card="#FFFFFF", deep="#EDF1F6",
        track="#DCE3EC", border="#E1E7EF",
        text="#161B22", soft="#4A5563", muted="#7B8794", faint="#A7B1BD",
        on_accent="#2A0E1A", knob="#FFFFFF",
    ),
    "amoled": dict(
        label="AMOLED", light=False, glow=True,
        bg="#000000", panel="#000000", card="#0C0C0C", deep="#141414",
        track="#262626", border="#1E1E1E",
        text="#FFFFFF", soft="#B4B4B4", muted="#6E6E6E", faint="#3D3D3D",
        on_accent="#12060C", knob="#FFFFFF",
    ),
}

# accent = (bright, deep) — one token pair drives the whole UI
ACCENTS = {
    "blush":  ("#FF99CC", "#E91E63"),
    "mint":   ("#7FE0C3", "#0FA97F"),
    "sky":    ("#8CC8FF", "#2E7DD1"),
    "violet": ("#C3A6FF", "#7C4DFF"),
    "amber":  ("#FFCF7A", "#E08C00"),
    "coral":  ("#FFA08C", "#E05A3F"),
}


class Canvas:
    def __init__(self, pal, accent=ACCENTS["blush"], uid=0, w=W, h=H):
        self.p, self.a0, self.a1 = pal, accent[0], accent[1]
        self.uid, self.w, self.h = f"u{uid}-", w, h
        self.out = []
        self.extra_defs = ""

    # --- ids ---
    def grad(self, name="accent"):
        return f"url(#{self.uid}{name})"

    def filt(self, name="glow"):
        return f"url(#{self.uid}{name})"

    @property
    def ink(self):
        """accent color safe for text on the current background"""
        return self.a1 if self.p["light"] else self.a0

    # --- primitives ---
    def rect(self, x, y, w, h, rx=0, fill="none", stroke="none", sw=1,
             opacity=None, fo=None, so=None, dash=None, use_filter=None):
        a = f'x="{x}" y="{y}" width="{w}" height="{h}" rx="{rx}" fill="{fill}"'
        if stroke != "none":
            a += f' stroke="{stroke}" stroke-width="{sw}"'
        if opacity is not None:
            a += f' opacity="{opacity}"'
        if fo is not None:
            a += f' fill-opacity="{fo}"'
        if so is not None:
            a += f' stroke-opacity="{so}"'
        if dash:
            a += f' stroke-dasharray="{dash}"'
        if use_filter:
            a += f' filter="{use_filter}"'
        self.out.append(f"<rect {a}/>")

    def circle(self, cx, cy, r, fill="none", stroke="none", sw=1, opacity=None, fo=None, dash=None):
        a = f'cx="{cx}" cy="{cy}" r="{r}" fill="{fill}"'
        if stroke != "none":
            a += f' stroke="{stroke}" stroke-width="{sw}"'
        if opacity is not None:
            a += f' opacity="{opacity}"'
        if fo is not None:
            a += f' fill-opacity="{fo}"'
        if dash:
            a += f' stroke-dasharray="{dash}"'
        self.out.append(f"<circle {a}/>")

    def line(self, x1, y1, x2, y2, stroke, sw=1, opacity=None, dash=None):
        a = f'x1="{x1}" y1="{y1}" x2="{x2}" y2="{y2}" stroke="{stroke}" stroke-width="{sw}"'
        if opacity is not None:
            a += f' opacity="{opacity}"'
        if dash:
            a += f' stroke-dasharray="{dash}"'
        self.out.append(f"<line {a}/>")

    def path(self, d, stroke="none", sw=1.5, fill="none", opacity=None):
        a = f'd="{d}" fill="{fill}" stroke="{stroke}" stroke-width="{sw}" stroke-linecap="round" stroke-linejoin="round"'
        if opacity is not None:
            a += f' opacity="{opacity}"'
        self.out.append(f"<path {a}/>")

    def text(self, x, y, s, size=12, fill=None, weight=400, family=SANS,
             anchor="start", ls=None, opacity=None):
        fill = self.p["text"] if fill is None else fill
        a = (f'x="{x}" y="{y}" fill="{fill}" font-size="{size}" '
             f'font-family="{family}" font-weight="{weight}" text-anchor="{anchor}"')
        if ls is not None:
            a += f' letter-spacing="{ls}"'
        if opacity is not None:
            a += f' opacity="{opacity}"'
        self.out.append(f"<text {a}>{s}</text>")

    def pill(self, x, y, w, h, fill, stroke="none", sw=1, fo=None, so=None, rx=None):
        self.rect(x, y, w, h, rx=rx if rx is not None else h / 2, fill=fill,
                  stroke=stroke, sw=sw, fo=fo, so=so)

    def wordmark(self, x, y, size=17):
        self.out.append(
            f'<text x="{x}" y="{y}" font-size="{size}" font-family="{SANS}" font-weight="600">'
            f'<tspan fill="{self.p["text"]}">open</tspan>'
            f'<tspan fill="{self.ink}">mod</tspan></text>')

    # --- output ---
    def defs(self):
        p, glow = self.p, ""
        if p["glow"]:
            glow = f'''<filter id="{self.uid}glow" x="-40%" y="-40%" width="180%" height="180%">
      <feGaussianBlur stdDeviation="7" result="b"/>
      <feMerge><feMergeNode in="b"/><feMergeNode in="SourceGraphic"/></feMerge>
    </filter>'''
        return f'''<defs>
    <linearGradient id="{self.uid}accent" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0" stop-color="{self.a0}"/><stop offset="1" stop-color="{self.a1}"/>
    </linearGradient>
    <linearGradient id="{self.uid}meter" x1="0" y1="0" x2="1" y2="0">
      <stop offset="0" stop-color="{self.a0}"/><stop offset="1" stop-color="{self.a1}"/>
    </linearGradient>
    {glow}
    <clipPath id="{self.uid}win"><rect x="0" y="0" width="{self.w}" height="{self.h}" rx="12"/></clipPath>
    {self.extra_defs}
  </defs>'''

    def content(self):
        return "\n".join(self.out)

    def svg(self):
        return (f'<svg xmlns="http://www.w3.org/2000/svg" width="{self.w}" height="{self.h}" '
                f'viewBox="0 0 {self.w} {self.h}">\n{self.defs()}\n'
                f'<g clip-path="url(#{self.uid}win)">\n{self.content()}\n</g>\n</svg>\n')


# --- icons ------------------------------------------------------------------
def i_wave(c, x, y, s, color=None, sw=2):
    color = color or c.ink
    for i, hh in enumerate((0.45, 1.0, 0.65)):
        bx = x + i * (s / 3) + s / 6
        c.line(bx, y + s / 2 - s * hh / 2, bx, y + s / 2 + s * hh / 2, color, sw)


def i_grid(c, x, y, s, color):
    g = s * 0.38
    for dx, dy in ((0, 0), (s - g, 0), (0, s - g), (s - g, s - g)):
        c.rect(x + dx, y + dy, g, g, rx=1.5, stroke=color, sw=1.4)


def i_soundboard(c, x, y, s, color):
    for i, ww in enumerate((1.0, 0.62, 0.85)):
        yy = y + i * (s / 2.6)
        c.line(x, yy + s / 8, x + s * ww, yy + s / 8, color, 1.8)


def i_gear(c, x, y, s, color):
    cx, cy, r = x + s / 2, y + s / 2, s * 0.26
    c.circle(cx, cy, r, stroke=color, sw=1.4)
    c.circle(cx, cy, r * 0.34, stroke=color, sw=1.4)
    for i in range(8):
        a = i * math.pi / 4
        c.line(cx + math.cos(a) * (r + 1.5), cy + math.sin(a) * (r + 1.5),
               cx + math.cos(a) * (r + 4), cy + math.sin(a) * (r + 4), color, 1.4)


def i_theme(c, x, y, s, color):
    """half-filled circle — appearance / light-dark toggle"""
    cx, cy, r = x + s / 2, y + s / 2, s * 0.42
    c.circle(cx, cy, r, stroke=color, sw=1.4)
    c.path(f"M {cx} {cy - r} a {r} {r} 0 0 1 0 {2 * r} z", fill=color, stroke=color, sw=1.2)


def i_mic(c, x, y, s, color):
    w = s * 0.34
    c.rect(x + (s - w) / 2, y, w, s * 0.52, rx=w / 2, stroke=color, sw=1.4)
    c.path(f"M {x + s*0.16:.2f} {y + s*0.46:.2f} "
           f"Q {x + s*0.5:.2f} {y + s*0.86:.2f} {x + s*0.84:.2f} {y + s*0.46:.2f}",
           stroke=color, sw=1.4)
    c.line(x + s / 2, y + s * 0.78, x + s / 2, y + s * 0.94, color, 1.4)


def i_headphones(c, x, y, s, color):
    c.path(f"M {x + s*0.12} {y + s*0.72} v -{s*0.22} a {s*0.38} {s*0.38} 0 0 1 {s*0.76} 0 v {s*0.22}",
           stroke=color, sw=1.4)
    c.rect(x + s * 0.08, y + s * 0.6, s * 0.18, s * 0.34, rx=2, stroke=color, sw=1.3)
    c.rect(x + s * 0.74, y + s * 0.6, s * 0.18, s * 0.34, rx=2, stroke=color, sw=1.3)


def i_search(c, cx, cy, r, color):
    c.circle(cx, cy, r, stroke=color, sw=1.4)
    c.line(cx + r * 0.7, cy + r * 0.7, cx + r * 1.6, cy + r * 1.6, color, 1.4)


def i_plus(c, cx, cy, s, color, sw=1.8):
    c.line(cx - s / 2, cy, cx + s / 2, cy, color, sw)
    c.line(cx, cy - s / 2, cx, cy + s / 2, color, sw)


def i_close(c, cx, cy, s, color):
    c.line(cx - s / 2, cy - s / 2, cx + s / 2, cy + s / 2, color, 1.4)
    c.line(cx + s / 2, cy - s / 2, cx - s / 2, cy + s / 2, color, 1.4)


def i_chevron(c, x, y, color, down=True):
    if down:
        c.path(f"M {x} {y} l 4 4 l 4 -4", stroke=color, sw=1.5)
    else:
        c.path(f"M {x} {y + 4} l 4 -4 l 4 4", stroke=color, sw=1.5)


def drag_dots(c, x, y, color):
    for r in range(3):
        for col in range(2):
            c.circle(x + col * 4, y + r * 5, 1.1, fill=color)


# --- shared chrome ----------------------------------------------------------
def top_bar(c):
    p = c.p
    c.rect(0, 0, W, 56, fill=p["panel"])
    c.line(0, 56, W, 56, p["border"])
    c.rect(20, 14, 28, 28, rx=8, fill=c.grad())
    for i, hh in enumerate((8, 14, 10)):
        c.rect(27 + i * 5.5, 28 - hh / 2, 2.6, hh, rx=1.3, fill=p["on_accent"], opacity=0.85)
    c.wordmark(58, 35, 17)

    # voice toggle
    c.rect(536, 11, 208, 34, rx=17, fill=c.a0, fo=0.10, stroke=c.ink, so=0.35)
    c.rect(542, 14, 196, 28, rx=14, fill=c.grad(),
           use_filter=c.filt() if p["glow"] else None, opacity=0.95)
    c.circle(578, 28, 4, fill=p["on_accent"])
    c.text(592, 32, "VOICE ON", 11.5, p["on_accent"], 700, ls=1.3)

    # latency chip + theme + settings
    c.pill(1010, 14, 88, 28, p["card"], p["border"])
    c.text(1054, 32.5, "34.8 ms", 10.5, p["soft"], 400, MONO, "middle")
    i_theme(c, 1126, 20, 16, p["muted"])
    i_gear(c, 1176, 20, 16, p["muted"])
    c.circle(1236, 28, 12, stroke=p["border"], sw=1)
    i_grid(c, 1230, 22, 12, p["muted"])


def sidebar(c, active="Voices"):
    p = c.p
    c.rect(0, 56, 208, H - 56, fill=p["panel"])
    c.line(208, 56, 208, H, p["border"])

    c.text(20, 88, "WORKSPACE", 9, p["muted"], 600, ls=1.4)
    nav = [("Voices", i_grid), ("Soundboard", i_soundboard), ("Settings", i_gear)]
    ny = 100
    for label, ic in nav:
        on = label == active
        if on:
            c.rect(12, ny, 184, 38, rx=9, fill=c.a0, fo=0.12 if p["light"] else 0.10)
            c.rect(12, ny + 6, 3, 26, rx=1.5, fill=c.ink)
        ic(c, 28, ny + 11, 16, c.ink if on else p["soft"])
        c.text(54, ny + 24, label, 12.5, p["text"] if on else p["soft"],
               600 if on else 500)
        ny += 46

    c.line(20, 240, 188, 240, p["border"])
    c.text(20, 266, "EFFECTS", 9, p["muted"], 600, ls=1.4)
    for i, (name, used) in enumerate((("pitch_shift", True), ("gain", True),
                                      ("formant", False), ("reverb", False),
                                      ("distortion", False))):
        fy = 288 + i * 20
        c.circle(24, fy - 3.5, 2.6, fill=c.ink if used else p["track"])
        c.text(36, fy, name, 10.5, p["soft"] if used else p["muted"], 400, MONO)

    c.line(20, 396, 188, 396, p["border"])
    c.text(20, 422, "FOLDERS", 9, p["muted"], 600, ls=1.4)
    for i, (name, info) in enumerate((("effects/", "5 files"),
                                      ("soundboards/", "0 clips"),
                                      ("themes/", "soon"))):
        yy = 444 + i * 20
        c.text(24, yy, name, 10.5, p["soft"], 400, MONO)
        c.text(188, yy, info, 9.5, p["muted"], 400, MONO, "end")
    c.line(20, 500, 188, 500, p["border"])

    # virtual mic card
    c.rect(12, 586, 184, 86, rx=10, fill=p["card"], stroke=p["border"])
    c.text(24, 606, "VIRTUAL MIC", 8.5, p["muted"], 600, ls=1.2)
    c.circle(28, 624, 3.4, fill=c.ink)
    c.text(38, 628, "openmod_vmic", 10.5, p["text"], 400, MONO)
    c.text(24, 646, "pick as mic in games", 9, p["muted"])
    c.pill(146, 596, 38, 16, c.a0, c.ink, fo=0.14, so=0.4)
    c.text(165, 607.5, "LIVE", 8, c.ink, 700, SANS, "middle", ls=0.8)

    c.text(24, 702, "Velinixx/openmod", 9.5, p["muted"], 400, MONO)
    c.text(24, 720, "MIT · v0.1.0", 9, p["faint"], 400, MONO)


def bottom_bar(c):
    p = c.p
    c.rect(0, 736, W, 64, fill=p["panel"])
    c.line(0, 736, W, 736, p["border"])

    i_mic(c, 26, 754, 16, p["soft"])
    c.text(52, 756, "INPUT", 8.5, p["muted"], 600, ls=1.2)
    c.text(52, 774, "default · 2ch 44.1kHz", 10.5, p["text"])

    c.rect(260, 748, 232, 40, rx=10, fill=c.a0, fo=0.10, stroke=c.ink, so=0.35)
    i_wave(c, 276, 760, 16, c.ink)
    c.text(300, 758, "OUTPUT", 8.5, c.ink, 600, ls=1.2)
    c.text(300, 775, "openmod_vmic", 10.5, p["text"], 400, MONO)

    for i, (lbl, pct) in enumerate((("IN", 0.62), ("OUT", 0.48))):
        yy = 752 + i * 18
        c.text(540, yy + 4, lbl, 8.5, p["muted"], 600, SANS, "end")
        c.rect(550, yy, 120, 5, rx=2.5, fill=p["track"])
        c.rect(550, yy, 120 * pct, 5, rx=2.5, fill=c.grad("meter"))

    c.text(712, 774, "VOL", 8.5, p["muted"], 600, SANS, "end")
    c.rect(722, 766, 120, 5, rx=2.5, fill=p["track"])
    c.rect(722, 766, 120 * 0.75, 5, rx=2.5, fill=p["soft"])
    c.circle(722 + 120 * 0.75, 768.5, 6, fill=p["knob"], stroke=p["track"], sw=1.2)

    c.rect(862, 748, 124, 40, rx=10, fill=p["card"], stroke=p["border"])
    i_headphones(c, 876, 758, 16, p["soft"])
    c.text(902, 758, "MONITOR", 8.5, p["muted"], 600, ls=1.1)
    c.text(902, 775, "off", 10.5, p["soft"], 400, MONO)

    c.rect(1152, 748, 104, 40, rx=10, fill=c.a1)
    c.rect(1170, 762, 12, 12, rx=2, fill="#FFFFFF", opacity=0.95)
    c.text(1190, 772, "Stop", 11.5, "#FFFFFF", 700)


# --- main window ------------------------------------------------------------
CARDS = [
    ("chipmunk",    ["pitch +7.0", "gain 0.0"], "34.8 ms", True,  i_wave),
    ("deep-voice",  ["pitch -5.0", "gain 0.0"], "33.6 ms", False, i_wave),
    ("passthrough", ["no effects"],             "0.0 ms",  False, i_soundboard),
    ("robot",       ["ring_mod 60", "gain -2"], "12.1 ms", False, i_wave),
    ("whisper",     ["gain -12", "reverb 0.3"], "48.9 ms", False, i_wave),
]


def draw_main(c):
    p = c.p
    c.rect(0, 0, W, H, fill=p["bg"])
    top_bar(c)
    sidebar(c, "Voices")

    # content header
    c.text(236, 112, "My Voices", 25, p["text"], 700)
    c.text(236, 134, "5 presets · JSON files in effects/", 11.5, p["muted"])
    c.rect(660, 88, 196, 34, rx=9, fill=p["card"], stroke=p["border"])
    i_search(c, 676, 103, 5.5, p["muted"])
    c.text(694, 109.5, "Search voices…", 11, p["muted"])
    c.rect(868, 88, 80, 34, rx=9, stroke=c.ink, so=0.55)
    c.text(908, 109.5, "+ New", 11.5, c.ink, 600, SANS, "middle")

    # voice cards
    CW, CH, GAP = 224, 152, 20
    for i, (name, chips, lat, active, ic) in enumerate(CARDS):
        x = 236 + (i % 3) * (CW + GAP)
        y = 160 + (i // 3) * (CH + GAP)
        if active:
            c.rect(x, y, CW, CH, rx=12, fill=p["card"], stroke=c.ink, sw=1.5,
                   so=0.9, use_filter=c.filt() if p["glow"] else None)
        else:
            c.rect(x, y, CW, CH, rx=12, fill=p["card"], stroke=p["border"])
        c.rect(x + 16, y + 16, 36, 36, rx=10, fill=c.a0, fo=0.20 if active else 0.10)
        ic(c, x + 25, y + 25, 18, c.ink)
        if active:
            c.pill(x + CW - 62, y + 16, 46, 18, c.a0, c.ink, fo=0.16, so=0.45)
            c.text(x + CW - 39, y + 29, "● LIVE", 8, c.ink, 700, SANS, "middle", ls=0.6)
        c.text(x + 16, y + 80, name, 14.5, p["text"], 600)
        chip_x = x + 16
        for ch in chips:
            wpx = 9 + len(ch) * 5.6
            c.pill(chip_x, y + 92, wpx, 17, p["deep"], p["border"])
            c.text(chip_x + wpx / 2, y + 104, ch, 9, p["soft"], 400, MONO, "middle")
            chip_x += wpx + 6
        c.text(x + 16, y + 140, lat, 8.5, p["muted"], 400, MONO)
        c.text(x + CW - 16, y + 140, "edit ›", 9, p["muted"], 400, SANS, "end")

    # new voice card
    nx, ny = 236 + 2 * (CW + GAP), 160 + (CH + GAP)
    c.rect(nx, ny, CW, CH, rx=12, stroke=p["border"], dash="5 5")
    c.circle(nx + CW / 2, ny + 58, 20, stroke=p["muted"], sw=1.2, opacity=0.8)
    i_plus(c, nx + CW / 2, ny + 58, 14, p["muted"], 1.6)
    c.text(nx + CW / 2, ny + 102, "New voice", 13, p["soft"], 600, SANS, "middle")
    c.text(nx + CW / 2, ny + 120, "JSON or code", 9.5, p["muted"], 400, MONO, "middle")

    # community presets
    c.text(236, 520, "COMMUNITY PRESETS", 9, p["muted"], 600, ls=1.4)
    c.text(948, 520, "browse on GitHub ↗", 9.5, c.ink, 500, SANS, "end")
    for i, (name, author, meta) in enumerate((
            ("anime-girl", "@mika", "pitch +9.0 · formant +2.0"),
            ("radio-static", "@kuro", "distortion 0.4 · bandpass 300–3k"),
            ("demon-lord", "@vel", "pitch -8.0 · reverb 0.6 · gain +2"))):
        ry = 536 + i * 54
        c.rect(236, ry, 712, 46, rx=10, fill=p["card"], stroke=p["border"])
        c.text(252, ry + 20, name, 11.5, p["text"], 400, MONO)
        c.text(252, ry + 36, f"{meta}  —  {author}", 9.5, p["muted"])
        c.rect(886, ry + 10, 48, 26, rx=8, stroke=c.ink, sw=1.1, so=0.45)
        c.text(910, ry + 27, "+ Add", 9.5, c.ink, 600, SANS, "middle")

    # chain panel
    c.rect(968, 56, 312, H - 56, fill=p["panel"])
    c.line(968, 56, 968, H, p["border"])
    c.text(992, 92, "ACTIVE CHAIN", 9, p["muted"], 600, ls=1.4)
    c.text(992, 120, "chipmunk", 19, p["text"], 700)
    c.rect(1216, 98, 44, 24, rx=12, fill=c.a0)
    c.circle(1248, 110, 8, fill=p["on_accent"], opacity=0.9)

    ey = 142
    for name, param, val, pct in (("pitch_shift", "semitones", "+7.0", 0.62),
                                  ("gain", "gain_db", "+0.0", 0.55)):
        c.rect(992, ey, 264, 112, rx=10, fill=p["card"], stroke=p["border"])
        drag_dots(c, 1006, ey + 18, p["muted"])
        c.text(1024, ey + 22, name, 11.5, p["text"], 400, MONO)
        i_close(c, 1240, ey + 18, 8, p["muted"])
        c.text(1008, ey + 50, param, 9.5, p["muted"], 400, MONO)
        c.text(1240, ey + 50, val, 10.5, c.ink, 400, MONO, "end")
        c.rect(1008, ey + 66, 232, 4, rx=2, fill=p["track"])
        c.rect(1008, ey + 66, 232 * pct, 4, rx=2, fill=c.grad("meter"))
        c.circle(1008 + 232 * pct, ey + 68, 6.5, fill=p["knob"], stroke=c.ink, sw=1.5)
        c.text(1008, ey + 96, "drag to reorder · remove", 8, p["faint"], 400, MONO)
        ey += 124

    c.rect(992, ey + 2, 264, 40, rx=10, stroke=p["border"], dash="5 5")
    c.text(1124, ey + 27, "+ add effect", 10.5, p["muted"], 500, SANS, "middle")

    c.line(992, 452, 1256, 452, p["border"])
    c.text(992, 478, "STATS", 9, p["muted"], 600, ls=1.4)
    for i, (k, v) in enumerate((("algorithmic latency", "34.8 ms"), ("queued", "42 ms"),
                                ("underruns", "2"), ("overruns", "0"))):
        sy = 504 + i * 24
        c.text(992, sy, k, 10, p["muted"])
        c.text(1256, sy, v, 10.5, p["soft"], 400, MONO, "end")

    c.line(992, 604, 1256, 604, p["border"])
    c.rect(992, 620, 128, 34, rx=9, fill=p["card"], stroke=p["border"])
    c.text(1056, 642, "Save preset", 10.5, p["soft"], 500, SANS, "middle")
    c.rect(1128, 620, 128, 34, rx=9, fill=c.a0, fo=0.12, stroke=c.ink, so=0.4)
    c.text(1192, 642, "Export JSON", 10.5, c.ink, 600, SANS, "middle")
    c.text(992, 676, "effects/chipmunk.json", 9, p["faint"], 400, MONO)

    bottom_bar(c)


# --- settings page (theme + accent picker) ----------------------------------
def draw_settings(c, theme_choice="Dark", accent_name="blush"):
    p = c.p
    c.rect(0, 0, W, H, fill=p["bg"])
    top_bar(c)
    sidebar(c, "Settings")

    c.text(236, 112, "Settings", 25, p["text"], 700)
    c.text(236, 134, "Appearance, audio and hotkeys", 11.5, p["muted"])

    # --- appearance card ---
    c.rect(236, 160, 704, 290, rx=12, fill=p["card"], stroke=p["border"])
    c.text(260, 190, "APPEARANCE", 9, p["muted"], 600, ls=1.4)

    # theme row
    c.text(260, 224, "Theme", 13, p["text"], 600)
    c.text(260, 242, "System follows your OS light/dark mode", 10, p["muted"])
    seg = ["System", "Light", "Dark", "AMOLED"]
    c.rect(536, 208, 384, 38, rx=10, fill=p["deep"], stroke=p["border"])
    for i, s in enumerate(seg):
        sx = 540 + i * 94
        on = s == theme_choice
        if on:
            c.rect(sx, 212, 94, 30, rx=8, fill=c.a0, fo=0.92)
        c.text(sx + 47, 232, s, 10.5, p["on_accent"] if on else p["soft"],
               600 if on else 500, SANS, "middle")

    c.line(260, 264, 916, 264, p["border"])

    # accent row
    c.text(260, 296, "Accent color", 13, p["text"], 600)
    c.text(260, 314, "Drives active states, meters and the voice toggle", 10, p["muted"])
    c.extra_defs = "".join(
        f'<linearGradient id="{c.uid}sw{i}" x1="0" y1="0" x2="1" y2="1">'
        f'<stop offset="0" stop-color="{a0}"/><stop offset="1" stop-color="{a1}"/>'
        f'</linearGradient>' for i, (a0, a1) in enumerate(ACCENTS.values()))
    for i, (name, (a0, a1)) in enumerate(ACCENTS.items()):
        ax = 552 + i * 36
        if name == accent_name:
            c.circle(ax, 344, 17, stroke=a1, sw=1.5, opacity=0.9)
        c.circle(ax, 344, 12, fill=f"url(#{c.uid}sw{i})")
    c.circle(552 + len(ACCENTS) * 36, 344, 12, stroke=p["muted"], sw=1.2, dash="3 3")
    i_plus(c, 552 + len(ACCENTS) * 36, 344, 9, p["muted"], 1.4)
    c.text(916, 348, "#FF99CC", 10, p["soft"], 400, MONO, "end")

    c.line(260, 374, 916, 374, p["border"])

    # system accent row
    c.text(260, 406, "Match system accent", 13, p["text"], 600)
    c.text(260, 424, "Use your desktop's accent color automatically", 10, p["muted"])
    c.rect(872, 394, 44, 24, rx=12, fill=c.a0)
    c.circle(900, 406, 8, fill=p["knob"])

    # --- audio card ---
    c.rect(236, 470, 704, 246, rx=12, fill=p["card"], stroke=p["border"])
    c.text(260, 500, "AUDIO", 9, p["muted"], 600, ls=1.4)

    c.text(260, 534, "Microphone", 13, p["text"], 600)
    c.text(260, 551, "capture device", 10, p["muted"])
    c.rect(600, 520, 316, 34, rx=9, fill=p["deep"], stroke=p["border"])
    c.text(616, 542, "default · 2ch 44.1kHz", 10.5, p["text"], 400, MONO)
    i_chevron(c, 896, 534, p["muted"])

    c.line(260, 570, 916, 570, p["border"])
    c.text(260, 602, "Virtual mic", 13, p["text"], 600)
    c.text(260, 619, "auto-created null sink", 10, p["muted"])
    c.rect(600, 588, 316, 34, rx=9, fill=c.a0, fo=0.10, stroke=c.ink, so=0.35)
    c.circle(616, 605, 3.4, fill=c.ink)
    c.text(628, 609, "openmod_vmic", 10.5, p["text"], 400, MONO)
    c.text(900, 609, "monitor", 9, p["muted"], 400, MONO, "end")

    c.line(260, 638, 916, 638, p["border"])
    c.text(260, 670, "Monitor processed voice", 13, p["text"], 600)
    c.text(260, 687, "hear yourself (adds latency)", 10, p["muted"])
    c.rect(872, 648, 44, 24, rx=12, fill=p["track"])
    c.circle(884, 660, 8, fill=p["knob"])

    # --- preview panel ---
    c.rect(964, 160, 292, 560, rx=12, fill=p["panel"], stroke=p["border"])
    c.text(988, 190, "PREVIEW", 9, p["muted"], 600, ls=1.4)
    c.text(988, 208, f"{p['label'].lower()} · {accent_name}", 9.5, p["muted"], 400, MONO)

    c.rect(988, 224, 244, 140, rx=10, fill=p["card"], stroke=c.ink, sw=1.5, so=0.8)
    c.rect(1004, 240, 32, 32, rx=9, fill=c.a0, fo=0.2)
    i_wave(c, 1012, 248, 16, c.ink)
    c.text(1004, 300, "chipmunk", 12.5, p["text"], 600)
    c.pill(1004, 310, 62, 16, p["deep"], p["border"])
    c.text(1035, 321, "pitch +7.0", 8.5, p["soft"], 400, MONO, "middle")
    c.text(1004, 348, "34.8 ms", 8, p["muted"], 400, MONO)

    c.rect(988, 380, 244, 38, rx=19, fill=c.grad())
    c.text(1110, 404, "● VOICE ON", 11, p["on_accent"], 700, SANS, "middle", ls=1.2)

    c.text(988, 444, "OUT", 8.5, p["muted"], 600, SANS, "end")
    c.rect(998, 438, 234, 5, rx=2.5, fill=p["track"])
    c.rect(998, 438, 234 * 0.7, 5, rx=2.5, fill=c.grad("meter"))

    c.line(988, 470, 1256, 470, p["border"])
    c.text(988, 496, "ACCENT TOKENS", 9, p["muted"], 600, ls=1.4)
    for i, (name, val) in enumerate((("accent", c.a0), ("accentDeep", c.a1),
                                     ("onAccent", p["on_accent"]))):
        yy = 520 + i * 22
        c.circle(994, yy - 3.5, 5, fill=val)
        c.text(1010, yy, name, 10, p["soft"], 400, MONO)
        c.text(1240, yy, val.upper(), 9.5, p["muted"], 400, MONO, "end")

    c.line(988, 596, 1256, 596, p["border"])
    c.text(988, 622, "Themes are pure tokens —", 9.5, p["muted"])
    c.text(988, 638, "no per-screen overrides.", 9.5, p["muted"])
    c.text(988, 660, "System = OS light/dark +", 9.5, p["muted"])
    c.text(988, 676, "OS accent (if enabled).", 9.5, p["muted"])
    c.text(988, 704, "ui/README.md", 9, p["faint"], 400, MONO)

    bottom_bar(c)


# --- onboarding wizard (Zen-style first launch) ------------------------------
# A full-window overlay, not the shell: no sidebar, no chain panel. One idea per
# step, big calm type, and the only thing that ever asks for a decision is the
# virtual-mic step (which is where openmod can genuinely help on Windows).

WIZ_STEPS = 5


def i_check(c, cx, cy, s, color, sw=2.0):
    c.path(f"M {cx - s*0.42:.2f} {cy:.2f} l {s*0.30:.2f} {s*0.32:.2f} l {s*0.56:.2f} -{s*0.64:.2f}",
           stroke=color, sw=sw)


def i_arrow(c, x, y, color, sw=1.6):
    c.line(x, y, x + 12, y, color, sw)
    c.path(f"M {x + 6:.2f} {y - 5:.2f} l 6 5 l -6 5", stroke=color, sw=sw)


def i_download(c, x, y, s, color):
    c.line(x + s / 2, y, x + s / 2, y + s * 0.58, color, 1.6)
    c.path(f"M {x + s*0.28:.2f} {y + s*0.38:.2f} l {s*0.22:.2f} {s*0.24:.2f} "
           f"l {s*0.22:.2f} -{s*0.24:.2f}", stroke=color, sw=1.6)
    c.path(f"M {x + s*0.14:.2f} {y + s*0.82:.2f} h {s*0.72:.2f}", stroke=color, sw=1.6)


def i_folder(c, x, y, s, color):
    c.path(f"M {x:.2f} {y + s*0.18:.2f} h {s*0.34:.2f} l {s*0.12:.2f} {s*0.16:.2f} "
           f"h {s*0.40:.2f} v {s*0.50:.2f} h -{s*0.86:.2f} z", stroke=color, sw=1.5)


def i_plug(c, x, y, s, color):
    """virtual cable / plug"""
    c.rect(x + s * 0.30, y + s * 0.34, s * 0.40, s * 0.40, rx=s * 0.10, stroke=color, sw=1.5)
    c.line(x + s * 0.40, y + s * 0.34, x + s * 0.40, y + s * 0.10, color, 1.5)
    c.line(x + s * 0.60, y + s * 0.34, x + s * 0.60, y + s * 0.10, color, 1.5)
    c.line(x + s * 0.50, y + s * 0.74, x + s * 0.50, y + s * 0.94, color, 1.5)


def i_shield(c, x, y, s, color):
    c.path(f"M {x + s*0.5:.2f} {y:.2f} l {s*0.36:.2f} {s*0.14:.2f} v {s*0.30:.2f} "
           f"q 0 {s*0.34:.2f} -{s*0.36:.2f} {s*0.46:.2f} "
           f"q -{s*0.36:.2f} -{s*0.12:.2f} -{s*0.36:.2f} -{s*0.46:.2f} "
           f"v -{s*0.30:.2f} z", stroke=color, sw=1.5)


def wizard_frame(c, step, next_label="Continue", back_label="Back", show_back=True,
                 show_skip=True, show_next=True, step_note=None):
    """Shared chrome: logo, skip, progress dots, footer buttons."""
    p = c.p
    c.rect(0, 0, W, H, fill=p["bg"])

    # header
    c.rect(40, 34, 28, 28, rx=8, fill=c.grad())
    for i, hh in enumerate((8, 14, 10)):
        c.rect(47 + i * 5.5, 48 - hh / 2, 2.6, hh, rx=1.3, fill=p["on_accent"], opacity=0.85)
    c.wordmark(78, 55, 16)
    if show_skip:
        c.text(1240, 55, "skip for now", 11, p["muted"], 400, SANS, "end")

    # footer
    c.line(40, 690, 1240, 690, p["border"])

    dots_x = 640 - (WIZ_STEPS - 1) * 9
    for i in range(WIZ_STEPS):
        on = i == step - 1
        if on:
            c.rect(dots_x + i * 18 - 7, 723, 20, 5, rx=2.5, fill=c.ink)
        else:
            c.circle(dots_x + i * 18 + 3, 725.5, 2.6,
                     fill=c.ink if i < step - 1 else p["track"])
    if step_note:
        c.text(640, 754, step_note, 9.5, p["faint"], 400, MONO, "middle")

    # footer buttons
    if show_back:
        c.rect(1006, 708, 84, 42, rx=10, fill="none", stroke=p["border"], sw=1)
        c.text(1048, 734, back_label, 11, p["soft"], 500, SANS, "middle")
    if show_next:
        bw = 158
        c.rect(1240 - bw, 708, bw, 42, rx=10, fill=c.grad())
        c.text(1240 - bw + bw / 2 - 8, 734, next_label, 11.5, p["on_accent"], 700, SANS, "middle")
        i_arrow(c, 1240 - 26, 729, p["on_accent"])


def wizard_head(c, title, subtitle=None, subtitle2=None, x=280):
    p = c.p
    c.text(x, 178, title, 30, p["text"], 700)
    if subtitle:
        c.text(x, 206, subtitle, 12.5, p["soft"])
    if subtitle2:
        c.text(x, 224, subtitle2, 12.5, p["soft"])


# --- step 1: welcome --------------------------------------------------------
def draw_wizard_welcome(c):
    p = c.p
    wizard_frame(c, 1, show_back=False, show_skip=False, show_next=False)

    # calm glow behind the mark
    c.circle(640, 268, 150, fill=c.a0, opacity=0.05)
    c.circle(640, 268, 96, fill=c.a0, opacity=0.05)

    c.rect(604, 232, 72, 72, rx=19, fill=c.grad(),
           use_filter=c.filt() if p["glow"] else None)
    for i, hh in enumerate((18, 32, 24)):
        c.rect(620 + i * 13, 268 - hh / 2, 6, hh, rx=3, fill=p["on_accent"], opacity=0.88)

    c.text(640, 386, "welcome to a calmer voice chat", 38, p["text"], 700, SANS, "middle")
    c.text(640, 420, "no subscription · no account · no upsell — just your voice, your rules",
           13.5, p["muted"], 400, SANS, "middle")

    c.rect(520, 456, 240, 48, rx=24, fill=c.grad(),
           use_filter=c.filt() if p["glow"] else None)
    c.text(628, 486, "Get started", 13, p["on_accent"], 700, SANS, "middle")
    i_arrow(c, 682, 481, p["on_accent"])

    c.text(640, 536, "used openmod before?  import your presets instead",
           10.5, p["faint"], 400, SANS, "middle")

    # three promises, evenly spaced
    for i, label in enumerate(("free forever, MIT licensed", "runs offline, no telemetry",
                               "your files stay on your disk")):
        cx = 380 + i * 260
        c.circle(cx - 78, 606, 7, fill=c.a0, fo=0.16)
        i_check(c, cx - 78, 606, 8, c.ink, 1.8)
        c.text(cx - 64, 610, label, 10.5, p["muted"])


# --- step 2: appearance -----------------------------------------------------
def draw_wizard_appearance(c, theme_choice="Dark", accent_name="blush"):
    p = c.p
    wizard_frame(c, 2, next_label="Continue")
    wizard_head(c, "make it yours",
                "pick a theme and an accent — every screen is built from these tokens,",
                "so this is the real thing recoloring, not a preview image.")

    # four theme cards with live mini previews
    order = ["dark", "light", "amoled"]
    cards = [("System", None)] + [(THEMES[k]["label"], k) for k in order]
    for i, (label, key) in enumerate(cards):
        x, y, w, h = 280 + i * 180, 262, 160, 104
        on = label == theme_choice
        c.rect(x, y, w, h, rx=10, fill=p["card"],
               stroke=c.ink if on else p["border"], sw=1.6 if on else 1)
        tp = THEMES[key] if key else THEMES["dark"]
        c.rect(x + 10, y + 10, 140, 76, rx=6, fill=tp["bg"])
        c.rect(x + 10, y + 10, 140, 13, rx=4, fill=tp["panel"])
        c.circle(x + 19, y + 16.5, 2.6, fill=c.a0)
        c.text(x + 27, y + 19.5, "openmod", 6.5, tp["text"], 600)
        c.rect(x + 18, y + 32, 42, 26, rx=4, fill=tp["card"])
        c.rect(x + 22, y + 38, 15, 3, rx=1.5, fill=c.a0)
        c.rect(x + 22, y + 46, 28, 3, rx=1.5, fill=tp["track"])
        c.rect(x + 66, y + 32, 74, 26, rx=4, fill=tp["card"])
        c.rect(x + 70, y + 38, 22, 3, rx=1.5, fill=tp["track"])
        c.rect(x + 18, y + 66, 46, 9, rx=4.5, fill=c.grad())
        if label == "System":
            c.circle(x + 132, y + 18, 5, fill=tp["track"])
            c.path(f"M {x + 132} {y + 13} a 5 5 0 0 1 0 10 z", fill=tp["text"])
        c.text(x + w / 2, y + h + 20, label, 11,
               p["text"] if on else p["muted"], 600 if on else 500, SANS, "middle")
        if on:
            c.circle(x + w - 12, y + 12, 8, fill=c.ink)
            i_check(c, x + w - 12, y + 12, 8, p["on_accent"], 1.6)

    # accent swatches
    c.text(280, 434, "ACCENT", 9, p["muted"], 600, ls=1.4)
    c.extra_defs = "".join(
        f'<linearGradient id="{c.uid}wz{i}" x1="0" y1="0" x2="1" y2="1">'
        f'<stop offset="0" stop-color="{a0}"/><stop offset="1" stop-color="{a1}"/>'
        f'</linearGradient>' for i, (a0, a1) in enumerate(ACCENTS.values()))
    for i, (name, (a0, a1)) in enumerate(ACCENTS.items()):
        ax = 294 + i * 42
        if name == accent_name:
            c.circle(ax, 472, 17, stroke=a1, sw=1.6, opacity=0.9)
        c.circle(ax, 472, 13, fill=f"url(#{c.uid}wz{i})")
    cx = 294 + len(ACCENTS) * 42
    c.circle(cx, 472, 13, stroke=p["muted"], sw=1.2, dash="3 3")
    i_plus(c, cx, 472, 9, p["muted"], 1.4)
    c.text(cx + 28, 476, "or any hex you like", 10, p["faint"])

    c.text(280, 528, "System follows your desktop's light/dark mode — and, if you let it,",
           10.5, p["muted"])
    c.text(280, 544, "your desktop's accent colour too. Nothing here is final: it's all in Settings.",
           10.5, p["muted"])

    # live preview strip — the accent in the places it actually shows up
    c.text(280, 596, "LIVE PREVIEW", 9, p["muted"], 600, ls=1.4)
    c.rect(280, 612, 232, 54, rx=10, fill=p["card"], stroke=c.ink, sw=1.4,
           use_filter=c.filt() if p["glow"] else None)
    c.rect(294, 624, 26, 26, rx=7, fill=c.a0, fo=0.2)
    i_wave(c, 300, 630, 14, c.ink)
    c.text(330, 634, "chipmunk", 10.5, p["text"], 600)
    c.pill(330, 640, 52, 14, c.a0, c.ink, fo=0.14, so=0.4)
    c.text(356, 650.5, "pitch +7", 7.5, c.ink, 400, MONO, "middle")

    c.rect(528, 622, 150, 34, rx=17, fill=c.grad())
    c.text(596, 643, "VOICE ON", 9.5, p["on_accent"], 700, SANS, "middle", ls=1.1)
    c.circle(546, 639, 3.4, fill=p["on_accent"])

    c.text(700, 632, "OUT", 8.5, p["muted"], 600, SANS, "end")
    c.rect(708, 626, 272, 6, rx=3, fill=p["track"])
    c.rect(708, 626, 272 * 0.66, 6, rx=3, fill=c.grad("meter"))
    c.text(708, 652, "every one of these is the same token pair", 9, p["faint"], 400, MONO)


# --- step 3: virtual mic ----------------------------------------------------
def draw_wizard_vmic_windows(c):
    p = c.p
    wizard_frame(c, 3, next_label="Continue", step_note="step 3 of 5 · windows")

    wizard_head(c, "one thing games need: a virtual mic",
                "Games listen to a microphone, not to an app. openmod needs a virtual",
                "audio device to show up as one — this is the only extra piece it needs.")

    # the optional add-on card
    c.rect(280, 252, 700, 178, rx=12, fill=p["card"], stroke=p["border"])
    c.rect(302, 274, 40, 40, rx=11, fill=c.a0, fo=0.18)
    i_plug(c, 311, 283, 22, c.ink)
    c.text(360, 292, "VB-CABLE virtual audio device", 13.5, p["text"], 600)
    c.text(360, 311, "free · donationware · by VB-Audio — the cable Voicemod and OBS users install",
           10, p["muted"])
    c.pill(856, 276, 100, 22, p["deep"], p["border"])
    c.circle(868, 287, 3.2, fill=p["muted"])
    c.text(876, 291, "not detected", 9.5, p["soft"], 400, MONO)

    c.text(302, 344, "openmod downloads the official installer from vb-audio.com and runs it",
           10, p["soft"])
    c.text(302, 359, "with the flags VB-Audio documents. Nothing is bundled, nothing is patched.",
           10, p["soft"])

    c.rect(302, 374, 190, 40, rx=10, fill=c.grad())
    i_download(c, 320, 385, 16, p["on_accent"])
    c.text(344, 399, "Install it for me", 11, p["on_accent"], 700)
    c.rect(504, 374, 172, 40, rx=10, fill="none", stroke=p["border"], sw=1)
    c.text(590, 399, "open vb-audio.com", 10.5, p["soft"], 500, SANS, "middle")
    c.text(700, 399, "asks for your admin password once", 9.5, p["faint"])

    # alternatives — the whole step is optional
    opts = [("I already have a virtual cable", "detect it and use that instead", False),
            ("skip this for now", "openmod still runs; you just won't be a mic yet", True)]
    for i, (label, note, dashed) in enumerate(opts):
        y = 448 + i * 56
        c.rect(280, y, 700, 46, rx=10, fill=p["deep"] if not dashed else "none",
               stroke=p["border"], sw=1, dash="4 4" if dashed else None)
        c.circle(306, y + 23, 7, stroke=p["muted"], sw=1.4)
        c.text(330, y + 21, label, 11.5, p["text"] if not dashed else p["soft"], 500)
        c.text(330, y + 36, note, 9.5, p["faint"])

    c.line(280, 620, 980, 620, p["border"])
    c.text(280, 644, "Why not just ship a cable? Because drivers need a signed installer and a",
           10, p["muted"])
    c.text(280, 660, "reboot, and openmod has no business doing that behind your back. Opt-in only.",
           10, p["muted"])


def draw_wizard_vmic_linux(c):
    p = c.p
    wizard_frame(c, 3, next_label="Continue", step_note="step 3 of 5 · linux")

    wizard_head(c, "your virtual mic is already here",
                "PipeWire makes this for you, so there's nothing to install on Linux —",
                "openmod created the sink on first run and will keep it alive.")

    c.rect(280, 252, 700, 178, rx=12, fill=p["card"], stroke=p["border"])
    c.rect(302, 274, 40, 40, rx=11, fill=c.a0, fo=0.18)
    i_check(c, 322, 294, 18, c.ink, 2.2)
    c.text(360, 292, "openmod_vmic", 13.5, p["text"], 600, MONO)
    c.text(360, 311, "PipeWire null sink — created for you, already running", 10, p["muted"])
    c.pill(870, 276, 86, 22, c.a0, c.ink, fo=0.16, so=0.4)
    c.circle(882, 287, 3.2, fill=c.ink)
    c.text(890, 291, "ready", 9.5, c.ink, 600, MONO)

    c.text(302, 344, "pick this as your microphone:", 10, p["soft"])
    c.rect(302, 354, 300, 30, rx=8, fill=p["deep"], stroke=p["border"])
    c.text(316, 373, "openmod_vmic", 10.5, p["text"], 400, MONO)
    c.text(590, 373, "copy", 9.5, c.ink, 500, MONO, "end")

    c.text(302, 404, "hear yourself (optional):", 10, p["soft"])
    c.text(452, 404, "openmod_vmic.monitor", 10.5, p["muted"], 400, MONO)

    for i, label in enumerate(("Test it", "Recreate the sink")):
        x = 280 + i * 132
        c.rect(x, 448, 120, 38, rx=9, fill="none", stroke=p["border"], sw=1)
        c.text(x + 60, 472, label, 10.5, p["soft"], 500, SANS, "middle")

    c.line(280, 620, 980, 620, p["border"])
    c.text(280, 644, "On Windows this same step offers to install VB-CABLE with one button —",
           10, p["muted"])
    c.text(280, 660, "Linux gets the equivalent for free from PipeWire, so we skip the install.",
           10, p["muted"])


# --- step 4: import ---------------------------------------------------------
def draw_wizard_import(c):
    p = c.p
    wizard_frame(c, 4, next_label="Import 2 items", step_note="step 4 of 5")

    wizard_head(c, "bring your stuff with you",
                "openmod only indexes file names up front and reads a file when you use it,",
                "so pointing at an existing folder is instant — nothing gets copied.")

    rows = [
        ("Presets", "effects/*.json", "3 found", "pitch, formant and gain — your chains come along", True),
        ("Soundboard", "soundboards/", "0 clips", "drop .wav .mp3 .ogg .flac files in any time", True),
        ("Voicemod presets", "AppData/Roaming/Voicemod", "not on this machine",
         "we read their JSON and map what we can — best effort", False),
    ]
    for i, (name, path, count, note, checked) in enumerate(rows):
        y = 252 + i * 76
        c.rect(280, y, 700, 64, rx=10, fill=p["card"], stroke=p["border"])
        c.rect(302, y + 23, 18, 18, rx=5,
               fill=c.a0 if checked else "none",
               stroke=c.ink if checked else p["track"], sw=1.4)
        if checked:
            i_check(c, 311, y + 32, 11, p["on_accent"], 1.8)
        i_folder(c, 334, y + 20, 22, c.ink if checked else p["muted"])
        c.text(368, y + 26, name, 12, p["text"], 600)
        c.text(368, y + 43, note, 9.5, p["muted"])
        c.text(940, y + 26, count, 10, p["soft"] if checked else p["faint"], 400, MONO, "end")
        c.text(940, y + 43, path, 9, p["faint"], 400, MONO, "end")

    c.text(280, 512, "nothing is converted, copied or uploaded — openmod points at the files you already have.",
           10, p["muted"])

    c.line(280, 620, 980, 620, p["border"])
    c.text(280, 644, "a fourth row appears if you have another voice changer installed:",
           10, p["muted"])
    c.text(280, 660, "presets we can read are imported, the rest are listed and skipped with a reason.",
           10, p["muted"])


# --- step 5: done -----------------------------------------------------------
def draw_wizard_done(c):
    p = c.p
    wizard_frame(c, 5, show_back=False, show_skip=False, show_next=False)

    c.circle(640, 214, 104, fill=c.a0, opacity=0.05)
    c.rect(608, 182, 64, 64, rx=32, fill=c.grad(),
           use_filter=c.filt() if p["glow"] else None)
    i_check(c, 640, 214, 28, p["on_accent"], 3.2)

    c.text(640, 356, "you're live", 38, p["text"], 700, SANS, "middle")
    c.text(640, 390, "your voice is on openmod_vmic — pick it as the mic wherever you talk.",
           13, p["muted"], 400, SANS, "middle")

    c.rect(340, 426, 600, 96, rx=12, fill=p["card"], stroke=p["border"])
    c.text(364, 458, "in games and apps", 9, p["muted"], 600, ls=1.2)
    c.text(364, 482, "openmod_vmic", 11, p["text"], 400, MONO)
    c.text(364, 504, "hear yourself (optional)  ·  openmod_vmic.monitor", 9.5, p["faint"], 400, MONO)
    c.pill(856, 442, 64, 20, c.a0, c.ink, fo=0.16, so=0.4)
    c.text(888, 456, "LIVE", 8.5, c.ink, 700, SANS, "middle", ls=1)

    c.rect(430, 548, 180, 44, rx=11, fill=c.grad())
    c.text(520, 576, "Open openmod", 11.5, p["on_accent"], 700, SANS, "middle")
    c.rect(626, 548, 180, 44, rx=11, fill="none", stroke=p["border"], sw=1)
    c.text(716, 576, "read the README", 10.5, p["soft"], 500, SANS, "middle")

    c.text(640, 632, "this wizard lives in Settings → setup, and never shows up again unless you ask",
           9.5, p["faint"], 400, SANS, "middle")


# --- write files ------------------------------------------------------------
def write(name, canvas):
    with open(f"ui/{name}.svg", "w") as f:
        f.write(canvas.svg())
    print("wrote ui/" + name + ".svg")


def main():
    # main window in all three palettes
    for i, key in enumerate(("dark", "light", "amoled")):
        c = Canvas(THEMES[key], ACCENTS["blush"], uid=i + 1)
        draw_main(c)
        write(f"mockup-main-{key}", c)

    # settings page (dark, Dark selected, blush)
    c = Canvas(THEMES["dark"], ACCENTS["blush"], uid=4)
    draw_settings(c, theme_choice="Dark")
    write("mockup-settings", c)

    # accent-swap proof: same dark palette, mint accent
    c = Canvas(THEMES["dark"], ACCENTS["mint"], uid=5)
    draw_main(c)
    write("mockup-main-mint", c)

    # onboarding wizard — Zen-style first launch
    c = Canvas(THEMES["dark"], ACCENTS["blush"], uid=6)
    draw_wizard_welcome(c)
    write("mockup-wizard-1-welcome", c)

    c = Canvas(THEMES["dark"], ACCENTS["blush"], uid=7)
    draw_wizard_appearance(c, theme_choice="Dark", accent_name="blush")
    write("mockup-wizard-2-appearance", c)

    # the virtual-mic step is the only one that differs per platform
    c = Canvas(THEMES["dark"], ACCENTS["blush"], uid=8)
    draw_wizard_vmic_windows(c)
    write("mockup-wizard-3-vmic-windows", c)

    c = Canvas(THEMES["dark"], ACCENTS["blush"], uid=9)
    draw_wizard_vmic_linux(c)
    write("mockup-wizard-3-vmic-linux", c)

    c = Canvas(THEMES["dark"], ACCENTS["blush"], uid=10)
    draw_wizard_import(c)
    write("mockup-wizard-4-import", c)

    c = Canvas(THEMES["dark"], ACCENTS["blush"], uid=11)
    draw_wizard_done(c)
    write("mockup-wizard-5-done", c)

    # the wizard on a light palette, to prove it's the same token set
    c = Canvas(THEMES["light"], ACCENTS["violet"], uid=12)
    draw_wizard_appearance(c, theme_choice="Light", accent_name="violet")
    write("mockup-wizard-2-appearance-light", c)

    # theme sheet: 2x2 at 50% + accent row
    panels = [("Dark", THEMES["dark"], draw_main),
              ("Light", THEMES["light"], draw_main),
              ("AMOLED", THEMES["amoled"], draw_main),
              ("Settings · theme + accent picker", THEMES["dark"], draw_settings)]
    SH_W, SH_H = 1400, 1070
    body = []
    for i, (label, pal, fn) in enumerate(panels):
        cc = Canvas(pal, ACCENTS["blush"], uid=20 + i)
        fn(cc)
        x, y = 40 + (i % 2) * 660, 96 + (i // 2) * 440
        body.append(f'<rect x="{x - 1}" y="{y - 1}" width="642" height="402" rx="7" '
                    f'fill="none" stroke="#2A2F38"/>')
        body.append(f'<g transform="translate({x},{y}) scale(0.5)">'
                    f'{cc.defs()}\n{cc.content()}</g>')
        body.append(f'<text x="{x}" y="{y - 14}" fill="#8A94A3" font-size="11" '
                    f'font-family="{SANS}" font-weight="600" letter-spacing="1.2">'
                    f'{label.upper()}</text>')

    head = (f'<text x="40" y="46" fill="#ECEFF4" font-size="20" font-family="{SANS}" '
            f'font-weight="700">openmod · themes</text>'
            f'<text x="40" y="66" fill="#6E7A8A" font-size="11" font-family="{SANS}">'
            f'same layout, three palettes — accent is a single token pair</text>')
    sw_y = 1006
    sw = [f'<text x="40" y="{sw_y - 32}" fill="#8A94A3" font-size="10" '
          f'font-family="{SANS}" font-weight="600" letter-spacing="1.2">ACCENT PRESETS</text>']
    for i, (name, (a0, a1)) in enumerate(ACCENTS.items()):
        sx = 60 + i * 150
        sw.append(f'<circle cx="{sx}" cy="{sw_y}" r="14" fill="url(#sheet-sw{i})"/>')
        sw.append(f'<text x="{sx + 24}" y="{sw_y - 2}" fill="#ECEFF4" font-size="11" '
                  f'font-family="{SANS}">{name}</text>')
        sw.append(f'<text x="{sx + 24}" y="{sw_y + 12}" fill="#6E7A8A" font-size="9.5" '
                  f'font-family="{MONO}">{a0}</text>')

    grads = "".join(f'<linearGradient id="sheet-sw{i}" x1="0" y1="0" x2="1" y2="1">'
                    f'<stop offset="0" stop-color="{a0}"/>'
                    f'<stop offset="1" stop-color="{a1}"/></linearGradient>'
                    for i, (a0, a1) in enumerate(ACCENTS.values()))
    sheet_svg = (f'<svg xmlns="http://www.w3.org/2000/svg" width="{SH_W}" height="{SH_H}" '
                 f'viewBox="0 0 {SH_W} {SH_H}"><defs>{grads}</defs>'
                 f'<rect x="0" y="0" width="{SH_W}" height="{SH_H}" fill="#15181E"/>'
                 f'{head}{"".join(body)}{"".join(sw)}</svg>')
    with open("ui/mockup-themes.svg", "w") as f:
        f.write(sheet_svg)
    print("wrote ui/mockup-themes.svg")


if __name__ == "__main__":
    main()
