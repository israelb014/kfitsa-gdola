#!/usr/bin/env python3
"""Adds the art new in version 1.2 to Assets/Resources/Art, without touching what prep_art.py made."""
import sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
root = Path(sys.argv[1]); out = root/'Assets/Resources/Art'
T = root/'_kenney/styles/new-platformer-pack-1.1/Sprites/Tiles/Double'
def tile(name, dst=None):
    Image.open(T/f'{name}.png').convert('RGBA').resize((72, 72), Image.LANCZOS).save(out/f'{dst or name}.png')
tile('block_exclamation_active', 'block_power'); tile('block_strong_danger', 'block_crumble')
for n in 'star block_red block_blue switch_red switch_blue torch_off flag_off'.split(): tile(n)
tile('flag_green_a', 'flag_a'); tile('flag_green_b', 'flag_b')
# Conveyor: two frames, the chevron shifted, so the belt looks like it runs
belt = Image.open(T/'conveyor.png').convert('RGBA').resize((72, 72), Image.LANCZOS)
belt.save(out/'conveyor_a.png')
b = belt.copy(); inner = belt.crop((14, 12, 58, 60))
b.paste(Image.new('RGBA', inner.size, belt.getpixel((18, 16))), (14, 12))
sh = Image.new('RGBA', inner.size, belt.getpixel((18, 16))); sh.paste(inner, (12, 0)); sh.paste(inner.crop((32, 0, 44, 48)), (0, 0))
b.paste(sh, (14, 12)); b.save(out/'conveyor_b.png')
# Wind streak
s = Image.new('RGBA', (96, 8), (0, 0, 0, 0)); d = ImageDraw.Draw(s)
for x in range(96):
    a = int(200 * min(x, 96 - x) / 48)
    d.line([(x, 2), (x, 5)], fill=(255, 250, 235, a))
s.save(out/'streak.png')
# The sevivon: the hero's thrown top, in the outline style of the pack
S = 4; n = 72 * S; im = Image.new('RGBA', (n, n), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
ink = (67, 74, 95, 255); body = (240, 170, 60, 255); light = (255, 215, 120, 255); shade = (205, 125, 40, 255)
def P(*pts): return [(x * S, y * S) for x, y in pts]
d.rounded_rectangle([(32*S, 2*S), (40*S, 24*S)], radius=3*S, fill=light, outline=ink, width=3*S)
top = P((14,20),(58,20),(60,44),(36,68),(12,44))
d.polygon(top, fill=body); d.line(top + [top[0]], fill=ink, width=4*S, joint='curve')
d.polygon(P((16,23),(30,23),(30,60),(15,44)), fill=light)
d.polygon(P((44,23),(56,23),(57,44),(44,58)), fill=shade)
d.line(top + [top[0]], fill=ink, width=4*S, joint='curve')
font = ImageFont.truetype(str(root/'docs/Rubik.ttf'), 28*S)
d.text((36*S, 36*S), 'ג', font=font, fill=ink, anchor='mm')
im.resize((72, 72), Image.LANCZOS).save(out/'sevivon.png')
