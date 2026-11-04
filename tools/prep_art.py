#!/usr/bin/env python3
"""Copies the Kenney sheets into Resources and derives two extras: teal surprise block, big hero frames."""
import sys, shutil, colorsys
from pathlib import Path
from PIL import Image
root = Path(sys.argv[1]); src = root/'_kenney/pixel-platformer/Tilemap'; out = root/'Assets/Resources/Art'; out.mkdir(parents=True, exist_ok=True)
for a, b in [('tilemap_packed.png','tiles.png'),('tilemap-characters_packed.png','chars.png'),('tilemap-backgrounds_packed.png','bgs.png')]:
    shutil.copy(src/a, out/b)
tiles = Image.open(src/'tilemap_packed.png').convert('RGBA'); chars = Image.open(src/'tilemap-characters_packed.png').convert('RGBA')
def tile(i): return tiles.crop((i%20*18, i//20*18, i%20*18+18, i//20*18+18))
def char(i): return chars.crop((i%9*24, i//9*24, i%9*24+24, i//9*24+24))
def hue(im, h):
    im = im.copy(); p = im.load()
    for y in range(im.height):
        for x in range(im.width):
            r,g,b,a = p[x,y]
            H,S,V = colorsys.rgb_to_hsv(r/255,g/255,b/255)
            if a and S > .35 and .03 < H < .2:   # recolour only the gold body, keep the slate outline
                r,g,b = colorsys.hsv_to_rgb(h + (H-.12)*.5, S, V); p[x,y] = (int(r*255),int(g*255),int(b*255),a)
    return im
# extras.png layout: 36px cells. 0,1 = big hero frames; 2 = surprise block (18px, top-left of the cell)
ex = Image.new('RGBA', (36*3, 36), (0,0,0,0))
for k, i in enumerate((6, 7)):
    c = char(i); print('hero', i, 'bbox', c.getbbox())
    ex.paste(c.resize((36,36), Image.NEAREST), (k*36, 0))
ex.paste(hue(tile(11), .47), (72, 0))
ex.save(out/'extras.png')
