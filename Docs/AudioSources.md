# Recorded ride audio

Both sources below are published under [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/), checked 2026-10-30. No paid M2 / Pro Sound Effects recording is used.

| Output | Recording | Author | Source |
|---|---|---|---|
| MotorLow / MotorMedium / MotorHigh | Acceleration of an electric car (Tesla Model S Plaid) | justVova | https://freesound.org/people/justVova/sounds/761685/ |
| W3CarPass / W3StreetPass | Car Passing | Johnnyfarmer | https://freesound.org/people/Johnnyfarmer/sounds/209767/ |

The author describes the Tesla recording as full-throttle acceleration. The recording's identity is based on that description, not independent vehicle verification. Attribution is retained for provenance; it does not imply endorsement.

## Input quality and reproducibility

This audition uses Freesound's public HQ MP3 renditions, decoded to 48 kHz PCM16. These are **not** the original lossless WAV / AIFF files. The W3 original requires a Freesound login. The imported PCM clips do not recover information lost in MP3 compression.

- Tesla preview: https://cdn.freesound.org/previews/761/761685_13088347-hq.mp3
- W3 preview: https://cdn.freesound.org/previews/209/209767_2012390-hq.mp3

Keep downloaded sources under ignored `.Local/AudioSources/`. Decode with ffmpeg to `TeslaPlaid-761685.wav` and `W3-CarPassing.wav`, both 48 kHz PCM16. Run `python Tools/Audio/prepare_recorded_audio.py` (numpy required). The script generates five WAVs under `Assets/Audio/Highway/Recorded` and a local `analysis.json`. In a stopped, saved Main scene, use **Tools > Highway Surfing > Use Recorded Ride Audio**.

## Motor preparation

The 5.158-second recording contains substantial road rumble and continuously rising motor tones. Simply repeating cuts would create a repeated acceleration. A measured motor partial is tracked with short-time FFTs, smoothed, and used to resample the recording at constant rotor phase speed. Filtering reduces road rumble and broadband hiss while retaining the recorded tone and nearby harmonics. No synthetic oscillator is added.

| Band | Source interval | Stabilized reference partial | Loop length | Speed anchor |
|---|---|---|---|---|
| Low | 1.50–2.60 s | 1692.11 Hz | 0.994 s | 70 km/h |
| Medium | 2.15–3.35 s | 2033.55 Hz | 1.096 s | 138 km/h |
| High | 3.00–4.30 s | 2403.67 Hz | 1.197 s | 220 km/h |

The reference is a tracked audible partial, not measured physical motor RPM. A 100 ms phase-aligned circular crossfade joins each loop. Each is normalized to RMS 0.13, with peaks below 0.57. Runtime applies a shared 0.85 pitch scale, continuously matches adjacent bands' tone frequencies, and blends them with equal-power weights. Actual speed controls the blend and pitch. Accelerator input raises gain and opens a low-pass filter; brake input reduces drive level. Neutral holds the current speed and pitch. Speed controls and their thresholds are unchanged.

## W3 preparation

W3 is one recorded car pass. The 2.9-second car clip has leading silence and long decay trimmed, a small low-frequency reduction, and edge fades. Its RMS peak is 0.95 seconds into the clip; gameplay starts playback ahead of the geometric crossing by that amount divided by playback rate. A separate 0.57-second, high-passed extract provides a short streetlight air sound (peak 0.2625 seconds). Both derive from W3.

The original is panned left-to-right. Mono derivatives allow runtime placement on the actual passing side. The original recorded Doppler movement is retained; runtime uses a fixed, modest playback rate per pass instead of adding another pitch fall. Up to eight voices can overlap, with no continuous wind loop.

## Low drive layer

`HighwayDriveBass` adds an original synthesized layer underneath the recordings. The existing recorded clips are unchanged. Its foundation is a 66 Hz rotor with harmonics, a quiet subharmonic, and a band-limited low rumble. All frequencies and modulation rates meet at a four-second loop boundary; the clip has no DC offset and a peak below 0.78. It is generated once per component lifetime as 22.05 kHz mono PCM, rather than synthesized per frame or on the audio thread.

Speed changes playback pitch from 0.82 to 1.28 (a roughly 54–84 Hz foundation, with upper harmonics audible on smaller speakers). Acceleration increases its weight, braking halves the drive envelope, and neutral holds the current speed/pitch. It fades at zero speed, pause and onboarding, and follows SoundEnabled / MasterVolume. `BassEnabled` provides an A/B switch; `BassVolume` defaults to 0.8 in both Main and HighwayKeyboard. The mix is intended as a low propulsion sound rather than a literal jet-engine recording.

Mac validation: loop continuity, zero DC, peak bounds and all 221 integer speeds passed. Ten Play checks covered A/B, speed/pitch, acceleration/braking, zero speed, pause and mute; measured full-output peak was 0.425 with no clipping in that trial. A steady 138 km/h motor-only comparison is saved locally as `.Local/Review/drive-bass-comparison.wav` (four seconds original, half-second silence, four seconds with bass). The new mix has substantially more 20–300 Hz energy; playback-device bass response still determines the audible weight.

## Limits to assess by ear

This is a processed, short field recording, not an isolated multi-RPM studio session. Listen for a periodic texture, thin/harsh high frequencies, residual road noise, and the relative level of pass-bys. Frequency and level checks establish signal behavior; speaker quality and the user's preferred timbre need in-person evaluation.


## 衝突の低音（2026-10-31）

`HighwayImpactFeedback` がオリジナルの約0.5秒の鈍い衝突音を合成し、Play開始時に準備します。108→68 Hzの低い胴鳴り、控えめな倍音とローパスした衝撃音を重ね、ピークを0.95に整えています。外部素材・追加ライセンスはありません。初期音量0.85、短い走行音の減衰で駆動低音への埋もれを抑えます。
