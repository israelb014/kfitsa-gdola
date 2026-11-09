#!/usr/bin/env python3
"""v2.6: the coin shop's special outfits, made with the same pipeline as the v2.1 outfits (tools/prep_art_v21.py):
three hats drawn in the characters' flat style with the same outline, and three more pants colours.
The first try had green pants, lost against the green shirt, and brown was lost against the shoes, so they are grey."""
import sys, colorsys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter
root = Path(sys.argv[1]); out = root/'Assets/Resources/Art'
H = root/'_kenney/chars/platformer-characters/PNG/Player/Poses'
OUTLINE = (67, 74, 95, 255)
POSES = 'stand walk1 walk2 jump fall skid hurt duck cheer1 cheer2 action1 climb1 climb2'.split()
# form: height, shirt (hue, lift, saturation) as the earlier scripts made them
FORMS = {'s': (92, None), 'b': (144, None), 'p': (144, (.78, 0, 1)), 'f': (144, (.54, .12, .8))}
# pants: hue, saturation factor, value change; None hue = grey scale
PANTS = {'red': (.0, 1.05, -.05), 'black': (None, .0, -.55), 'white': (None, .0, .45), 'orange': (.075, 1.1, .05), 'pink': (.9, .75, .1)}

def load(p): return Image.open(p).convert('RGBA')

def outlined(im, w=3):
    pad = w + 2; c = Image.new('RGBA', (im.width+pad*2, im.height+pad*2), (0,0,0,0)); c.paste(im, (pad,pad), im)
    a = c.getchannel('A').filter(ImageFilter.MaxFilter(w*2+1)).point(lambda v: 255 if v > 90 else 0)
    o = Image.new('RGBA', c.size, OUTLINE); o.putalpha(a); o.alpha_composite(c); return o

def recolor(im, h0, h1, to, lift=0.0, sat=1.0):
    im = im.copy(); p = im.load()
    for y in range(im.height):
        for x in range(im.width):
            r,g,b,a = p[x,y]
            if not a: continue
            Hh,Sa,V = colorsys.rgb_to_hsv(r/255,g/255,b/255)
            if h0 < Hh < h1 and Sa > .25:
                r,g,b = colorsys.hsv_to_rgb(to, Sa*sat, min(1, V+lift)); p[x,y] = (int(r*255),int(g*255),int(b*255),a)
    return im

def pants(im, hue, sat, dv):
    im = im.copy(); p = im.load()
    for y in range(im.height):
        for x in range(im.width):
            r,g,b,a = p[x,y]
            if not a: continue
            Hh,Sa,V = colorsys.rgb_to_hsv(r/255,g/255,b/255)
            if .52 < Hh < .64 and Sa > .25:
                v = max(0, min(1, V + dv))
                r,g,b = colorsys.hsv_to_rgb(hue if hue is not None else 0, Sa*sat, v); p[x,y] = (int(r*255),int(g*255),int(b*255),a)
    return im

stand = load(H/'player_stand.png'); body = stand.getbbox()[3] - stand.getbbox()[1]
def hero(pose, form, pant=None):
    height, shirt = FORMS[form]
    im = load(H/f'player_{pose}.png')
    if pant: im = pants(im, *PANTS[pant])
    if shirt: im = recolor(im, .22, .50, *shirt)
    k = height / body; im = im.resize((int(im.width*k), int(im.height*k)), Image.LANCZOS)
    return outlined(im)

# 2. Where the head is: the hair, found by its colour in the top half of each picture
def hair(im):
    p = im.load(); bb = im.getbbox(); pts = []
    for y in range(bb[1], bb[1] + (bb[3]-bb[1]) * 55 // 100):
        for x in range(im.width):
            r,g,b,a = p[x,y]
            if a < 200: continue
            Hh,Sa,V = colorsys.rgb_to_hsv(r/255,g/255,b/255)
            if .05 < Hh < .12 and Sa > .45 and .6 < V < .85: pts.append((x, y))
    top = min(y for _, y in pts); bot = max(y for _, y in pts)
    band = [x for x, y in pts if abs(y - (top + (bot-top) * .3)) < 2]
    return (min(band) + max(band)) / 2, im.height - top, bot - top, max(band) - min(band)

# 3. The hats, drawn large in flat colours with one darker shade, then shrunk and outlined like the hero
def hat(name, wpx):
    S = 8; W = int(wpx * S); im = Image.new('RGBA', (W*2, W*2), (0,0,0,0)); d = ImageDraw.Draw(im)
    def box(x0, y0, x1, y1, c): d.rectangle([x0*W, y0*W, x1*W, y1*W], fill=c)
    def ell(x0, y0, x1, y1, c): d.ellipse([x0*W, y0*W, x1*W, y1*W], fill=c)
    def poly(pts, c): d.polygon([(x*W, y*W) for x, y in pts], fill=c)
    # units: the hat is 1 wide, x from .5 to 1.5; its brim is at y = 1.9
    if name == 'beanie':
        ell(.5, .95, 1.5, 2.3, (226, 76, 76)); ell(.62, 1.0, 1.0, 1.6, (238, 110, 105))
        box(.46, 1.62, 1.54, 1.92, (186, 52, 60))
        for i in range(6): box(.52 + i*.17, 1.66, .58 + i*.17, 1.88, (170, 45, 54))
        ell(.86, .72, 1.14, 1.0, (248, 248, 248))
        d.rectangle([0, 1.92*W, W*2, W*2], fill=(0,0,0,0))
    elif name == 'party':
        poly([(.62, 1.9), (1.0, .4), (1.38, 1.9)], (255, 196, 60))
        for t0 in (.65, 1.1, 1.55):
            t1 = t0 + .22; k0 = (t0 - .4) / 1.5; k1 = (t1 - .4) / 1.5
            poly([(1 - .38*k0, t0), (1 + .38*k0, t0), (1 + .38*k1, t1), (1 - .38*k1, t1)], (236, 88, 140))
        ell(.9, .3, 1.1, .5, (90, 190, 240))
    elif name == 'top':
        box(.66, .95, 1.34, 1.8, (62, 64, 80)); box(.66, .95, .78, 1.8, (84, 87, 108))
        box(.66, 1.56, 1.34, 1.72, (214, 64, 72))
        box(.45, 1.78, 1.55, 1.9, (62, 64, 80))
    elif name == 'chef':
        box(.66, 1.2, 1.34, 1.9, (246, 246, 250)); box(.66, 1.72, 1.34, 1.9, (222, 226, 236))
        for x0, y0, s in ((.5, 1.0, .5), (.75, .78, .5), (1.0, 1.0, .5)):
            ell(x0, y0, x0 + s, y0 + s, (246, 246, 250))
    elif name == 'crown':
        poly([(.55, 1.9), (.55, 1.3), (.73, 1.55), (1.0, 1.12), (1.27, 1.55), (1.45, 1.3), (1.45, 1.9)], (255, 200, 60))
        box(.55, 1.68, 1.45, 1.9, (232, 166, 40))
        ell(.93, 1.36, 1.07, 1.5, (226, 70, 80)); ell(.66, 1.74, .76, 1.84, (80, 170, 235)); ell(1.24, 1.74, 1.34, 1.84, (80, 170, 235))
    elif name == 'wizard':
        poly([(.58, 1.86), (1.12, .3), (1.42, 1.86)], (88, 96, 214))
        poly([(1.12, .3), (1.24, .62), (1.02, .7)], (70, 76, 180))
        for sx, sy in ((.95, 1.2), (1.18, 1.5), (1.08, .85)):
            poly([(sx, sy - .08), (sx + .03, sy - .02), (sx + .09, sy), (sx + .03, sy + .02), (sx, sy + .08), (sx - .03, sy + .02), (sx - .09, sy), (sx - .03, sy - .02)], (255, 214, 80))
        box(.4, 1.76, 1.6, 1.92, (70, 76, 180))
    elif name == 'pirate':
        poly([(.4, 1.9), (.5, 1.25), (1.0, 1.05), (1.5, 1.25), (1.6, 1.9), (1.0, 1.7)], (52, 54, 66))
        poly([(.48, 1.82), (1.0, 1.62), (1.52, 1.82), (1.0, 1.7)], (226, 180, 60))
        ell(.88, 1.24, 1.12, 1.48, (255, 204, 70)); ell(.93, 1.29, 1.07, 1.43, (232, 166, 40))
    elif name == 'cowboy':
        ell(.3, 1.72, 1.7, 2.02, (150, 96, 54)); box(.3, 1.87, 1.7, 2.05, (0, 0, 0, 0))
        poly([(.66, 1.82), (.7, 1.12), (.88, 1.02), (1.0, 1.14), (1.12, 1.02), (1.3, 1.12), (1.34, 1.82)], (176, 116, 66))
        box(.67, 1.58, 1.33, 1.72, (96, 58, 34))
    im = im.crop(im.getbbox()); im = im.resize((max(1, im.width // S), max(1, im.height // S)), Image.LANCZOS)
    return outlined(im)


# v2.6: the coin shop's special outfits: three more hats and three more pants colours, in the same style
stand_b = load(out/'hero_b_stand.png'); stand_s = load(out/'hero_s_stand.png')
widths = {'b': hair(stand_b)[3], 's': hair(stand_s)[3]}
HATS = ('wizard', 'pirate', 'cowboy')
for name in HATS:
    hat(name, widths['b'] * 1.12).save(out/f'hat_{name}.png')
    hat(name, widths['s'] * 1.12).save(out/f'hat_{name}_s.png')
SHOP_PANTS = {'gold': (.12, 1.0, .12), 'purple': (.78, .9, -.02), 'gray': (None, .0, -.12)}
PANTS.update(SHOP_PANTS)
for form in FORMS:
    for pose in POSES:
        for pant in SHOP_PANTS:
            hero(pose, form, pant).save(out/f'hero_{form}_{pose}_{pant}.png')
for pant in SHOP_PANTS:
    im = load(out/f'hero_b_stand_{pant}.png'); bb = im.getbbox(); h = bb[3] - bb[1]
    im.crop((bb[0], bb[1] + int(h * .64), bb[2], bb[3])).save(out/f'pants_{pant}.png')
print('v2.6 shop outfits written:', HATS, list(SHOP_PANTS), 'hair width', widths)
