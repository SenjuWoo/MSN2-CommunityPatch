"""Extract named AudioClips from My Sexy Neighbor (MSN1) as 16-bit PCM WAV (mono, 22.05 kHz) for MSN2 Expansion data.

usage: python -I extract_audio.py --msn1 "<MSN1>/My Sexy Neighbor_Data" --out data rain_light rain_medium
"""
import argparse
import io
import os
import sys
import wave

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import convert_skin as C  # noqa: E402


def to_mono_22k(wav_bytes):
    with wave.open(io.BytesIO(wav_bytes)) as w:
        ch, sw, rate, n = w.getnchannels(), w.getsampwidth(), w.getframerate(), w.getnframes()
        raw = w.readframes(n)
    dt = {1: np.int8, 2: np.int16, 4: np.int32}[sw]
    a = np.frombuffer(raw, dt).astype(np.float32).reshape(-1, ch).mean(1)
    a /= float(np.iinfo(dt).max)
    if rate != 22050:
        t = np.linspace(0, len(a) / rate, int(len(a) * 22050 / rate), endpoint=False)
        a = np.interp(t, np.arange(len(a)) / rate, a)
    out = io.BytesIO()
    with wave.open(out, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(22050)
        w.writeframes((np.clip(a, -1, 1) * 32767).astype("<i2").tobytes())
    return out.getvalue()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--msn1", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("names", nargs="+")
    a = ap.parse_args()
    UnityPy = C.unitypy()
    env = UnityPy.load(*[os.path.join(a.msn1, n) for n in ("data.unity3d", "sharedassets0.resource", "resources.resource")
                         if os.path.exists(os.path.join(a.msn1, n))])
    os.makedirs(a.out, exist_ok=True)
    want = set(a.names)
    for o in env.objects:
        if o.type.name != "AudioClip" or o.peek_name() not in want:
            continue
        clip = o.read()
        for name, data in clip.samples.items():
            p = os.path.join(a.out, f"{o.peek_name()}.wav")
            open(p, "wb").write(to_mono_22k(data))
            print(p, os.path.getsize(p))
            want.discard(o.peek_name())
            break
    if want:
        sys.exit(f"not found: {sorted(want)}")


if __name__ == "__main__":
    main()
