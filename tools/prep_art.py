#!/usr/bin/env python3
"""Builds Assets/Resources/Art and Audio from the downloaded packs. 72 px = 1 tile."""
import sys, shutil, colorsys
from pathlib import Path
from PIL import Image, ImageFilter
root = Path(sys.argv[1]); out = root/'Assets/Resources/Art'; aud = root/'Assets/Resources/Audio'
for d in (out, aud): shutil.rmtree(d, ignore_errors=True); d.mkdir(parents=True)
N = root/'_kenney/styles/new-platformer-pack-1.1'; T = N/'Sprites/Tiles/Double'; E = N/'Sprites/Enemies/Double'; B = N/'Sprites/Backgrounds/Double'
TILE = 72; OUTLINE = (67, 74, 95, 255)
def load(p): return Image.open(p).convert('RGBA')
def tile(name, dst=None):
    load(T/f'{name}.png').resize((TILE, TILE), Image.LANCZOS).save(out/f'{dst or name}.png')

for th in ('grass', 'sand', 'stone', 'dirt'):
    for part in ('block', 'block_top', 'block_top_left', 'block_top_right', 'block_left', 'block_center', 'block_right',
                 'block_bottom', 'block_bottom_left', 'block_bottom_right', 'horizontal_left', 'horizontal_middle', 'horizontal_right',
                 'vertical_top', 'vertical_middle', 'vertical_bottom'):
        tile(f'terrain_{th}_{part}', f't_{th}_{part}')
for n in ('block_coin block_empty block_strong_coin bricks_brown bricks_grey block_plank block_planks coin_gold coin_gold_side gem_blue gem_red heart '
          'spikes spring spring_out door_open door_open_top door_closed door_closed_top torch_on_a torch_on_b lever lever_right saw '
          'bush grass rock cactus fence sign_right sign_exit lava lava_top water water_top bridge hud_coin hud_heart lock_yellow').split():
    tile(n)

# Backgrounds: the pack's sky is white, so bake a sky colour in; 1080 px is the full 15-tile screen height
for name, sky in (('hills', (190,225,250)), ('trees', (205,235,225)), ('desert', (250,226,190)), ('dusk', (252,205,170)), ('night', (66,56,110)), ('ember', (70,40,60))):
    src = {'hills':'color_hills','trees':'color_trees','desert':'color_desert','dusk':'fade_desert','night':'fade_trees','ember':'fade_hills'}[name]
    im = load(B/f'background_{src}.png'); px = im.load()
    for y in range(im.height):
        for x in range(im.width):
            r,g,b,a = px[x,y]
            if r > 245 and g > 245 and b > 245: px[x,y] = (r,g,b,0)
    base = Image.new('RGBA', im.size, sky+(255,)); base.alpha_composite(im)
    if name in ('night','ember','dusk'):   # dim the hills for the late-day themes
        over = Image.new('RGBA', im.size, sky+(110,)); base.alpha_composite(over)
    base.convert('RGB').resize((1080,1080), Image.LANCZOS).save(out/f'bg_{name}.png')
load(B/'background_solid_dirt.png').convert('RGB').resize((256,256), Image.LANCZOS).save(out/'bg_room.png')

def outlined(im, w=3):
    pad = w + 2; c = Image.new('RGBA', (im.width+pad*2, im.height+pad*2), (0,0,0,0)); c.paste(im, (pad,pad), im)
    a = c.getchannel('A').filter(ImageFilter.MaxFilter(w*2+1)).point(lambda v: 255 if v > 90 else 0)
    o = Image.new('RGBA', c.size, OUTLINE); o.putalpha(a); o.alpha_composite(c); return o

def recolor(im, h0, h1, to):
    im = im.copy(); p = im.load()
    for y in range(im.height):
        for x in range(im.width):
            r,g,b,a = p[x,y]
            if not a: continue
            H,S,V = colorsys.rgb_to_hsv(r/255,g/255,b/255)
            if h0 < H < h1 and S > .25:
                r,g,b = colorsys.hsv_to_rgb(to, S, V); p[x,y] = (int(r*255),int(g*255),int(b*255),a)
    return im

# Hero: same 80x110 canvas for every pose, so poses never jump; feet sit PAD pixels above the bottom edge
H = root/'_kenney/chars/platformer-characters/PNG/Player/Poses'
POSES = 'stand walk1 walk2 jump fall skid hurt duck cheer1 cheer2 action1'.split()
stand = load(H/'player_stand.png'); feet = stand.getbbox()[3]; body = feet - stand.getbbox()[1]
print('hero canvas', stand.size, 'feet', feet, 'body', body)
def hero(pose, height, shirt=None):
    im = load(H/f'player_{pose}.png')
    if shirt is not None: im = recolor(im, .22, .50, shirt)
    k = height / body; im = im.resize((int(im.width*k), int(im.height*k)), Image.LANCZOS)
    return outlined(im)
meta = {}
for form, height, shirt in (('s', 92, None), ('b', 144, None), ('p', 144, .78)):
    for pose in POSES:
        im = hero(pose, height, shirt); im.save(out/f'hero_{form}_{pose}.png')
    k = height/body; meta[form] = (5 + (stand.height - feet)*k) / im.height
(out/'hero_meta.txt').write_text(' '.join(f'{k}={v:.4f}' for k,v in meta.items()))
print(meta)

def enemy(name, dst=None, w=TILE):
    im = load(E/f'{name}.png'); im = im.resize((w, w), Image.LANCZOS); bb = im.getbbox()
    im.crop((0, 0, w, bb[3])).save(out/f'{dst or name}.png')
for n in ('slime_normal_walk_a slime_normal_walk_b slime_normal_flat snail_walk_a snail_walk_b snail_shell fly_a fly_b fly_rest '
          'frog_idle frog_jump frog_rest slime_spike_walk_a slime_spike_walk_b slime_spike_flat').split():
    enemy(n)
for n in ('slime_fire_walk_a', 'slime_fire_walk_b', 'slime_fire_flat', 'slime_fire_rest'):
    enemy(n, n.replace('slime_fire', 'boss'), 264)

# Sound
K = root/'_kenney'
for n in 'bump coin disappear gem hurt jump jump-high magic select throw'.split():
    shutil.copy(N/f'Sounds/sfx_{n}.ogg', aud/f'{n}.ogg')
shutil.copy(next(K.glob('impact-sounds/**/impactSoft_medium_001.ogg')), aud/'stomp.ogg')
shutil.copy(next(K.glob('impact-sounds/**/impactPlank_medium_001.ogg')), aud/'brick.ogg')
shutil.copy(next(K.glob('digital-audio/**/powerUp2.ogg')), aud/'power.ogg')
shutil.copy(next(K.glob('digital-audio/**/lowDown.ogg')), aud/'shrink.ogg')
shutil.copy(next(K.glob('digital-audio/**/phaserUp2.ogg')), aud/'spring.ogg')
J = K/'music-jingles/music-jingles/Audio/8-Bit jingles' if (K/'music-jingles/music-jingles').exists() else next(K.glob('music-jingles/**/8-Bit jingles'))
shutil.copy(J/'jingles_NES03.ogg', aud/'win.ogg'); shutil.copy(J/'jingles_NES08.ogg', aud/'lose.ogg'); shutil.copy(J/'jingles_NES15.ogg', aud/'gameover.ogg')
M = K/'music'
for src, dst in (('b/Juhani Junkala [Chiptune Adventures] 1. Stage 1.ogg','m_world1.ogg'),('b/Juhani Junkala [Chiptune Adventures] 2. Stage 2.ogg','m_world2.ogg'),
                 ('b/Juhani Junkala [Chiptune Adventures] 3. Boss Fight.ogg','m_boss.ogg'),('b/Juhani Junkala [Chiptune Adventures] 4. Stage Select.ogg','m_map.ogg'),
                 ('a/Juhani Junkala [Retro Game Music Pack] Title Screen.wav','m_title.wav'),('a/Juhani Junkala [Retro Game Music Pack] Ending.wav','m_ending.wav')):
    shutil.copy(M/src, aud/dst)
print('art files', len(list(out.iterdir())), 'audio', len(list(aud.iterdir())))
