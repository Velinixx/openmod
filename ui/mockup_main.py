#!/usr/bin/env python3
"""Generates the openmod main-window mockup (SVG -> PNG).

Run:  python3 ui/mockup_main.py
Output: ui/mockup-main.svg, ui/mockup-main.png (2x)
"""

W, H = 1280, 800

# --- palette (Nord-ish + blush) -------------------------------------------
BG       = "#23272F"
SIDEBAR  = "#1B1F26"
CARD     = "#2C323C"
CARD_DK  = "#262B34"
TRACK    = "#3A4250"
BORDER   = "#333B47"
TXT      = "#ECEFF4"
TXT2     = "#AEB8C6"
MUTED    = "#6E7A8A"
PINK     = "#FF99CC"
PINK_D   = "#E91E63"
SANS     = "Cantarell, DejaVu Sans, sans-serif"
MONO     = "DejaVu Sans Mono, monospace"

out = []


def el(tag, **a):
    parts = []
    for k, v in a.items():
        k = k.replace("_", "-")
        parts.append(f'{k}="{v}"')
    out.append(f"<{tag} {' '.join(parts)}/>")


def rect(x, y, w, h, rx=0, fill="none", stroke="none", sw=1, opacity=None,
         fill_opacity=None, stroke_opacity=None, dash=None, filt=None):
    a = dict(x=x, y=y, width=w, height=h, rx=rx, fill=fill, stroke=stroke,
             stroke_width=sw)
    if opacity is not None:
        a["opacity"] = opacity
    if fill_opacity is not None:
        a["fill-opacity"] = fill_opacity
    if stroke_opacity is not None:
        a["stroke_opacity"] = stroke_opacity
    if dash:
        a["stroke_dasharray"] = dash
    if filt:
        a["filter"] = filt
    el("rect", **a)


def circle(cx, cy, r, fill="none", stroke="none", sw=1, opacity=None,
           fill_opacity=None):
    a = dict(cx=cx, cy=cy, r=r, fill=fill, stroke=stroke, stroke_width=sw)
    if opacity is not None:
        a["opacity"] = opacity
    if fill_opacity is not None:
        a["fill-opacity"] = fill_opacity
    el("circle", **a)


def line(x1, y1, x2, y2, stroke, sw=1, opacity=None, dash=None):
    a = dict(x1=x1, y1=y1, x2=x2, y2=y2, stroke=stroke, stroke_width=sw)
    if opacity is not None:
        a["opacity"] = opacity
    if dash:
        a["stroke_dasharray"] = dash
    el("line", **a)


def path(d, fill="none", stroke="none", sw=1.5, opacity=None):
    a = dict(d=d, fill=fill, stroke=stroke, stroke_width=sw, stroke_linecap="round",
             stroke_linejoin="round")
    if opacity is not None:
        a["opacity"] = opacity
    el("path", **a)


def text(x, y, s, size=12, fill=TXT, weight=400, family=SANS, anchor="start",
         ls=None, opacity=None):
    a = {"x": x, "y": y, "fill": fill, "font-size": size, "font-family": family,
         "font-weight": weight, "text-anchor": anchor}
    if ls is not None:
        a["letter-spacing"] = ls
    if opacity is not None:
        a["opacity"] = opacity
    attrs = " ".join(f'{k}="{v}"' for k, v in a.items())
    out.append(f"<text {attrs}>{s}</text>")


# --- icons ------------------------------------------------------------------
def icon_wave(x, y, s, color=PINK, sw=2):
    """small waveform: 3 bars"""
    for i, hh in enumerate((0.45, 1.0, 0.65)):
        bx = x + i * (s / 3) + s / 6
        line(bx, y + s / 2 - s * hh / 2, bx, y + s / 2 + s * hh / 2, color, sw)


def icon_grid(x, y, s, color):
    g = s * 0.38
    for dx, dy in ((0, 0), (s - g, 0), (0, s - g), (s - g, s - g)):
        rect(x + dx, y + dy, g, g, rx=1.5, stroke=color, sw=1.4)


def icon_soundboard(x, y, s, color):
    for i, ww in enumerate((1.0, 0.62, 0.85)):
        yy = y + i * (s / 2.6)
        line(x, yy + s / 8, x + s * ww, yy + s / 8, color, 1.8)


def icon_gear(x, y, s, color):
    cx, cy, r = x + s / 2, y + s / 2, s * 0.26
    circle(cx, cy, r, stroke=color, sw=1.4)
    circle(cx, cy, r * 0.34, stroke=color, sw=1.4)
    import math
    for i in range(8):
        a = i * math.pi / 4
        line(cx + math.cos(a) * (r + 1.5), cy + math.sin(a) * (r + 1.5),
             cx + math.cos(a) * (r + 4), cy + math.sin(a) * (r + 4), color, 1.4)


def icon_mic(x, y, s, color):
    w = s * 0.34
    rect(x + (s - w) / 2, y, w, s * 0.52, rx=w / 2, stroke=color, sw=1.4)
    path(f"M {x + s*0.16:.2f} {y + s*0.46:.2f} "
         f"Q {x + s*0.5:.2f} {y + s*0.86:.2f} {x + s*0.84:.2f} {y + s*0.46:.2f}",
         stroke=color, sw=1.4)
    line(x + s / 2, y + s * 0.78, x + s / 2, y + s * 0.94, color, 1.4)


def icon_headphones(x, y, s, color):
    path(f"M {x + s*0.12} {y + s*0.72} v -{s*0.22} a {s*0.38} {s*0.38} 0 0 1 {s*0.76} 0 v {s*0.22}",
         stroke=color, sw=1.4)
    rect(x + s * 0.08, y + s * 0.6, s * 0.18, s * 0.34, rx=2, stroke=color, sw=1.3)
    rect(x + s * 0.74, y + s * 0.6, s * 0.18, s * 0.34, rx=2, stroke=color, sw=1.3)


def icon_search(cx, cy, r, color):
    circle(cx, cy, r, stroke=color, sw=1.4)
    line(cx + r * 0.7, cy + r * 0.7, cx + r * 1.6, cy + r * 1.6, color, 1.4)


def icon_plus(cx, cy, s, color, sw=1.8):
    line(cx - s / 2, cy, cx + s / 2, cy, color, sw)
    line(cx, cy - s / 2, cx, cy + s / 2, color, sw)


def icon_close(cx, cy, s, color):
    line(cx - s / 2, cy - s / 2, cx + s / 2, cy + s / 2, color, 1.4)
    line(cx + s / 2, cy - s / 2, cx - s / 2, cy + s / 2, color, 1.4)


def drag_dots(x, y, color):
    for r in range(3):
        for c in range(2):
            circle(x + c * 4, y + r * 5, 1.1, fill=color)


def pill(x, y, w, h, fill, stroke="none", sw=1, fill_opacity=None, stroke_opacity=None, rx=None):
    rect(x, y, w, h, rx=rx if rx is not None else h / 2, fill=fill, stroke=stroke,
         sw=sw, fill_opacity=fill_opacity, stroke_opacity=stroke_opacity)


# --- document ---------------------------------------------------------------
out.append(f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" '
           f'viewBox="0 0 {W} {H}">')
out.append(f"""<defs>
  <linearGradient id="pink" x1="0" y1="0" x2="1" y2="1">
    <stop offset="0" stop-color="#FFB8DC"/><stop offset="1" stop-color="{PINK_D}"/>
  </linearGradient>
  <linearGradient id="meter" x1="0" y1="0" x2="1" y2="0">
    <stop offset="0" stop-color="{PINK}"/><stop offset="1" stop-color="{PINK_D}"/>
  </linearGradient>
  <filter id="glow" x="-40%" y="-40%" width="180%" height="180%">
    <feGaussianBlur stdDeviation="7" result="b"/>
    <feMerge><feMergeNode in="b"/><feMergeNode in="SourceGraphic"/></feMerge>
  </filter>
  <clipPath id="win"><rect x="0" y="0" width="{W}" height="{H}" rx="12"/></clipPath>
</defs>""")

out.append('<g clip-path="url(#win)">')
rect(0, 0, W, H, fill=BG)

# --- top bar ----------------------------------------------------------------
rect(0, 0, W, 56, fill=SIDEBAR)
line(0, 56, W, 56, BORDER)

# logo
rect(20, 14, 28, 28, rx=8, fill="url(#pink)")
for i, hh in enumerate((8, 14, 10)):
    bx = 27 + i * 5.5
    rect(bx, 28 - hh / 2, 2.6, hh, rx=1.3, fill="#2A0E1A", opacity=0.85)
text(58, 35, "open", 17, TXT, 600)
out.pop(-1)
out.append(f'<text x="58" y="35" font-size="17" font-family="{SANS}" '
           f'font-weight="600"><tspan fill="{TXT}">open</tspan>'
           f'<tspan fill="{PINK}">mod</tspan></text>')

# voice toggle
rect(536, 11, 208, 34, rx=17, fill=PINK, fill_opacity=0.10, stroke=PINK,
     stroke_opacity=0.35)
rect(542, 14, 196, 28, rx=14, fill="url(#pink)", filt="url(#glow)", opacity=0.95)
circle(578, 28, 4, fill="#2A0E1A")
text(592, 32, "VOICE ON", 11.5, "#2A0E1A", 700, ls=1.3)

# right: latency chip + gear
pill(1030, 14, 88, 28, CARD, BORDER, sw=1)
text(1074, 32.5, "34.8 ms", 10.5, TXT2, 400, MONO, "middle")
icon_gear(1136, 20, 16, MUTED)
circle(1200, 28, 12, stroke=BORDER, sw=1)
icon_grid(1194, 22, 12, MUTED)

# --- sidebar ----------------------------------------------------------------
rect(0, 56, 208, H - 56, fill=SIDEBAR)
line(208, 56, 208, H, BORDER)

text(20, 88, "WORKSPACE", 9, MUTED, 600, ls=1.4)
nav = [("Voices", icon_grid, True), ("Soundboard", icon_soundboard, False),
       ("Settings", icon_gear, False)]
ny = 100
for label, ic, active in nav:
    if active:
        rect(12, ny, 184, 38, rx=9, fill=PINK, fill_opacity=0.10)
        rect(12, ny + 6, 3, 26, rx=1.5, fill=PINK)
    col = PINK if active else TXT2
    ic(28, ny + 11, 16, col)
    text(54, ny + 24, label, 12.5, TXT if active else TXT2, 600 if active else 500)
    ny += 46

line(20, 240, 188, 240, BORDER)
text(20, 266, "EFFECTS", 9, MUTED, 600, ls=1.4)
fx = [("pitch_shift", True), ("gain", True), ("formant", False),
      ("reverb", False), ("distortion", False)]
fy = 288
for name, used in fx:
    circle(24, fy - 3.5, 2.6, fill=PINK if used else TRACK)
    text(36, fy, name, 10.5, TXT2 if used else MUTED, 400, MONO)
    fy += 20

line(20, 396, 188, 396, BORDER)

# folders (JSON-first architecture made visible)
text(20, 422, "FOLDERS", 9, MUTED, 600, ls=1.4)
for i, (name, info) in enumerate((("effects/", "5 files"),
                                  ("soundboards/", "0 clips"),
                                  ("themes/", "soon"))):
    yy = 444 + i * 20
    text(24, yy, name, 10.5, TXT2, 400, MONO)
    text(188, yy, info, 9.5, MUTED, 400, MONO, "end")

line(20, 500, 188, 500, BORDER)

# virtual mic card
rect(12, 586, 184, 86, rx=10, fill=CARD, stroke=BORDER)
text(24, 606, "VIRTUAL MIC", 8.5, MUTED, 600, ls=1.2)
circle(28, 624, 3.4, fill=PINK)
text(38, 628, "openmod_vmic", 10.5, TXT, 400, MONO)
text(24, 646, "pick as mic in games", 9, MUTED)
pill(146, 596, 38, 16, PINK, PINK, sw=1, fill_opacity=0.14, stroke_opacity=0.4)
text(165, 607.5, "LIVE", 8, PINK, 700, SANS, "middle", ls=0.8)

# footer
text(24, 702, "Velinixx/openmod", 9.5, MUTED, 400, MONO)
text(24, 720, "MIT · v0.1.0", 9, "#4E5764", 400, MONO)

# --- main content -----------------------------------------------------------
text(236, 112, "My Voices", 25, TXT, 700)
text(236, 134, "5 presets · JSON files in effects/", 11.5, MUTED)

rect(660, 88, 196, 34, rx=9, fill=CARD, stroke=BORDER)
icon_search(676, 103, 5.5, MUTED)
text(694, 109.5, "Search voices…", 11, MUTED)

rect(868, 88, 80, 34, rx=9, stroke=PINK, sw=1.2, stroke_opacity=0.55)
text(908, 109.5, "+ New", 11.5, PINK, 600, SANS, "middle")

CARDS = [
    ("chipmunk",   ["pitch +7.0", "gain 0.0"], "34.8 ms", True,  icon_wave),
    ("deep-voice", ["pitch -5.0", "gain 0.0"], "33.6 ms", False, icon_wave),
    ("passthrough", ["no effects"],            "0.0 ms",  False, icon_soundboard),
    ("robot",      ["ring_mod 60", "gain -2"], "12.1 ms", False, icon_wave),
    ("whisper",    ["gain -12", "reverb 0.3"], "48.9 ms", False, icon_wave),
]
CW, CH, GAP = 224, 152, 20
cx0, cy0 = 236, 160
for i, (name, chips, lat, active, ic) in enumerate(CARDS):
    x = cx0 + (i % 3) * (CW + GAP)
    y = cy0 + (i // 3) * (CH + GAP)
    if active:
        rect(x, y, CW, CH, rx=12, fill=CARD, stroke=PINK, sw=1.5, filt="url(#glow)",
             stroke_opacity=0.9)
    else:
        rect(x, y, CW, CH, rx=12, fill=CARD, stroke=BORDER)
    rect(x + 16, y + 16, 36, 36, rx=10, fill=PINK,
         fill_opacity=0.2 if active else 0.10)
    ic(x + 25, y + 25, 18, PINK)
    if active:
        pill(x + CW - 62, y + 16, 46, 18, PINK, PINK, sw=1, fill_opacity=0.16,
             stroke_opacity=0.45)
        text(x + CW - 39, y + 29, "● LIVE", 8, PINK, 700, SANS, "middle", ls=0.6)
    text(x + 16, y + 80, name, 14.5, TXT, 600)
    chip_x = x + 16
    for c in chips:
        wpx = 9 + len(c) * 5.6
        pill(chip_x, y + 92, wpx, 17, CARD_DK, BORDER, sw=1)
        text(chip_x + wpx / 2, y + 104, c, 9, TXT2, 400, MONO, "middle")
        chip_x += wpx + 6
    text(x + 16, y + 140, lat, 8.5, MUTED, 400, MONO)
    text(x + CW - 16, y + 140, "edit ›", 9, MUTED, 400, SANS, "end")

# new voice card (dashed)
nx, ny2 = cx0 + 2 * (CW + GAP), cy0 + (CH + GAP)
rect(nx, ny2, CW, CH, rx=12, stroke=BORDER, dash="5 5")
circle(nx + CW / 2, ny2 + 58, 20, stroke=MUTED, sw=1.2, opacity=0.8)
icon_plus(nx + CW / 2, ny2 + 58, 14, MUTED, 1.6)
text(nx + CW / 2, ny2 + 102, "New voice", 13, TXT2, 600, SANS, "middle")
text(nx + CW / 2, ny2 + 120, "JSON or code", 9.5, MUTED, 400, MONO, "middle")

# community presets
text(236, 520, "COMMUNITY PRESETS", 9, MUTED, 600, ls=1.4)
text(948, 520, "browse on GitHub ↗", 9.5, PINK, 500, SANS, "end")
COMM = [
    ("anime-girl", "@mika", "pitch +9.0 · formant +2.0"),
    ("radio-static", "@kuro", "distortion 0.4 · bandpass 300–3k"),
    ("demon-lord", "@vel", "pitch -8.0 · reverb 0.6 · gain +2"),
]
ry = 536
for name, author, meta in COMM:
    rect(236, ry, 712, 46, rx=10, fill=CARD, stroke=BORDER)
    text(252, ry + 20, name, 11.5, TXT, 400, MONO)
    text(252, ry + 36, f"{meta}  —  {author}", 9.5, MUTED)
    rect(886, ry + 10, 48, 26, rx=8, stroke=PINK, sw=1.1, stroke_opacity=0.45)
    text(910, ry + 27, "+ Add", 9.5, PINK, 600, SANS, "middle")
    ry += 54

# --- chain panel ------------------------------------------------------------
rect(968, 56, 312, H - 56, fill=SIDEBAR)
line(968, 56, 968, H, BORDER)

text(992, 92, "ACTIVE CHAIN", 9, MUTED, 600, ls=1.4)
text(992, 120, "chipmunk", 19, TXT, 700)
rect(1216, 98, 44, 24, rx=12, fill=PINK)
circle(1248, 110, 8, fill="#2A0E1A", opacity=0.9)

CHAIN = [("pitch_shift", "semitones", "+7.0", 0.62),
         ("gain", "gain_db", "+0.0", 0.55)]
ey = 142
for name, param, val, pct in CHAIN:
    rect(992, ey, 264, 112, rx=10, fill=CARD, stroke=BORDER)
    drag_dots(1006, ey + 18, MUTED)
    text(1024, ey + 22, name, 11.5, TXT, 400, MONO)
    icon_close(1240, ey + 18, 8, MUTED)
    text(1008, ey + 50, param, 9.5, MUTED, 400, MONO)
    text(1240, ey + 50, val, 10.5, PINK, 400, MONO, "end")
    rect(1008, ey + 66, 232, 4, rx=2, fill=TRACK)
    rect(1008, ey + 66, 232 * pct, 4, rx=2, fill="url(#meter)")
    circle(1008 + 232 * pct, ey + 68, 6.5, fill="#F4F7FB", stroke=PINK, sw=1.5)
    text(1008, ey + 96, "drag to reorder · remove", 8, "#5A6371", 400, MONO)
    ey += 124

rect(992, ey + 2, 264, 40, rx=10, stroke=BORDER, dash="5 5")
text(1124, ey + 27, "+ add effect", 10.5, MUTED, 500, SANS, "middle")

line(992, 452, 1256, 452, BORDER)
text(992, 478, "STATS", 9, MUTED, 600, ls=1.4)
stats = [("algorithmic latency", "34.8 ms"), ("queued", "42 ms"),
         ("underruns", "2"), ("overruns", "0")]
sy = 504
for k, v in stats:
    text(992, sy, k, 10, MUTED)
    text(1256, sy, v, 10.5, TXT2, 400, MONO, "end")
    sy += 24

line(992, 604, 1256, 604, BORDER)
rect(992, 620, 128, 34, rx=9, fill=CARD, stroke=BORDER)
text(1056, 642, "Save preset", 10.5, TXT2, 500, SANS, "middle")
rect(1128, 620, 128, 34, rx=9, fill=PINK, fill_opacity=0.12, stroke=PINK,
     stroke_opacity=0.4)
text(1192, 642, "Export JSON", 10.5, PINK, 600, SANS, "middle")
text(992, 676, "effects/chipmunk.json", 9, "#4E5764", 400, MONO)

# --- bottom bar -------------------------------------------------------------
rect(0, 736, W, 64, fill=SIDEBAR)
line(0, 736, W, 736, BORDER)

icon_mic(26, 754, 16, TXT2)
text(52, 756, "INPUT", 8.5, MUTED, 600, ls=1.2)
text(52, 774, "default · 2ch 44.1kHz", 10.5, TXT)

rect(260, 748, 232, 40, rx=10, fill=PINK, fill_opacity=0.10, stroke=PINK,
     stroke_opacity=0.35)
icon_wave(276, 760, 16, PINK)
text(300, 758, "OUTPUT", 8.5, PINK, 600, ls=1.2)
text(300, 775, "openmod_vmic", 10.5, TXT, 400, MONO)

for i, (lbl, pct) in enumerate((("IN", 0.62), ("OUT", 0.48))):
    yy = 752 + i * 18
    text(540, yy + 4, lbl, 8.5, MUTED, 600, SANS, "end")
    rect(550, yy, 120, 5, rx=2.5, fill=TRACK)
    rect(550, yy, 120 * pct, 5, rx=2.5, fill="url(#meter)")

text(712, 774, "VOL", 8.5, MUTED, 600, SANS, "end")
rect(722, 766, 120, 5, rx=2.5, fill=TRACK)
rect(722, 766, 120 * 0.75, 5, rx=2.5, fill="#8A94A3")
circle(722 + 120 * 0.75, 768.5, 6, fill="#F4F7FB", stroke=TRACK, sw=1.2)

rect(862, 748, 124, 40, rx=10, fill=CARD, stroke=BORDER)
icon_headphones(876, 758, 16, TXT2)
text(902, 758, "MONITOR", 8.5, MUTED, 600, ls=1.1)
text(902, 775, "off", 10.5, TXT2, 400, MONO)

rect(1152, 748, 104, 40, rx=10, fill=PINK_D)
rect(1170, 762, 12, 12, rx=2, fill="#FFFFFF", opacity=0.95)
text(1190, 772, "Stop", 11.5, "#FFFFFF", 700)

out.append("</g></svg>")

svg = "\n".join(out)
with open("ui/mockup-main.svg", "w") as f:
    f.write(svg)
print("wrote ui/mockup-main.svg")
