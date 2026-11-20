"""
Galia icon generator (flat version).

A Galia melon cut in half, seen face-on: golden rind ring, pale-green flesh,
and a play button sitting in the seed cavity. Four flat colors, no texture,
so it holds up from 16px to 1024px.

Writes galia.svg (source of truth), PNGs at common sizes, and galia.ico.

    python make_galia_icon.py                      # this folder only
    python make_galia_icon.py <app Assets folder>  # also writes the WinUI asset set
"""
import math
import os
from PIL import Image, ImageDraw

OUT = os.path.dirname(os.path.abspath(__file__))

# Geometry on a 100x100 canvas
CX = CY = 50
R_RIND = 47       # outer edge (gold rind)
R_FLESH = 39      # flesh starts
R_CAVITY = 27     # seed cavity
TRI = 15.5        # play triangle circumradius
TRI_ROUND = 2.6   # corner rounding
TRI_SHIFT = -0.9  # centroid sits ~1 unit left of centre; bounding box ~1.7 right. Reads as optically centred

GOLD = "#E2A33A"
FLESH = "#A8DB6E"
CAVITY = "#F3F7C8"
PLAY = "#2E7A3E"


def triangle(scale=1.0, inset=0.0):
    """Right-pointing equilateral triangle, optionally inset (for rounding)."""
    r = TRI - inset * 2  # inset of d along normals == shrinking circumradius by 2d
    cx = CX + TRI_SHIFT
    return [((cx + r * math.cos(math.radians(a))) * scale,
             (CY + r * math.sin(math.radians(a))) * scale) for a in (0, 120, 240)]


def write_svg():
    pts = triangle(inset=TRI_ROUND)
    path = " ".join(f"{x:.2f},{y:.2f}" for x, y in pts)
    svg = f'''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
  <circle cx="{CX}" cy="{CY}" r="{R_RIND}" fill="{GOLD}"/>
  <circle cx="{CX}" cy="{CY}" r="{R_FLESH}" fill="{FLESH}"/>
  <circle cx="{CX}" cy="{CY}" r="{R_CAVITY}" fill="{CAVITY}"/>
  <polygon points="{path}" fill="{PLAY}" stroke="{PLAY}" stroke-width="{TRI_ROUND * 2}" stroke-linejoin="round"/>
</svg>
'''
    open(os.path.join(OUT, "galia.svg"), "w").write(svg)


def render(size):
    ss = 8
    k = size * ss / 100
    img = Image.new("RGBA", (size * ss,) * 2, (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    def circle(r, fill):
        d.ellipse([(CX - r) * k, (CY - r) * k, (CX + r) * k, (CY + r) * k], fill=fill)

    circle(R_RIND, GOLD)
    circle(R_FLESH, FLESH)
    circle(R_CAVITY, CAVITY)
    pts = triangle(scale=k, inset=TRI_ROUND)
    d.polygon(pts, fill=PLAY)
    w = TRI_ROUND * 2 * k
    d.line(pts + [pts[0]], fill=PLAY, width=round(w), joint="curve")
    rr = TRI_ROUND * k
    for x, y in pts:
        d.ellipse([x - rr, y - rr, x + rr, y + rr], fill=PLAY)
    return img.resize((size, size), Image.LANCZOS)


def tile(w, h, icon_frac):
    """Icon centred on a transparent w x h canvas, sized to icon_frac of the short side."""
    canvas = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    ic = render(round(min(w, h) * icon_frac))
    canvas.alpha_composite(ic, ((w - ic.width) // 2, (h - ic.height) // 2))
    return canvas


def write_ico(path):
    sizes = (16, 24, 32, 48, 64, 128, 256)
    frames = [render(s) for s in sizes]
    frames[-1].save(path, sizes=[(s, s) for s in sizes], append_images=frames[:-1])


def export_app_assets(assets_dir):
    """Write the icon set a WinUI 3 app uses (file names match the VS template)."""
    j = lambda n: os.path.join(assets_dir, n)
    write_ico(j("Galia.ico"))
    render(512).save(j("Galia.png"))
    render(300).save(j("Square150x150Logo.scale-200.png"))
    render(88).save(j("Square44x44Logo.scale-200.png"))
    render(24).save(j("Square44x44Logo.targetsize-24_altform-unplated.png"))
    render(48).save(j("LockScreenLogo.scale-200.png"))
    render(50).save(j("StoreLogo.png"))
    tile(620, 300, 0.8).save(j("Wide310x150Logo.scale-200.png"))
    tile(1240, 600, 0.6).save(j("SplashScreen.scale-200.png"))


if __name__ == "__main__":
    import sys
    write_svg()
    for s in (1024, 512, 256, 192, 150, 128, 64, 48, 44, 32, 24, 16):
        render(s).save(os.path.join(OUT, f"galia-{s}.png"))
    write_ico(os.path.join(OUT, "galia.ico"))
    if len(sys.argv) > 1:          # python make_galia_icon.py <WinUI Assets folder>
        export_app_assets(sys.argv[1])
    print("done")
