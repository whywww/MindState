"""Summarize matched Inner60 captures and make a before/after opening clip."""
from pathlib import Path
import csv
import json
import cv2
import numpy as np

root = Path(__file__).resolve().parents[1] / 'Evidence/Inner60'
metrics = {}
for version in ('Reference', 'Final'):
    folder = root / version
    phases = np.array([float(p) for p in (folder/'Phases.txt').read_text().splitlines()])
    geometry = np.fromfile(folder/'Geometry.bin', dtype='<f4').reshape(len(phases),5,81,129,3)
    rows = list(csv.DictReader((folder/'InnerContacts.csv').open()))
    target = [r for r in rows if float(r['phase']) < .6]
    metrics[version] = dict(poses=len(phases), targetContactRecords=sum(int(r['contacts']) for r in target))
    if version == 'Reference':
        baseline = geometry
    else:
        delta = geometry - baseline
        metrics[version]['maxAddedOffset'] = float(np.linalg.norm(delta,axis=-1).max())
        assert np.max(np.abs(delta[phases >= .65625])) == 0
        assert np.max(np.abs(delta[:,:,:8])) == 0
        # Measure cache-frame velocity changes; report, rather than hiding outliers.
        cache = geometry[[np.argmin(abs(phases-k/32)) for k in range(33)]]
        old_cache = baseline[[np.argmin(abs(phases-k/32)) for k in range(33)]]
        for name, value in [('reference',old_cache),('final',cache)]:
            acceleration = np.linalg.norm(np.diff(value,n=2,axis=0),axis=-1)
            metrics[version][name+'MaxCacheSecondDifference'] = float(acceleration.max())
    cross = []
    for phase in phases:
        path = min(folder.glob('Contacts_*.csv'),key=lambda f:abs(float(f.stem[9:])-phase))
        records = list(csv.DictReader(path.open()))
        cross.append(sum(r['a'].split('-')[0]!=r['b'].split('-')[0] for r in records))
    metrics[version]['crossContactRecords'] = cross

writer = cv2.VideoWriter(str(root/'Comparison.mp4'),cv2.VideoWriter_fourcc(*'mp4v'),30,(1080,580))
for percent in range(66):
    images = []
    for version in ('Reference','Final'):
        paths = list((root/version).glob('View_*.png'))
        path = min(paths,key=lambda f:abs(float(f.stem[5:])-percent/100))
        img = cv2.imread(str(path))[210:750,210:750]
        canvas = np.zeros((580,540,3),np.uint8)
        canvas[40:] = img
        cv2.putText(canvas,f'{version}  {percent}%',(15,28),cv2.FONT_HERSHEY_SIMPLEX,.7,(255,255,255),1)
        images.append(canvas)
    frame = np.concatenate(images,axis=1)
    for _ in range(3): writer.write(frame)
    if percent in (0,30,50,58,60): cv2.imwrite(str(root/f'Comparison_{percent:02}.png'),frame)
writer.release()
(root/'Metrics.json').write_text(json.dumps(metrics,indent=2))
print(json.dumps({k:{a:b for a,b in v.items() if a!='crossContactRecords'} for k,v in metrics.items()},indent=2))
