"""Prepare the CC0 Tesla/W3 audition assets. Requires numpy and ffmpeg on PATH.

Input: .Local/AudioSources/TeslaPlaid-761685.wav and W3-CarPassing.wav (48 kHz PCM16).
Source URLs and derivation notes: Docs/AudioSources.md. Does not fetch or buy audio.
"""
from pathlib import Path
import json
import wave
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / '.Local/AudioSources'
OUT = ROOT / 'Assets/Audio/Highway/Recorded'
RATE = 48000


def read(name):
    with wave.open(str(SOURCE / name), 'rb') as w:
        assert w.getframerate() == RATE and w.getsampwidth() == 2
        return np.frombuffer(w.readframes(w.getnframes()), '<i2').reshape(-1, w.getnchannels()).mean(1) / 32768


def write(name, x):
    assert np.isfinite(x).all() and abs(x).max() < .98
    OUT.mkdir(parents=True, exist_ok=True)
    with wave.open(str(OUT / (name + '.wav')), 'wb') as w:
        w.setparams((1, 2, RATE, len(x), 'NONE', 'not compressed'))
        w.writeframes(np.round(x * 32767).astype('<i2').tobytes())
    return dict(duration=len(x) / RATE, rms=float(np.sqrt(np.mean(x*x))),
                peak=float(abs(x).max()), seam_step=float(abs(x[0]-x[-1])))


def band_filter(x, highpass, lowpass):
    f = np.fft.rfftfreq(len(x), 1 / RATE)
    gain = (1 - np.exp(-(f / highpass)**4)) * np.exp(-(f / lowpass)**8)
    return np.fft.irfft(np.fft.rfft(x) * gain, n=len(x))


def fade(x, attack, release):
    x = x.copy()
    a, b = int(attack * RATE), int(release * RATE)
    x[:a] *= np.sin(np.linspace(0, np.pi / 2, a))**2
    x[-b:] *= np.cos(np.linspace(0, np.pi / 2, b))**2
    return x


def motor_loop(x, start, end):
    # Follow the visible accelerating rotor partial, not the loud road rumble below 300 Hz.
    anchors_t = [1.4, 2, 2.5, 3, 3.5, 4, 4.7]
    anchors_f = [1310, 1680, 1930, 2150, 2350, 2540, 2780]
    times = np.arange(start, end, .01)
    tracked, ratios = [], []
    n = 4096
    freqs = np.fft.rfftfreq(n, 1 / RATE)
    for t in times:
        centre = int(t * RATE)
        spectrum = abs(np.fft.rfft(x[centre-n//2:centre+n//2] * np.hanning(n)))
        expected = np.interp(t, anchors_t, anchors_f)
        candidates = np.where(abs(freqs-expected) < 75)[0]
        k = candidates[np.argmax(spectrum[candidates])]
        values = np.log(spectrum[k-1:k+2] + 1e-12)
        offset = .5 * (values[0]-values[2]) / (values[0]-2*values[1]+values[2])
        tracked.append((k + np.clip(offset, -.5, .5)) * RATE / n)
        ratios.append(spectrum[k] / (np.median(spectrum[candidates]) + 1e-12))
    tracked = np.array(tracked)
    # Smooth the rising trajectory, then resample along equal rotor-phase increments.
    # This removes the baked-in acceleration; gameplay now controls pitch.
    fit = np.polynomial.Polynomial.fit(times, tracked, 2)
    target = float(fit((start+end)/2))
    sample_t = np.arange(int(start*RATE), int(end*RATE)) / RATE
    instantaneous = fit(sample_t)
    cycles = np.cumsum(instantaneous / RATE)
    cycles -= cycles[0]
    output_cycles = np.arange(0, cycles[-1], target/RATE)
    input_positions = np.interp(output_cycles, cycles, sample_t * RATE)
    segment = np.interp(input_positions, np.arange(len(x)), x)
    # Retain the original recorded tone and nearby harmonics. Reduce road/wind noise.
    f = np.fft.rfftfreq(len(segment), 1/RATE)
    envelope = np.full_like(f, .08)
    for harmonic, strength in [(1, 1), (2, .55), (3, .3)]:
        envelope += strength * np.exp(-.5 * ((f-target*harmonic)/(70+harmonic*25))**2)
    envelope *= (1-np.exp(-(f/400)**4)) * np.exp(-(f/8000)**8)
    segment = np.fft.irfft(np.fft.rfft(segment) * envelope, n=len(segment))
    crossfade = int(.10 * RATE)
    cycles_in_loop = int((len(segment)-crossfade) * target / RATE)
    period = int(round(cycles_in_loop * RATE / target))
    mix = np.sin(np.linspace(0, np.pi/2, crossfade))**2
    loop = np.concatenate((segment[crossfade:period],
                           segment[period:period+crossfade]*(1-mix)+segment[:crossfade]*mix))
    loop -= loop.mean()
    loop *= min(.13 / np.sqrt(np.mean(loop*loop)), .65 / abs(loop).max())
    return loop, dict(source_start=start, source_end=end, reference_hz=target,
                     tracked_median_contrast=float(np.median(ratios)),
                     pitch_fit_error_hz=float(np.sqrt(np.mean((fit(times)-tracked)**2))))


def main():
    report = {'sources': {'motor': 'https://freesound.org/people/justVova/sounds/761685/',
                          'pass': 'https://freesound.org/people/Johnnyfarmer/sounds/209767/'},
              'license': 'CC0-1.0', 'source_quality': 'Public HQ MP3 previews decoded to PCM; not original WAV/AIFF',
              'motor': [], 'pass': {}}
    motor = read('TeslaPlaid-761685.wav')
    for name, start, end in [('MotorLow', 1.5, 2.6), ('MotorMedium', 2.15, 3.35), ('MotorHigh', 3.0, 4.3)]:
        loop, info = motor_loop(motor, start, end)
        info.update(write(name, loop)); info['name'] = name
        report['motor'].append(info)
    passing = read('W3-CarPassing.wav')
    # Mono avoids hard-coded left-to-right motion; the game pans to the actual passing side.
    car = band_filter(passing, 130, 10000)[int(.75*RATE):int(3.65*RATE)]
    car = fade(car, .14, .35)
    car *= .68 / abs(car).max()
    street = band_filter(passing, 1200, 8500)[int(1.43*RATE):int(2.00*RATE)]
    street = fade(street, .17, .32)
    street *= .55 / abs(street).max()
    for name, clip in [('W3CarPass', car), ('W3StreetPass', street)]:
        block = int(.025*RATE)
        rms = np.array([np.mean(clip[i:i+block]**2) for i in range(0,len(clip)-block,block//2)])
        info = write(name, clip)
        info['peak_seconds'] = (int(rms.argmax())*(block//2)+block/2)/RATE
        report['pass'][name] = info
    (SOURCE / 'analysis.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()
