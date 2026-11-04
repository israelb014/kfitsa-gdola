#!/usr/bin/env python3
"""Writes Assets/Resources/Levels/level1.txt. Rows are top first; y here counts up from the bottom row (0)."""
import sys
from pathlib import Path
W, H = 212, 15
g = [[' '] * W for _ in range(H)]
def put(x, y, ch): g[H - 1 - y][x] = ch
def ground(x0, x1, top):            # solid from row 0 up to and including row top-1
    for x in range(x0, x1 + 1):
        for y in range(top): put(x, y, '#')
def plat(x0, x1, y):
    for x in range(x0, x1 + 1): put(x, y, '#')
def coins(pts):
    for x, y in pts: put(x, y, 'o')
def arc(x0, y0, n=5, rise=2):
    coins([(x0 + i, y0 + (rise if 0 < i < n - 1 else rise - 1 if n > 2 else 0)) for i in range(n)])

# Meadow: room to feel the run and the first jumps
ground(0, 26, 2)
put(3, 2, 'S'); put(6, 2, '>'); put(11, 2, 'a'); put(16, 2, 'b'); put(24, 2, 't')
coins([(9, 3), (10, 4), (11, 5), (12, 4), (13, 3)])
put(17, 6, '?')
put(22, 2, 'e')

# Terrace up and over, then the first gap
ground(27, 32, 3); ground(33, 40, 5); ground(41, 46, 3)
put(29, 3, 'a'); put(36, 5, 't'); put(38, 5, 'b')
coins([(34, 7), (36, 8), (38, 7)])
# gap 47-49
ground(50, 74, 2)
put(52, 2, 'f'); put(53, 2, 'f'); put(72, 2, 'k')

# Floating ledge with the crystal block
plat(56, 58, 6); put(60, 6, '!'); plat(62, 64, 6)
coins([(56, 8), (57, 8), (58, 8), (62, 8), (63, 8), (64, 8)])
put(68, 2, 'e')

# Long gap over a stepping platform
# gap 75-84
plat(78, 81, 4)
coins([(79, 6), (80, 6)])
ground(85, 100, 3)
for i, x in enumerate(range(92, 96)):       # crate stair
    for y in range(3, 4 + i): put(x, y, 'X')
for y in range(3, 7): put(96, y, 'X')
put(88, 3, 'a'); put(90, 3, 'b')
coins([(92, 5), (93, 6), (94, 7), (95, 8), (96, 9)])

# Sky islands over a wide pit
plat(105, 109, 5); plat(113, 116, 7); plat(120, 124, 5)
coins([(106, 7), (107, 7), (108, 7), (114, 9), (115, 9), (121, 7), (122, 7), (123, 7)])
ground(128, 147, 2)
put(130, 2, 'b'); put(131, 2, 'f'); put(132, 2, 'f')
put(141, 2, 'e')
plat(136, 139, 6)
coins([(136, 8), (139, 8)])

# The one gap that needs a running start
# gap 148-153
coins([(149, 6), (150, 7), (151, 7), (152, 6)])
ground(154, 172, 3)
put(158, 3, 't'); put(170, 3, 'k')
put(165, 3, 'e')
ground(173, 178, 5); ground(179, 183, 3)
put(175, 5, 'a')

# Home stretch and the gate
ground(184, W - 1, 2)
put(187, 2, 'a'); put(190, 2, 'b')
coins([(186, 5), (188, 5), (190, 5), (192, 5)])
put(198, 2, 'G')
put(205, 2, 't'); put(208, 2, 'b')

out = Path(sys.argv[1]); out.parent.mkdir(parents=True, exist_ok=True)
out.write_text('\n'.join(''.join(r).rstrip() for r in g) + '\n')
text = out.read_text()
print('coins', text.count('o'), 'enemies', text.count('e'), 'surprise', text.count('?') + text.count('!'))
