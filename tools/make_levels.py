#!/usr/bin/env python3
"""Writes Assets/Resources/Levels/*.txt. Rows are top first; y counts up from the bottom row (0).
Legend: # ground, g stone, X planks, b brick, ? coin block, ! power block, * life block, h/H hidden coin/life,
o coin, e slime, n snail, y fly, j frog, p spike slime, B boss, S start, G gate, C checkpoint, D secret door, E room door,
R room wall, T spring, M/V moving platform, ^ spikes, l lava, w water, | torch, decor , % r k f >"""
import sys
from pathlib import Path
H = 15
class L:
    def __init__(s, w): s.w = w; s.g = [[' ']*w for _ in range(H)]
    def put(s, x, y, ch): s.g[H-1-y][x] = ch
    def ground(s, x0, x1, top):
        for x in range(x0, x1+1):
            for y in range(top): s.put(x, y, '#')
    def fill(s, x0, x1, y0, y1, ch):
        for x in range(x0, x1+1):
            for y in range(y0, y1+1): s.put(x, y, ch)
    def row(s, x0, x1, y, ch='#'):
        for x in range(x0, x1+1): s.put(x, y, ch)
    def coins(s, pts):
        for x, y in pts: s.put(x, y, 'o')
    def arc(s, x0, y0, n=5):
        for i in range(n):
            s.put(x0+i, y0 + (2 if 0 < i < n-1 else 0) - (1 if n > 4 and i in (1, n-2) else 0), 'o')
    def line(s, x0, x1, y):
        for x in range(x0, x1+1): s.put(x, y, 'o')
    def stairs(s, x0, n, y, ch='X', up=True):
        for i in range(n):
            h = i+1 if up else n-i
            s.fill(x0+i, x0+i, y, y+h-1, ch)
    def room(s, main_end, entry_y=2, rich=True):
        """Bonus room after the main level: a stone box full of coins."""
        s.fill(main_end, main_end, 0, H-1, 'R' if False else 'g')
        s.put(main_end, 0, 'R')
        x0 = main_end + 1; w = s.w - x0
        s.fill(x0, s.w-1, 0, 1, 'g'); s.fill(x0, s.w-1, 13, 14, 'g'); s.fill(s.w-1, s.w-1, 0, 14, 'g')
        s.put(x0+2, 2, 'E')
        for r in (3, 5, 7):
            s.line(x0+6, s.w-4, r)
        s.row(x0+8, x0+12, 9, 'g'); s.coins([(x0+9, 10), (x0+10, 11), (x0+11, 10)])
    def save(s, name, out):
        (out/f'{name}.txt').write_text('\n'.join(''.join(r).rstrip() for r in s.g) + '\n')
        t = '\n'.join(''.join(r) for r in s.g)
        print(name, 'w', s.w, {k: t.count(k) for k in 'oenyjp?!*bhH'})

def w1l1():
    l = L(250)
    l.ground(0, 26, 2); l.put(3, 2, 'S'); l.put(6, 2, '>'); l.put(11, 2, ','); l.put(16, 2, '%')
    l.coins([(9,3),(10,4),(11,5),(12,4),(13,3)]); l.put(17, 6, '?'); l.put(22, 2, 'e'); l.put(24, 2, 'r')
    l.ground(27, 32, 3); l.ground(33, 40, 5); l.ground(41, 46, 3)
    l.put(29, 3, ','); l.put(36, 5, '%'); l.coins([(34,7),(36,8),(38,7)])
    l.ground(50, 74, 2); l.put(52, 2, 'f'); l.put(53, 2, 'f'); l.put(72, 2, '%')
    l.row(56, 58, 6); l.put(60, 6, '!'); l.row(62, 64, 6); l.put(63, 10, 'h')
    l.line(56, 58, 8); l.line(62, 64, 8); l.put(68, 2, 'e'); l.put(70, 2, 'n')
    l.row(78, 81, 4); l.coins([(79,6),(80,6)])
    l.ground(85, 104, 3); l.put(88, 3, 'C'); l.put(90, 3, ',')
    l.stairs(92, 4, 3); l.fill(96, 96, 3, 6, 'X'); l.coins([(92,5),(93,6),(94,7),(95,8),(96,9)])
    l.put(100, 3, 'D')
    l.row(109, 113, 5); l.row(117, 120, 7); l.row(124, 128, 5)
    l.line(110, 112, 7); l.line(118, 119, 9); l.line(125, 127, 7); l.put(118, 8, 'y')
    l.ground(132, 151, 2); l.put(134, 2, '%'); l.put(135, 2, 'f'); l.put(136, 2, 'f')
    l.row(140, 143, 6, 'b'); l.put(141, 6, '?'); l.put(145, 2, 'e'); l.put(147, 2, 'e')
    l.coins([(153,6),(154,7),(155,7),(156,6)])
    l.ground(158, 176, 3); l.put(162, 3, '%'); l.put(174, 3, 'r'); l.put(169, 3, 'j')
    l.ground(177, 182, 5); l.ground(183, 187, 3); l.put(179, 5, ',')
    l.ground(188, 216, 2); l.put(191, 2, ','); l.put(194, 2, '%'); l.line(190, 196, 5); l.put(202, 2, 'G'); l.put(209, 2, '%')
    l.room(217)
    return l

def w1l2():
    l = L(262)
    l.ground(0, 20, 2); l.put(3, 2, 'S'); l.put(8, 2, '%'); l.put(15, 2, 'e'); l.put(12, 6, '?'); l.put(13, 6, 'b'); l.put(14, 6, '!'); l.put(15, 6, 'b')
    l.row(24, 27, 4); l.row(31, 34, 6); l.row(38, 42, 4); l.line(25, 26, 6); l.line(32, 33, 8); l.line(39, 41, 6); l.put(32, 9, 'y')
    l.ground(46, 70, 2); l.put(48, 2, 'T'); l.row(52, 58, 9); l.line(52, 58, 10); l.put(55, 13, 'H'); l.put(62, 2, 'e'); l.put(64, 2, 'e'); l.put(66, 2, 'n')
    l.stairs(66, 1, 2)
    l.put(58, 2, ','); l.put(60, 2, '%')
    l.put(74, 5, 'M'); l.put(83, 5, 'M')
    l.ground(90, 116, 3); l.put(92, 3, 'C'); l.put(100, 3, 'j'); l.put(108, 3, 'j'); l.row(96, 102, 7, 'b'); l.put(99, 7, '?'); l.line(96, 102, 8); l.put(112, 3, '%')
    l.fill(116, 116, 3, 5, 'X')
    l.ground(120, 125, 4); l.ground(129, 134, 6); l.ground(138, 143, 4); l.line(121, 124, 6); l.line(130, 133, 8); l.put(131, 9, 'y'); l.put(127, 7, 'y')
    l.ground(147, 176, 2); l.put(150, 2, 'D'); l.put(158, 2, 'n'); l.put(162, 2, 'e'); l.put(166, 2, 'e'); l.row(156, 160, 6); l.put(158, 7, 'j')
    l.put(170, 6, 'h'); l.put(171, 6, 'h')
    l.ground(180, 186, 3); l.fill(187, 187, 3, 3, 'X'); l.ground(190, 198, 3); l.put(194, 3, 'p')
    l.ground(202, 230, 2); l.line(204, 210, 5); l.put(218, 2, 'G'); l.put(225, 2, '%')
    l.room(231)
    return l

def w1l3():
    l = L(270)
    l.ground(0, 16, 2); l.put(3, 2, 'S'); l.put(10, 2, ','); l.stairs(13, 4, 2, 'X')
    l.ground(17, 28, 6); l.put(22, 6, 'e'); l.put(20, 10, '!'); l.put(26, 6, 'r')
    l.ground(29, 34, 4); l.put(31, 4, 'T')
    l.row(38, 42, 9); l.line(38, 42, 10); l.ground(46, 60, 6); l.put(48, 6, 'j'); l.put(55, 6, 'n'); l.put(52, 10, 'h')
    l.row(64, 66, 8); l.row(70, 72, 10); l.row(76, 78, 8); l.put(71, 11, 'o'); l.put(68, 11, 'y'); l.put(74, 11, 'y')
    l.ground(82, 106, 5); l.put(84, 5, 'C'); l.put(90, 5, '^'); l.put(91, 5, '^'); l.put(97, 5, 'p'); l.put(102, 5, 'e'); l.row(90, 92, 9, 'b'); l.put(91, 9, '?')
    l.ground(107, 112, 3); l.put(116, 7, 'V'); l.ground(122, 142, 7); l.put(125, 7, 'D'); l.put(132, 7, 'j'); l.put(137, 7, 'n'); l.line(128, 136, 10)
    l.ground(143, 150, 4); l.put(146, 4, 'T'); l.row(151, 158, 11, 'b'); l.line(151, 158, 12); l.put(155, 8, '*')
    l.ground(162, 170, 3); l.fill(164, 165, 3, 3, '^'); l.put(168, 3, 'e')
    l.ground(174, 182, 5); l.ground(186, 192, 7); l.ground(196, 214, 5); l.put(200, 5, 'p'); l.put(206, 5, 'e'); l.put(209, 5, 'e'); l.line(200, 208, 9)
    l.ground(215, 240, 2); l.put(226, 2, 'G'); l.put(233, 2, '%')
    l.room(241)
    return l

def w2l1():
    l = L(262)
    l.ground(0, 24, 2); l.put(3, 2, 'S'); l.put(7, 2, 'k'); l.put(14, 2, 'e'); l.put(11, 6, '?'); l.put(19, 2, 'n'); l.put(22, 2, 'k')
    l.ground(28, 44, 3); l.fill(32, 33, 3, 3, '^'); l.row(31, 34, 7); l.line(31, 34, 8); l.put(40, 3, 'j')
    l.ground(48, 53, 4); l.ground(57, 62, 6); l.ground(66, 71, 4); l.line(58, 61, 8); l.put(64, 9, 'y')
    l.ground(75, 110, 2); l.put(78, 6, '!'); l.put(76, 6, 'b'); l.put(77, 6, 'b'); l.put(79, 6, 'b'); l.put(85, 2, 'C'); l.put(90, 2, 'p'); l.put(95, 2, 'n'); l.put(100, 2, 'j'); l.put(105, 2, 'k'); l.put(97, 6, 'h')
    l.put(113, 5, 'M'); l.ground(122, 148, 3); l.put(126, 3, 'D'); l.fill(135, 137, 3, 3, '^'); l.row(134, 138, 7, 'b'); l.line(134, 138, 8); l.put(144, 3, 'e'); l.put(146, 3, 'e')
    l.ground(152, 158, 5); l.ground(162, 168, 7); l.put(165, 7, 'k'); l.put(170, 10, 'y'); l.ground(172, 178, 5); l.put(175, 9, '*')
    l.ground(182, 206, 3); l.put(188, 3, 'T'); l.row(192, 200, 11); l.line(192, 200, 12); l.put(196, 3, 'n'); l.put(200, 3, 'j'); l.put(203, 3, 'p')
    l.ground(210, 234, 2); l.line(212, 218, 5); l.put(224, 2, 'G'); l.put(230, 2, 'k')
    l.room(235)
    return l

def w2l2():
    l = L(274)
    l.ground(0, 18, 2); l.put(3, 2, 'S'); l.put(8, 2, 'r'); l.put(13, 2, 'j'); l.fill(0, 0, 0, 0, '#')
    l.fill(19, 45, 0, 0, 'l')
    for x, y, n in ((22,3,3),(28,5,3),(34,3,3),(40,5,3)): l.row(x, x+n-1, y)
    l.line(23, 23, 5); l.line(29, 29, 7); l.line(35, 35, 5); l.line(41, 41, 7); l.put(31, 8, 'y'); l.put(37, 7, 'y')
    l.ground(46, 74, 3); l.put(50, 7, '!'); l.put(49, 7, 'b'); l.put(51, 7, 'b'); l.put(56, 3, 'n'); l.put(62, 3, 'p'); l.put(68, 3, 'j'); l.put(60, 7, 'H')
    l.fill(75, 92, 0, 0, 'l'); l.put(78, 5, 'M'); l.put(87, 5, 'M')
    l.ground(93, 120, 3); l.put(96, 3, 'C'); l.fill(100, 101, 3, 3, '^'); l.fill(106, 107, 3, 3, '^'); l.row(100, 107, 7, 'b'); l.put(103, 7, '?'); l.line(100, 107, 8); l.put(113, 3, 'e'); l.put(115, 3, 'e'); l.put(117, 3, 'n')
    l.ground(124, 130, 5); l.put(127, 5, 'T'); l.row(133, 139, 12); l.line(133, 139, 13); l.ground(134, 148, 2); l.put(142, 2, 'D'); l.put(138, 2, 'p'); l.put(146, 2, 'j')
    l.fill(149, 163, 0, 0, 'l'); l.row(152, 154, 4); l.row(158, 160, 6); l.put(156, 8, 'y')
    l.ground(164, 190, 3); l.put(170, 3, 'n'); l.put(176, 3, 'j'); l.put(182, 3, 'p'); l.put(186, 3, 'p'); l.row(172, 178, 7); l.line(172, 178, 8)
    l.ground(194, 200, 5); l.ground(204, 210, 7); l.ground(214, 244, 3); l.line(216, 222, 6); l.put(234, 3, 'G'); l.put(240, 3, 'k')
    l.room(245)
    return l

def w2l3():
    l = L(282)
    l.ground(0, 20, 2); l.put(3, 2, 'S'); l.put(8, 2, '|'); l.put(16, 2, '|'); l.put(12, 6, '!'); l.fill(14, 15, 12, 14, '#')
    l.ground(24, 50, 2); l.fill(24, 50, 12, 14, '#'); l.fill(30, 31, 2, 2, '^'); l.fill(38, 39, 2, 2, '^'); l.put(34, 2, 'p'); l.put(44, 2, 'j'); l.put(27, 2, '|'); l.put(47, 2, '|'); l.row(30, 39, 6, 'b'); l.put(35, 6, '?'); l.put(36, 10, 'h')
    l.fill(51, 66, 0, 0, 'l'); l.put(54, 4, 'M'); l.put(62, 6, 'V')
    l.ground(67, 90, 4); l.put(70, 4, 'C'); l.put(76, 4, 'n'); l.put(82, 4, 'p'); l.put(86, 4, 'e'); l.put(73, 4, '|'); l.fill(78, 79, 4, 4, '^'); l.row(77, 80, 8, 'b'); l.put(88, 8, 'y')
    l.ground(94, 98, 6); l.ground(102, 106, 8); l.ground(110, 114, 6); l.put(100, 11, 'y'); l.put(108, 11, 'y'); l.line(103, 105, 10)
    l.fill(115, 136, 0, 0, 'l'); l.put(118, 4, 'M'); l.row(125, 127, 7); l.put(131, 4, 'M'); l.line(125, 127, 9)
    l.ground(137, 170, 3); l.put(140, 3, 'D'); l.put(146, 3, 'j'); l.put(152, 3, 'p'); l.put(158, 3, 'n'); l.put(164, 3, 'p'); l.row(148, 156, 7, 'b'); l.put(152, 7, '*'); l.fill(144, 145, 3, 3, '^'); l.fill(160, 161, 3, 3, '^')
    l.ground(174, 180, 5); l.ground(184, 190, 7); l.ground(194, 200, 5); l.put(197, 5, 'p')
    l.ground(204, 252, 3); l.put(208, 3, 'e'); l.put(212, 3, 'e'); l.put(216, 3, 'n'); l.put(220, 3, 'j'); l.line(222, 232, 6); l.put(240, 3, 'G'); l.put(236, 3, '|'); l.put(244, 3, '|')
    l.room(253)
    return l

def boss():
    l = L(62)
    l.ground(0, 61, 2); l.put(3, 2, 'S'); l.put(8, 2, '|'); l.put(14, 2, '|'); l.put(10, 6, '!'); l.put(12, 6, '*'); l.line(18, 24, 4)
    l.fill(61, 61, 0, 14, '#')
    l.row(36, 38, 6); l.row(50, 52, 6)
    l.put(48, 2, 'B'); l.put(58, 2, 'G'); l.put(30, 2, '|'); l.put(56, 2, '|')
    return l

out = Path(sys.argv[1]); out.mkdir(parents=True, exist_ok=True)
for n, f in (('w1l1',w1l1),('w1l2',w1l2),('w1l3',w1l3),('w2l1',w2l1),('w2l2',w2l2),('w2l3',w2l3),('boss',boss)):
    f().save(n, out)
