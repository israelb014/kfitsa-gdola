#!/usr/bin/env python3
"""v2.5: the smart helper's box, and every boss gets its own battle music. Two CC0 8-bit tracks from OpenGameArt ("Boss Battle #3" and
"Boss Battle #4", uploaded by nene), made as loud as the game's other music with a soft limiter."""
import sys, wave, array, math
from pathlib import Path
root = Path(sys.argv[1]); src = root/'_kenney/music/oga'; aud = root/'Assets/Resources/Audio'
K = 1.8; N = math.tanh(K)
for name, dst in (('boss3.wav', 'm_boss2.wav'), ('boss4.wav', 'm_boss3.wav')):
    w = wave.open(str(src/name)); p = w.getparams(); a = array.array('h', w.readframes(w.getnframes())); w.close()
    b = array.array('h', (int(math.tanh(K * x / 32768) / N * 32000) for x in a))
    o = wave.open(str(aud/dst), 'wb'); o.setparams(p); o.writeframes(b.tobytes()); o.close()
    print(dst, len(b) // p.nchannels / p.framerate, 's')

# The smart helper's box: the pack's exclamation block, at the size of the other tiles
from PIL import Image
T = root/'_kenney/styles/new-platformer-pack-1.1/Sprites/Tiles/Double'
Image.open(T/'block_exclamation.png').convert('RGBA').resize((72, 72), Image.LANCZOS).save(root/'Assets/Resources/Art/help_box.png')
print('help_box written')
