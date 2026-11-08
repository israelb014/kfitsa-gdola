#!/usr/bin/env python3
"""Adds the hidden world of v2.4, "the land of colours": purple ground, a cloud sky, switches and blocks in four
colours, a bee, a ladybug and a fish, the jelly king in four colours, and the world's music."""
import sys, shutil, colorsys
from pathlib import Path
from PIL import Image
root = Path(sys.argv[1]); out = root/'Assets/Resources/Art'; aud = root/'Assets/Resources/Audio'
N = root/'_kenney/styles/new-platformer-pack-1.1/Sprites'; T = N/'Tiles/Double'; E = N/'Enemies/Double'; B = N/'Backgrounds/Double'
TILE = 72
def load(p): return Image.open(p).convert('RGBA')
def tile(name, dst=None): load(T/f'{name}.png').resize((TILE, TILE), Image.LANCZOS).save(out/f'{dst or name}.png')

for part in ('block', 'block_top', 'block_top_left', 'block_top_right', 'block_left', 'block_center', 'block_right',
             'block_bottom', 'block_bottom_left', 'block_bottom_right', 'horizontal_left', 'horizontal_middle', 'horizontal_right',
             'vertical_top', 'vertical_middle', 'vertical_bottom'):
    tile(f'terrain_purple_{part}', f't_purple_{part}')
for c in ('red', 'blue', 'green', 'yellow'):
    tile(f'block_{c}'); tile(f'switch_{c}'); tile(f'switch_{c}_pressed')
tile('grass_purple')

# The sky: the pack's clouds over a soft lilac, like the other skies baked to the full screen height
im = load(B/'background_clouds.png'); px = im.load()
for y in range(im.height):
    for x in range(im.width):
        r, g, b, a = px[x, y]
        if r > 245 and g > 245 and b > 245: px[x, y] = (r, g, b, 0)
base = Image.new('RGBA', im.size, (226, 212, 246, 255)); base.alpha_composite(im)
base.convert('RGB').resize((1080, 1080), Image.LANCZOS).save(out/'bg_clouds.png')

def enemy(name, dst=None, w=TILE):
    im = load(E/f'{name}.png').resize((w, w), Image.LANCZOS); bb = im.getbbox()
    im.crop((0, 0, w, bb[3])).save(out/f'{dst or name}.png')
for n in ('bee_a bee_b bee_rest ladybug_walk_a ladybug_walk_b ladybug_fly ladybug_rest fish_purple_up fish_purple_down fish_purple_rest').split():
    enemy(n)

# The jelly king in four colours: the fire slime of the first fight, its hue turned
HUES = {'red': None, 'blue': .6, 'green': .33, 'yellow': .13}
for frame in ('rest', 'walk_a', 'walk_b'):
    src = load(E/f'slime_fire_{frame}.png').resize((264, 264), Image.LANCZOS); bb = src.getbbox(); src = src.crop((0, 0, 264, bb[3]))
    for name, hue in HUES.items():
        im = src.copy(); p = im.load()
        if hue is not None:
            for y in range(im.height):
                for x in range(im.width):
                    r, g, b, a = p[x, y]
                    if not a: continue
                    H, S, V = colorsys.rgb_to_hsv(r/255, g/255, b/255)
                    if S > .3 and (H < .17 or H > .9):
                        r, g, b = colorsys.hsv_to_rgb(hue, S, V); p[x, y] = (int(r*255), int(g*255), int(b*255), a)
        im.save(out/f'king_{name}_{frame}.png')

shutil.copy(root/'_kenney/music/a/Juhani Junkala [Retro Game Music Pack] Level 1.wav', aud/'m_world4.wav')
print('v2.4 art and music written')
