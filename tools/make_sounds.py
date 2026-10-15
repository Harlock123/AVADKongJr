#!/usr/bin/env python3
"""Synthesises AVADKongJr's sound effects and tunes into ../src/AVADKongJr/Sounds as 16-bit mono WAV.

Nothing is sampled from any arcade machine: every effect is built here from square, triangle and
noise voices, and the tunes are new compositions. Run it from anywhere:
    python3 tools/make_sounds.py
"""
import math
import os
import random
import struct
import wave

RATE = 22050
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src", "AVADKongJr", "Sounds")
RNG = random.Random(1982)


def write(name, samples, volume=0.7):
    peak = max(1e-9, max(abs(s) for s in samples))
    with wave.open(os.path.join(OUT, name), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(s / peak * volume * 32767)) for s in samples))


def voice(kind, phase):
    p = phase % 1.0
    if kind == "square":
        return 1.0 if p < 0.5 else -1.0
    if kind == "pulse":
        return 1.0 if p < 0.25 else -1.0
    if kind == "tri":
        return 4 * p - 1 if p < 0.5 else 3 - 4 * p
    if kind == "saw":
        return 2 * p - 1
    if kind == "sine":
        return math.sin(2 * math.pi * p)
    return RNG.uniform(-1, 1)


def tone(seconds, f0, f1=None, kind="square", attack=0.004, release=1.0, vibrato=0.0, vib_rate=8.0, gain=1.0):
    """A note gliding from f0 to f1 (exponentially), with a short attack and a shaped decay."""
    n = int(RATE * seconds)
    f1 = f1 or f0
    out, phase, held = [], 0.0, 0.0
    for i in range(n):
        t = i / RATE
        f = f0 * (f1 / f0) ** (i / max(1, n - 1))
        if vibrato:
            f *= 1 + vibrato * math.sin(2 * math.pi * vib_rate * t)
        phase += f / RATE
        if kind == "noise":
            # Noise re-sampled at the voice's rate: a lower 'pitch' sounds rougher.
            if int(phase) != int(phase - f / RATE):
                held = RNG.uniform(-1, 1)
            s = held
        else:
            s = voice(kind, phase)
        env = min(1.0, t / attack) * (1 - i / n) ** release
        out.append(s * env * gain)
    return out


def silence(seconds):
    return [0.0] * int(RATE * seconds)


def cat(*parts):
    out = []
    for p in parts:
        out.extend(p)
    return out


def mix(*parts):
    n = max(len(p) for p in parts)
    out = [0.0] * n
    for p in parts:
        for i, v in enumerate(p):
            out[i] += v
    return out


NOTES = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}


def hz(note):
    n = NOTES[note[0]]
    rest = note[1:]
    while rest and rest[0] in "#b":
        n += 1 if rest[0] == "#" else -1
        rest = rest[1:]
    return 440.0 * 2 ** ((12 * (int(rest) + 1) + n - 69) / 12)


def tune(bpm, lead, bass, kind="pulse"):
    """Two voices: a lead and a bass line, as (note, beats) lists; '-' is a rest."""
    beat = 60.0 / bpm

    def line(notes, k, gain, release):
        out = []
        for note, beats in notes:
            d = beats * beat
            out.extend(silence(d) if note == "-" else tone(d, hz(note), kind=k, release=release, gain=gain))
        return out

    return mix(line(lead, kind, 0.55, 0.6), line(bass, "tri", 0.6, 0.3))


def seq(bpm, notes, kind="pulse", gain=0.55, release=0.6):
    beat = 60.0 / bpm
    out = []
    for note, beats in notes:
        d = beats * beat
        out.extend(silence(d) if note == "-" else tone(d, hz(note), kind=kind, release=release, gain=gain))
    return out


def main():
    os.makedirs(OUT, exist_ok=True)
    for f in os.listdir(OUT):
        if f.endswith(".wav"):
            os.remove(os.path.join(OUT, f))
    # Kip: jumps, grabs and the patter of climbing.
    write("Jump.wav", tone(0.18, 300, 900, "square", release=0.7), 0.26)
    write("Grab.wav", cat(tone(0.03, 1200, 900, "noise", release=0.8), tone(0.06, 700, 500, "tri", release=0.6)), 0.3)
    write("Climb.wav", tone(0.05, 520, 600, "pulse", release=0.8), 0.18)
    write("KeyPush.wav", mix(tone(0.06, 900, 1100, "pulse", release=0.8), tone(0.04, 3000, 2000, "noise", release=1.0, gain=0.3)), 0.2)
    # Fruit: a rustle as it's knocked loose, and a splat on whatever it lands on.
    write("FruitDrop.wav", cat(tone(0.08, 2400, 1200, "noise", release=0.7), tone(0.4, 1400, 300, "tri", release=0.5)), 0.3)
    write("FruitHit.wav", cat(mix(tone(0.15, 400, 80, "square", release=0.8), tone(0.12, 1500, 300, "noise", release=0.9, gain=0.6)),
                              *[tone(0.06, hz(n), kind="pulse", release=0.3) for n in ["C6", "E6", "G6"]]), 0.42)
    write("JumpOver.wav", cat(tone(0.06, hz("E6"), kind="pulse", release=0.4), tone(0.1, hz("A6"), kind="pulse", release=0.5)), 0.3)
    # The Keeper's whip, a Spark let loose, a bird and its egg.
    write("Whip.wav", mix(tone(0.12, 6000, 1500, "noise", attack=0.001, release=1.2), tone(0.05, 2400, 800, "square", release=1.0, gain=0.4)), 0.34)
    write("Zap.wav", mix(tone(0.25, 1800, 400, "saw", release=0.6, vibrato=0.2, vib_rate=60), tone(0.25, 5000, 2000, "noise", release=0.7, gain=0.4)), 0.3)
    write("Squawk.wav", cat(tone(0.1, 1300, 900, "saw", release=0.6, vibrato=0.1, vib_rate=40), tone(0.12, 1100, 700, "saw", release=0.7, vibrato=0.1, vib_rate=40)), 0.26)
    write("Egg.wav", tone(0.35, 1600, 500, "tri", release=0.6), 0.24)
    write("KeyLock.wav", cat(mix(tone(0.05, 3000, 1500, "noise", release=0.9), tone(0.08, 300, 200, "square", release=0.8)),
                             *[tone(0.07, hz(n), kind="pulse", release=0.3) for n in ["G5", "C6", "E6"]]), 0.4)
    write("Fall.wav", tone(0.9, 1500, 150, "tri", release=0.3, vibrato=0.02, vib_rate=10), 0.3)
    write("Splash.wav", mix(tone(0.6, 2500, 400, "noise", release=0.6), tone(0.3, 300, 120, "sine", release=0.8, gain=0.6)), 0.45)
    write("Death.wav", cat(tone(0.25, 900, 300, "square", release=0.6), *[tone(0.15, hz(n), kind="tri", release=0.4) for n in ["E5", "D5", "C5", "B4", "C5"]]), 0.42)
    write("StageStart.wav", tune(170, [("C5", 0.5), ("E5", 0.5), ("G5", 0.5), ("E5", 0.5), ("C6", 1.5)], [("C3", 1), ("G2", 1), ("C3", 1)]), 0.42)
    write("Clear.wav", tune(176, [("G5", 0.5), ("A5", 0.5), ("B5", 0.5), ("C6", 0.5), ("D6", 0.5), ("E6", 0.5), ("G6", 2)], [("G2", 1), ("D3", 1), ("G2", 1), ("C3", 1)]), 0.44)
    write("PapaFree.wav", tune(150, [("C5", 0.5), ("C5", 0.5), ("G5", 1), ("E5", 0.5), ("G5", 0.5), ("C6", 1), ("A5", 0.5), ("B5", 0.5), ("C6", 0.5), ("D6", 0.5), ("E6", 3)],
                                [("C3", 1), ("G2", 1), ("C3", 1), ("E3", 1), ("F3", 1), ("G3", 1), ("C3", 3)]), 0.46)
    write("ExtraLife.wav", cat(*[tone(0.07, hz(n), kind="pulse", release=0.3) for n in ["C6", "E6", "G6", "C7", "G6", "C7"]]), 0.4)

    # The stages' tune: a jaunty swing in F over a walking bass (new). Loops.
    lead = [("F5", 0.5), ("A5", 0.5), ("C6", 1), ("A5", 0.5), ("F5", 0.5), ("G5", 1),
            ("Bb5", 0.5), ("A5", 0.5), ("G5", 0.5), ("F5", 0.5), ("E5", 1), ("C5", 1),
            ("D5", 0.5), ("F5", 0.5), ("Bb5", 1), ("A5", 0.5), ("G5", 0.5), ("A5", 1),
            ("C6", 0.5), ("Bb5", 0.5), ("A5", 0.5), ("G5", 0.5), ("F5", 2),
            ("A5", 0.5), ("C6", 0.5), ("F6", 1), ("E6", 0.5), ("D6", 0.5), ("C6", 1),
            ("Bb5", 0.5), ("D6", 0.5), ("C6", 0.5), ("Bb5", 0.5), ("A5", 1), ("F5", 1),
            ("G5", 0.5), ("A5", 0.5), ("Bb5", 0.5), ("G5", 0.5), ("E5", 0.5), ("G5", 0.5), ("C6", 1),
            ("A5", 0.5), ("G5", 0.5), ("F5", 1), ("F5", 1), ("-", 1)]
    bass = [("F2", 1), ("A2", 1), ("C3", 1), ("A2", 1), ("Bb2", 1), ("D3", 1), ("C3", 1), ("E3", 1),
            ("D3", 1), ("F3", 1), ("C3", 1), ("E3", 1), ("F2", 1), ("C3", 1), ("F2", 1), ("A2", 1),
            ("F2", 1), ("A2", 1), ("C3", 1), ("A2", 1), ("Bb2", 1), ("D3", 1), ("F3", 1), ("D3", 1),
            ("C3", 1), ("E3", 1), ("G3", 1), ("E3", 1), ("F2", 1), ("C3", 1), ("F2", 1), ("C3", 1)]
    write("Music.wav", tune(144, lead, bass, kind="pulse"), 0.36)

    # The title: a lazy jungle stroll in C minor (new).
    lead = [("G4", 1), ("C5", 0.5), ("Eb5", 0.5), ("G5", 1.5), ("F5", 0.5), ("Eb5", 1), ("D5", 1), ("C5", 2),
            ("Ab4", 1), ("C5", 0.5), ("F5", 0.5), ("Ab5", 1.5), ("G5", 0.5), ("F5", 1), ("D5", 1), ("G5", 2),
            ("G4", 1), ("C5", 0.5), ("Eb5", 0.5), ("G5", 1.5), ("Bb5", 0.5), ("Ab5", 1), ("G5", 1), ("F5", 1), ("Eb5", 1),
            ("D5", 1), ("B4", 1), ("C5", 3), ("-", 1)]
    bass = [("C3", 2), ("G2", 2), ("C3", 2), ("G2", 2), ("F2", 2), ("C3", 2), ("G2", 2), ("D3", 2),
            ("C3", 2), ("G2", 2), ("Eb3", 2), ("C3", 2), ("G2", 2), ("G2", 2), ("C3", 4)]
    write("TitleTune.wav", tune(112, lead, bass, kind="tri"), 0.45)
    write("StartTune.wav", tune(160, [("C5", 0.5), ("E5", 0.5), ("G5", 0.5), ("C6", 0.5), ("A5", 0.5), ("C6", 0.5), ("E6", 2)], [("C3", 1), ("G2", 1), ("C3", 2)], kind="pulse"), 0.45)
    write("GameOver.wav", tune(76, [("G5", 1), ("Eb5", 1), ("C5", 1), ("D5", 1), ("B4", 1), ("C5", 3)], [("C3", 2), ("Ab2", 2), ("G2", 2), ("C3", 2)], kind="tri"), 0.45)


if __name__ == "__main__":
    main()
