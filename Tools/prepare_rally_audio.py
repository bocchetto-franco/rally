"""Download verified CC0 audio; prepare mono seamless SFX and keep music as Ogg.

Requires imageio-ffmpeg (only for decoding public Freesound HQ previews).
Run from the repository root. Originals are cached under Temp/RallyAudio.
"""
import array
import hashlib
import json
import math
from pathlib import Path
import shutil
import subprocess
import sys
import urllib.request
import wave

ROOT = Path(__file__).resolve().parents[1]
CACHE = ROOT / 'Temp/RallyAudio'
OUTPUT = ROOT / 'Assets/Resources/Audio'
SOURCES = [
    ('EngineLoop', 'Sedan engine loop', 'Dmitry_mansurev64',
     'https://freesound.org/people/Dmitry_mansurev64/sounds/748027/',
     'https://cdn.freesound.org/previews/748/748027_16197341-hq.mp3'),
    ('TyreSkid', 'screeching tyres / tires', 'johnnydekk',
     'https://freesound.org/people/johnnydekk/sounds/614627/',
     'https://cdn.freesound.org/previews/614/614627_5790048-hq.mp3'),
    ('Impact', 'Collision', 'qubodup',
     'https://freesound.org/people/qubodup/sounds/332058/',
     'https://cdn.freesound.org/previews/332/332058_71257-hq.mp3'),
    ('MenuMusic', 'Racing Game Title', 'MintoDog',
     'https://opengameart.org/content/racing-game-title',
     'https://opengameart.org/sites/default/files/racing_game_title_bpm140.ogg'),
    ('RaceMusic', 'Hot Roadway', 'MintoDog',
     'https://opengameart.org/content/hot-roadway',
     'https://opengameart.org/sites/default/files/hot_roadway_bpm160.ogg'),
]


def fetch(url):
    with urllib.request.urlopen(url, timeout=60) as response:
        return response.read()


def rms(samples):
    return math.sqrt(sum(x*x for x in samples) / max(1, len(samples)))


def prepare():
    import imageio_ffmpeg
    ffmpeg = imageio_ffmpeg.get_ffmpeg_exe()
    CACHE.mkdir(parents=True, exist_ok=True)
    OUTPUT.mkdir(parents=True, exist_ok=True)
    manifest = []
    for name, title, author, page_url, audio_url in SOURCES:
        html = fetch(page_url).decode('utf-8')
        if 'creativecommons.org/publicdomain/zero/1.0' not in html:
            raise RuntimeError('CC0 could not be verified: ' + page_url)
        source = CACHE / (name + Path(audio_url).suffix)
        if not source.exists():
            source.write_bytes(fetch(audio_url))
        raw = source.read_bytes()
        if name.endswith('Music'):
            target = OUTPUT / (name + '.ogg')
            shutil.copyfile(source, target)
            changes = 'Original Ogg file, unchanged.'
            duration = None
        else:
            pcm = subprocess.check_output([ffmpeg, '-v', 'error', '-i', str(source),
                '-ac', '1', '-ar', '44100', '-f', 'f32le', '-'])
            samples = array.array('f', pcm)
            original_duration = len(samples) / 44100
            # Remove codec padding / silent lead-ins before preparing the loop.
            peak = max(abs(x) for x in samples)
            start = next(i for i, x in enumerate(samples) if abs(x) > peak * .008)
            end = len(samples) - next(i for i, x in enumerate(reversed(samples)) if abs(x) > peak * .008)
            samples = samples[max(0, start - 220):min(len(samples), end + 220)]
            if name == 'TyreSkid':
                # This recording contains separate rubber screeches. Pick the
                # strongest continuous two-second section, avoiding quiet gaps.
                size = min(88200, len(samples))
                offset = max(range(0, max(1, len(samples)-size+1), 4410),
                    key=lambda i: rms(samples[i:i+size]))
                samples = samples[offset:offset+size]
            if name != 'Impact':
                fade = min(3528, len(samples)//8)
                loop = samples[fade:]
                for i in range(fade):
                    t = i / (fade-1)
                    loop[-fade+i] = samples[-fade+i]*(1-t) + samples[i]*t
                samples = loop
            peak = max(abs(x) for x in samples)
            gain = min(.8 / peak, .18 / max(.001, rms(samples))) if name != 'Impact' else .85 / peak
            encoded = array.array('h', (round(max(-1, min(1, x*gain))*32767) for x in samples))
            target = OUTPUT / (name + '.wav')
            with wave.open(str(target), 'wb') as wav:
                wav.setnchannels(1)
                wav.setsampwidth(2)
                wav.setframerate(44100)
                wav.writeframes(encoded.tobytes())
            duration = len(samples) / 44100
            changes = ('Public HQ MP3 preview decoded to mono PCM 44.1 kHz/16-bit; silence trimmed and level normalized. '
                       + ('80 ms circular crossfade for looping.' if name != 'Impact' else 'One-shot, not looped.')
                       + (' Strongest 2-second screech excerpt.' if name == 'TyreSkid' else ''))
            print(f'{name}: original {original_duration:.3f}s, final {duration:.3f}s, peak {max(abs(x) for x in encoded)/32767:.3f}')
        manifest.append(dict(name=name, title=title, author=author, page=page_url,
            download=audio_url, license='CC0-1.0', license_url='https://creativecommons.org/publicdomain/zero/1.0/',
            source_sha256=hashlib.sha256(raw).hexdigest(),
            file=str(target.relative_to(ROOT)).replace('\\','/'),
            sha256=hashlib.sha256(target.read_bytes()).hexdigest(), duration_seconds=duration, changes=changes))
    (OUTPUT / 'DOWNLOAD_MANIFEST.json').write_text(json.dumps(manifest, indent=2, ensure_ascii=False)+'\n', encoding='utf-8')
    print('Prepared', len(manifest), 'CC0 audio assets.')


if __name__ == '__main__':
    prepare()
