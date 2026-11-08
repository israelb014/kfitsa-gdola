#!/usr/bin/env python3
"""Adds the art and music of world 3, without touching what the earlier scripts made."""
import sys, shutil, colorsys
from pathlib import Path
from PIL import Image, ImageFilter
root = Path(sys.argv[1]); out = root/'Assets/Resources/Art'; aud = root/'Assets/Resources/Audio'
N = root/'_kenney/styles/new-platformer-pack-1.1/Sprites'; T = N/'Tiles/Double'; E = N/'Enemies/Double'; B = N/'Backgrounds/Double'
TILE = 72; OUTLINE = (67, 74, 95, 255)
def load(p): return Image.open(p).convert('RGBA')
def tile(name, dst=None): load(T/f'{name}.png').resize((TILE, TILE), Image.LANCZOS).save(out/f'{dst or name}.png')

# Snow ground, the same parts as the other terrains, plus the snow cloud for the sinking clouds
for part in ('block', 'block_top', 'block_top_left', 'block_top_right', 'block_left', 'block_center', 'block_right',
             'block_bottom', 'block_bottom_left', 'block_bottom_right', 'horizontal_left', 'horizontal_middle', 'horizontal_right',
             'vertical_top', 'vertical_middle', 'vertical_bottom'):
    tile(f'terrain_snow_{part}', f't_snow_{part}')
for part in ('cloud', 'cloud_left', 'cloud_middle', 'cloud_right'):
    tile(f'terrain_snow_{part}', f'snow_{part}')
for n in ('ladder_top', 'ladder_middle', 'ladder_bottom', 'rope', 'snow'):
    tile(n)

# The snowy sky: the hills turned white and blue
im = load(B/'background_color_hills.png'); px = im.load(); sky = (205, 228, 248)
for y in range(im.height):
    for x in range(im.width):
        r, g, b, a = px[x, y]
        if r > 245 and g > 245 and b > 245: px[x, y] = (r, g, b, 0); continue
        v = (r + g + b) / 765
        px[x, y] = (int(150 + 100 * v), int(175 + 75 * v), int(205 + 50 * v), a)
base = Image.new('RGBA', im.size, sky + (255,)); base.alpha_composite(im)
base.convert('RGB').resize((1080, 1080), Image.LANCZOS).save(out/'bg_snow.png')

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
            H,Sa,V = colorsys.rgb_to_hsv(r/255,g/255,b/255)
            if h0 < H < h1 and Sa > .25:
                r,g,b = colorsys.hsv_to_rgb(to, Sa*sat, min(1, V+lift)); p[x,y] = (int(r*255),int(g*255),int(b*255),a)
    return im

# Climbing poses for every form, on the same canvas and scale as the other poses
H = root/'_kenney/chars/platformer-characters/PNG/Player/Poses'
stand = load(H/'player_stand.png'); body = stand.getbbox()[3] - stand.getbbox()[1]
for form, height, shirt in (('s', 92, None), ('b', 144, None), ('p', 144, (.78, 0, 1)), ('f', 144, (.54, .12, .8))):
    for pose in ('climb1', 'climb2'):
        im = load(H/f'player_{pose}.png')
        if shirt: im = recolor(im, .22, .50, shirt[0], shirt[1], shirt[2])
        k = height / body; im = im.resize((int(im.width*k), int(im.height*k)), Image.LANCZOS)
        outlined(im).save(out/f'hero_{form}_{pose}.png')

def enemy(name, dst=None, w=TILE):
    im = load(E/f'{name}.png').resize((w, w), Image.LANCZOS); bb = im.getbbox()
    im.crop((0, 0, w, bb[3])).save(out/f'{dst or name}.png')
for n in ('worm_normal_rest worm_normal_move_a worm_normal_move_b mouse_rest mouse_walk_a mouse_walk_b '
          'slime_block_rest slime_block_walk_a slime_block_walk_b slime_block_jump').split():
    enemy(n)

shutil.copy(root/'_kenney/music/a/Juhani Junkala [Retro Game Music Pack] Level 3.wav', aud/'m_world3.wav')
print('v1.6 art and music written')
