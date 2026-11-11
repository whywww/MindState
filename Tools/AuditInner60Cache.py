"""Check that the inner lift bake preserves the accepted non-target geometry."""
from pathlib import Path
import io
import sys
import struct
import numpy as np

ROOT = Path(__file__).resolve().parents[1]

def read(path):
    f = io.BytesIO(path.read_bytes())
    def integer(): return struct.unpack('<i', f.read(4))[0]
    def string():
        size = shift = 0
        while True:
            b = f.read(1)[0]
            size |= (b & 127) << shift
            if b < 128: break
            shift += 7
        return f.read(size)
    header = string(), string(), f.read(8)
    patches = {}
    for _ in range(integer()):
        key = integer(), integer()
        groups = []
        for _ in range(3):
            count = integer()
            groups.append((f.read(count * 4), f.read(count * 33 * 12)))
        patches[key] = groups
    assert not f.read()
    return header, patches

old = read(ROOT / 'Evidence/Inner60/Baseline/Assets/Flower/Resources/BottomOverlap.bytes')
new = read(Path(sys.argv[1]) if len(sys.argv) > 1 else ROOT / 'Assets/Flower/Resources/BottomOverlap.bytes')
assert old[0] == new[0] and old[1].keys() == new[1].keys()
lines = ['PASS Config, band and patch set unchanged.']
for key, before in old[1].items():
    after = new[1][key]
    assert before[:2] == after[:2], key
    if key[0] != 2:
        assert before == after, key
        continue
    arrays = []
    for ids, data in (before[2], after[2]):
        ids = np.frombuffer(ids, dtype='<i4')
        v = np.zeros((33, 129*81, 3), dtype=np.float32)
        v[:, ids] = np.frombuffer(data, dtype='<f4').reshape(33, len(ids), 3)
        arrays.append(v)
    delta = arrays[1] - arrays[0]
    assert np.count_nonzero(delta[:, :8*129]) == 0, key
    assert np.count_nonzero(delta[21:]) == 0, key
    maximum = np.linalg.norm(delta, axis=-1).max()
    lines.append(f'PASS Inner {key[1]} max added offset {maximum:.8f}; roots, phases >= 0.65625 unchanged.')
lines.append('PASS Original bottom/upper patches and all outer/middle caches byte-identical.')
(ROOT / 'Evidence/Inner60/CacheAudit.txt').write_text('\n'.join(lines), encoding='utf-8')
print('\n'.join(lines))
