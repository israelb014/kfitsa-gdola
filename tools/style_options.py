#!/usr/bin/env python3
"""Throwaway: composes one identical test scene in each candidate art style, at 1920x1080. Not game code."""
import sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageFilter
root = Path(sys.argv[1]); K = root/'_kenney/styles'; OUT = root/'verification/style-options'
W, H = 1920, 1080

def load(p): return Image.open(p).convert('RGBA')

class Scene:
    def __init__(s, T=72):
        s.T = T; s.im = Image.new('RGBA', (W, H), (255,255,255,255)); s.rows = H / T
    def at(s, x, y, img, w=None, h=None, smooth=True, anchor='bottom'):
        """Places img so its bottom centre sits at tile (x, y); y counts up from the bottom row."""
        T = s.T; w = w or 1; 
        tw = int(round(w*T)); th = int(round(h*T)) if h else int(round(tw*img.height/img.width))
        r = img.resize((tw, th), Image.LANCZOS if smooth else Image.NEAREST)
        px = int(x*T - tw/2); py = int(H - y*T - th)
        s.im.alpha_composite(r, (max(px,0), max(py,0)) ) if px >= 0 and py >= 0 else s.im.paste(r, (px, py), r)
    def tile(s, c, r, img, smooth=True):
        T = s.T; t = img.resize((T, int(round(T*img.height/img.width))), Image.LANCZOS if smooth else Image.NEAREST)
        s.im.paste(t, (c*T, H - (r+1)*T), t)
    def tiled_bg(s, img, y=0, h=H, parallax=None):
        sc = h / img.height; t = img.resize((int(img.width*sc), h), Image.LANCZOS)
        for x in range(0, W, t.width): s.im.alpha_composite(t, (x, y)) if x + t.width <= W else s.im.paste(t, (x, y), t)

# Shared layout, in tiles. Ground heights by column, then the props.
def heights(cols):
    hs = []
    for c in range(cols):
        hs.append(3 if c < 10 else 0 if c < 13 else 2 if c < 20 else 4)
    return hs

def terrain(s, top_l, top, top_r, left, fill, right, cols=28, smooth=True):
    hs = heights(cols)
    for c, h in enumerate(hs):
        for r in range(h):
            lf = c > 0 and hs[c-1] > r; rt = c < cols-1 and hs[c+1] > r
            if r == h-1: img = top if lf and rt else (top_l if rt else top_r if lf else top)
            else: img = fill if lf and rt else (left if rt else right if lf else fill)
            s.tile(c, r, img, smooth)

def style_cartoon(s=None, terrain_name='grass', bg='background_color_hills', save='a-cartoon.png', hud=True, T=72, hero='yellow', chars=None):
    b = K/'new-platformer-pack-1.1/Sprites'; t = lambda n: load(b/f'Tiles/Default/{n}.png')
    s = s or Scene(T)
    s.im.paste((190,225,250,255) if terrain_name != 'purple' else (70,50,110,255), (0,0,W,H))
    bgim = load(b/f'Backgrounds/Default/{bg}.png')
    px = bgim.load()     # the pack's sky is plain white: let the sky colour through
    for yy in range(bgim.height):
        for xx in range(bgim.width):
            r, g_, b_, a = px[xx, yy]
            if r > 245 and g_ > 245 and b_ > 245: px[xx, yy] = (r, g_, b_, 0)
    s.tiled_bg(bgim)
    g = lambda n: t(f'terrain_{terrain_name}_block_{n}')
    terrain(s, g('top_left'), g('top'), g('top_right'), g('left'), g('center'), g('right'))
    for c, n in ((5,'left'),(6,'middle'),(7,'right')): s.tile(c, 6, t(f'terrain_{terrain_name}_horizontal_{n}'))
    s.tile(9, 7, t('block_coin')); s.tile(11, 7, t('block_strong_coin'))
    for x, y in ((3.5,5.5),(4.5,6.5),(5.5,7.5+.4),(6.5,8.5),(7.5,8.5)): s.at(x, y, t('coin_gold'), 1)
    s.at(21.5, 4, t('door_closed'), 1); s.at(21.5, 5, t('door_closed_top'), 1)
    if terrain_name == 'sand': props = [('cactus',2.5,3),('cactus',17.5,2),('rock',15.5,2)]
    elif terrain_name == 'snow': props = [('rock',2.5,3),('snow',17.5,2),('rock',15.5,2)]
    else: props = [('bush',2.5,3),('grass',17.5,2),('rock',15.5,2)]
    for n, x, y in props: s.at(x, y, t(n), 1)
    if chars: chars(s); return finish(s, save, hud)
    s.at(4.5, 3, load(b/f'Characters/Default/character_{hero}_walk_a.png'), 1.9)
    s.at(16.5, 2, load(b/'Enemies/Default/slime_normal_walk_a.png'), 1)
    s.at(24, 4, load(b/'Enemies/Default/worm_normal_move_a.png'), 1)
    return finish(s, save, hud)

def style_flat(save='b-flat.png'):
    b = K/'abstract-platformer/PNG'; s = Scene()
    s.tiled_bg(load(b/'Backgrounds/set2_background.png')); s.tiled_bg(load(b/'Backgrounds/set2_hills.png'))
    t = lambda i: load(b/f'Tiles/Green tiles/tileGreen_{i:02d}.png')
    fill = t(3).resize((64, 64))
    terrain(s, t(4), t(22), t(7), t(3), fill, t(3))
    for c, n in ((5,4),(6,5),(7,7)): s.tile(c, 6, t(n))
    o = lambda n: load(b/f'Other/{n}.png'); it = lambda n: load(b/f'Items/{n}.png')
    s.tile(9, 7, o('blockGreen_puzzle')); s.tile(11, 7, o('blockRed_puzzle'))
    for x, y in ((3.5,5.5),(4.5,6.5),(5.5,7.9),(6.5,8.5),(7.5,8.5)): s.at(x, y, it('yellowGem'), .7)
    s.at(21.5, 4, o('doorGreen'), 1); s.at(21.5, 5, o('doorGreen_top'), 1)
    for n, x, y in (('plantGreen_1',2.5,3),('plantGreen_3',17.5,2),('plantGreen_5',15.5,2)): s.at(x, y, o(n), 1)
    s.at(4.5, 3, load(b/'Players/Player Blue/playerBlue_walk1.png'), 1.1)
    s.at(16.5, 2, load(b/'Enemies/enemyWalking_1.png'), 1)
    s.at(24, 4, load(b/'Enemies/enemyWalking_3.png'), 1)
    return finish(s, save)

def style_sketch(save='d-sketch.png'):
    b = K/'scribble-platformer/PNG/Retina'; t = lambda n: load(b/f'{n}.png'); s = Scene()
    s.im.paste((245,243,238,255), (0,0,W,H))
    for n, x, y, w in (('background_cloudA',4,10.5,3),('background_cloudB',13,11.5,3.5),('background_cloudA',22,10,3),('background_treeLarge',8.5,3,3),('background_tree',18.5,2,2.2)): s.at(x, y, t(n), w)
    terrain(s, t('tile_grass'), t('tile_grass'), t('tile_grass'), t('tile_block'), t('tile_block'), t('tile_block'))
    for c in (5,6,7): s.tile(c, 6, t('tile_stone'))
    s.tile(9, 7, t('tile_item')); s.tile(11, 7, t('tile_crate'))
    for x, y in ((3.5,5.5),(4.5,6.5),(5.5,7.9),(6.5,8.5),(7.5,8.5)): s.at(x, y, t('tile_coin'), .8)
    s.at(21.5, 4, t('tile_door'), 1.4)
    s.at(2.5, 3, t('tile_bush'), 1); s.at(17.5, 2, t('tile_bush'), 1)
    s.at(4.5, 3, t('character_roundYellow'), 1.1)
    s.at(16.5, 2, t('character_squareRed'), 1); s.at(24, 4, t('character_squarePurple'), 1)
    return finish(s, save)

def style_rich_pixel(save='c-rich-pixel.png'):
    b = K/'sunny-woods/Sunny-land-woods-files'; e = b/'ENVIRONMENT'; px = 4.5
    s = Scene(); ts = load(e/'tileset.png')
    def bg(n):
        l = load(e/f'{n}.png'); sc = 5; r = l.resize((l.width*sc, l.height*sc), Image.NEAREST)
        for x in range(0, W, r.width): s.im.paste(r, (x, 0), r)
    s.im.paste((36,70,40,255), (0,0,W,H))
    for n in ('bg-clouds','bg-mountains','bg-trees'): bg(n)
    cut = lambda x, y, w=16, h=16: ts.crop((x, y, x+w, y+h))
    # the wide ground strip from the sheet: bright top band, dark fill below
    strip = cut(240, 288, 128, 48)
    hs = heights(28); T = 72
    for c, h in enumerate(hs):
        if not h: continue
        col = strip.crop(((c*16) % 128, 0, (c*16) % 128 + 16, 48))
        col = col.resize((T, int(48*T/16)), Image.NEAREST)
        s.im.paste(col, (c*T, H - h*T), col)
        for r in range(h-3): s.im.paste(col.crop((0, T*2, T, T*3)), (c*T, H - (r+1)*T))
    for c in (5,6,7): s.tile(c, 6, cut(384, 32, 16, 16), False)
    s.tile(9, 7, cut(352, 96), False); s.tile(11, 7, cut(384, 96), False)
    sp = b/'SPRITES'
    for x, y in ((3.5,5.5),(4.5,6.5),(5.5,7.9),(6.5,8.5),(7.5,8.5)): s.at(x, y, load(sp/'misc/acorn/acorn-1.png'), .7, smooth=False)
    s.tile(21, 4, cut(352+0, 144, 64, 48).crop((0,0,48,48)), False) if False else None
    p = load(sp/'player/run/player-run-1.png'); s.at(4.5, 3, p, p.width/16*1, smooth=False)
    g = load(sp/'enemies/gator/gator-1.png'); s.at(16.5, 2, g, g.width/16, smooth=False)
    a = load(sp/'enemies/ant/ant-1.png'); s.at(24, 4, a, a.width/16, smooth=False)
    return finish(s, save)

def font(name, size):
    f = ImageFont.truetype(str(name), size)
    try: f.set_variation_by_axes([700])
    except Exception: pass
    return f

RUBIK = root/'Assets/Resources/Fonts/Rubik.ttf'; SECULAR = Path('/home/ib/work/mishmeret-layla-unity/Assets/Game/Fonts/SecularOne-Regular.ttf')

def hud_text(d, x, y, text, f, fill='white', stroke=(35,40,60), sw=4, anchor='ra'):
    d.text((x, y), text, font=f, fill=fill, stroke_width=sw, stroke_fill=stroke, anchor=anchor)

def hud_plain(s, fname=RUBIK):
    d = ImageDraw.Draw(s.im); f = font(fname, 46)
    hud_text(d, 1880, 24, 'ניקוד 012400', f)
    hud_text(d, 1240, 24, 'x18', f); hud_text(d, 960, 24, 'x3', f)
    s.im.alpha_composite(heart_coin()[0], (1100, 24)); s.im.alpha_composite(heart_coin()[1], (830, 24))

def heart_coin():
    b = K/'new-platformer-pack-1.1/Sprites/Tiles/Default'
    return load(b/'hud_coin.png').resize((56,56)), load(b/'hud_heart.png').resize((56,56))

def hud_panel(s):
    panel = Image.new('RGBA', (W, H), (0,0,0,0)); d = ImageDraw.Draw(panel)
    for box in ((1540,18,1900,88),(1120,18,1420,88),(800,18,1060,88)):
        d.rounded_rectangle(box, 22, fill=(35,40,60,190))
    s.im.alpha_composite(panel); d = ImageDraw.Draw(s.im); f = font(SECULAR, 42)
    hud_text(d, 1880, 30, 'ניקוד 012400', f, sw=0)
    hud_text(d, 1300, 30, 'x18', f, sw=0, anchor='la'); hud_text(d, 950, 30, 'x3', f, sw=0, anchor='la')
    c, h = heart_coin(); s.im.alpha_composite(c, (1220, 25)); s.im.alpha_composite(h, (870, 25))

def hud_icons(s):
    d = ImageDraw.Draw(s.im); f = font(SECULAR, 54)
    c, h = heart_coin()
    hud_text(d, 1880, 20, '012400', f, sw=5)
    s.im.alpha_composite(c.resize((64,64)), (1490, 22)); hud_text(d, 1470, 20, '18', f, sw=5)
    s.im.alpha_composite(h.resize((64,64)), (1270, 22)); hud_text(d, 1250, 20, '3', f, sw=5)

def finish(s, save, hud=True):
    if hud is True: hud_plain(s)
    elif callable(hud): hud(s)
    s.im.convert('RGB').save(OUT/save); return s

def label():
    pass

if __name__ == '__main__':
    style_cartoon(); style_flat(); style_rich_pixel(); style_sketch()
    style_cartoon(terrain_name='sand', bg='background_color_desert', save='world2-1-desert.png', hero='pink')
    style_cartoon(terrain_name='snow', bg='background_fade_trees', save='world2-2-snow.png', hero='pink')
    style_cartoon(terrain_name='purple', bg='background_color_trees', save='world2-3-purple.png', hero='pink')
    style_cartoon(T=72, save='zoom-1-wide.png')
    style_cartoon(T=96, save='zoom-2-close.png')
    style_cartoon(save='hud-1-plain.png', hud=hud_plain)
    style_cartoon(save='hud-2-panels.png', hud=hud_panel)
    style_cartoon(save='hud-3-icons.png', hud=hud_icons)
