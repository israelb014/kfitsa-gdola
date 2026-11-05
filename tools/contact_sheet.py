#!/usr/bin/env python3
"""Usage: contact_sheet.py <dir> <out.png> [names...] ; two-column sheet of screen_<name>.png files."""
import sys
from PIL import Image
d, out, names = sys.argv[1], sys.argv[2], sys.argv[3:]
W = 760
ims = [Image.open(f"{d}/screen_{n}.png").convert('RGB') for n in names]
h = int(W * ims[0].height / ims[0].width)
s = Image.new('RGB', (W * 2 + 8, (h + 8) * ((len(ims) + 1) // 2)), 'white')
for i, m in enumerate(ims):
    s.paste(m.resize((W, h)), ((i % 2) * (W + 8), (i // 2) * (h + 8)))
s.save(out)
print(ims[0].size)
