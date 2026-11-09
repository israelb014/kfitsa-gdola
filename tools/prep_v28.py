#!/usr/bin/env python3
"""v2.8: the three bonus levels. A mine cart and rails drawn in code in the characters' flat style with the same
outline; a sea background in the same soft style as the pack's skies; the pack's blue fish and saw; a deep
stone tile for the mine, and the music of the three levels."""
import sys, shutil, math
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter
root = Path(sys.argv[1]); out = root/'Assets/Resources/Art'; aud = root/'Assets/Resources/Audio'
N = root/'_kenney/styles/new-platformer-pack-1.1/Sprites'; T = N/'Tiles/Double'; E = N/'Enemies/Double'; B = N/'Backgrounds/Double'
OUTLINE = (67, 74, 95, 255); TILE = 72

def outlined(im, w=3):
    pad = w + 2; c = Image.new('RGBA', (im.width+pad*2, im.height+pad*2), (0,0,0,0)); c.paste(im, (pad,pad), im)
    a = c.getchannel('A').filter(ImageFilter.MaxFilter(w*2+1)).point(lambda v: 255 if v > 90 else 0)
    o = Image.new('RGBA', c.size, OUTLINE); o.putalpha(a); o.alpha_composite(c); return o

S = 8
def canvas(w, h): im = Image.new('RGBA', (w*S, h*S), (0,0,0,0)); return im, ImageDraw.Draw(im)
def shrink(im): im = im.crop(im.getbbox()); return im.resize((max(1, im.width // S), max(1, im.height // S)), Image.LANCZOS)

# The mine cart, about 1.4 tiles wide: a grey tub with a darker rim and two wheels; two frames, the wheels turned
for k in range(2):
    im, d = canvas(110, 70)
    d.polygon([(6*S, 8*S), (104*S, 8*S), (94*S, 50*S), (16*S, 50*S)], fill=(150, 156, 172))
    d.rectangle([4*S, 4*S, 106*S, 14*S], fill=(110, 116, 132))
    for x in (30, 55, 80): d.rectangle([(x-2)*S, 18*S, (x+2)*S, 46*S], fill=(128, 134, 150))
    for cx in (30, 80):
        d.ellipse([(cx-13)*S, 44*S, (cx+13)*S, 70*S], fill=(70, 72, 86)); d.ellipse([(cx-6)*S, 51*S, (cx+6)*S, 63*S], fill=(176, 180, 192))
        a = k * math.pi / 4
        for t in (a, a + math.pi / 2):
            d.line([((cx - 10*math.cos(t))*S, (57 - 10*math.sin(t))*S), ((cx + 10*math.cos(t))*S, (57 + 10*math.sin(t))*S)], fill=(176, 180, 192), width=3*S)
    outlined(shrink(im)).save(out/f'cart_{k}.png')

# Rails: a tile-wide piece of track, sleepers under two steel lines, sitting at the bottom of its cell
im, d = canvas(72, 22)
for x in (4, 28, 52): d.rectangle([x*S, 10*S, (x+14)*S, 22*S], fill=(150, 104, 62))
d.rectangle([0, 4*S, 72*S, 10*S], fill=(170, 176, 190)); d.rectangle([0, 9*S, 72*S, 11*S], fill=(120, 126, 140))
r = im.resize((72, 22), Image.LANCZOS); t = Image.new('RGBA', (TILE, TILE), (0,0,0,0)); t.paste(r, (0, TILE - 22), r); t.save(out/'rail.png')

# The sea: a deep blue that lightens toward the top, slanting light, and seaweed and bubbles in soft flat shapes
W = 1080; im = Image.new('RGB', (W, W)); d = ImageDraw.Draw(im, 'RGBA')
for y in range(W):
    k = y / W; d.line([(0, y), (W, y)], fill=(int(120 - 70*k), int(196 - 90*k), int(236 - 70*k)))
for x0 in (80, 360, 700, 930): d.polygon([(x0, 0), (x0 + 90, 0), (x0 - 120, W), (x0 - 260, W)], fill=(255, 255, 255, 22))
for x0, h in ((60, 300), (240, 420), (520, 260), (760, 380), (980, 320)):
    for i in range(3):
        cx = x0 + i * 22
        pts = [(cx + math.sin(y / 60 + i) * 18, W - y) for y in range(0, h, 12)]
        d.line(pts, fill=(46, 120, 110, 150), width=16)
for cx, cy, rr in ((150, 600, 14), (180, 520, 9), (640, 420, 12), (860, 700, 16), (420, 300, 10), (900, 260, 8)):
    d.ellipse([cx - rr, cy - rr, cx + rr, cy + rr], outline=(230, 246, 255, 170), width=3)
im.save(out/'bg_sea.png')

# The mine: the night sky of world 2 darkened to rock, so the mine reads as underground
im = Image.open(out/'bg_night.png').convert('RGB'); px = im.load()
for y in range(im.height):
    for x in range(im.width):
        r, g, b = px[x, y]; v = (r + g + b) // 3; px[x, y] = (int(v*.55 + 40), int(v*.45 + 30), int(v*.4 + 26))
im.save(out/'bg_mine.png')

def enemy(name, dst=None, w=TILE):
    im = Image.open(E/f'{name}.png').convert('RGBA').resize((w, w), Image.LANCZOS); bb = im.getbbox()
    im.crop((0, 0, w, bb[3])).save(out/f'{dst or name}.png')
for n in ('fish_blue_swim_a', 'fish_blue_swim_b', 'fish_blue_rest', 'saw_a', 'saw_b'): enemy(n)

for name, dst in (('Juhani Junkala [Retro Game Music Pack] Level 2.wav', 'm_bonus.wav'),):
    shutil.copy(root/'_kenney/music/a'/name, aud/dst)
print('v2.8 art and music written')
