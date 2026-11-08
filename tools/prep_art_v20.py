#!/usr/bin/env python3
"""Adds the frost giant (v2.0): the block slime at the boss's size, turned icy blue."""
import sys, colorsys
from pathlib import Path
from PIL import Image
root = Path(sys.argv[1]); out = root/'Assets/Resources/Art'
E = root/'_kenney/styles/new-platformer-pack-1.1/Sprites/Enemies/Double'
def icy(im):
    p = im.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = p[x, y]
            if not a: continue
            H, S, V = colorsys.rgb_to_hsv(r/255, g/255, b/255)
            if S > .2:
                r, g, b = colorsys.hsv_to_rgb(.56, S * .55, min(1, V * .9 + .15)); p[x, y] = (int(r*255), int(g*255), int(b*255), a)
    return im
for n in ('rest', 'walk_a', 'walk_b'):
    im = Image.open(E/f'slime_block_{n}.png').convert('RGBA').resize((264, 264), Image.LANCZOS); bb = im.getbbox()
    icy(im.crop((0, 0, 264, bb[3]))).save(out/f'giant_{n}.png')
print('v2.0 art written')
