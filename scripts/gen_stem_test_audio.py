"""
สร้างเพลงทดสอบแบบแยก stem สำหรับลองระบบเพลงซ้อนชั้น (LayeredMusicPlayer / MusicDirector)

เป็นเสียงสังเคราะห์ล้วน ไม่ใช่เพลงจริง — มีไว้ฟังว่า "ชั้นไหนเปิดตอนไหน" กับ "ทุกชั้นตรงจังหวะกัน"
ทุกชั้นของเพลงเดียวกันยาวเท่ากันระดับ sample (กติกาของ LayeredTrack) และหางเสียงวนกลับไปต้นลูป
จึงต่อกันเนียนตอนวน

    python3 scripts/gen_stem_test_audio.py

เขียนทับไฟล์ใน Assets/Sound/BGM/StemTest/ · .meta ไม่ถูกแตะ (guid เดิมยังอยู่)
"""
import os
import wave

import numpy as np

SR = 22050                      # เสียงทดสอบ ไม่ต้องคมชัด — ไฟล์เล็กลงครึ่งหนึ่ง
OUT = os.path.join(os.path.dirname(__file__), "..", "Assets", "Sound", "BGM", "StemTest")
rng = np.random.default_rng(7)


def midi(n):
    return 440.0 * 2 ** ((n - 69) / 12)


class Loop:
    """บัฟเฟอร์ยาวเท่าลูปพอดี · โน้ตที่ล้นท้ายวนกลับไปต้น ลูปจึงไม่มีรอยต่อ"""

    def __init__(self, bpm, bars, beats_per_bar=4):
        self.beat = 60.0 / bpm
        self.n = int(round(bars * beats_per_bar * self.beat * SR))
        self.buf = np.zeros(self.n)

    def add(self, at_beats, sig):
        start = int(round(at_beats * self.beat * SR)) % self.n
        idx = (start + np.arange(len(sig))) % self.n
        np.add.at(self.buf, idx, sig)


def env(n_samples, attack=0.005, release=0.05):
    e = np.ones(n_samples)
    a = min(n_samples, max(1, int(attack * SR)))
    r = min(n_samples, max(1, int(release * SR)))
    e[:a] = np.linspace(0, 1, a)
    e[-r:] *= np.linspace(1, 0, r)
    return e


def tone(freq, dur, harmonics=(1.0,), decay=None, attack=0.005, release=0.05):
    t = np.arange(int(dur * SR)) / SR
    s = sum(a * np.sin(2 * np.pi * freq * (i + 1) * t) for i, a in enumerate(harmonics))
    if decay:
        s = s * np.exp(-t / decay)
    return s * env(len(t), attack, release)


def noise(dur, decay, highpass=False):
    s = rng.standard_normal(int(dur * SR))
    if highpass:
        s = np.diff(s, prepend=0)       # ตัดเสียงทุ้มออก ให้เป็นฉาบ
    t = np.arange(len(s)) / SR
    return s * np.exp(-t / decay) * env(len(s), 0.001, 0.01)


def kick(dur=0.35):
    t = np.arange(int(dur * SR)) / SR
    f = 50 + 90 * np.exp(-t / 0.04)
    return np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / 0.12) * env(len(t), 0.001, 0.02)


def save(name, loop_or_array):
    data = loop_or_array.buf if isinstance(loop_or_array, Loop) else loop_or_array
    peak = np.max(np.abs(data))
    if peak > 0.89:                     # กันแตก แต่ไม่ normalize — ระดับเทียบกันระหว่างชั้นต้องคงเดิม
        data = data * (0.89 / peak)
    pcm = (np.clip(data, -1, 1) * 32767).astype("<i2")
    path = os.path.join(OUT, name + ".wav")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())
    print(f"{name:22s} {len(pcm):7d} samples  {len(pcm) / SR:5.2f}s  peak {peak:.2f}")
    return len(pcm)


# ── เพลงหลัก: A minor · 120 BPM · 4 ห้อง (Am F C G) ─────────────────────────
BPM, BARS = 120, 4
CHORDS = [(57, 60, 64), (53, 57, 60), (48, 52, 55), (55, 59, 62)]   # Am F C G
ROOTS = [45, 41, 36, 43]


def main_track():
    lengths = []

    pad = Loop(BPM, BARS)
    for bar, chord in enumerate(CHORDS):
        for n in chord:
            pad.add(bar * 4, 0.12 * tone(midi(n), 4 * pad.beat + 0.3, (1, 0.4, 0.2), attack=0.4, release=0.6))
    lengths.append(save("StemTest_pad", pad))

    melody = Loop(BPM, BARS)          # arpeggio เขบ็ต 1 ชั้น
    for bar, chord in enumerate(CHORDS):
        notes = [chord[0] + 12, chord[1] + 12, chord[2] + 12, chord[1] + 12]
        for step in range(8):
            melody.add(bar * 4 + step * 0.5, 0.22 * tone(midi(notes[step % 4]), 0.4, (1, 0.3), decay=0.15))
    lengths.append(save("StemTest_melody", melody))

    drums = Loop(BPM, BARS)
    for beat in range(BARS * 4):
        if beat % 2 == 0: drums.add(beat, 0.5 * kick())
        else:             drums.add(beat, 0.25 * noise(0.2, 0.06))          # snare
        drums.add(beat,       0.12 * noise(0.05, 0.015, highpass=True))     # hat
        drums.add(beat + 0.5, 0.08 * noise(0.05, 0.015, highpass=True))
    lengths.append(save("StemTest_drums", drums))

    bass = Loop(BPM, BARS)
    for bar, root in enumerate(ROOTS):
        for step in range(8):
            n = root + (12 if step % 4 == 3 else 0)
            bass.add(bar * 4 + step * 0.5, 0.4 * tone(midi(n), 0.45, (1, 0.5, 0.25), decay=0.25))
    lengths.append(save("StemTest_bass", bass))

    # overlay มินิบอส — เขบ็ต 2 ชั้นถี่ๆ + เสียงเตือนสูงต้นห้อง ฟังแล้วรู้ทันทีว่า "มีอะไรมา"
    perc = Loop(BPM, BARS)
    for step in range(BARS * 16):
        accent = 1.0 if step % 4 == 0 else 0.5
        perc.add(step * 0.25, 0.13 * accent * noise(0.08, 0.02, highpass=True))
    for bar in range(BARS):
        perc.add(bar * 4, 0.1 * tone(midi(88), 0.3, (1,), decay=0.1))
        perc.add(bar * 4 + 2, 0.35 * tone(110, 0.3, (1, 0.5), decay=0.12))  # tom
    lengths.append(save("StemTest_perc", perc))

    assert len(set(lengths)) == 1, f"stem ยาวไม่เท่ากัน {lengths}"


# ── ธีมบอส: D minor · 140 BPM · 4 ห้อง ────────────────────────────────────
BOSS_BPM = 140
BOSS_ROOTS = [38, 38, 34, 36]                 # D D Bb C


def boss_track():
    lengths = []

    drums = Loop(BOSS_BPM, BARS)
    for beat in range(BARS * 4):
        drums.add(beat, 0.33 * kick(0.3))                                    # four on the floor
        if beat % 2 == 1: drums.add(beat, 0.2 * noise(0.25, 0.08))
        for sub in range(4):
            drums.add(beat + sub * 0.25, 0.07 * noise(0.04, 0.01, highpass=True))
    lengths.append(save("StemTest_boss_drums", drums))

    bass = Loop(BOSS_BPM, BARS)
    for bar, root in enumerate(BOSS_ROOTS):
        for step in range(16):
            bass.add(bar * 4 + step * 0.25, 0.3 * tone(midi(root), 0.22, (1, 0.6, 0.4, 0.3), decay=0.1))
    lengths.append(save("StemTest_boss_bass", bass))

    lead = Loop(BOSS_BPM, BARS)               # เมโลดี้คลื่นสี่เหลี่ยม (ฮาร์มอนิกคี่)
    phrase = [62, 65, 69, 67, 65, 64, 62, 61]
    for bar in range(BARS):
        for i, n in enumerate(phrase):
            lead.add(bar * 4 + i * 0.5, 0.3 * tone(midi(n + (12 if bar == 3 else 0)), 0.45,
                                                   (1, 0, 0.33, 0, 0.2), decay=0.4))
    lengths.append(save("StemTest_boss_lead", lead))

    assert len(set(lengths)) == 1, f"stem ยาวไม่เท่ากัน {lengths}"


def stingers():
    win = np.zeros(int(2.0 * SR))
    for i, n in enumerate([72, 76, 79, 84]):                     # C E G C ขึ้น
        s = 0.3 * tone(midi(n), 1.6 - i * 0.15, (1, 0.3), decay=0.6, release=0.3)
        at = int(i * 0.12 * SR)
        win[at:at + len(s)] += s[: len(win) - at]
    save("StemTest_win", win)

    lose = np.zeros(int(2.5 * SR))
    for i, n in enumerate([64, 63, 62, 57]):                     # ไล่ลงช้าๆ
        s = 0.3 * tone(midi(n), 0.9, (1, 0.5, 0.2), decay=0.5, release=0.3)
        at = int(i * 0.45 * SR)
        lose[at:at + len(s)] += s[: len(lose) - at]
    save("StemTest_lose", lose)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    main_track()
    boss_track()
    stingers()
