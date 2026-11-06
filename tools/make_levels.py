#!/usr/bin/env python3
"""Writes Assets/Resources/Levels/*.txt. Rows are top first; y counts up from the bottom row (0).
Legend: # ground, g stone, X planks, b brick, ? coin block, ! power box, * life block, h/H hidden coin/life,
P palm, F storm flag, o coin, s star, e slime, n snail, y fly, j frog, p spike slime, B boss, S start, G gate, C checkpoint, D secret door, E room door,
R room wall, T spring, M/V moving platform, ^ spikes, l lava, w water, | torch, decor , % r k f >,
{ } conveyor left/right, u crumbling block, a/A switch blocks, 1 switch, Q false wall, L room lamp.
Lines starting with @ are notes for what tiles cannot say, see Grid2.Note. Each level is built around one idea,
in three parts: met safely, developed, then combined with a twist."""
import sys
from pathlib import Path
class L:
    def __init__(s, w, h=15, idea=''):
        s.w = w; s.H = h; s.g = [[' ']*w for _ in range(h)]; s.notes = ['idea ' + idea]
    def note(s, t): s.notes.append(t)
    def put(s, x, y, ch): s.g[s.H-1-y][x] = ch
    def get(s, x, y): return s.g[s.H-1-y][x]
    def ground(s, x0, x1, top, ch='#'):
        for x in range(x0, x1+1):
            for y in range(top): s.put(x, y, ch)
    def fill(s, x0, x1, y0, y1, ch):
        for x in range(x0, x1+1):
            for y in range(y0, y1+1): s.put(x, y, ch)
    def row(s, x0, x1, y, ch='#'):
        for x in range(x0, x1+1): s.put(x, y, ch)
    def coins(s, pts):
        for x, y in pts: s.put(x, y, 'o')
    def line(s, x0, x1, y):
        for x in range(x0, x1+1): s.put(x, y, 'o')
    def col(s, x, y0, y1):
        for y in range(y0, y1+1): s.put(x, y, 'o')
    def arc(s, x0, y0, n=5):
        for i in range(n):
            k = min(i, n-1-i)
            s.put(x0+i, y0+min(k, 2), 'o')
    def stairs(s, x0, n, y, ch='X', up=True):
        for i in range(n):
            h = i+1 if up else n-i
            s.fill(x0+i, x0+i, y, y+h-1, ch)
    def plat(s, cx, top, dx, dy, period, phase=0, width=3):
        s.note(f'plat {cx} {top} {dx} {dy} {period} {phase} {width}')
    def box(s, x0, rows=15):
        """The stone box of the bonus room, from the wall at x0 to the right edge."""
        s.fill(x0, x0, 0, s.H-1, 'g'); s.put(x0, 0, 'R')
        x1 = s.w-1
        s.fill(x0+1, x1, 0, 1, 'g'); s.fill(x0+1, x1, rows-2, rows-1, 'g'); s.fill(x1, x1, 0, rows-1, 'g')
        return x0+1, x1-1
    def save(s, name, out):
        rows = ['@' + n for n in s.notes] + [''.join(r).rstrip() for r in s.g]
        (out/f'{name}.txt').write_text('\n'.join(rows) + '\n')
        t = '\n'.join(''.join(r) for r in s.g)
        print(name, 'w', s.w, 'h', s.H, {k: t.count(k) for k in 'senyjp!?TCDEG'})

def w1l1():
    """Springs. Met on open ground, then the only way up a wall, then chained over pits; the twist is a tower of springs."""
    l = L(214, idea='springs')
    # Met safely: flat ground, nothing to lose
    l.ground(0, 42, 2); l.put(9, 2, 'S'); l.put(6, 2, '>'); l.put(11, 2, ',')
    l.put(13, 5, '?'); l.put(16, 5, '!'); l.put(19, 5, '?'); l.put(24, 2, 'e')
    l.put(30, 2, 'T'); l.col(30, 5, 10); l.row(33, 38, 8); l.line(33, 38, 9); l.put(37, 2, '%')
    # The wall: a spring is the only way over. A false wall on the far side hides the first star
    l.ground(43, 58, 9); l.put(41, 2, 'T'); l.put(48, 9, 'e'); l.line(46, 52, 10)
    l.fill(50, 58, 2, 3, ' '); l.fill(55, 58, 2, 3, 'Q'); l.put(51, 2, 's'); l.line(52, 54, 2)
    # Down the far side, then a chain of springs over pits
    l.ground(59, 70, 2); l.put(61, 2, ','); l.put(66, 2, 'n')
    l.ground(75, 77, 2); l.put(76, 2, 'T'); l.ground(83, 85, 2); l.put(84, 2, 'T'); l.arc(77, 9, 7)
    l.ground(91, 122, 2); l.put(94, 2, 'C'); l.put(92, 2, ',')
    # Second half: higher and narrower, with something to stomp on the way
    l.put(101, 5, '!'); l.put(100, 5, 'b'); l.put(102, 5, 'b'); l.put(106, 2, 'e'); l.put(109, 2, 'e')
    l.put(112, 2, 'D'); l.put(117, 2, 'T'); l.row(119, 122, 9); l.line(119, 122, 10); l.put(121, 9, 'n')
    l.ground(127, 129, 2); l.put(128, 2, 'T'); l.row(134, 137, 10); l.put(136, 11, 'y')
    l.ground(141, 143, 2); l.put(142, 2, 'T')
    # The twist: a tower. Each spring reaches the next ledge, the top one holds the second star
    l.ground(149, 176, 2); l.put(162, 2, 'T'); l.row(155, 158, 8); l.put(156, 8, 'T'); l.row(150, 152, 12); l.put(151, 13, 's')
    l.row(161, 165, 10); l.line(161, 165, 11); l.put(170, 2, 'j'); l.put(173, 2, 'e')
    l.ground(177, 196, 2); l.line(178, 184, 5); l.put(187, 2, 'G'); l.put(193, 2, '%')
    x0, x1 = l.box(197)
    # Bonus room: every coin hangs above a spring, and the clock is running
    l.note('room coins 30')
    l.put(x0+1, 2, 'E')
    for x in (x0+5, x0+9, x0+13): l.put(x, 2, 'T'); l.col(x, 6, 10)
    l.put(x0+9, 4, 's')
    return l

def w1l2():
    """Moving platforms. Met over a shallow ditch, then over pits and as lifts; the twist is a relay out of step."""
    l = L(244, idea='moving')
    l.ground(0, 34, 2); l.put(9, 2, 'S'); l.put(7, 2, '%')
    # Two power boxes right at the start: big, then the sevivon
    l.put(11, 5, '!'); l.put(12, 5, 'b'); l.put(13, 5, 'b'); l.put(14, 5, '!'); l.put(22, 2, 'e'); l.put(26, 2, 'e'); l.put(30, 2, 'n')
    # Met safely: a ditch you can walk out of
    l.ground(35, 49, 1); l.stairs(47, 3, 1, '#'); l.ground(50, 56, 4); l.plat(41.5, 4, 3.5, 0, 6); l.line(38, 45, 6)
    # Pits, then a lift. A star sits high over the pit
    l.plat(63.5, 5, 3, 0, 6); l.plat(59.5, 8, 0, 3.5, 6, .25, 2); l.row(62, 64, 11); l.put(63, 12, 's'); l.ground(69, 76, 5); l.put(73, 5, 'n')
    l.plat(80.5, 8, 0, 3, 6); l.ground(84, 92, 9); l.line(85, 91, 10); l.put(88, 9, 'e'); l.put(88, 13, 'h')
    l.plat(98.5, 7, 3.5, 0, 6, .5); l.ground(105, 130, 2); l.put(108, 2, 'C'); l.put(106, 2, ',')
    l.put(116, 5, '!'); l.put(115, 5, 'b'); l.put(117, 5, 'b'); l.put(121, 2, 'j'); l.put(126, 2, 'D')
    # The twist: a relay, each platform out of step with the last, then two lifts with a fly between them
    l.plat(135.5, 4, 2.5, 0, 4); l.plat(143.5, 6, 2.5, 0, 4, .5); l.plat(151.5, 8, 2.5, 0, 4); l.line(149, 153, 11)
    l.ground(157, 162, 8); l.put(160, 8, 'n')
    l.plat(167.5, 7, 0, 3.5, 6, .25); l.put(171, 10, 'y'); l.plat(175.5, 7, 0, 3.5, 6, .75); l.ground(180, 186, 10); l.put(183, 12, 's')
    l.plat(192.5, 6, 3, 0, 6)
    l.ground(198, 219, 2); l.put(203, 2, 'n'); l.line(200, 206, 5); l.put(211, 2, 'G'); l.put(216, 2, '%')
    x0, x1 = l.box(220)
    # Bonus room: no enemies, only a clock and platforms; a fall just costs the time to climb back
    l.note('room race 10')
    l.ground(x0, x0+3, 4, 'g'); l.put(x0+1, 4, 'E'); l.stairs(x0+4, 1, 2, 'g')
    l.plat(x0+8.5, 5, 2, 0, 4, 0, 3); l.plat(x0+14.5, 7, 2, 0, 4, .5, 3)
    l.ground(x1-3, x1, 7, 'g'); l.put(x1-1, 8, 's')
    return l

def w1l3():
    """A vertical climb. Slabs catch every fall at first; then lifts; after the flag the ledges crumble under you."""
    W, H = 58, 84
    l = L(W, H, 'climb')
    M = 36
    l.ground(0, M-1, 2, '#'); l.put(11, 2, 'S'); l.put(9, 2, ','); l.put(14, 2, 'e')
    def slab(y, gap0, gap1):
        l.row(0, gap0-1, y); l.row(gap1+1, M-1, y)
    # Met safely: wide steps, a slab under every part of the climb
    l.put(8, 5, '!'); l.put(11, 5, '?'); l.row(16, 21, 5); l.row(24, 29, 8); l.row(31, 35, 11)
    slab(14, 24, 27); l.put(6, 14, 'n'); l.put(32, 14, '%'); l.line(25, 26, 12)
    l.row(18, 22, 17); l.row(11, 15, 20); l.row(3, 8, 23); l.put(5, 26+3, '!'); l.put(4, 29, 'b'); l.put(6, 29, 'b'); l.put(14, 26, 'j')
    slab(26, 0, 2); l.put(20, 26, 'e'); l.put(30, 26, ',')
    # Lifts carry you up the middle
    l.plat(32.5, 31, 0, 3, 6); l.row(24, 28, 33); l.line(24, 28, 34); l.plat(18.5, 36, 3, 0, 6); l.row(7, 12, 37); l.put(9, 37, 'n')
    l.row(0, 3, 40); l.put(1, 41, 'D')
    l.row(6, M-1, 43); l.put(14, 43, 'C'); l.put(12, 43, ','); l.put(24, 46, '!'); l.put(23, 46, 'b'); l.put(25, 46, 'b'); l.put(30, 43, 'p')
    # The twist: ledges that crumble, with nothing to catch a fall until the slab below
    for x, y in ((33, 46), (29, 49), (25, 52), (21, 55)): l.row(x, x+2, y, 'u')
    l.row(14, 18, 57); l.put(16, 57, 'e')
    for x, y in ((9, 60), (5, 63)): l.row(x, x+2, y, 'u')
    l.row(0, 2, 65); l.put(1, 66, 's')
    l.plat(12.5, 66, 0, 2.5, 6); l.row(16, 19, 69); l.put(18, 70, 'T'); l.row(10, 13, 74, 'u'); l.row(22, 26, 72, 'u'); l.row(29, M-1, 74)
    l.put(33, 74, 'G'); l.put(31, 74, '|'); l.put(35, 74, ',')
    # A pocket behind a false wall, off the first slab
    l.fill(M-5, M-1, 15, 16, ' ')
    l.row(M-5, M-1, 17, '#'); l.fill(M-6, M-6, 15, 16, 'Q'); l.put(M-2, 15, 's'); l.line(M-5, M-3, 15)
    x0, x1 = l.box(M)
    # Bonus room: it looks like a dead end at the top of the crumbling steps. One wall is not a wall
    l.note('room hidden 0')
    l.put(x0+1, 2, 'E')
    for x, y in ((x0+6, 4), (x0+10, 7), (x0+6, 10)): l.row(x, x+2, y, 'u')
    l.fill(x0+13, x1, 11, 12, 'g'); l.fill(x0+12, x0+12, 2, 12, 'g'); l.fill(x0+12, x0+12, 8, 10, 'Q'); l.fill(x0+13, x1, 2, 7, 'g')
    l.put(x1-1, 8, 's'); l.line(x0+13, x1-2, 8)
    return l

def w2l1():
    """Sandstorm. One clock for the whole level: calm, warning, storm. Met on flat ground with a shelter close by, then
    islands where it decides each jump; past the flag it turns around and throws you forward, and rising air opens a short way up.
    Everything can also be reached in the calm, so the storm is a threat to read and never a lock."""
    l = L(246, idea='wind')
    l.note('storm 6 2 4')
    l.note('gust 0 45 0 15 -3.5'); l.note('gust 45 112 0 15 -5.5'); l.note('gust 112 227 0 15 6')
    l.note('gust 152 158 0 15 0 62'); l.note('gust 173 178 0 15 0 62')
    def lee(x, top, side):
        """A stone windbreak at x, three blocks tall; the safe spot is on the side away from the wind."""
        l.fill(x, x, top, top+2, 'g')
        l.note(f'shelter {x-3 if side < 0 else x+1} {x if side < 0 else x+4} {top} {top+2}')
    # Met safely: flat ground, and the first storm is weak
    l.ground(0, 44, 2); l.put(9, 2, 'S'); l.put(7, 2, 'k'); l.put(12, 5, '!'); l.put(15, 5, '!'); l.put(13, 5, 'b'); l.put(14, 5, 'b')
    l.put(4, 2, 'P'); l.put(18, 2, 'P'); l.put(11, 2, 'F')
    lee(24, 2, -1); l.put(30, 2, 'e'); l.put(31, 2, 'k'); l.put(27, 2, 'F'); l.line(28, 40, 3); l.put(36, 2, 'n'); l.put(34, 2, 'P'); lee(42, 2, -1)
    # Islands: each has a windbreak, and the storm decides whether a jump lands
    l.ground(50, 56, 2); lee(55, 2, -1); l.put(50, 2, 'P'); l.ground(62, 68, 3); lee(67, 3, -1); l.ground(74, 80, 3); lee(79, 3, -1); l.put(75, 3, 'F')
    l.arc(57, 6, 5); l.arc(69, 7, 5); l.put(63, 3, 'j')
    # The flag; then the wind turns around at the end of this ground
    l.ground(82, 112, 2); l.put(85, 2, 'C'); l.put(83, 2, 'k'); l.put(87, 2, 'P'); l.put(92, 5, '!'); l.put(91, 5, 'b'); l.put(93, 5, 'b')
    lee(96, 2, -1); l.put(101, 2, 'p'); l.put(100, 2, 'F'); l.put(104, 2, 'D'); lee(110, 2, -1)
    # Tailwind: over two gaps, and under an overhang that keeps the storm off a star
    l.ground(118, 122, 2); l.line(114, 126, 6); l.put(120, 8, 'y'); l.put(120, 2, 'F')
    l.ground(127, 150, 2); l.put(128, 2, 'P'); l.fill(132, 132, 2, 5, 'g'); l.row(133, 139, 5, 'g'); l.note('shelter 133 138 2 4'); l.put(136, 2, 's')
    l.put(146, 2, 'e'); l.put(148, 2, 'n'); l.put(142, 2, 'P')
    # The twist: rising air, only in the storm, is a quick way up; a spring does the same job in the calm
    l.ground(151, 158, 2); l.put(153, 2, '>'); l.put(155, 2, 'T')
    l.ground(160, 172, 9); lee(162, 9, 1); l.put(168, 9, 'e'); l.put(170, 9, 'k'); l.line(164, 171, 10); l.put(171, 9, 'F')
    l.row(176, 177, 10, 'X')
    l.row(182, 186, 12); l.put(184, 13, 's'); l.ground(190, 198, 4); lee(190, 4, 1); l.put(196, 4, 'e')
    l.ground(198, 226, 2); l.line(200, 207, 5); lee(209, 2, 1); l.put(205, 2, 'P'); l.put(217, 2, 'G'); l.put(223, 2, 'k'); l.put(215, 2, 'P')
    x0, x1 = l.box(227)
    # Bonus room: a switch turns the wind around. Across on the tailwind, flip it, and ride back to the high ledge
    l.note('room puzzle 0')
    l.put(x0+1, 5, 'E'); l.ground(x0, x0+3, 5, 'g'); l.put(x0+2, 11, 's'); l.row(x0, x0+3, 10, 'g')
    l.ground(x1-4, x1, 5, 'g'); l.put(x1-2, 5, '1')
    l.note(f'wind {x0+4} {x1-5} 0 13 8 0'); l.note(f'wind {x0+4} {x1-5} 0 13 -8 1')
    l.put(x0+5, 2, 'T')
    return l

def w2l2():
    """Water. The tide comes and goes over low ground; the climb out is timed; the twist is a flood that never stops."""
    W, H = 240, 34
    l = L(W, H, 'water')
    M = 214
    l.ground(0, 26, 2); l.put(9, 2, 'S'); l.put(8, 2, 'r'); l.put(11, 5, '!'); l.put(14, 5, '!'); l.put(12, 5, 'b'); l.put(13, 5, 'b')
    l.note('tide -1 5.2 10 27 150')
    # Rocks stand above the high tide; the ground between them drowns
    def rock(x0, x1, top=6):
        l.ground(x0, x1, top); l.stairs(x0-2, 2, 2, 'X')
    rock(20, 26); l.put(23, 6, 'r')
    # Met safely: a short run between two rocks
    l.ground(27, 38, 2); rock(39, 44); l.line(29, 37, 3); l.put(34, 2, 'e')
    l.ground(45, 62, 2); rock(63, 67); l.line(47, 60, 3); l.put(52, 2, 'n')
    # Developed: a ledge halfway, because the run is too long for one low tide
    l.ground(68, 92, 2); l.row(77, 80, 5); l.put(78, 6, 'e'); rock(93, 99); l.put(96, 6, 'k'); l.put(79, 9, 'h')
    l.ground(100, 104, 2); rock(105, 118); l.put(107, 6, 'C'); l.put(112, 9, '!'); l.put(111, 9, 'b'); l.put(113, 9, 'b'); l.put(115, 6, 'D')
    l.ground(119, 138, 2); l.row(124, 126, 5); l.row(131, 133, 5); l.put(125, 2, 's'); l.put(137, 2, 'n'); rock(139, 150); l.put(144, 6, 'r')
    # The twist: past this point the water rises and does not stop. Up the canyon, fast
    l.note('flood 150 2 .85 33')
    l.ground(151, 160, 6); l.ground(161, M-1, 9)
    l.row(166, 170, 11); l.row(174, 178, 14); l.row(168, 171, 17); l.row(159, 163, 19); l.row(151, 155, 21); l.put(153, 22, 'e')
    l.row(160, 165, 23); l.row(170, 176, 25); l.put(173, 26, 'n'); l.put(165, 27, 'y'); l.row(182, 186, 26); l.row(190, 196, 27)
    l.put(181, 31, 's'); l.row(179, 182, 29); l.row(199, M-1, 28); l.put(206, 29, 'G'); l.put(203, 29, 'r'); l.put(211, 29, 'k'); l.line(191, 195, 28)
    x0, x1 = l.box(M, 15)
    # Bonus room: the switch drains the pool but also moves the blocks. It takes two presses to reach the star
    l.note('room pool 0')
    l.note(f'pool {x0+5} {x1-6} 7.5 -1')
    l.ground(x0, x0+4, 8, 'g'); l.put(x0+1, 8, 'E'); l.put(x0+3, 8, '1'); l.stairs(x0+5, 4, 2, 'g', up=False)
    l.ground(x1-5, x1, 8, 'g'); l.stairs(x1-8, 3, 2, 'g')
    l.put(x1-5, 8, '1'); l.fill(x1-3, x1-3, 8, 12, 'A'); l.put(x1-1, 8, 's')
    return l

def w2l3():
    """Darkness. Torches light the first steps; then they thin out; the twist is conveyors in the dark."""
    l = L(262, idea='dark')
    l.note('dark 5.5')
    l.ground(0, 40, 2); l.put(9, 2, 'S'); l.fill(0, 40, 13, 14, '#')
    for x in (6, 14, 22, 30, 38): l.put(x, 2, '|')
    l.put(10, 5, '!'); l.put(13, 5, '!'); l.put(11, 5, 'b'); l.put(12, 5, 'b'); l.put(19, 2, 'e'); l.put(27, 2, 'n')
    # Met safely: a pit only where a torch shows it
    l.ground(44, 56, 2); l.put(45, 2, '|'); l.fill(44, 56, 13, 14, '#'); l.fill(50, 51, 2, 2, '^'); l.put(48, 2, '|'); l.put(54, 2, '|')
    # Developed: fewer torches, spikes you see late, and lava
    l.fill(57, 72, 0, 0, 'l'); l.row(60, 62, 3); l.row(66, 68, 5); l.put(67, 6, '|'); l.line(60, 62, 5)
    l.ground(73, 104, 3); l.put(75, 3, '|'); l.fill(82, 83, 3, 3, '^'); l.put(90, 3, 'p'); l.fill(96, 97, 3, 3, '^'); l.put(93, 6, 'H'); l.row(84, 90, 6, 'b'); l.put(87, 6, '?')
    l.ground(105, 110, 5); l.ground(114, 118, 7); l.put(116, 7, '|'); l.put(112, 10, 'y')
    l.ground(122, 142, 3); l.put(124, 3, 'C'); l.put(126, 3, '|'); l.put(131, 6, '!'); l.put(130, 6, 'b'); l.put(132, 6, 'b'); l.put(138, 3, 'D'); l.put(140, 3, '|')
    # The twist: conveyors. Against you over lava, then with you into spikes, with torches only at the ends
    l.fill(143, 196, 0, 0, 'l')
    l.row(146, 156, 3, '{'); l.row(157, 158, 3); l.put(158, 4, 'T'); l.put(150, 8, 'y'); l.row(160, 170, 5, '}'); l.put(159, 5, '|'); l.line(160, 170, 7)
    l.row(174, 177, 3); l.put(175, 4, '|'); l.row(181, 192, 3, '{'); l.fill(186, 187, 4, 4, '^')
    l.ground(197, 244, 3); l.put(198, 3, '|'); l.put(205, 3, 'p'); l.row(209, 216, 3, '}'); l.fill(217, 218, 3, 3, '^'); l.put(222, 3, 'n')
    # Stars shine a little by themselves: a high one over the lava, one behind a false wall at the end
    l.row(150, 152, 11); l.put(151, 12, 's')
    l.fill(226, 233, 3, 5, '#'); l.fill(227, 232, 3, 4, ' '); l.fill(226, 226, 3, 4, 'Q'); l.put(231, 3, 's')
    l.put(237, 3, 'G'); l.put(235, 3, '|'); l.put(240, 3, '|')
    x0, x1 = l.box(245)
    # Bonus room: darker still. Light three lamps and the star appears
    l.note('room lights 0')
    l.put(x0+1, 2, 'E'); l.put(x0+3, 2, '|')
    l.row(x0+4, x0+7, 5, 'g'); l.put(x0+5, 7, 'L'); l.row(x0+9, x0+11, 8, 'g'); l.put(x0+10, 10, 'L'); l.put(x1-1, 3, 'L'); l.fill(x1-3, x1-3, 2, 3, '^')
    l.put(x0+13, 3, 's')
    return l

def boss():
    """A chase. The king runs you down the road first; the flag stops him; then you fight him on crumbling ground."""
    l = L(236, idea='chase')
    l.note('chase 14 -4 6.6 124')
    l.ground(0, 34, 2); l.put(9, 2, 'S'); l.put(8, 2, '|')
    # Met safely: nothing but open road for a few seconds
    l.put(20, 5, '!'); l.put(23, 5, '!'); l.put(21, 5, 'b'); l.put(22, 5, 'b')
    l.ground(38, 54, 3); l.put(46, 3, 'e'); l.line(40, 52, 6)
    l.ground(58, 66, 3); l.put(64, 7, 's'); l.row(63, 65, 6, 'b')
    l.fill(67, 80, 0, 0, 'l'); l.row(70, 79, 3, 'u'); l.fill(74, 74, 4, 4, '^')
    l.ground(81, 92, 4); l.ground(93, 105, 2); l.put(97, 2, 'p'); l.put(101, 6, '?')
    l.fill(106, 116, 0, 0, 'l'); l.row(109, 113, 3, 'u')
    l.ground(117, 148, 2); l.fill(124, 124, 5, 14, 'g'); l.put(127, 2, 'C'); l.put(125, 2, '|')
    l.put(132, 5, '!'); l.put(131, 5, 'b'); l.put(133, 5, 'b'); l.put(136, 5, '*'); l.put(143, 2, 'D'); l.put(140, 2, '|')
    # The arena: the king again, with crumbling ledges overhead and a star above them
    l.ground(149, 214, 2); l.fill(214, 214, 2, 14, 'g')
    l.row(155, 159, 5, 'u'); l.row(170, 174, 5, 'u'); l.row(185, 189, 5, 'u'); l.row(162, 166, 8); l.row(178, 182, 8); l.put(180, 10, 's')
    l.put(178, 2, 'B'); l.put(205, 2, 'G'); l.put(200, 2, '|'); l.put(210, 2, '|')
    x0, x1 = l.box(215)
    # Bonus room: a clock and a crowd of slimes
    l.note('room stomp 25')
    l.put(x0+1, 2, 'E')
    for x in (x0+6, x0+9, x0+12, x0+15): l.put(x, 2, 'e')
    l.row(x0+7, x0+11, 5); l.put(x0+9, 7, 's')
    return l

def test():
    """Not a real level: one open floor with everything the self test needs at known places."""
    l = L(120, idea='test')
    l.note('room test 0')
    l.ground(0, 90, 2); l.put(3, 2, 'S'); l.put(70, 2, 'C'); l.put(76, 2, 'D'); l.put(84, 2, 'G')
    l.put(20, 6, 'b'); l.put(24, 6, '?'); l.put(28, 6, '!'); l.put(32, 6, 'h'); l.put(36, 2, 'T')
    l.row(40, 42, 2, 'u'); l.row(46, 50, 1, '}'); l.put(54, 2, 's'); l.put(56, 2, 's'); l.put(58, 2, 's')
    l.fill(66, 66, 2, 8, '#'); l.fill(91, 94, 0, 14, '#')
    x0, x1 = l.box(95); l.put(x0+2, 2, 'E')
    return l

out = Path(sys.argv[1]); out.mkdir(parents=True, exist_ok=True)
for n, f in (('w1l1',w1l1),('w1l2',w1l2),('w1l3',w1l3),('w2l1',w2l1),('w2l2',w2l2),('w2l3',w2l3),('boss',boss),('test',test)):
    f().save(n, out)
