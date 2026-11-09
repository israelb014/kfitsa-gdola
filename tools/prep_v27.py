#!/usr/bin/env python3
"""v2.7: four animals to ride, one for each world, drawn in code in the flat style of the characters with the
same outline as the hero and the hats (tools/prep_art_v21.py). Original designs: a woolly ram, a camel with a
red blanket, a round penguin, and a pink long-legged bird. Each has stand, walk_a, walk_b and jump; the
penguin also has a belly slide, the bird wings spread to glide. Also the animal box and its picture on the map."""
import sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter
root = Path(sys.argv[1]); out = root/'Assets/Resources/Art'
OUTLINE = (67, 74, 95, 255)
WIDTH = 110          # pixels across, about 1.5 tiles: big enough to carry the rider

def outlined(im, w=3):
    pad = w + 2; c = Image.new('RGBA', (im.width+pad*2, im.height+pad*2), (0,0,0,0)); c.paste(im, (pad,pad), im)
    a = c.getchannel('A').filter(ImageFilter.MaxFilter(w*2+1)).point(lambda v: 255 if v > 90 else 0)
    o = Image.new('RGBA', c.size, OUTLINE); o.putalpha(a); o.alpha_composite(c); return o

class Pen:
    """Units: the picture is 2 wide and 1.6 high, the feet on y = 1.6, facing right."""
    S = 160
    def __init__(s):
        s.im = Image.new('RGBA', (2*s.S, int(1.7*s.S)), (0,0,0,0)); s.d = ImageDraw.Draw(s.im)
    def P(s, x, y): return (x*s.S, y*s.S)
    def ell(s, x0, y0, x1, y1, c): s.d.ellipse([*s.P(x0, y0), *s.P(x1, y1)], fill=c)
    def box(s, x0, y0, x1, y1, c, r=.05): s.d.rounded_rectangle([*s.P(x0, y0), *s.P(x1, y1)], radius=r*s.S, fill=c)
    def poly(s, pts, c): s.d.polygon([s.P(x, y) for x, y in pts], fill=c)
    def leg(s, x, top, swing, c, w=.11, foot=None):
        # a leg from its top to the ground, its foot shifted by swing
        s.poly([(x - w/2, top), (x + w/2, top), (x + w/2 + swing, 1.6), (x - w/2 + swing, 1.6)], c)
        if foot: s.ell(x - w/2 + swing - .02, 1.52, x + w/2 + swing + .06, 1.62, foot)
    def eye(s, x, y, r=.045):
        s.ell(x - r, y - r, x + r, y + r, (255, 255, 255)); s.ell(x - r*.45, y - r*.55, x + r*.75, y + r*.65, (40, 40, 52))
    def done(s, name):
        im = s.im.crop(s.im.getbbox())
        k = WIDTH / im.width * (s.width_scale if hasattr(s, 'width_scale') else 1)
        im = im.resize((max(1, int(im.width * k)), max(1, int(im.height * k))), Image.LANCZOS)
        im = outlined(im); im.save(out/f'{name}.png'); return im

SWING = {'stand': (0, 0), 'walk_a': (.09, -.09), 'walk_b': (-.09, .09), 'jump': (-.12, .12), 'slide': (0, 0), 'glide': (0, 0)}

def ram(frame):
    p = Pen(); a, b = SWING[frame]; up = frame == 'jump'
    dark = (78, 80, 96); wool = (246, 246, 238); shade = (224, 224, 214)
    lt = .98 if not up else .9
    for x, sw in ((.62, b), (.78, a), (1.18, b), (1.34, a)): p.leg(x, lt, sw * (.6 if up else 1), dark, foot=(54, 56, 68))
    for cx, cy, r in ((.62, .82, .24), (.85, .66, .27), (1.1, .66, .27), (1.32, .8, .24), (.97, .92, .3), (.7, 1.0, .2), (1.22, 1.0, .2)):
        p.ell(cx - r, cy - r, cx + r, cy + r, shade if cy > .9 else wool)
    for cx, cy, r in ((.85, .63, .2), (1.1, .63, .2)): p.ell(cx - r, cy - r, cx + r, cy + r, (252, 252, 246))
    p.ell(1.38, .5, 1.78, .88, dark)                       # the face
    p.ell(1.3, .44, 1.52, .66, (198, 142, 82)); p.ell(1.35, .5, 1.47, .61, (166, 112, 60))   # the curled horn
    p.eye(1.62, .64); p.ell(1.7, .78, 1.76, .82, (40, 40, 52))
    return p.done('ride_ram_' + frame)

def camel(frame):
    p = Pen(); a, b = SWING[frame]; up = frame == 'jump'
    tan = (220, 176, 114); shade = (190, 146, 88); red = (214, 64, 72)
    for x, sw in ((.58, b), (.74, a), (1.22, b), (1.38, a)): p.leg(x, 1.0, sw * (.6 if up else 1), shade if x in (.74, 1.38) else tan, w=.1, foot=(150, 110, 70))
    p.ell(.45, .66, 1.5, 1.14, tan)                         # body
    p.ell(.62, .42, 1.06, .9, tan)                          # the hump
    p.box(.98, .62, 1.36, .8, red, .04); p.box(.98, .76, 1.36, .82, (240, 196, 70), .02)   # the blanket, where the rider sits
    p.poly([(1.36, .84), (1.5, .78), (1.66, .36), (1.54, .3)], tan)   # neck
    p.ell(1.5, .2, 1.86, .44, tan); p.ell(1.74, .28, 1.9, .44, shade)  # head and muzzle
    p.ell(1.52, .14, 1.6, .26, shade)                       # ear
    p.eye(1.66, .3, .04)
    p.ell(.36, .78, .5, .98, shade)                         # tail tuft
    return p.done('ride_camel_' + frame)

def penguin(frame):
    p = Pen(); a, b = SWING[frame]
    black = (52, 56, 72); white = (246, 246, 250); orange = (250, 160, 60)
    slide = frame == 'slide'
    if slide:
        p.ell(.3, .96, 1.7, 1.58, black); p.ell(.42, 1.16, 1.62, 1.6, white)
        p.ell(1.42, .9, 1.82, 1.32, black); p.ell(1.52, 1.0, 1.74, 1.26, white)
        p.poly([(1.78, 1.08), (1.96, 1.14), (1.78, 1.2)], orange); p.eye(1.66, 1.06)
        p.poly([(.86, 1.12), (.5, .9), (.6, 1.2)], (40, 44, 58))
        p.ell(.14, 1.36, .34, 1.5, orange); p.ell(.18, 1.46, .38, 1.6, orange)
        return p.done('ride_penguin_slide')
    sa, sb = (a, b) if frame != 'jump' else (-.06, .06)
    p.ell(.78 + sa, 1.46, 1.02 + sa, 1.62, orange); p.ell(1.06 + sb, 1.46, 1.3 + sb, 1.62, orange)   # feet
    p.ell(.5, .5, 1.5, 1.52, black)                         # body
    p.ell(.7, .74, 1.46, 1.5, white)                        # belly
    p.ell(1.1, .3, 1.66, .84, black); p.ell(1.24, .44, 1.6, .8, white)   # head
    p.poly([(1.6, .56), (1.84, .62), (1.6, .7)], orange)
    p.eye(1.48, .56)
    p.poly([(.62, .8), (.42, 1.2), (.66, 1.14)], (40, 44, 58))   # flipper
    return p.done('ride_penguin_' + frame)

def bird(frame):
    p = Pen(); a, b = SWING[frame]; glide = frame == 'glide'
    pink = (236, 120, 186); deep = (200, 84, 156); orange = (250, 170, 60)
    if frame == 'jump' or glide: a, b = -.04, .04
    lt = 1.0
    for x, sw in ((.92, a), (1.06, b)): p.leg(x, lt, sw * 2, orange, w=.06, foot=orange)
    p.ell(.46, .42, 1.42, 1.06, pink)                       # body
    p.poly([(.5, .62), (.18, .5), (.28, .7), (.16, .84), (.5, .86)], deep)   # tail feathers
    p.ell(1.26, .16, 1.68, .56, pink)                       # head
    for i in range(3): p.ell(1.34 + i*.08, .04 + (i % 2)*.04, 1.44 + i*.08, .22, deep)   # tuft
    p.poly([(1.64, .32), (1.9, .38), (1.64, .46)], orange)
    p.eye(1.5, .32)
    if glide:
        p.poly([(.66, .62), (.4, .02), (1.0, .3), (1.14, .62)], deep)   # the wing up, spread to glide
    else:
        p.ell(.62, .58, 1.12, .88, deep)                    # the wing folded
    return p.done('ride_bird_' + frame)

for f in ('stand', 'walk_a', 'walk_b', 'jump'):
    ram(f); camel(f); penguin(f); bird(f)
penguin('slide'); bird('glide')

# Where the rider's feet go: height above the ground and x from the middle, as fractions of the stand picture
meta = []
for name, seat_y, seat_x in (('ram', .62, -.02), ('camel', .56, .02), ('penguin', .68, -.04), ('bird', .64, -.04)):
    im = Image.open(out/f'ride_{name}_stand.png')
    meta.append(f'{name} {seat_y:.3f} {seat_x:.3f} {im.width} {im.height}')
(out/'ride_meta.txt').write_text('\n'.join(meta) + '\n')

# The animal box: the pack's stone exclamation block, at the size of the other tiles
T = root/'_kenney/styles/new-platformer-pack-1.1/Sprites/Tiles/Double'
Image.open(T/'block_strong_exclamation.png').convert('RGBA').resize((72, 72), Image.LANCZOS).save(out/'block_animal.png')
print('v2.7 animals written')
