"""Read-only checks of road material links, texture import settings and packed maps."""
import hashlib
import json
from pathlib import Path
import re
from PIL import Image, ImageChops, ImageOps

ROOT = Path(__file__).resolve().parents[1]
SURFACES = ROOT / 'Assets/Art/RoadSurfaces'

guids = []
for meta in SURFACES.rglob('*.meta'):
    guid = re.search(r'guid: (\w+)', meta.read_text())[1]
    assert re.fullmatch(r'[0-9a-f]{32}', guid), (meta, guid)
    assert guid not in guids, (meta, 'duplicate GUID')
    guids.append(guid)

for item in json.loads((SURFACES / 'DOWNLOAD_MANIFEST.json').read_text()):
    path = SURFACES / item['asset'] / (item['channel'] + '.jpg')
    assert hashlib.sha256(path.read_bytes()).hexdigest() == item['sha256'], path

for asset, material, circuits in (
    ('rocky_trail_02', 'Assets/Art/Environment/Materials/Road - dry gravel.mat', (1, 2)),
    ('muddy_tracks', 'Assets/Art/Forest/Materials/Wet forest road.mat', (3,)),
):
    mat = (ROOT / material).read_text()
    guid = re.search(r'guid: (\w+)', Path(str(ROOT / material) + '.meta').read_text())[1]
    for circuit in circuits:
        scene = ROOT / f'Assets/Scenes/Circuit_{circuit:02}.unity'
        assert guid in scene.read_text(), (scene, 'missing material reference')
    for slot, name in (('_BaseMap', 'diff.jpg'), ('_BumpMap', 'nor_gl.jpg'),
                       ('_MetallicGlossMap', 'urp_mask.png'), ('_OcclusionMap', 'urp_mask.png')):
        path = SURFACES / asset / name
        meta = Path(str(path) + '.meta').read_text()
        tex_guid = re.search(r'guid: (\w+)', meta)[1]
        assert re.search(r'- ' + slot + r':\s+m_Texture: \{fileID: 2800000, guid: ' + tex_guid, mat)
        assert 'maxTextureSize: 2048' in meta and 'textureCompression: 1' in meta
        assert 'enableMipMap: 1' in meta and 'aniso: 4' in meta
        if name != 'diff.jpg':
            assert 'sRGBTexture: 0' in meta
        if name == 'nor_gl.jpg':
            assert 'textureType: 1' in meta
        with Image.open(path) as image:
            assert image.size == (2048, 2048), (path, image.size)
    for keyword in ('_NORMALMAP', '_METALLICSPECGLOSSMAP', '_OCCLUSIONMAP'):
        assert '  - ' + keyword + '\n' in mat
    with Image.open(SURFACES / asset / 'arm.jpg') as arm, Image.open(SURFACES / asset / 'urp_mask.png') as packed:
        ao, roughness, metal = arm.convert('RGB').split()
        r, g, b, a = packed.split()
        for actual, expected in ((r, metal), (g, ao), (a, ImageOps.invert(roughness))):
            assert ImageChops.difference(actual, expected).getbbox() is None
    print('PASS:', asset, '=> circuits', circuits, '; 2K, normal importer, masks, compression and scene references')
print('Static asset verification passed. This is not a Unity rendered preview or FPS measurement.')
