#!/usr/bin/env python3
"""Adds the art and sounds new in version 1.4, without touching what the earlier scripts made."""
import sys, shutil, colorsys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter
root = Path(sys.argv[1]); out = root/'Assets/Resources/Art'; aud = root/'Assets/Resources/Audio'
OUTLINE = (67, 74, 95, 255); S = 4

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

# The frost hero: the big form in an ice-blue shirt, same canvas and feet as the other forms
H = root/'_kenney/chars/platformer-characters/PNG/Player/Poses'
stand = Image.open(H/'player_stand.png').convert('RGBA'); body = stand.getbbox()[3] - stand.getbbox()[1]
for pose in 'stand walk1 walk2 jump fall skid hurt duck cheer1 cheer2 action1'.split():
    im = recolor(Image.open(H/f'player_{pose}.png').convert('RGBA'), .22, .50, .54, lift=.12, sat=.8)
    k = 144 / body; im = im.resize((int(im.width*k), int(im.height*k)), Image.LANCZOS)
    outlined(im).save(out/f'hero_f_{pose}.png')

# The frost sevivon: the orange top turned ice blue
sev = Image.open(out/'sevivon.png').convert('RGBA')
recolor(sev, .0, .2, .55, lift=.1, sat=.75).save(out/'sevivon_frost.png')

def canvas(n): im = Image.new('RGBA', (n*S, n*S), (0,0,0,0)); return im, ImageDraw.Draw(im)
def done(im, n, name): im.resize((n, n), Image.LANCZOS).save(out/f'{name}.png')

# Bubble shield: a clear ring with a highlight, drawn around the hero
im, d = canvas(128); n = 128*S
d.ellipse([6*S, 6*S, n-6*S, n-6*S], fill=(170, 225, 255, 46), outline=(235, 250, 255, 210), width=5*S)
d.ellipse([22*S, 18*S, 50*S, 40*S], fill=(255, 255, 255, 170))
d.ellipse([86*S, 92*S, 98*S, 102*S], fill=(255, 255, 255, 120))
done(im, 128, 'bubble')

# Dust puff: soft round cloud, white so the game can tint it to the ground
im, d = canvas(32)
for i, (x, y, r) in enumerate(((16, 18, 11), (10, 20, 7), (22, 20, 7))):
    d.ellipse([(x-r)*S, (y-r)*S, (x+r)*S, (y+r)*S], fill=(255, 255, 255, 200))
im = im.filter(ImageFilter.GaussianBlur(S*1.2)); done(im, 32, 'puff')

# The boss's landing shadow: a flat dark ellipse
im = Image.new('RGBA', (96*S, 24*S), (0,0,0,0)); d = ImageDraw.Draw(im)
d.ellipse([2*S, 2*S, 94*S, 22*S], fill=(30, 10, 25, 150))
im.filter(ImageFilter.GaussianBlur(S*2)).resize((96, 24), Image.LANCZOS).save(out/'shadow.png')

# Ice cube: a frozen enemy sits inside it, so it is mostly clear with bright edges
im, d = canvas(72); n = 72*S
d.rounded_rectangle([2*S, 2*S, n-2*S, n-2*S], radius=9*S, fill=(175, 230, 255, 120), outline=(240, 252, 255, 235), width=4*S)
d.polygon([(12*S, 14*S), (30*S, 10*S), (14*S, 30*S)], fill=(255, 255, 255, 170))
d.line([(50*S, 58*S), (60*S, 48*S)], fill=(255, 255, 255, 160), width=3*S)
done(im, 72, 'ice')

# Teaching hand: a pointing finger, white with the pack's outline
im, d = canvas(96)
ink = OUTLINE; skin = (255, 255, 255, 255); w = 4*S
d.rounded_rectangle([38*S, 6*S, 54*S, 52*S], radius=8*S, fill=skin, outline=ink, width=w)        # finger
d.rounded_rectangle([24*S, 40*S, 74*S, 88*S], radius=14*S, fill=skin, outline=ink, width=w)      # palm
d.rounded_rectangle([12*S, 46*S, 34*S, 66*S], radius=9*S, fill=skin, outline=ink, width=w)       # thumb
d.rounded_rectangle([40*S, 8*S, 52*S, 50*S], radius=6*S, fill=skin)                             # join finger into palm
d.rectangle([28*S, 44*S, 70*S, 60*S], fill=skin)
for x in (52, 62): d.line([(x*S, 46*S), (x*S, 58*S)], fill=ink, width=3*S)                        # folded fingers
done(im, 96, 'hand')

# Sounds: a bubble pop, ice forming, ice breaking
K = root/'_kenney'
shutil.copy(next(K.glob('interface-sounds/**/drop_002.ogg')), aud/'pop.ogg')
shutil.copy(next(K.glob('impact-sounds/**/impactGlass_light_001.ogg')), aud/'freeze.ogg')
shutil.copy(next(K.glob('impact-sounds/**/impactGlass_medium_000.ogg')), aud/'shatter.ogg')
print('v1.4 art and sounds written')
