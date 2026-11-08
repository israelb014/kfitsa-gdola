#!/usr/bin/env python3
"""Writes Assets/Resources/Levels/*.txt. Rows are top first; y counts up from the bottom row (0).
Legend: # ground, g stone, X planks, b brick, ? coin block, ! power box, * life block, h/H hidden coin/life,
P palm, F storm flag, o coin, s star, e slime, n snail, y fly, j frog, p spike slime, B boss, S start, G gate, C checkpoint, D secret door, E room door,
R room wall, T spring, M/V moving platform, ^ spikes, l lava, w water, | torch, decor , % r k f >,
{ } conveyor left/right, u crumbling block, a/A switch blocks, 1 switch, Q false wall, L room lamp,
I ladder (its top is a ledge), t rope, q block slime, c sinking snow cloud (each run of c is one cloud), m worm, z mouse (enemies near it sleep until it squeaks).
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
    l.note('needs springs')
    # Met safely: flat ground, nothing to lose
    l.ground(0, 42, 2); l.put(9, 2, 'S'); l.put(6, 2, '>'); l.put(11, 2, ',')
    l.put(13, 5, '?'); l.put(16, 5, '!'); l.put(19, 5, '?'); l.put(24, 2, 'e')
    l.put(30, 2, 'T'); l.col(30, 5, 10); l.row(33, 38, 9); l.line(33, 38, 10); l.put(37, 2, '%')
    # The wall: a spring is the only way over. A false wall on the far side hides the first star
    l.ground(43, 58, 10); l.put(41, 2, 'T'); l.put(48, 10, 'e'); l.line(46, 52, 11)
    l.row(190, 192, 7); l.line(190, 192, 8); l.note('easyflag 60 2'); l.note('easyflag 152 2')
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
    l.note('needs platforms')
    l.ground(0, 34, 2); l.put(9, 2, 'S'); l.put(7, 2, '%')
    # Two power boxes right at the start: big, then the sevivon
    l.put(11, 5, '!'); l.put(12, 5, 'b'); l.put(13, 5, 'b'); l.put(14, 5, '!'); l.put(22, 2, 'e'); l.put(26, 2, 'e'); l.put(30, 2, 'n')
    # Met safely: a ditch you can walk out of
    l.ground(35, 49, 1); l.stairs(47, 3, 1, '#'); l.ground(50, 56, 4); l.plat(41.5, 4, 3.5, 0, 6); l.line(38, 45, 6)
    # Pits, then a lift. A star sits high over the pit
    l.plat(63.5, 5, 3, 0, 6); l.plat(59.5, 8, 0, 3.5, 6, .25, 2); l.row(62, 64, 11); l.put(63, 12, 's'); l.ground(69, 76, 5); l.put(73, 5, 'n')
    l.plat(80.5, 8, 0, 3, 6); l.ground(84, 92, 9); l.line(85, 91, 10); l.put(88, 9, 'e'); l.put(88, 13, 'h')
    l.plat(98.5, 7, 3.5, 0, 6, .5); l.ground(105, 130, 2); l.put(108, 2, 'C'); l.put(106, 2, ','); l.row(213, 215, 7); l.line(213, 215, 8); l.note('easyflag 52 4'); l.note('easyflag 182 10')
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
    l.put(113, 5, '+')   # bubble shield box (v1.4)
    return l

def w1l3():
    """A vertical climb. Slabs catch every fall at first; then lifts; after the flag the ledges crumble under you."""
    W, H = 58, 84
    l = L(W, H, 'climb')
    l.note('needs crumble')
    M = 36
    l.ground(0, M-1, 2, '#'); l.row(1, 3, 7); l.line(1, 3, 8); l.put(11, 2, 'S'); l.put(9, 2, ','); l.put(14, 2, 'e')
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
    l.row(14, 18, 57); l.put(16, 58, 'e'); l.note('easyflag 28 27'); l.note('easyflag 15 58')
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
    l.put(18, 47, '+')   # bubble shield box (v1.4)
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
    l.ground(82, 112, 2); l.row(219, 221, 7); l.line(219, 221, 8); l.note('easyflag 38 2'); l.note('easyflag 144 2'); l.put(85, 2, 'C'); l.put(83, 2, 'k'); l.put(87, 2, 'P'); l.put(92, 5, '!'); l.put(91, 5, 'b'); l.put(93, 5, 'b')
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
    l.put(98, 5, '+')   # bubble shield box (v1.4)
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
    l.ground(100, 104, 2); rock(105, 118); l.row(142, 144, 11); l.line(142, 144, 12); l.note('easyflag 64 6'); l.note('easyflag 192 28'); l.put(107, 6, 'C'); l.put(112, 9, '!'); l.put(111, 9, 'b'); l.put(113, 9, 'b'); l.put(115, 6, 'D')
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
    l.put(121, 5, '+')   # bubble shield box (v1.4)
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
    l.ground(197, 244, 3); l.row(201, 203, 8); l.line(201, 203, 9); l.note('easyflag 77 3'); l.note('easyflag 200 3'); l.put(198, 3, '|'); l.put(205, 3, 'p'); l.row(209, 216, 3, '}'); l.fill(217, 218, 3, 3, '^'); l.put(222, 3, 'n')
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
    l.put(131, 10, '+')   # bubble shield box (v1.4)
    return l

def boss():
    """A chase. The king runs you down the road first; the flag stops him; then you fight him on crumbling ground."""
    l = L(236, idea='chase')
    l.note('needs crumble')
    l.note('chase 14 -4 6.6 124')
    l.ground(0, 34, 2); l.put(9, 2, 'S'); l.put(8, 2, '|')
    # Met safely: nothing but open road for a few seconds
    l.put(20, 5, '!'); l.put(23, 5, '!'); l.put(21, 5, 'b'); l.put(22, 5, 'b')
    l.ground(38, 54, 3); l.put(46, 3, 'e'); l.line(40, 52, 6)
    l.ground(58, 66, 3); l.put(64, 7, 's'); l.row(63, 65, 6, 'b')
    l.fill(67, 80, 0, 0, 'l'); l.row(70, 79, 3, 'u'); l.fill(74, 74, 4, 4, '^')
    l.ground(81, 92, 4); l.ground(93, 105, 2); l.put(97, 2, 'p'); l.put(101, 6, '?')
    l.fill(106, 116, 0, 0, 'l'); l.row(109, 113, 3, 'u')
    l.ground(117, 148, 2); l.row(137, 139, 7); l.line(137, 139, 8); l.note('easyflag 60 3'); l.note('easyflag 151 2'); l.fill(124, 124, 5, 14, 'g'); l.put(127, 2, 'C'); l.put(125, 2, '|')
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
    l.put(147, 5, '+')   # bubble shield box (v1.4)
    return l

def w3l1():
    """Ladders and ropes. A short ladder first, then walls too high to jump; then ropes over a pit; the twist is
    a tall climb past a fly and a high crossing from rope to rope."""
    W, H = 232, 28
    l = L(W, H, 'ladders')
    l.note('needs ladders')
    def ladder(x, y0, y1):
        for y in range(y0, y1+1): l.put(x, y, 'I')
    def rope(x, y0, y1):
        for y in range(y0, y1+1): l.put(x, y, 't')
        l.row(x-1, x+1, y1+1, 'g')      # the beam it hangs from
    # Met safely: a short ladder onto a low ledge, which a jump also reaches
    l.ground(0, 64, 2); l.put(9, 2, 'S'); l.put(7, 2, '>'); l.put(11, 2, ',')
    l.put(12, 5, '!'); l.put(13, 5, 'b'); l.put(14, 5, 'b'); l.put(15, 5, '!'); l.put(20, 2, 'e')
    ladder(26, 2, 5); l.row(27, 34, 5); l.line(28, 33, 6); l.put(31, 6, 'q')
    # The wall: nine tiles high, the ladder is the only way up
    l.ground(40, 52, 11); ladder(39, 2, 10); l.line(42, 50, 12); l.put(47, 11, 'q'); l.put(44, 11, ',')
    # A rope beside the wall holds the first star at its top
    rope(56, 6, 14); l.put(56, 15, 's')
    l.put(60, 2, 'n'); l.note('easyflag 61 2')
    # Developed: ropes over a pit with nothing below
    rope(69, 4, 9); rope(76, 4, 9); l.coins([(72, 8), (73, 8)])
    l.ground(81, 129, 2); l.put(83, 2, ','); l.put(90, 5, '+'); l.put(96, 2, 'e')
    l.put(100, 2, 'C'); l.put(98, 2, ',')
    l.put(104, 5, '!'); l.put(103, 5, 'b'); l.put(105, 5, 'b'); l.put(108, 6, 'h'); l.put(112, 2, 'D'); l.put(118, 2, 'q')
    # The twist: a tall climb with a fly crossing the ladder, then rope to rope high over a chasm
    l.ground(130, 140, 14); ladder(129, 2, 13); l.put(127, 9, 'y'); l.put(136, 14, 'e'); l.line(132, 139, 15)
    rope(146, 12, 21); rope(153, 12, 21); l.coins([(149, 18), (150, 18)])
    l.ground(158, 166, 12); l.put(161, 12, 'n')
    ladder(165, 12, 19); l.row(166, 169, 19); l.put(168, 20, 's')
    ladder(167, 2, 11)
    l.ground(167, 214, 2); l.put(180, 2, 'q'); l.put(190, 2, 'n'); l.line(193, 199, 5); l.note('easyflag 172 2')
    l.put(205, 2, 'G'); l.put(202, 2, ','); l.put(209, 2, '%')
    x0, x1 = l.box(215)
    # Bonus room: a race up the ladders and ropes to the star at the top
    l.note('room climb 12')
    l.put(x0+1, 2, 'E')
    ladder(x0+4, 2, 5); l.row(x0+5, x0+7, 5, 'g')
    for y in range(6, 13): l.put(x0+10, y, 't')      # this rope hangs from the ceiling
    l.row(x0+12, x1, 9, 'g'); l.put(x1-1, 10, 's')
    return l

def w3l2():
    """Sinking clouds. Over a shallow ditch first, where a cloud only sets you down; then over pits; the twist is a
    climb from cloud to cloud that never lets you stand still, with worms on the islands between."""
    l = L(230, 18, 'clouds')
    l.note('needs clouds')
    def cloud(x0, x1, y): l.row(x0, x1, y, 'c')
    # Met safely: a ditch with a floor, so a cloud that sinks just sets you down
    l.ground(0, 30, 3); l.put(9, 3, 'S'); l.put(7, 3, '>'); l.put(11, 3, ',')
    l.put(12, 6, '!'); l.put(13, 6, 'b'); l.put(14, 6, 'b'); l.put(15, 6, '!'); l.put(24, 3, 'm')
    l.ground(31, 47, 1); l.stairs(45, 2, 1, '#'); cloud(34, 36, 2); cloud(40, 42, 2); l.line(34, 36, 5); l.line(40, 42, 5)
    l.ground(47, 64, 3); l.put(52, 3, 'e'); l.put(58, 3, 'm'); l.note('easyflag 50 3')
    # Developed: clouds over pits with nothing below
    cloud(69, 71, 2); cloud(75, 77, 4); l.put(76, 9, 's'); cloud(82, 84, 3); l.arc(80, 7, 5)
    l.ground(89, 118, 3); l.put(92, 6, '+'); l.put(95, 3, 'm'); l.put(100, 7, 'h'); l.put(101, 3, 'm')
    l.put(106, 3, 'C'); l.put(104, 3, ',')
    l.put(110, 6, '!'); l.put(109, 6, 'b'); l.put(111, 6, 'b'); l.put(114, 3, 'D')
    # The twist: up and down from cloud to cloud, with an island where a worm keeps time
    cloud(123, 125, 4); cloud(129, 130, 6); cloud(134, 136, 8); cloud(140, 142, 6)
    l.ground(146, 150, 5); l.put(148, 5, 'm')
    cloud(155, 157, 5); cloud(161, 163, 7); cloud(167, 169, 9); l.put(168, 13, 's'); cloud(173, 175, 6)
    l.ground(180, 212, 3); l.put(186, 3, 'q'); l.put(192, 3, 'm'); l.line(194, 199, 6); l.note('easyflag 182 3')
    l.put(205, 3, 'G'); l.put(202, 3, ','); l.put(209, 3, '%')
    x0, x1 = l.box(213)
    # Bonus room: stomp every block slime and the star appears
    l.note('room blocks 0')
    l.put(x0+1, 2, 'E')
    for x in (x0+4, x0+7, x0+10): l.put(x, 2, 'q')
    l.row(x1-2, x1, 4, 'g'); l.put(x1-1, 5, 's')
    return l

def w3l3():
    """Sneaking. A mouse squeaks when the hero runs near it or bumps into it, and every enemy near it wakes up.
    Walking past is quiet, and a stomp silences it.
    Met on open ground with one sleeper; then sleepers below a mouse on a ledge, with worms; the twist is a
    corridor of sleepers between two mice, then sinking clouds over a pit with a mouse on the far side."""
    l = L(240, 18, 'sneak')
    l.ground(0, 62, 2); l.put(9, 2, 'S'); l.put(7, 2, '>'); l.put(11, 2, ',')
    l.put(12, 5, '!'); l.put(13, 5, 'b'); l.put(14, 5, 'b'); l.put(15, 5, '!')
    # Met safely: one mouse, one sleeping slime, room to back off
    l.put(36, 2, 'z'); l.put(44, 2, 'e'); l.line(40, 48, 5); l.put(52, 2, 'm'); l.note('easyflag 56 2')
    # Developed: a mouse keeps watch from a ledge over sleepers and worms
    l.row(70, 76, 6); l.put(73, 7, 'z'); l.ground(63, 112, 2)
    l.put(66, 2, 'n'); l.put(79, 2, 'p'); l.put(84, 2, 'e'); l.put(88, 2, 'm'); l.row(90, 93, 6); l.put(91, 10, 's')
    l.put(97, 5, '+'); l.put(101, 6, 'h'); l.put(104, 2, 'C'); l.put(102, 2, ',')
    l.put(108, 5, '!'); l.put(107, 5, 'b'); l.put(109, 5, 'b')
    # The twist: a low corridor of sleepers between two mice, where a running start is out of the question
    l.ground(113, 160, 2); l.put(116, 2, 'D'); l.fill(124, 152, 7, 17, '#')
    l.put(122, 2, 'z'); l.put(128, 2, 'e'); l.put(134, 2, 'n'); l.put(140, 2, 'e'); l.put(146, 2, 'p'); l.put(154, 2, 'z')
    l.line(126, 150, 3)
    # Then clouds over a pit, with worms on the far shore and a star above the last cloud
    l.row(166, 168, 3, 'c'); l.row(172, 174, 5, 'c'); l.row(178, 180, 4, 'c'); l.put(179, 9, 's')
    l.ground(184, 222, 2); l.put(188, 2, 'm'); l.put(194, 2, 'z'); l.put(199, 2, 'e'); l.put(203, 2, 'm')
    l.note('easyflag 186 2'); l.put(214, 2, 'G'); l.put(211, 2, ','); l.put(218, 2, '%')
    x0, x1 = l.box(223)
    # Bonus room: the star sits past a mouse. Walk, never run, and it stays asleep
    l.note('room sneak 0')
    l.put(x0+1, 2, 'E'); l.row(x0+5, x0+9, 5, 'g'); l.put(x0+7, 6, 'z'); l.put(x1-1, 2, 's')
    return l

def boss2():
    """The frost giant. A climb through everything world 3 taught, then the arena: the giant throws snow blocks,
    is stuck for a moment after each throw, and only then can it be stomped."""
    l = L(214, 18, 'giant')
    def ladder(x, y0, y1):
        for y in range(y0, y1+1): l.put(x, y, 'I')
    l.ground(0, 40, 2); l.put(9, 2, 'S'); l.put(7, 2, '>')
    l.put(12, 5, '!'); l.put(13, 5, 'b'); l.put(14, 5, 'b'); l.put(15, 5, '!'); l.put(22, 2, 'q'); l.put(30, 2, 'm')
    # A wall with a ladder, then clouds down the far side
    l.ground(41, 50, 10); ladder(40, 2, 9); l.put(45, 10, 'e'); l.line(43, 49, 11)
    l.row(54, 56, 8, 'c'); l.row(60, 62, 5, 'c'); l.put(61, 10, 's')
    l.ground(66, 112, 2); l.put(70, 2, 'z'); l.put(76, 2, 'e'); l.put(80, 2, 'n'); l.note('easyflag 67 2')
    l.put(86, 5, '+'); l.put(90, 6, 'h'); l.put(94, 2, 'C'); l.put(92, 2, '|')
    l.put(99, 5, '!'); l.put(98, 5, 'b'); l.put(100, 5, 'b'); l.put(104, 5, '*'); l.put(108, 2, 'D'); l.put(111, 2, 'm')
    # The arena: a wall behind, two ladders up to safe ledges, the star on the higher one
    l.ground(113, 196, 2); l.note('easyflag 116 2'); l.fill(196, 196, 2, 17, 'g')
    ladder(134, 2, 6); l.row(135, 138, 6); ladder(176, 2, 9); l.row(172, 175, 10); l.put(173, 11, 's')
    l.put(165, 2, 'B'); l.put(190, 2, 'G'); l.put(186, 2, '|'); l.put(193, 2, '|')
    x0, x1 = l.box(197)
    # Bonus room: coins hang along ropes; take them all before the clock runs out
    l.note('room ropes 15')
    l.put(x0+1, 2, 'E')
    for x in (x0+4, x0+8, x0+12):
        for y in range(4, 13): l.put(x, y, 't')
    l.coins([(x0+6, 7), (x0+10, 7), (x0+6, 10), (x0+10, 10)])
    l.put(x0+8, 3, 's')
    return l

# ---- v2.4: the hidden world, "the land of colours" ----
# 2..5 are switches (red, blue, green, yellow); 6..9 the blocks of those colours. Red is on when a level starts.
RED, BLUE, GREEN, YELLOW = '6789'
def sw(l, x, y, color): l.put(x, y, '2345'['rbgy'.index(color)])
def bridge(l, x0, x1, ch, y=1): l.row(x0, x1, y, ch)
def wall(l, x, ch, y0=2): l.fill(x, x, y0, l.H-1, ch)
def lake(l, x0, x1): l.row(x0, x1, 0, 'w')

def w4l1():
    """Colour switches. A blue switch opens a red wall in a safe place; then bridges come and go; the twist is
    a lake crossed on bridges of three colours, each switch standing on a small island between them."""
    M = 200; l = L(M + 30, idea='colors')
    l.note('needs switches')
    l.ground(0, 40, 2); l.put(8, 2, 'S'); l.put(6, 2, '>')
    l.put(12, 5, '!'); l.put(13, 5, 'b'); l.put(14, 5, '?')
    sw(l, 18, 2, 'b'); wall(l, 24, RED); l.put(32, 2, 'e'); l.put(30, 5, '!')
    # Developed: a blue bridge over a pit, then a green one
    bridge(l, 41, 52, BLUE); l.ground(53, 70, 2); l.put(58, 2, 'e'); sw(l, 64, 2, 'g'); l.line(55, 62, 3)
    bridge(l, 71, 84, GREEN); l.put(77, 5, 's')
    l.ground(85, 110, 2); l.put(90, 2, 'C'); l.put(88, 2, '|'); l.put(95, 5, '!'); l.put(94, 5, 'b'); l.put(96, 5, 'b'); l.put(100, 2, 'D'); l.put(98, 2, '|')
    l.put(105, 6, 'h'); l.put(103, 2, 'n'); sw(l, 108, 2, 'y')
    # The twist: a lake, three bridges, a switch on each island; every switch takes away the bridge behind
    lake(l, 111, 140)
    bridge(l, 111, 118, YELLOW); l.ground(119, 121, 2); sw(l, 120, 2, 'r'); bridge(l, 122, 129, RED); l.put(125, 5, 's')
    l.ground(130, 132, 2); sw(l, 131, 2, 'b'); bridge(l, 133, 140, BLUE)
    l.ground(141, M - 1, 2); sw(l, 147, 2, 'g'); wall(l, 152, BLUE); l.put(160, 2, 'e'); l.put(170, 2, 'n')
    l.put(178, 5, '+'); l.put(190, 2, 'G'); l.put(186, 2, '|'); l.put(194, 2, '|')
    l.note('easyflag 54 2'); l.note('easyflag 142 2')
    x0, x1 = l.box(M)
    # Bonus room: three lamps, two of them behind colour blocks
    l.note('room beacons 0')
    l.put(x0 + 1, 2, 'E'); sw(l, x0 + 4, 2, 'b'); l.put(x0 + 7, 3, 'L')
    l.fill(x0 + 10, x0 + 10, 2, 12, RED)
    l.row(x0 + 14, x0 + 17, 4, GREEN); l.put(x0 + 16, 7, 'L'); sw(l, x0 + 21, 2, 'g'); l.put(x0 + 24, 3, 'L')
    l.put(x0 + 19, 3, 's')
    return l

def w4l2():
    """Bees and ladybugs. A bee flies its figure of eight over safe ground first; then over a bridge; yellow
    stairs climb a wall; the twist is ladybugs on small red platforms over a pit, with a bee among them."""
    M = 200; l = L(M + 30, idea='bugs')
    l.note('needs switches')
    l.ground(0, 35, 2); l.put(8, 2, 'S'); l.put(6, 2, '>'); l.put(12, 5, '!'); l.put(13, 5, 'b')
    l.put(20, 2, 'K'); l.put(27, 3, 'W'); sw(l, 33, 2, 'b'); l.put(16, 5, '!')
    bridge(l, 36, 49, BLUE); l.put(42, 3, 'W')
    l.ground(50, 74, 2); l.put(55, 2, 'K'); l.put(65, 2, 'K'); l.line(56, 64, 3); sw(l, 71, 2, 'y')
    # Yellow stairs up a wall too high to jump, a star on top
    l.fill(75, 75, 2, 3, YELLOW); l.fill(76, 76, 2, 5, YELLOW); l.fill(77, 77, 2, 7, YELLOW)
    l.fill(78, 80, 0, 8, '#'); l.put(79, 11, 's')
    l.ground(81, 120, 2); l.put(86, 2, 'C'); l.put(84, 2, '|'); l.put(95, 3, 'W'); l.put(102, 3, 'W')
    l.put(106, 5, '!'); l.put(105, 5, 'b'); l.put(107, 5, 'b'); l.put(111, 2, 'D'); l.put(109, 2, '|'); l.put(115, 6, 'h'); sw(l, 118, 2, 'r')
    # The twist: ladybugs patrol small red platforms over a pit
    for x0 in (124, 133, 142): l.row(x0, x0 + 3, 4, RED); l.put(x0 + 1, 5, 'K')
    l.put(130, 7, 'W'); l.put(139, 8, 's')
    l.ground(149, M - 1, 2); l.put(160, 2, 'e'); l.put(172, 3, 'W'); l.put(180, 5, '+')
    l.put(190, 2, 'G'); l.put(186, 2, '|'); l.put(194, 2, '|')
    l.note('easyflag 51 2'); l.note('easyflag 150 2')
    x0, x1 = l.box(M)
    # Bonus room: ladybugs, two stomps each
    l.note('room bugs 0')
    l.put(x0 + 1, 2, 'E')
    for x in (x0 + 7, x0 + 13, x0 + 19): l.put(x, 2, 'K')
    l.row(x0 + 10, x0 + 16, 5); l.put(x0 + 13, 7, 's')
    return l

def w4l3():
    """Fish. A fish leaps from a narrow gap where it is easy to watch; then it leaps through a bridge; the twist
    is a long lake of bridges in four colours, fish jumping through each, a switch on every island."""
    M = 200; l = L(M + 30, idea='fish')
    l.note('needs switches')
    l.ground(0, 30, 2); l.put(8, 2, 'S'); l.put(6, 2, '>'); l.put(12, 5, '!'); l.put(13, 5, 'b'); l.put(24, 5, '!')
    lake(l, 31, 34); l.put(32, 0, 'N')
    l.ground(35, 55, 2); l.put(45, 2, 'e'); sw(l, 52, 2, 'g')
    lake(l, 56, 71); bridge(l, 56, 71, GREEN); l.put(60, 0, 'N'); l.put(64, 0, 'N'); l.put(68, 0, 'N'); l.put(64, 6, 's')
    l.ground(72, 94, 2); l.put(78, 2, 'C'); l.put(76, 2, '|'); l.put(84, 5, '!'); l.put(83, 5, 'b'); l.put(85, 5, 'b'); l.put(88, 2, 'D'); l.put(86, 2, '|')
    l.put(91, 6, 'h'); sw(l, 93, 2, 'b')
    # The twist: four bridges, four colours, fish under every one
    lake(l, 95, 167)
    bridge(l, 95, 110, BLUE); l.put(100, 0, 'N'); l.put(105, 0, 'N')
    l.ground(111, 113, 2); sw(l, 112, 2, 'y'); bridge(l, 114, 129, YELLOW); l.put(118, 0, 'N'); l.put(124, 0, 'N')
    l.ground(130, 132, 2); sw(l, 131, 2, 'r'); bridge(l, 133, 148, RED); l.put(137, 0, 'N'); l.put(143, 0, 'N'); l.put(140, 6, 's')
    l.ground(149, 151, 2); sw(l, 150, 2, 'g'); bridge(l, 152, 167, GREEN); l.put(156, 0, 'N'); l.put(162, 0, 'N')
    l.ground(168, M - 1, 2); l.put(175, 2, 'n'); l.put(181, 3, 'W'); l.put(186, 5, '+')
    l.put(194, 2, 'G'); l.put(190, 2, '|'); l.put(198, 2, '|')
    l.note('easyflag 73 2'); l.note('easyflag 169 2')
    x0, x1 = l.box(M)
    # Bonus room: a race through walls that open one after another
    l.note('room dash 14')
    l.put(x0 + 1, 2, 'E'); sw(l, x0 + 4, 2, 'b'); wall(l, x0 + 8, RED); sw(l, x0 + 11, 2, 'y'); wall(l, x0 + 14, BLUE)
    sw(l, x0 + 17, 2, 'g'); wall(l, x0 + 20, YELLOW); l.put(x0 + 24, 3, 's')
    return l

def boss3():
    """The jelly king returns. A short way in past everything the world taught, then the arena: four switches on
    its floor; the king can be stomped only after the switch of the colour he wears, and every hit changes it."""
    M = 190; l = L(M + 30, idea='king')
    l.ground(0, 30, 2); l.put(8, 2, 'S'); l.put(6, 2, '>'); l.put(12, 5, '!'); l.put(13, 5, 'b'); sw(l, 22, 2, 'b'); l.put(26, 5, '!')
    lake(l, 31, 40); bridge(l, 31, 40, BLUE); l.put(35, 0, 'N'); l.put(36, 6, 's')
    l.ground(41, 99, 2); l.put(50, 3, 'W'); l.put(58, 2, 'K'); l.put(64, 2, 'C'); l.put(62, 2, '|'); l.put(70, 5, '!'); l.put(69, 5, 'b'); l.put(71, 5, 'b')
    l.put(78, 2, 'D'); l.put(76, 2, '|'); l.put(84, 6, 'h'); l.put(92, 5, '+')
    # The arena
    l.ground(100, M - 1, 2); l.fill(M - 1, M - 1, 2, l.H - 1, 'g')
    sw(l, 108, 2, 'r'); sw(l, 126, 2, 'b'); sw(l, 154, 2, 'g'); sw(l, 172, 2, 'y')
    l.row(116, 120, 4); l.row(160, 164, 4); l.put(118, 7, 's')
    l.put(140, 2, 'B'); l.put(182, 2, 'G'); l.put(178, 2, '|'); l.put(186, 2, '|')
    l.note('easyflag 42 2'); l.note('easyflag 101 2')
    x0, x1 = l.box(M)
    # Bonus room: coins over ledges that only a switch of their colour brings out
    l.note('room paint 20')
    l.put(x0 + 1, 2, 'E'); sw(l, x0 + 3, 2, 'g'); l.row(x0 + 6, x0 + 9, 4, GREEN); l.line(x0 + 6, x0 + 9, 6)
    sw(l, x0 + 11, 2, 'y'); l.row(x0 + 14, x0 + 17, 4, YELLOW); l.line(x0 + 14, x0 + 17, 6)
    sw(l, x0 + 19, 2, 'b'); l.row(x0 + 21, x0 + 24, 4, BLUE); l.line(x0 + 21, x0 + 24, 6)
    l.put(x0 + 26, 3, 's')
    return l

def test():
    """Not a real level: one open floor with everything the self test needs at known places."""
    l = L(120, idea='test')
    l.note('room test 0')
    l.ground(0, 90, 2); l.put(3, 2, 'S'); l.put(70, 2, 'C'); l.put(76, 2, 'D'); l.put(84, 2, 'G')
    l.put(20, 6, 'b'); l.put(24, 6, '?'); l.put(28, 6, '!'); l.put(32, 6, 'h'); l.put(36, 2, 'T'); l.put(12, 6, '+')
    l.row(40, 42, 2, 'u'); l.row(46, 50, 1, '}'); l.put(54, 2, 's'); l.put(56, 2, 's'); l.put(58, 2, 's')
    l.fill(66, 66, 2, 8, '#'); l.fill(91, 94, 0, 14, '#')
    for y in range(2, 7): l.put(62, y, 'I')     # a ladder onto a ledge (v1.6)
    l.row(63, 65, 6)
    for y in range(5, 10): l.put(44, y, 't')    # a rope from a beam
    l.row(43, 45, 10, 'g')
    l.row(6, 8, 10, 'c')                        # a snow cloud high over the floor (v1.7)
    x0, x1 = l.box(95); l.put(x0+2, 2, 'E')
    return l

out = Path(sys.argv[1]); out.mkdir(parents=True, exist_ok=True)
for n, f in (('w1l1',w1l1),('w1l2',w1l2),('w1l3',w1l3),('w2l1',w2l1),('w2l2',w2l2),('w2l3',w2l3),('boss',boss),('w3l1',w3l1),('w3l2',w3l2),('w3l3',w3l3),('boss2',boss2),('w4l1',w4l1),('w4l2',w4l2),('w4l3',w4l3),('boss3',boss3),('test',test)):
    f().save(n, out)
