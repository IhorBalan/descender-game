#!/usr/bin/env python3
"""Generates the placeholder sounds under godot_assets/audio from scratch.

    python3 tools/gen_sounds.py

Every sound is synthesised (sines, noise, simple envelopes) so the game has
audio to wire up before real sound design happens. Rerun after changing a
recipe below; the output files are committed.
"""

import os
import wave

import numpy as np

RATE = 22050
OUT = os.path.join(os.path.dirname(__file__), "..", "godot_assets", "audio")
rng = np.random.default_rng(7)


def t(seconds):
    return np.arange(int(RATE * seconds)) / RATE


def env(n, attack=0.005, decay=None, hold=0.0):
    """Attack to full, optional hold, then exponential-ish decay to zero."""
    out = np.ones(n)
    a = int(RATE * attack)
    out[:a] = np.linspace(0.0, 1.0, a)
    h = a + int(RATE * hold)
    if decay is None:
        d = n - h
    else:
        d = min(int(RATE * decay), n - h)
    if d > 0:
        out[h : h + d] = np.linspace(1.0, 0.0, d) ** 2
        out[h + d :] = 0.0
    return out


def sine(freq, dur):
    x = t(dur)
    freq = np.broadcast_to(freq, x.shape) if np.ndim(freq) else freq
    phase = np.cumsum(np.ones_like(x) * freq) / RATE if np.ndim(freq) else freq * x
    return np.sin(2 * np.pi * phase)


def sweep(f0, f1, dur, curve=1.0):
    x = t(dur)
    f = f0 + (f1 - f0) * (x / x[-1]) ** curve
    return sine(f, dur)


def noise(dur):
    return rng.uniform(-1.0, 1.0, int(RATE * dur))


def lowpass(x, alpha):
    out = np.empty_like(x)
    acc = 0.0
    for i, v in enumerate(x):
        acc += alpha * (v - acc)
        out[i] = acc
    return out


def square(freq, dur):
    return np.sign(sine(freq, dur))


def write(name, data, gain=0.8):
    data = np.asarray(data, dtype=np.float64)
    peak = np.max(np.abs(data)) or 1.0
    pcm = (data / peak * gain * 32767).astype("<i2")
    path = os.path.join(OUT, name)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, "wb") as f:
        f.setnchannels(1)
        f.setsampwidth(2)
        f.setframerate(RATE)
        f.writeframes(pcm.tobytes())
    print("wrote", os.path.relpath(path, os.path.dirname(OUT)), len(pcm) / RATE, "s")


def chord(freqs, dur, decay):
    out = np.zeros(int(RATE * dur))
    for i, f in enumerate(freqs):
        start = int(RATE * i * 0.09)
        note = sine(f, dur - i * 0.09) + 0.3 * sine(f * 2, dur - i * 0.09)
        note *= env(len(note), decay=decay)
        out[start : start + len(note)] += note
    return out


def main():
    # Hero
    write("sfx/jump.wav", sweep(260, 620, 0.14, 0.6) * env(int(RATE * 0.14), decay=0.12))
    write("sfx/double_jump.wav", sweep(420, 980, 0.16, 0.6) * env(int(RATE * 0.16), decay=0.14))
    land = lowpass(noise(0.09), 0.08) * 1.5 + sine(75, 0.09)
    write("sfx/land.wav", land * env(int(RATE * 0.09), decay=0.08))
    write("sfx/dash.wav", lowpass(noise(0.12), 0.35) * env(int(RATE * 0.12), 0.02, 0.09))
    swing = lowpass(noise(0.16), 0.25) * sweep(1, 1, 0.16)
    write("sfx/swing.wav", swing * env(int(RATE * 0.16), 0.03, 0.12))
    hurt = sweep(420, 140, 0.22) * 0.7 + square(110, 0.22) * 0.3
    write("sfx/hurt.wav", hurt * env(int(RATE * 0.22), decay=0.2))
    write("sfx/heal.wav", chord([523.25, 659.25, 783.99], 0.6, 0.4))
    death = sweep(330, 60, 0.9, 1.4) * (1 + 0.2 * sine(6, 0.9))
    write("sfx/death.wav", death * env(int(RATE * 0.9), 0.01, 0.85))
    # Enemies
    hit = lowpass(noise(0.1), 0.5) + 0.6 * square(190, 0.1)
    write("sfx/hit.wav", hit * env(int(RATE * 0.1), decay=0.09))
    enemy_die = lowpass(noise(0.28), 0.2) * 1.2 + sweep(160, 40, 0.28)
    write("sfx/enemy_die.wav", enemy_die * env(int(RATE * 0.28), decay=0.26))
    # A boss's roar at the turn of its fight: a growl that climbs and breaks.
    roar = sweep(70, 140, 0.55, 0.7) * 0.6 + square(48, 0.55) * 0.25 + lowpass(noise(0.55), 0.12) * 0.9
    roar = roar * (1.0 + 0.5 * square(18, 0.55))
    write("sfx/roar.wav", roar * env(int(RATE * 0.55), 0.03, 0.4))
    # The hanged thing's rope creaking before it lets go: a low, dry wobble.
    creak = sweep(95, 62, 0.3, 0.8) * (1.0 + 0.6 * square(27, 0.3)) * 0.5 + lowpass(noise(0.3), 0.08) * 0.6
    write("sfx/creak.wav", creak * env(int(RATE * 0.3), 0.02, 0.22), 0.6)
    # World
    write("sfx/pickup.wav", chord([659.25, 987.77], 0.35, 0.25))
    write("sfx/checkpoint.wav", chord([440, 554.37, 659.25, 880], 0.9, 0.6))
    write("sfx/level_complete.wav", chord([392, 493.88, 587.33, 783.99, 987.77], 1.3, 0.9))
    write("sfx/menu_move.wav", sine(820, 0.045) * env(int(RATE * 0.045), 0.003, 0.04), 0.5)
    confirm = np.concatenate([sine(660, 0.06) * env(int(RATE * 0.06)), sine(990, 0.1) * env(int(RATE * 0.1))])
    write("sfx/menu_confirm.wav", confirm, 0.6)
    # Dialogue: the blip the text box ticks with. Short and dull, so forty
    # of them a second do not grate; the box pitches it per speaker.
    talk = sine(520, 0.035) * 0.7 + square(260, 0.035) * 0.3
    write("sfx/talk.wav", talk * env(int(RATE * 0.035), 0.002, 0.03), 0.4)
    # Ambient loop: a soft Heaven pad. Six seconds long; every frequency is a
    # multiple of 1/6 Hz so the loop joins without a click.
    dur = 6.0
    pad = np.zeros(int(RATE * dur))
    for f, g in ((220.0, 1.0), (1663 / 6, 0.7), (1978 / 6, 0.6), (440.0, 0.25)):
        pad += g * sine(f, dur) * (0.8 + 0.2 * sine(1 / 6, dur))
    pad += 0.08 * lowpass(noise(dur), 0.02) * (1.0 + 0.5 * sine(2 / 6, dur))
    write("ambient_heaven.wav", pad, 0.35)
    # Earth: wind over rubble, a low drone from the torn sky and a far-off
    # rumble that comes and goes. Same six-second loop rule.
    wind = lowpass(noise(dur), 0.06) * (0.6 + 0.4 * sine(1 / 6, dur)) * (0.8 + 0.2 * sine(5 / 6, dur))
    drone = 0.5 * sine(55.0, dur) * (0.7 + 0.3 * sine(2 / 6, dur)) + 0.25 * sine(82.5, dur)
    rumble = lowpass(noise(dur), 0.01) * np.clip(sine(1 / 6, dur) * 3.0 - 1.5, 0.0, 1.0)
    write("ambient_earth.wav", wind * 1.4 + drone + rumble * 2.0, 0.3)
    # Bosses: a low horn when the doors seal, and a long fall when one dies.
    horn = sine(98, 0.9) * 0.6 + square(49, 0.9) * 0.25 + sine(147, 0.9) * 0.3
    horn = horn * (1.0 + 0.15 * sine(5, 0.9)) + lowpass(noise(0.9), 0.05) * 0.5
    write("sfx/boss_engage.wav", horn * env(int(RATE * 0.9), 0.08, 0.6, 0.2))
    fall = sweep(220, 30, 1.6, 1.2) * 0.7 + lowpass(noise(1.6), 0.15) * 0.8
    fall = fall * (1.0 + 0.3 * square(12, 1.6))
    write("sfx/boss_defeat.wav", fall * env(int(RATE * 1.6), 0.01, 1.5))
    # A new ability: the pickup chord, taller and slower.
    write("sfx/ability.wav", chord([392, 493.88, 587.33, 783.99, 1174.66, 1567.98], 1.5, 1.1))
    # More menu sounds: back out of a page, and the pause itself.
    back = np.concatenate([sine(880, 0.06) * env(int(RATE * 0.06)), sine(560, 0.1) * env(int(RATE * 0.1))])
    write("sfx/menu_back.wav", back, 0.5)
    pause = sine(440, 0.12) * 0.6 + sine(660, 0.12) * 0.4
    write("sfx/pause.wav", pause * env(int(RATE * 0.12), 0.005, 0.1), 0.5)


if __name__ == "__main__":
    main()
