#!/usr/bin/env python3
"""Usage: render_level.py <levels dir> <out dir> ; a quick colour map of each level, for checking a layout by eye."""
import sys
from pathlib import Path
from PIL import Image, ImageDraw
C = {'#':(120,90,60),'g':(110,110,125),'X':(170,130,80),'b':(190,100,60),'?':(240,200,60),'!':(255,240,0),'*':(255,120,160),'+':(150,220,255),'h':(230,230,120),'H':(255,190,210),
     'o':(250,210,50),'s':(255,255,255),'e':(60,200,60),'n':(60,160,200),'y':(170,90,220),'j':(30,140,60),'p':(230,40,40),'B':(255,60,0),'S':(0,255,255),'G':(0,120,255),
     'C':(255,0,255),'D':(0,0,0),'E':(0,0,0),'R':(90,90,100),'T':(255,120,0),'M':(0,200,160),'V':(0,200,160),'^':(255,0,0),'l':(255,90,0),'w':(40,110,255),'|':(255,180,90),
     '{':(80,90,110),'}':(80,90,110),'u':(210,140,90),'a':(220,50,50),'A':(50,90,230),'1':(255,0,120),'Q':(140,110,80),'L':(255,230,160)}
out = Path(sys.argv[2]); out.mkdir(exist_ok=True, parents=True); K = 8
for f in sorted(Path(sys.argv[1]).glob('*.txt')):
    lines = f.read_text().split('\n'); notes = [l for l in lines if l.startswith('@')]; rows = [l for l in lines if not l.startswith('@')][:-1]
    w = max(map(len, rows)); h = len(rows)
    im = Image.new('RGB', (w*K, h*K), (205, 228, 245)); d = ImageDraw.Draw(im)
    for r, row in enumerate(rows):
        for c, ch in enumerate(row):
            if ch in C: d.rectangle([c*K, r*K, c*K+K-1, r*K+K-1], fill=C[ch])
    for n in notes:
        a = n[1:].split()
        if a[0] == 'plat':
            cx, top, dx, dy, per, ph, wd = map(float, a[1:8]); y = (h-top)*K
            d.rectangle([(cx-wd/2)*K, y, (cx+wd/2)*K, y+K-1], fill=(0,200,160))
            d.line([(cx-dx)*K, y+4-dy*K, (cx+dx)*K, y+4+dy*K], fill=(0,120,100))
        if a[0] in ('wind', 'gust', 'shelter'):
            x0,x1,y0,y1 = map(float,a[1:5]); d.rectangle([x0*K,(h-y1)*K,x1*K,(h-y0)*K], outline=(120,120,255))
    seg = 130*K; parts = (w*K + seg - 1)//seg
    sheet = Image.new('RGB', (seg, parts*(h*K+6)), 'white')
    for i in range(parts): sheet.paste(im.crop((i*seg, 0, (i+1)*seg, h*K)), (0, i*(h*K+6)))
    sheet.save(out/f'{f.stem}.png')
