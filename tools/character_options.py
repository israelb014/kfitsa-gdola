#!/usr/bin/env python3
"""Throwaway: drops candidate heroes and enemy sets into the unchanged style 1 test scene."""
import sys
from pathlib import Path
root = Path(sys.argv[1]); sys.path.insert(0, str(root/'tools'))
sys.argv = [sys.argv[0], str(root)]
import style_options as so
from PIL import Image, ImageFilter
C = root/'_kenney/chars'; ST = root/'_kenney/styles'
so.OUT = root/'verification/style-options/characters'
OUTLINE = (67, 74, 95, 255)

def prep(path, height_px, flip=False, outline=4):
    """Crops to the figure, scales to a pixel height, and adds the world's dark outline."""
    im = Image.open(path).convert('RGBA'); im = im.crop(im.getbbox())
    w = int(im.width * height_px / im.height)
    im = im.resize((w, height_px), Image.LANCZOS)
    if flip: im = im.transpose(Image.FLIP_LEFT_RIGHT)
    pad = outline + 2; c = Image.new('RGBA', (im.width + pad*2, im.height + pad*2), (0,0,0,0)); c.paste(im, (pad, pad), im)
    a = c.getchannel('A').filter(ImageFilter.MaxFilter(outline*2 + 1)).point(lambda v: 255 if v > 90 else 0)
    o = Image.new('RGBA', c.size, OUTLINE); o.putalpha(a); o.alpha_composite(c)
    return o

def place(s, img, x, y):
    """x in tiles at the centre, y in tiles at the feet."""
    px = int(x*s.T - img.width/2); py = int(so.H - y*s.T - img.height)
    s.im.alpha_composite(img, (px, py))

HEROES = {
 '1-green-shirt': (C/'platformer-characters/PNG/Player/Poses/player_', 'walk1', 'jump', False),
 '2-adventurer': (C/'toon-characters/Male adventurer/PNG/Poses/character_maleAdventurer_', 'run0', 'jump', False),
 '3-explorer': (C/'platformer-characters/PNG/Adventurer/Poses/adventurer_', 'walk1', 'jump', False),
}

def hero_on(s, base, run, jump, flip):
    place(s, prep(f'{base}{run}.png', 104, flip), 4.5, 3)     # small form
    place(s, prep(f'{base}{jump}.png', 104, flip), 11.5, 5.2) # mid-jump over the gap
    place(s, prep(f'{base}{run}.png', 150, flip), 18.8, 2)    # grown form

def default_enemies(s):
    b = ST/'new-platformer-pack-1.1/Sprites/Enemies/Default'
    s.at(16.5, 2, so.load(b/'slime_normal_walk_a.png'), 1); s.at(24, 4, so.load(b/'worm_normal_move_a.png'), 1)

EA = ST/'new-platformer-pack-1.1/Sprites/Enemies/Default'; EB = ST/'platformer-art-deluxe/Extra animations and enemies/Enemy sprites'

def enemies(s, walker, flyer, shelled, jumper, line):
    p = lambda f, h: prep(f, h, outline=4 if line else 1)
    place(s, p(walker, 60), 14, 2); place(s, p(flyer, 62), 13.5, 8.2)
    place(s, p(shelled, 66), 24.2, 4); place(s, p(jumper, 78), 18.6, 3.6)
    base, run, jump, flip = HEROES['1-green-shirt']; place(s, prep(f'{base}{run}.png', 104, flip), 4.5, 3)

for name, (base, run, jump, flip) in HEROES.items():
    def chars(s, a=(base, run, jump, flip)): hero_on(s, *a); default_enemies(s)
    so.style_cartoon(save=f'hero-{name}.png', chars=chars)
so.style_cartoon(save='enemies-1-same-pack.png', chars=lambda s: enemies(s, EA/'slime_normal_walk_a.png', EA/'fly_a.png', EA/'snail_walk_a.png', EA/'frog_jump.png', False))
so.style_cartoon(save='enemies-2-classic-critters.png', chars=lambda s: enemies(s, EB/'spider_walk1.png', EB/'bat_fly.png', EB/'snail_walk.png', EB/'frog_leap.png', True))
