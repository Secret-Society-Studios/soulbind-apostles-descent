"""Generate an original, dry paper-flick draw sound using only the Python standard library.

Overwrites the existing clip in place so Unity's GUID and library assignment stay intact.
Run from any directory: python Tools/Audio/generate_card_draw.py
"""

import math
from pathlib import Path
import random
import struct
import wave


def generate():
    rate = 44100
    duration = 0.17
    rng = random.Random(731)
    samples = []
    low = 0.0
    body = 0.0
    previous = 0.0
    for index in range(round(rate * duration)):
        t = index / rate
        noise = rng.uniform(-1.0, 1.0)
        low += 0.19 * (noise - low)
        body += 0.025 * (noise - body)
        # Band-limited noise gives the slide a papery texture without a harsh hiss.
        paper = low - body
        slide = (1.0 - math.exp(-t / 0.002)) * math.exp(-t / 0.024)
        flutter = 0.8 + 0.2 * math.sin(2.0 * math.pi * (95 * t + 220 * t * t))
        # Two tiny contacts suggest a card peeling off a deck, then landing.
        contacts = 0.0
        for start, strength in ((0.004, 0.48), (0.034, 0.32)):
            age = t - start
            if age >= 0:
                contacts += strength * (noise - previous) * math.exp(-age / 0.0025)
        tap_age = max(0.0, t - 0.033)
        tap = 0.16 * math.sin(2 * math.pi * 175 * tap_age) * math.exp(-tap_age / 0.008)
        value = 2.5 * paper * slide * flutter + contacts + tap
        fade = min(1.0, t / 0.001, (duration - t) / 0.012)
        samples.append(value * fade)
        previous = noise

    # Leave headroom for overlapping draws and the rest of the game's mix.
    gain = 0.68 / max(abs(value) for value in samples)
    pcm = b"".join(struct.pack("<h", round(value * gain * 32767)) for value in samples)
    target = Path(__file__).resolve().parents[2] / "Assets/Audio/SFX/Game/placeholder_card_draw.wav"
    with wave.open(str(target), "wb") as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(rate)
        output.writeframes(pcm)
    with wave.open(str(target), "rb") as output:
        assert output.getnframes() == len(samples)
        assert output.getnchannels() == 1 and output.getsampwidth() == 2
    print(f"Generated {target.name}: {duration:.2f}s, {rate} Hz, mono PCM16, peak -3.35 dBFS")


if __name__ == "__main__":
    generate()
