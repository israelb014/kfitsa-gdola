#!/usr/bin/env python3
"""Usage: prep_storm.py <project> ; draws the sandstorm art and synthesizes its wind sounds. All original, nothing downloaded."""
import math, random, struct, sys, wave
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter
root = Path(sys.argv[1]); out = root/'Assets/Resources/Art'; audio = root/'Assets/Resources/Audio'
random.seed(7)
S = 4
ink = (67, 74, 95, 255)
def canvas(w, h): 
    im = Image.new('RGBA', (w*S, h*S), (0, 0, 0, 0)); return im, ImageDraw.Draw(im)
def P(*pts): return [(x*S, y*S) for x, y in pts]
def done(im, name, w, h): im.resize((w, h), Image.LANCZOS).save(out/f'{name}.png')

Image.new('RGBA', (4, 4), (255, 255, 255, 255)).save(out/'px.png')

# Sand grains and a long grain, white so the game can tint them
for name, w, h in (('sand_a', 14, 14), ('sand_b', 44, 10)):
    im, d = canvas(w, h); d.ellipse([S, S, (w-1)*S, (h-1)*S], fill=(255, 255, 255, 255)); done(im, name, w, h)

# A soft bank of dust that tiles left to right
W, H = 512, 256
dust = Image.new('L', (W, H), 0); d = ImageDraw.Draw(dust)
for _ in range(170):
    x, y, r = random.randrange(W), random.randrange(H), random.randrange(14, 60)
    v = random.randrange(70, 200)
    for ox in (-W, 0, W): d.ellipse([x+ox-r*1.8, y-r*.6, x+ox+r*1.8, y+r*.6], fill=v)
dust = dust.filter(ImageFilter.GaussianBlur(16))
im = Image.new('RGBA', (W, H), (255, 255, 255, 0)); im.putalpha(dust); im.save(out/'dust.png')

# Palm: the trunk leans from its foot, the crown swings on top of it
im, d = canvas(60, 216)
pts = [(30+3*math.sin(t/20*2.4), 212-t/20*200) for t in range(21)]
for i in range(20):
    a, b = pts[i], pts[i+1]; w = 13-i*.3
    d.line(P(a, b), fill=ink, width=int((w+5)*S))
for i in range(20):
    a, b = pts[i], pts[i+1]; w = 13-i*.3
    d.line(P(a, b), fill=(186, 134, 82, 255) if i % 2 else (160, 112, 66, 255), width=int(w*S))
done(im, 'palm_trunk', 60, 216)
im, d = canvas(200, 110)
for ang, ln in ((-168, 92), (-140, 84), (-112, 62), (-68, 62), (-40, 84), (-12, 92)):
    a = math.radians(ang); ex, ey = 100+math.cos(a)*ln, 62+math.sin(a)*ln*.55+abs(math.cos(a))*38
    mx, my = 100+math.cos(a)*ln*.55, 62+math.sin(a)*ln*.75
    leaf = P((100, 62), (mx, my-10), (ex, ey), (mx, my+11))
    d.polygon(leaf, fill=(72, 160, 86, 255)); d.line(leaf+[leaf[0]], fill=ink, width=3*S, joint='curve')
    d.line(P((100, 62), (mx, my), (ex, ey)), fill=(120, 205, 120, 255), width=2*S)
d.ellipse(P((86, 54), (114, 80))[0]+P((86, 54), (114, 80))[1], fill=(150, 100, 60, 255), outline=ink, width=3*S)
done(im, 'palm_crown', 200, 110)

# Flag: a pole, and cloth that hangs from its left edge
im, d = canvas(16, 150)
d.rounded_rectangle([3*S, 2*S, 12*S, 148*S], radius=3*S, fill=(225, 225, 235, 255), outline=ink, width=2*S); done(im, 'storm_pole', 16, 150)
im, d = canvas(96, 54)
c = P((2, 3), (92, 27), (2, 51)); d.polygon(c, fill=(226, 64, 58, 255)); d.line(c+[c[0]], fill=ink, width=3*S, joint='curve'); done(im, 'storm_cloth', 96, 54)

# Scarf link
im, d = canvas(30, 22); d.rounded_rectangle([2*S, 2*S, 28*S, 20*S], radius=6*S, fill=(232, 58, 66, 255), outline=ink, width=3*S); done(im, 'scarf', 30, 22)

# Shelter sign: a green shield on a post, the mark of every safe spot
im, d = canvas(84, 96)
sh = P((42, 4), (76, 14), (72, 54), (42, 90), (12, 54), (8, 14))
d.polygon(sh, fill=(70, 200, 110, 255)); d.line(sh+[sh[0]], fill=(255, 255, 255, 255), width=7*S, joint='curve')
d.line(P((26, 44), (38, 58), (60, 28)), fill=(255, 255, 255, 255), width=9*S, joint='curve')
done(im, 'shelter', 84, 96)

# HUD
def ring(name, col):
    im, d = canvas(220, 220); d.ellipse(P((10, 10), (210, 210))[0]+P((10, 10), (210, 210))[1], outline=col, width=20*S); done(im, name, 220, 220)
ring('storm_ring', (255, 255, 255, 255))
im, d = canvas(200, 200); d.ellipse([6*S, 6*S, 194*S, 194*S], fill=(46, 36, 34, 215)); done(im, 'storm_disc', 200, 200)
# Arrow pointing right; the game turns it to the wind
im, d = canvas(320, 170)
a = P((6, 55), (190, 55), (190, 8), (312, 85), (190, 162), (190, 115), (6, 115))
d.polygon(a, fill=(255, 255, 255, 255)); d.line(a+[a[0]], fill=ink, width=8*S, joint='curve'); done(im, 'storm_arrow', 320, 170)
# Warning mark: a whirl of sand
im, d = canvas(160, 160)
for i, (x0, x1, y) in enumerate(((22, 138, 26), (34, 126, 54), (48, 112, 82), (60, 100, 110), (72, 88, 136))):
    d.line(P((x0, y), (x1, y)), fill=(255, 255, 255, 255), width=14*S)
done(im, 'storm_icon', 160, 160)

# Wind. Noise through a resonant filter whose pitch wanders, so it howls instead of hisses
RATE = 22050
def wind(seconds, level, pitch):
    n = int(seconds*RATE); y = [0.0]*n; lo = bp = 0.0
    for i in range(n):
        t = i/n
        f = pitch(t, i/RATE); q = .22
        fc = 2*math.sin(math.pi*min(f, 3000)/RATE)
        x = random.uniform(-1, 1)
        lo += fc*bp; hi = x-lo-q*bp; bp += fc*hi
        y[i] = (bp*.75+lo*.25)*level(t, i/RATE)
    return y
def write(name, y):
    top = max(abs(v) for v in y)
    with wave.open(str(audio/f'{name}.wav'), 'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(RATE)
        w.writeframes(b''.join(struct.pack('<h', int(v/top*.8*32767)) for v in y))
write('wind_rise', wind(2, lambda t, s: .05+t**1.6*.95, lambda t, s: 220+t*620+40*math.sin(s*9)))
loop = wind(7, lambda t, s: .75+.25*math.sin(s*2*math.pi/3.5), lambda t, s: 780+230*math.sin(s*2*math.pi/3.5)+70*math.sin(s*2*math.pi*1.5))
X = RATE; body = loop[:len(loop)-X]
for i in range(X):                      # the tail fades into the head, so the loop has no click
    k = i/X; body[i] = body[i]*k+loop[len(loop)-X+i]*(1-k)
write('wind_storm', body)
print('ok')
