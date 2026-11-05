#!/usr/bin/env python3
"""Builds the Android app icons in Assets/Icons from the hero sprite."""
from pathlib import Path
from PIL import Image, ImageDraw
root = Path(__file__).resolve().parent.parent
out = root / 'Assets/Icons'
S = 432

bg = Image.new('RGBA', (S, S))
d = ImageDraw.Draw(bg)
for y in range(S):
    t = y / S
    d.line([(0, y), (S, y)], fill=(int(150 + 70 * t), int(205 + 35 * t), 245, 255))
d.ellipse([-120, 300, S + 120, 760], fill=(55, 200, 113, 255))
d.ellipse([-120, 322, S + 120, 760], fill=(31, 157, 91, 255))

hero = Image.open(root / 'Assets/Resources/Art/hero_b_cheer1.png').convert('RGBA')
hero = hero.crop(hero.getbbox())
h = 290
hero = hero.resize((round(hero.width * h / hero.height), h), Image.LANCZOS)
fg = Image.new('RGBA', (S, S), (0, 0, 0, 0))
fg.alpha_composite(hero, ((S - hero.width) // 2, (S - h) // 2 + 6))

bg.save(out / 'icon_bg.png')
fg.save(out / 'icon_fg.png')

