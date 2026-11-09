"""Audit the serialized upper-corner addition against the retained bottom-only baseline."""
from pathlib import Path
import io
import struct
import numpy as np

ROOT = Path(__file__).resolve().parents[1]

def read_cache(path):
    stream = io.BytesIO(path.read_bytes())
    def integer():
        return struct.unpack('<i', stream.read(4))[0]
    def string():
        size, shift = 0, 0
        while True:
            byte = stream.read(1)[0]
            size |= (byte & 127) << shift
            if byte < 128:
                break
            shift += 7
        return stream.read(size)
    version, config, band = string(), string(), stream.read(8)
    patches = {}
    for _ in range(integer()):
        layer, index, count = integer(), integer(), integer()
        ids, delta = stream.read(count * 4), stream.read(count * 33 * 12)
        upper_ids, upper_delta = b'', b''
        if version in (b'BottomOverlap/2', b'BottomOverlap/3'):
            count = integer()
            upper_ids = stream.read(count * 4)
            upper_delta = stream.read(count * 33 * 12)
        if version == b'BottomOverlap/3':
            extra_count = integer()
            stream.read(extra_count * (4 + 33 * 12))
        patches[layer, index] = ids, delta, upper_ids, upper_delta
    assert not stream.read(), 'Unconsumed cache bytes'
    return config, band, patches

old = read_cache(ROOT / 'Evidence/UpperCorner/Baseline/Assets/Flower/Resources/BottomOverlap.bytes')
new = read_cache(ROOT / 'Assets/Flower/Resources/BottomOverlap.bytes')
assert old[:2] == new[:2], 'Changed original config hash or height band'
assert old[2].keys() == new[2].keys(), 'Changed original per-petal work mesh set'
for key, patch in old[2].items():
    assert patch[:2] == new[2][key][:2], f'Changed bottom IDs/deltas: {key}'
lines = ['PASS All 18 original bottom sparse ID arrays and all 33 delta poses byte-identical.',
         'PASS Original config hash and bottom height band byte-identical.']
for key, (_, _, ids, data) in sorted(new[2].items()):
    if not ids:
        continue
    vertices = np.frombuffer(ids, dtype='<i4')
    delta = np.frombuffer(data, dtype='<f4').reshape(33, len(vertices), 3)
    assert key[0] == 0 and (vertices // 129 >= 68).all()
    assert np.count_nonzero(delta[-1]) == 0
    maximum = np.linalg.norm(delta, axis=-1).max()
    assert maximum <= .080001
    lines.append(f'PASS P{key[1]} upper vertices={len(vertices)}, maxOffset={maximum:.8f}; root protected, full-bloom zero.')
assert sum(bool(v[2]) for v in new[2].values()) == 7
(ROOT / 'Evidence/UpperCorner/CacheAudit.txt').write_text('\n'.join(lines), encoding='utf-8')
print('\n'.join(lines))
