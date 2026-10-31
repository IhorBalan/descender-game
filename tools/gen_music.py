#!/usr/bin/env python3
"""Composes the looping music under godot_assets/audio/music from scratch.

    python3 tools/gen_music.py            # every track
    python3 tools/gen_music.py wrath boss # just these

One track per region of the world, named by the region's key (its name
lowercased without its number, see EdgeDecor.region_key), plus `title` for
the title screen and `boss` for the fights. Each is a short loop written
with a tiny sequencer: notes on a handful of synthesised voices, a tempo-
synced delay and a little drive. Every tail, echo and filter wraps round
the loop's end, so the loops join without a click.

Tracks are encoded to Ogg Vorbis with ffmpeg when it is installed, else
left as WAV; Audio.play_music looks for either. Needs numpy. Rerun after
changing a composition below; the output files are committed.
"""

import os
import shutil
import subprocess
import sys
import tempfile
import wave

import numpy as np

RATE = 22050
OUT = os.path.join(os.path.dirname(__file__), "..", "godot_assets", "audio", "music")
rng = np.random.default_rng(11)


# --- Signal helpers ----------------------------------------------------


def midi_hz(note):
    return 440.0 * 2.0 ** ((note - 69) / 12.0)


def circular_lowpass(x, cutoff_hz, taps=96):
    """Windowed-sinc low-pass applied circularly, so a loop stays a loop."""
    n = len(x)
    k = np.arange(taps) - (taps - 1) / 2.0
    fc = cutoff_hz / RATE
    kernel = 2 * fc * np.sinc(2 * fc * k) * np.hamming(taps)
    kernel /= kernel.sum()
    padded = np.zeros(n)
    padded[: len(kernel)] = kernel
    padded = np.roll(padded, -(taps // 2))
    return np.fft.irfft(np.fft.rfft(x) * np.fft.rfft(padded), n)


def circular_highpass(x, cutoff_hz):
    return x - circular_lowpass(x, cutoff_hz)


def delay(x, seconds, feedback=0.4, mix=0.3, taps=6):
    """A feedback echo whose repeats wrap round the loop."""
    d = int(RATE * seconds)
    out = np.array(x)
    for k in range(1, taps + 1):
        out += mix * (feedback ** (k - 1)) * np.roll(x, k * d)
    return out


def drive(x, amount):
    return np.tanh(x * amount) / np.tanh(amount)


def adsr(n, attack, decay, sustain, release):
    """Envelope over n samples; the release is appended after them."""
    a = max(1, int(RATE * attack))
    d = max(1, int(RATE * decay))
    r = max(1, int(RATE * release))
    env = np.empty(n + r)
    env[:a] = np.linspace(0.0, 1.0, a)
    end_d = min(n, a + d)
    env[a:end_d] = np.linspace(1.0, sustain, end_d - a)
    env[end_d:n] = sustain
    env[n:] = np.linspace(env[n - 1] if n > 0 else sustain, 0.0, r) ** 1.5
    return env


def osc(kind, freq, n, detune=0.0, bright=1.0):
    """One oscillator cycle set; `freq` may be an array for vibrato."""
    t = np.arange(n) / RATE
    f = np.broadcast_to(freq, (n,)) * (2.0 ** (detune / 1200.0))
    phase = np.cumsum(f) / RATE
    if kind == "sine":
        return np.sin(2 * np.pi * phase)
    if kind == "tri":
        return 2.0 * np.abs(2.0 * (phase % 1.0) - 1.0) - 1.0
    if kind == "square":
        return np.sign(np.sin(2 * np.pi * phase)) * 0.7
    if kind == "saw":
        out = np.zeros(n)
        f0 = float(np.mean(f))
        for h in range(1, int(min(24, (RATE * 0.45) / max(f0, 1.0))) + 1):
            out += np.sin(2 * np.pi * h * phase) / h * (bright ** (h - 1))
        return out * 0.6
    if kind == "organ":
        out = np.zeros(n)
        for h, g in ((1, 1.0), (2, 0.6), (3, 0.35), (4, 0.25), (6, 0.12)):
            out += g * np.sin(2 * np.pi * h * phase)
        return out * 0.45
    if kind == "bell":
        out = np.zeros(n)
        decay = np.exp(-t * 2.0)
        for ratio, g, dec in ((1.0, 1.0, 1.0), (2.76, 0.5, 2.5), (5.4, 0.25, 4.0), (8.9, 0.12, 6.0)):
            out += g * np.sin(2 * np.pi * ratio * phase) * decay**dec
        return out * 0.55
    if kind == "pluck":
        # Karplus-Strong: a burst of noise through a looped averaging filter.
        period = max(2, int(RATE / max(float(np.mean(f)), 20.0)))
        buf = rng.uniform(-1.0, 1.0, period)
        out = np.empty(n)
        for i in range(n):
            out[i] = buf[i % period]
            buf[i % period] = 0.5 * (buf[i % period] + buf[(i + 1) % period]) * 0.996
        return out
    if kind == "noise":
        return rng.uniform(-1.0, 1.0, n)
    raise ValueError(kind)


VOICES = {
    # name: oscillator, (attack, decay, sustain, release), gain, extras
    "harp": dict(osc="pluck", env=(0.002, 0.3, 0.4, 0.4), gain=0.5, lp=5000),
    "choir": dict(osc="saw", env=(0.6, 0.5, 0.8, 1.2), gain=0.18, lp=1400, detune=7, vib=0.3),
    "pad": dict(osc="saw", env=(0.9, 0.5, 0.8, 1.5), gain=0.16, lp=900, detune=9),
    "warm": dict(osc="tri", env=(0.3, 0.4, 0.7, 0.8), gain=0.3, lp=2000, detune=5),
    "flute": dict(osc="sine", env=(0.08, 0.2, 0.8, 0.3), gain=0.3, vib=0.4),
    "lead": dict(osc="square", env=(0.01, 0.1, 0.6, 0.2), gain=0.22, lp=2600),
    "bell": dict(osc="bell", env=(0.002, 0.8, 0.3, 1.5), gain=0.4),
    "music_box": dict(osc="bell", env=(0.002, 0.4, 0.2, 0.8), gain=0.35, lp=6000),
    "organ": dict(osc="organ", env=(0.05, 0.1, 1.0, 0.3), gain=0.28, lp=2400),
    "bass": dict(osc="saw", env=(0.01, 0.15, 0.7, 0.15), gain=0.35, lp=420, bright=0.8),
    "sub": dict(osc="sine", env=(0.01, 0.2, 0.8, 0.2), gain=0.45),
    "growl": dict(osc="saw", env=(0.005, 0.1, 0.8, 0.1), gain=0.4, lp=700, drive=4.0),
    "brass": dict(osc="saw", env=(0.03, 0.15, 0.7, 0.2), gain=0.3, lp=1800, detune=6, bright=0.85),
    "glass": dict(osc="sine", env=(0.5, 0.5, 0.8, 1.5), gain=0.28, vib=0.15, detune=3),
    "pluck": dict(osc="pluck", env=(0.002, 0.2, 0.3, 0.3), gain=0.45, lp=3500),
    "strings": dict(osc="saw", env=(0.25, 0.3, 0.8, 0.6), gain=0.2, lp=1600, detune=8, vib=0.25),
}


class Track:
    """A loop of `bars` bars at `bpm`; notes are placed in beats."""

    def __init__(self, bpm, bars, beats=4, swing=0.0):
        self.bpm = bpm
        self.beats_per_bar = beats
        self.swing = swing
        self.total_beats = bars * beats
        self.n = int(round(RATE * self.total_beats * 60.0 / bpm))
        self.layers = {}

    def beat_sec(self):
        return 60.0 / self.bpm

    def _at(self, beat):
        # Swing pushes the off-eighths late.
        if self.swing and abs((beat * 2) % 2 - 1.0) < 1e-6:
            beat += self.swing * 0.5
        return int(round(beat * self.beat_sec() * RATE)) % self.n

    def _add(self, layer, start, sound):
        buf = self.layers.setdefault(layer, np.zeros(self.n))
        idx = (start + np.arange(len(sound))) % self.n
        np.add.at(buf, idx, sound)

    def note(self, voice, beat, length, midi, vel=1.0, layer=None):
        spec = VOICES[voice]
        n = max(1, int(length * self.beat_sec() * RATE))
        env = adsr(n, *spec["env"])
        total = len(env)
        freq = midi_hz(midi)
        t = np.arange(total) / RATE
        if spec.get("vib"):
            freq = freq * (1.0 + 0.004 * spec["vib"] * np.sin(2 * np.pi * 5.0 * t) * np.minimum(t / 0.4, 1.0))
        sound = osc(spec["osc"], freq, total, bright=spec.get("bright", 1.0))
        if spec.get("detune"):
            sound = 0.6 * sound + 0.4 * osc(spec["osc"], freq, total, detune=spec["detune"], bright=spec.get("bright", 1.0))
        sound = sound * env * spec["gain"] * vel
        if spec.get("drive"):
            sound = drive(sound, spec["drive"])
        self._add(layer or voice, self._at(beat), sound)

    def chord(self, voice, beat, length, notes, vel=1.0, strum=0.0):
        for i, midi in enumerate(notes):
            self.note(voice, beat + i * strum, length, midi, vel)

    def arp(self, voice, beat, notes, step, count, vel=1.0, length=None):
        for i in range(count):
            self.note(voice, beat + i * step, length or step, notes[i % len(notes)], vel)

    # Drums, as little synthesised hits.
    def kick(self, beat, vel=1.0):
        n = int(RATE * 0.25)
        t = np.arange(n) / RATE
        f = 40 + 120 * np.exp(-t * 30)
        s = np.sin(2 * np.pi * np.cumsum(f) / RATE) * np.exp(-t * 12)
        self._add("drums", self._at(beat), drive(s, 2.0) * 0.8 * vel)

    def snare(self, beat, vel=1.0):
        n = int(RATE * 0.2)
        t = np.arange(n) / RATE
        s = rng.uniform(-1, 1, n) * np.exp(-t * 22) + 0.5 * np.sin(2 * np.pi * 185 * t) * np.exp(-t * 30)
        self._add("drums", self._at(beat), s * 0.45 * vel)

    def hat(self, beat, vel=1.0, length=0.05):
        n = int(RATE * length)
        t = np.arange(n) / RATE
        s = rng.uniform(-1, 1, n) * np.exp(-t * (6.0 / length))
        self._add("hats", self._at(beat), s * 0.22 * vel)

    def tom(self, beat, pitch=90.0, vel=1.0):
        n = int(RATE * 0.3)
        t = np.arange(n) / RATE
        f = pitch * (1 + 0.8 * np.exp(-t * 20))
        s = np.sin(2 * np.pi * np.cumsum(f) / RATE) * np.exp(-t * 9)
        self._add("drums", self._at(beat), s * 0.6 * vel)

    def texture(self, layer, sound, gain=1.0):
        """A loop-long bed of sound, such as wind or fire."""
        sound = np.resize(sound, self.n)
        self._add(layer, 0, sound * gain)

    def mix(self, fx=None):
        fx = fx or {}
        out = np.zeros(self.n)
        for name, buf in self.layers.items():
            x = buf
            spec = VOICES.get(name, {})
            if spec.get("lp"):
                x = circular_lowpass(x, spec["lp"])
            if name in fx:
                x = fx[name](x)
            out += x
        if "master" in fx:
            out = fx["master"](out)
        return out


def wind(n, speed=0.05, gust_hz=0.1):
    t = np.arange(n) / RATE
    gust = 0.6 + 0.4 * np.sin(2 * np.pi * gust_hz * t)
    return circular_lowpass(rng.uniform(-1, 1, n), 400 * speed * 20) * gust


def crackle(n, density=0.002):
    pops = (rng.uniform(0, 1, n) < density).astype(float) * rng.uniform(-1, 1, n)
    return circular_lowpass(pops, 3000) * 3.0 + circular_lowpass(rng.uniform(-1, 1, n), 180) * 0.5


# --- Compositions ------------------------------------------------------


def paradise():
    """Celestial Gardens: harp arpeggios over a choir, in Lydian light."""
    tr = Track(72, 8)
    prog = [[60, 64, 67, 71], [57, 60, 64, 67], [62, 66, 69, 72], [55, 59, 62, 67]]
    for bar, chord in enumerate(prog * 2):
        b = bar * 4
        tr.chord("choir", b, 4, [c - 12 for c in chord], 0.9)
        tr.arp("harp", b, chord + [c + 12 for c in chord], 0.5, 8, 0.7)
        if bar % 2 == 1:
            tr.note("flute", b + 2, 1.5, chord[2] + 12, 0.6)
            tr.note("flute", b + 3.5, 0.5, chord[3] + 12, 0.5)
    tr.note("bell", 0, 4, 84, 0.3)
    tr.note("bell", 16, 4, 88, 0.3)
    return tr.mix({"harp": lambda x: delay(x, tr.beat_sec() * 0.75, 0.35, 0.3)})


def ruined_earth():
    """The hub: a tired pluck over wind, few notes, much space."""
    tr = Track(80, 8)
    tr.texture("wind", wind(tr.n, 0.08), 0.08)
    prog = [[50, 57, 60], [48, 55, 60], [46, 53, 57], [45, 52, 57]]
    melody = [62, 60, 57, 55, 57, 60, 62, 65, 64, 62, 60, 57, 55, 53, 50, 50]
    for bar, chord in enumerate(prog * 2):
        b = bar * 4
        tr.chord("warm", b, 4, chord, 0.5)
        tr.note("sub", b, 3.5, chord[0] - 12, 0.5)
        for i in range(2):
            m = melody[(bar * 2 + i) % len(melody)]
            tr.note("pluck", b + i * 2, 1.5, m, 0.8)
    return tr.mix({"pluck": lambda x: delay(x, tr.beat_sec() * 1.5, 0.4, 0.3)})


def purgatory():
    """The Waiting: a slow toll on a drone that never moves."""
    tr = Track(60, 8)
    tr.texture("wind", wind(tr.n, 0.04, 0.05), 0.05)
    for bar in range(8):
        b = bar * 4
        tr.note("pad", b, 4, 40, 0.8)
        tr.note("pad", b, 4, 47, 0.6)
        tr.note("pad", b, 4, 53, 0.4)
        if bar % 2 == 0:
            tr.note("bell", b, 4, 64, 0.5)
        else:
            tr.note("bell", b + 2, 2, 65, 0.3)
    tr.note("glass", 12, 4, 71, 0.3)
    tr.note("glass", 28, 4, 70, 0.3)
    return tr.mix({"bell": lambda x: delay(x, tr.beat_sec() * 2, 0.5, 0.4)})


def limbo():
    """The Forgotten: a music box far away, half remembered."""
    tr = Track(70, 8)
    prog = [[57, 60, 64], [53, 57, 60], [55, 59, 62], [52, 55, 59]]
    tune = [76, 79, 81, 79, 76, 72, 74, 76, 72, 69, 71, 72, 74, 71, 69, 67]
    for bar, chord in enumerate(prog * 2):
        b = bar * 4
        tr.chord("glass", b, 4, chord, 0.5)
        tr.note("sub", b, 4, chord[0] - 24, 0.35)
        for i in range(2):
            tr.note("music_box", b + i * 2, 1.0, tune[(bar * 2 + i) % len(tune)], 0.7)
            if bar >= 4:
                tr.note("music_box", b + i * 2 + 1, 0.5, chord[(i + bar) % 3] + 12, 0.35)
    return tr.mix({"music_box": lambda x: delay(x, tr.beat_sec() * 1.5, 0.45, 0.4)})


def lust():
    """The Desire: a swaying 6/8 with a warm, bent pad and a low pulse."""
    tr = Track(96, 8, beats=6)
    prog = [[54, 57, 61], [52, 56, 59], [50, 54, 57], [53, 56, 60]]
    for bar, chord in enumerate(prog * 2):
        b = bar * 6
        tr.chord("pad", b, 6, chord, 0.8)
        tr.note("sub", b, 2.5, chord[0] - 24, 0.6)
        tr.note("sub", b + 3, 2.5, chord[0] - 24, 0.5)
        tr.arp("warm", b, [chord[0], chord[1], chord[2], chord[1] + 12, chord[2], chord[1]], 1, 6, 0.4)
        tr.hat(b, 0.5, 0.08)
        tr.hat(b + 3, 0.35, 0.08)
        if bar % 2 == 1:
            tr.note("flute", b + 2, 2, chord[2] + 12, 0.45)
            tr.note("flute", b + 4, 2, chord[2] + 11, 0.4)
    return tr.mix({"flute": lambda x: delay(x, tr.beat_sec() * 2, 0.4, 0.3)})


def gluttony():
    """The Devourers: a swampy, chewing bass with a lurching beat."""
    tr = Track(84, 8, swing=0.25)
    tr.texture("wind", circular_lowpass(rng.uniform(-1, 1, tr.n), 120), 0.06)
    roots = [43, 43, 46, 41, 43, 43, 39, 41]
    for bar, root in enumerate(roots):
        b = bar * 4
        for i, step in enumerate([0, 0.75, 1.5, 2, 2.75, 3.5]):
            tr.note("growl", b + step, 0.4, root + (3 if i == 4 else 0) + (12 if i == 2 else 0), 0.8)
        tr.kick(b)
        tr.kick(b + 2.5, 0.7)
        tr.snare(b + 1.5, 0.6)
        tr.snare(b + 3.5, 0.45)
        tr.note("organ", b, 4, root + 24, 0.3)
        tr.note("organ", b, 4, root + 30, 0.25)
        if bar % 4 == 3:
            tr.tom(b + 3, 80)
            tr.tom(b + 3.5, 60)
    return tr.mix({"master": lambda x: drive(x, 1.4)})


def greed():
    """The Hoarders: coins and keys, a tense clockwork of bells."""
    tr = Track(110, 8)
    prog = [[51, 54, 58], [49, 52, 56], [51, 54, 58], [56, 59, 63]]
    for bar, chord in enumerate(prog * 2):
        b = bar * 4
        tr.arp("bell", b, [c + 24 for c in chord] + [chord[1] + 36], 0.5, 8, 0.45)
        tr.arp("pluck", b, [chord[0], chord[2], chord[1], chord[2]], 0.25, 16, 0.4)
        tr.note("bass", b, 1.5, chord[0] - 12, 0.9)
        tr.note("bass", b + 2, 1.5, chord[0] - 12, 0.8)
        tr.note("bass", b + 3.5, 0.5, chord[0] - 10, 0.7)
        tr.hat(b + 1, 0.4)
        tr.hat(b + 3, 0.4)
        tr.hat(b + 3.5, 0.25)
        if bar >= 4:
            tr.note("strings", b, 4, chord[2] + 12, 0.5)
    return tr.mix({"bell": lambda x: delay(x, tr.beat_sec() * 0.5, 0.35, 0.25)})


def wrath():
    """The Raging: driving drums and a distorted bass riff."""
    tr = Track(150, 8)
    riff = [40, 40, 52, 40, 46, 40, 45, 43]
    for bar in range(8):
        b = bar * 4
        root_shift = 3 if bar in (3, 7) else 0
        for i, step in enumerate(np.arange(0, 4, 0.5)):
            tr.note("growl", b + step, 0.45, riff[i] + root_shift, 0.9)
        for beat in range(4):
            tr.kick(b + beat)
            tr.hat(b + beat + 0.5, 0.6)
        tr.kick(b + 2.5, 0.8)
        tr.snare(b + 1)
        tr.snare(b + 3)
        if bar % 2 == 1:
            tr.snare(b + 3.5, 0.6)
        if bar >= 4:
            tr.note("brass", b, 2, 64 + root_shift, 0.7)
            tr.note("brass", b + 2, 2, 63 + root_shift, 0.7)
            tr.note("brass", b, 2, 67 + root_shift, 0.5)
    return tr.mix({"master": lambda x: drive(x, 1.6)})


def heresy():
    """The Burned: an organ held over fire, with a tritone that will not resolve."""
    tr = Track(66, 8)
    tr.texture("fire", crackle(tr.n), 0.12)
    for bar in range(8):
        b = bar * 4
        tr.note("organ", b, 4, 38, 0.8)
        tr.note("organ", b, 4, 44, 0.6)
        tr.note("organ", b, 4, 50 if bar % 2 == 0 else 49, 0.5)
        tr.note("sub", b, 4, 26, 0.5)
        if bar % 4 == 2:
            tr.note("strings", b, 8, 62, 0.4)
            tr.note("strings", b + 4, 4, 61, 0.4)
        if bar % 2 == 1:
            tr.note("bell", b + 3, 1, 68, 0.25)
    return tr.mix({"organ": lambda x: delay(x, tr.beat_sec() * 3, 0.3, 0.2)})


def violence():
    """The Wounded: war drums and low brass stabs."""
    tr = Track(120, 8)
    stabs = [[50, 53, 57], [50, 53, 57], [48, 51, 55], [49, 53, 56]]
    for bar in range(8):
        b = bar * 4
        chord = stabs[bar % 4]
        for beat in range(4):
            tr.tom(b + beat, 70 if beat % 2 == 0 else 95, 0.9 if beat == 0 else 0.6)
        tr.tom(b + 1.5, 95, 0.5)
        tr.tom(b + 3.5, 120, 0.5)
        tr.kick(b)
        tr.kick(b + 2)
        tr.snare(b + 1, 0.8)
        tr.snare(b + 3, 0.8)
        tr.chord("brass", b, 0.5, chord, 0.9)
        tr.chord("brass", b + 1.5, 0.5, chord, 0.7)
        tr.chord("brass", b + 3, 1, [c - 2 for c in chord] if bar % 4 == 3 else chord, 0.8)
        tr.note("bass", b, 4, chord[0] - 12, 0.8)
        if bar >= 4:
            tr.note("lead", b, 2, chord[2] + 12, 0.5)
            tr.note("lead", b + 2, 2, chord[1] + 12, 0.5)
    return tr.mix({"master": lambda x: drive(x, 1.3)})


def fraud():
    """The Deceivers: a waltz that smiles and is out of tune."""
    tr = Track(132, 8, beats=3)
    prog = [[52, 56, 59], [54, 58, 62], [56, 60, 64], [51, 55, 59]]
    tune = [71, 72, 74, 76, 74, 72, 71, 70]
    for bar, chord in enumerate(prog * 2):
        b = bar * 3
        tr.note("bass", b, 1, chord[0] - 12, 0.9)
        tr.chord("warm", b + 1, 0.8, chord, 0.5)
        tr.chord("warm", b + 2, 0.8, chord, 0.5)
        tr.note("lead", b, 2, tune[bar], 0.6)
        tr.note("lead", b + 2, 1, tune[bar] + (1 if bar % 2 else -1), 0.5)
        tr.hat(b + 1, 0.3)
        tr.hat(b + 2, 0.3)
        if bar >= 4:
            tr.note("music_box", b, 1, tune[bar] + 12, 0.3)
    # The whole thing drifts slightly flat and back, like a warped record.
    def warp(x):
        t = np.arange(len(x)) / RATE
        shift = (0.004 * RATE * np.sin(2 * np.pi * t / (tr.n / RATE))).astype(int)
        return x[(np.arange(len(x)) + shift) % len(x)]
    return tr.mix({"master": warp})


def treachery():
    """The Frozen: ice high up, a pulse far below, and almost nothing between."""
    tr = Track(54, 8)
    tr.texture("wind", wind(tr.n, 0.03, 0.03), 0.07)
    prog = [[59, 62, 66], [57, 60, 64], [55, 59, 62], [54, 57, 61]]
    for bar, chord in enumerate(prog * 2):
        b = bar * 4
        tr.chord("glass", b, 4, [c + 12 for c in chord], 0.6, strum=0.6)
        tr.note("sub", b, 2, chord[0] - 24, 0.5)
        if bar % 2 == 1:
            tr.note("bell", b + 1, 2, chord[2] + 24, 0.3)
            tr.note("bell", b + 3, 1, chord[1] + 24, 0.2)
    return tr.mix({"bell": lambda x: delay(x, tr.beat_sec() * 1.5, 0.55, 0.4), "glass": lambda x: delay(x, tr.beat_sec(), 0.3, 0.2)})


def title():
    """The title: a hymn that falls into a minor turn and climbs back."""
    tr = Track(76, 8)
    prog = [[48, 55, 60, 64], [45, 52, 57, 60], [41, 48, 53, 57], [43, 50, 55, 59], [48, 55, 60, 64], [44, 51, 56, 59], [41, 48, 53, 56], [43, 50, 55, 59]]
    tune = [72, 71, 69, 67, 72, 75, 74, 71]
    for bar, chord in enumerate(prog):
        b = bar * 4
        tr.chord("choir", b, 4, chord, 0.9)
        tr.note("sub", b, 4, chord[0] - 12, 0.4)
        tr.note("flute", b, 3, tune[bar], 0.55)
        tr.note("flute", b + 3, 1, tune[bar] - 2 if bar % 2 == 0 else tune[bar] + 2, 0.4)
        tr.arp("harp", b, chord, 1, 4, 0.4)
    return tr.mix({"harp": lambda x: delay(x, tr.beat_sec() * 0.75, 0.3, 0.3)})


def boss():
    """A fight: fast, loud, with the Wrath riff's cousin and a choir on top."""
    tr = Track(140, 8)
    riff = [38, 38, 50, 38, 44, 45, 38, 41]
    for bar in range(8):
        b = bar * 4
        shift = 1 if bar in (3, 7) else 0
        for i, step in enumerate(np.arange(0, 4, 0.5)):
            tr.note("growl", b + step, 0.45, riff[i] + shift, 0.9)
        for beat in range(4):
            tr.kick(b + beat)
            tr.hat(b + beat, 0.5)
            tr.hat(b + beat + 0.5, 0.7)
        tr.snare(b + 1)
        tr.snare(b + 3)
        tr.snare(b + 3.75, 0.5)
        tr.tom(b + 2.5, 100, 0.6)
        tr.chord("choir", b, 4, [62 + shift, 65 + shift, 69 + shift], 0.8)
        if bar >= 4:
            tr.note("brass", b, 1, 74 + shift, 0.6)
            tr.note("brass", b + 1.5, 0.5, 73 + shift, 0.6)
            tr.note("brass", b + 2, 2, 70 + shift, 0.6)
    return tr.mix({"master": lambda x: drive(x, 1.6)})


TRACKS = {
    "paradise": paradise,
    "ruined_earth": ruined_earth,
    "purgatory": purgatory,
    "limbo": limbo,
    "lust": lust,
    "gluttony": gluttony,
    "greed": greed,
    "wrath": wrath,
    "heresy": heresy,
    "violence": violence,
    "fraud": fraud,
    "treachery": treachery,
    "title": title,
    "boss": boss,
}


# --- Output --------------------------------------------------------------


def write_wav(path, data, loudness=0.2, gain=0.9):
    """Brings every track to the same loudness (RMS), soft-clipping the
    peaks, so the quiet pads and the drum tracks sit at one level."""
    data = np.asarray(data, dtype=np.float64)
    rms = np.sqrt(np.mean(data**2)) or 1.0
    data = np.tanh(data / rms * loudness * 1.5) / 1.5
    peak = np.max(np.abs(data)) or 1.0
    pcm = (data / peak * gain * 32767).astype("<i2")
    with wave.open(path, "wb") as f:
        f.setnchannels(1)
        f.setsampwidth(2)
        f.setframerate(RATE)
        f.writeframes(pcm.tobytes())


def write(name, data):
    os.makedirs(OUT, exist_ok=True)
    ogg = os.path.join(OUT, name + ".ogg")
    wav = os.path.join(OUT, name + ".wav")
    ffmpeg = shutil.which("ffmpeg")
    if ffmpeg is None:
        write_wav(wav, data)
        print("wrote", os.path.relpath(wav, os.path.dirname(OUT)), "%.1f s (no ffmpeg, left as WAV)" % (len(data) / RATE))
        return
    with tempfile.TemporaryDirectory() as tmp:
        src = os.path.join(tmp, "track.wav")
        write_wav(src, data)
        subprocess.run(
            [ffmpeg, "-loglevel", "error", "-y", "-i", src, "-c:a", "libvorbis", "-q:a", "3", ogg],
            check=True,
        )
    if os.path.exists(wav):
        os.remove(wav)
    print("wrote", os.path.relpath(ogg, os.path.dirname(OUT)), "%.1f s" % (len(data) / RATE))


def main(names):
    for name in names or TRACKS:
        write(name, TRACKS[name]())


if __name__ == "__main__":
    main(sys.argv[1:])
