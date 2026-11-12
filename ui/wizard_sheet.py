#!/usr/bin/env python3
"""Compose the first-launch wizard screens into one contact sheet.

Run after mockup.py + rsvg-convert (it embeds the 2x PNGs, not the SVGs):

    python3 ui/mockup.py
    for f in ui/mockup-wizard-*.svg; do rsvg-convert -z 2 "$f" -o "${f%.svg}.png"; done
    python3 ui/wizard_sheet.py
    rsvg-convert -z 1 ui/mockup-wizard-sheet.svg -o ui/mockup-wizard-sheet.png
"""

import os

os.chdir(os.path.dirname(os.path.abspath(__file__)))

PANELS = [
    ("mockup-wizard-1-welcome.png", "1 · welcome"),
    ("mockup-wizard-2-appearance.png", "2 · appearance (dark)"),
    ("mockup-wizard-3-vmic-windows.png", "3 · virtual mic — windows (one-press install)"),
    ("mockup-wizard-3-vmic-linux.png", "3 · virtual mic — linux (already done)"),
    ("mockup-wizard-4-import.png", "4 · import"),
    ("mockup-wizard-5-done.png", "5 · you're live"),
]

S = 0.5
PW, PH = int(1280 * S), int(800 * S)
GAP_X, GAP_Y = 28, 44
M = 40
COLS = 3
ROWS = (len(PANELS) + COLS - 1) // COLS
W = M * 2 + COLS * PW + (COLS - 1) * GAP_X
H = M + 70 + ROWS * (PH + GAP_Y) + 20

SANS = "Cantarell, DejaVu Sans, sans-serif"

body = [
    f'<text x="{M}" y="{M + 14}" fill="#ECEFF4" font-size="21" font-family="{SANS}" '
    f'font-weight="700">openmod · first launch</text>',
    f'<text x="{M}" y="{M + 38}" fill="#6E7A8A" font-size="11.5" font-family="{SANS}">'
    f'five calm steps — the only platform-specific one is the virtual mic '
    f'(a driver on Windows, free on Linux)</text>',
]

for i, (f, label) in enumerate(PANELS):
    col, row = i % COLS, i // COLS
    x = M + col * (PW + GAP_X)
    y = M + 70 + row * (PH + GAP_Y)
    body.append(f'<rect x="{x - 1}" y="{y - 1}" width="{PW + 2}" height="{PH + 2}" '
                f'rx="8" fill="none" stroke="#2A2F38"/>')
    body.append(f'<image x="{x}" y="{y}" width="{PW}" height="{PH}" href="{f}" '
                f'preserveAspectRatio="none"/>')
    body.append(f'<text x="{x}" y="{y + PH + 20}" fill="#8A94A3" font-size="10.5" '
                f'letter-spacing="0.4" font-family="{SANS}" font-weight="600">{label}</text>')

panels = "".join(body)
svg = (f'<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" '
       f'width="{W}" height="{H}" viewBox="0 0 {W} {H}">'
       f'<rect width="{W}" height="{H}" fill="#15181E"/>{panels}</svg>')

with open("mockup-wizard-sheet.svg", "w") as fh:
    fh.write(svg)
print("wrote ui/mockup-wizard-sheet.svg", W, "x", H)
