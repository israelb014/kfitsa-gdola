#!/usr/bin/env python3
"""v2.9: the holiday levels. Everything drawn in code in the characters' flat style with the same outline as the
hero, the hats and the animals: for Hanukkah a hanukkiah, a candle and an oil jug; for Purim a mask and a
noisemaker; for Pesach a matzah and a cup; for Sukkot a sukkah, a lulav and an etrog; for Rosh Hashana an apple,
a honey jar, a pomegranate and a shofar. Small ones replace the coins in their level; large ones are decoration."""
import sys, math
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter
root = Path(sys.argv[1]); out = root/'Assets/Resources/Art'
OUTLINE = (67, 74, 95, 255)

def outlined(im, w=3):
    pad = w + 2; c = Image.new('RGBA', (im.width+pad*2, im.height+pad*2), (0,0,0,0)); c.paste(im, (pad,pad), im)
    a = c.getchannel('A').filter(ImageFilter.MaxFilter(w*2+1)).point(lambda v: 255 if v > 90 else 0)
    o = Image.new('RGBA', c.size, OUTLINE); o.putalpha(a); o.alpha_composite(c); return o

class Pen:
    """Units 0..1 across, 0..h down; drawn eight times larger, then shrunk to the width asked."""
    S = 400
    def __init__(s, h=1.0):
        s.im = Image.new('RGBA', (s.S, int(h*s.S)), (0,0,0,0)); s.d = ImageDraw.Draw(s.im)
    def P(s, x, y): return (x*s.S, y*s.S)
    def ell(s, x0, y0, x1, y1, c): s.d.ellipse([*s.P(x0, y0), *s.P(x1, y1)], fill=c)
    def box(s, x0, y0, x1, y1, c, r=.03): s.d.rounded_rectangle([*s.P(x0, y0), *s.P(x1, y1)], radius=r*s.S, fill=c)
    def poly(s, pts, c): s.d.polygon([s.P(x, y) for x, y in pts], fill=c)
    def line(s, pts, c, w): s.d.line([s.P(x, y) for x, y in pts], fill=c, width=int(w*s.S))
    def save(s, name, width, line=3):
        im = s.im.crop(s.im.getbbox()); k = width / im.width
        im = im.resize((max(1, int(im.width*k)), max(1, int(im.height*k))), Image.LANCZOS)
        outlined(im, line).save(out/f'{name}.png')

GOLD, GOLD_D = (250, 200, 70), (220, 160, 40)
FLAME, FLAME_I = (255, 150, 40), (255, 226, 110)

def flame(p, cx, top, w=.05):
    p.poly([(cx, top - w*2.2), (cx + w, top), (cx, top + w*.6), (cx - w, top)], FLAME)
    p.poly([(cx, top - w*1.1), (cx + w*.45, top), (cx, top + w*.3), (cx - w*.45, top)], FLAME_I)

# ---- Hanukkah ----
p = Pen(1.1)
for i in range(9):
    x = .1 + i * .1; tall = i == 4
    top = .18 if tall else .3
    p.box(x - .025, top, x + .025, .62, (120, 170, 240) if not tall else (240, 240, 250), .01)
    flame(p, x, top - .01, .03)
    p.box(x - .04, .6, x + .04, .65, GOLD_D, .01)
p.box(.06, .62, .94, .67, GOLD, .02); p.box(.47, .67, .53, .95, GOLD); p.poly([(.3, 1.05), (.7, 1.05), (.58, .93), (.42, .93)], GOLD_D)
p.save('h_hanukkiah', 150)
p = Pen(1.6); p.box(.38, .5, .62, 1.55, (120, 170, 240), .04); p.box(.38, .5, .62, .6, (160, 200, 250), .04); flame(p, .5, .45, .12); p.save('h_candle', 34)
p = Pen(1.1); p.ell(.18, .38, .82, 1.05, (196, 132, 74)); p.box(.36, .18, .64, .48, (196, 132, 74)); p.box(.3, .12, .7, .22, (160, 100, 54))
p.ell(.3, .55, .7, .85, (226, 168, 98)); p.line([(.66, .3), (.86, .36), (.84, .6), (.74, .66)], (160, 100, 54), .06); p.save('h_oil', 44)

# ---- Purim ----
p = Pen(.6)
p.poly([(.02, .12), (.5, .2), (.98, .12), (.92, .42), (.6, .52), (.5, .42), (.4, .52), (.08, .42)], (226, 70, 150))
p.ell(.2, .2, .4, .36, (40, 40, 52)); p.ell(.6, .2, .8, .36, (40, 40, 52))
for x in (.08, .92): p.ell(x - .05, .02, x + .05, .12, GOLD)
p.save('h_mask', 46)
p = Pen(1.3); p.box(.44, .7, .56, 1.28, (176, 116, 66)); p.box(.2, .18, .8, .74, (90, 180, 240), .06)
for i in range(4): p.box(.24, .24 + i * .12, .76, .3 + i * .12, (60, 140, 210), .02)
p.box(.56, .4, .9, .5, (176, 116, 66)); p.save('h_gragger', 40)
p = Pen(1.1); p.save  # balloons for the decoration
for cx, c in ((.3, (236, 90, 90)), (.55, (90, 180, 240)), (.78, GOLD)):
    p.ell(cx - .15, .05, cx + .15, .45, c); p.line([(cx, .45), (cx + .04, .7), (.5, 1.05)], (90, 90, 110), .015)
p.save('h_balloons', 90, 2)

# ---- Pesach ----
p = Pen(.85); p.box(.04, .04, .96, .8, (238, 206, 150), .06)
for y in (.2, .4, .6):
    for x in (.16, .32, .48, .64, .8): p.ell(x - .025, y - .025, x + .025, y + .025, (196, 150, 90))
for x in (.25, .5, .75): p.line([(x, .08), (x, .76)], (220, 180, 120), .02)
p.save('h_matzah', 46)
p = Pen(1.3); p.poly([(.14, .06), (.86, .06), (.74, .5), (.26, .5)], (200, 200, 216)); p.poly([(.2, .14), (.8, .14), (.72, .42), (.28, .42)], (150, 40, 70))
p.box(.45, .48, .55, 1.0, (200, 200, 216)); p.box(.24, 1.0, .76, 1.1, (176, 176, 196), .03); p.save('h_cup', 40)
p = Pen(1.0); p.box(.06, .5, .94, .62, (238, 206, 150), .03); p.box(.1, .62, .9, .74, (226, 192, 136), .03); p.box(.04, .74, .96, .86, (238, 206, 150), .03)
p.box(.08, .86, .92, .98, (226, 192, 136), .03); p.save('h_matzot', 110)

# ---- Sukkot ----
p = Pen(1.0)
p.box(.06, .2, .12, 1.0, (166, 112, 62)); p.box(.88, .2, .94, 1.0, (166, 112, 62)); p.box(.47, .2, .53, 1.0, (166, 112, 62))
p.box(.12, .38, .88, .98, (238, 222, 190)); p.box(.12, .38, .88, .44, (222, 200, 160))
for i in range(11): p.poly([(.0 + i*.1, .24), (.08 + i*.1, .06), (.16 + i*.1, .24)], (70, 160, 90) if i % 2 else (90, 190, 100))
p.box(0, .18, 1, .26, (60, 140, 80), .02)
for x, c in ((.25, (236, 90, 90)), (.5, GOLD), (.72, (150, 110, 220))): p.line([(x, .26), (x, .36)], (90, 90, 110), .008); p.ell(x - .04, .36, x + .04, .44, c)
p.save('h_sukkah', 230)
p = Pen(1.8); p.poly([(.44, .02), (.56, .02), (.6, 1.4), (.4, 1.4)], (80, 170, 90)); p.line([(.5, .06), (.5, 1.4)], (60, 140, 72), .03)
for s in (-1, 1): p.poly([(.5, .9), (.5 + s*.3, .7), (.5 + s*.34, 1.1)], (70, 150, 80))
p.box(.38, 1.38, .62, 1.75, (166, 112, 62), .04); p.save('h_lulav', 34)
p = Pen(1.0); p.ell(.08, .16, .92, .96, (238, 214, 80)); p.ell(.2, .26, .5, .5, (250, 236, 140)); p.box(.46, .02, .54, .2, (120, 150, 70)); p.save('h_etrog', 40)

# ---- Rosh Hashana ----
p = Pen(1.0); p.ell(.06, .2, .94, 1.0, (226, 60, 70)); p.ell(.2, .3, .44, .5, (246, 120, 120)); p.box(.47, .02, .55, .26, (130, 90, 50))
p.ell(.55, .02, .9, .22, (90, 180, 90)); p.save('h_apple', 42)
p = Pen(1.15); p.box(.14, .3, .86, 1.1, (246, 186, 60), .1); p.box(.1, .18, .9, .34, (200, 120, 60), .03)
p.box(.22, .52, .78, .86, (255, 250, 240), .03); p.line([(.5, .02), (.5, .2)], (166, 112, 62), .06); p.ell(.42, .0, .58, .1, (166, 112, 62))
p.save('h_honey', 46)
p = Pen(1.0); p.ell(.08, .18, .92, 1.0, (190, 40, 70)); p.poly([(.38, .22), (.42, .04), (.5, .14), (.58, .04), (.62, .22)], (160, 30, 60))
p.ell(.24, .34, .46, .52, (226, 90, 110)); p.save('h_pomegranate', 44)
p = Pen(.6)
# A smooth horn: a curved centre line, thin at the mouthpiece and wide at the bell
c = [(.04 + .92 * t, .45 - .36 * math.sin(t * math.pi * .9)) for t in [i / 30 for i in range(31)]]
up, dn = [], []
for i, (x, y) in enumerate(c):
    j = min(i + 1, 30); k = max(i - 1, 0); dx, dy = c[j][0] - c[k][0], c[j][1] - c[k][1]; n = math.hypot(dx, dy)
    r = .02 + .1 * (i / 30) ** 1.8
    up.append((x - dy / n * r, y + dx / n * r)); dn.append((x + dy / n * r, y - dx / n * r))
p.poly(up + dn[::-1], (226, 196, 150))
p.ell(c[-1][0] - .11, c[-1][1] - .12, c[-1][0] + .06, c[-1][1] + .12, (190, 150, 104))
p.save('h_shofar', 120)
print('v2.9 holiday art written')
