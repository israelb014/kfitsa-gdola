#!/usr/bin/env python3
"""The itch.io cover, 630x500, from the game's own art: the hills sky, ground tiles, the hero cheering, a slime,
coins and the title in the game's font (Rubik)."""
import sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
root = Path(sys.argv[1]); A = root/'Assets/Resources/Art'; out = root/'itch/cover.png'
W, H = 630, 500
def art(n, k=1.0):
    im = Image.open(A/f'{n}.png').convert('RGBA')
    return im.resize((int(im.width*k), int(im.height*k)), Image.LANCZOS)
c = Image.open(A/'bg_hills.png').convert('RGBA').resize((W, W)).crop((0, W - H, W, W))
T = 54
for x in range(0, W, T):
    c.alpha_composite(art('t_grass_block_top', T/72), (x, H - 2*T)); c.alpha_composite(art('t_grass_block_center', T/72), (x, H - T))
hero = art('hero_b_cheer1', 1.25); c.alpha_composite(hero, (W//2 - hero.width//2 + 150, H - 2*T - hero.height))
c.alpha_composite(art('slime_normal_walk_a', .9), (90, H - 2*T - 46))
for i, x in enumerate((210, 260, 310, 360)): c.alpha_composite(art('coin_gold', .7), (x, int(250 - abs(i - 1.5)*18 - 10)))
c.alpha_composite(art('block_coin', .8), (120, 215)); c.alpha_composite(art('star', .8), (40, 300))
d = ImageDraw.Draw(c)
f = ImageFont.truetype(str(root/'Assets/Resources/Fonts/Rubik.ttf'), 92)
t = 'קפיצה גדולה'
d.text((W//2, 92), t, font=f, anchor='mm', direction='rtl', fill=(255, 220, 80), stroke_width=7, stroke_fill=(36, 42, 62))
f2 = ImageFont.truetype(str(root/'Assets/Resources/Fonts/Rubik.ttf'), 30)
d.text((W//2, 160), 'Kfitsa Gdola', font=f2, anchor='mm', fill=(255, 255, 255), stroke_width=4, stroke_fill=(36, 42, 62))
c.convert('RGB').save(out); print(out, c.size)
