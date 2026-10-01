"""Download verified Poly Haven CC0 2K maps; pack ARM for URP Lit."""
import hashlib
import json
from pathlib import Path
import urllib.request
from PIL import Image, ImageOps

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / 'Assets/Art/RoadSurfaces'

def fetch(url):
    request = urllib.request.Request(url, headers={'User-Agent': 'RallyAssetImport/1.0'})
    with urllib.request.urlopen(request, timeout=90) as response:
        return response.read()

def main():
    manifest = []
    for asset in ('rocky_trail_02', 'muddy_tracks'):
        files = json.loads(fetch('https://api.polyhaven.com/files/' + asset))
        folder = DEST / asset
        folder.mkdir(parents=True, exist_ok=True)
        for channel, key in (('diff', 'Diffuse'), ('nor_gl', 'nor_gl'), ('arm', 'arm')):
            entry = files[key]['2k']['jpg']
            data = fetch(entry['url'])
            if hashlib.md5(data).hexdigest() != entry['md5']:
                raise ValueError('Download checksum mismatch: ' + asset + channel)
            path = folder / (channel + '.jpg')
            path.write_bytes(data)
            with Image.open(path) as image:
                if max(image.size) > 2048:
                    raise ValueError('Unexpected resolution: ' + str(image.size))
            manifest.append(dict(asset=asset, channel=channel, url=entry['url'],
                                 license='CC0-1.0', sha256=hashlib.sha256(data).hexdigest()))
        with Image.open(folder / 'arm.jpg') as arm:
            ao, roughness, metal = arm.convert('RGB').split()
            # URP Lit: metallic R, occlusion G, smoothness A. Shared by both slots.
            Image.merge('RGBA', (metal, ao, Image.new('L', arm.size, 0),
                                  ImageOps.invert(roughness))).save(folder / 'urp_mask.png')
        print('Downloaded and packed:', asset)
    (DEST / 'DOWNLOAD_MANIFEST.json').write_text(json.dumps(manifest, indent=2) + '\n')

if __name__ == '__main__':
    main()
