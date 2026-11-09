"""Encode matched art-transition captures and summarize measured update costs."""
from pathlib import Path
import csv
import json
import cv2
import numpy as np
root=Path(__file__).resolve().parent.parent/'Evidence'/'ArtMotion'
summary={'measurementNotes':'Before capture subtracted 40 bytes for a Stopwatch allocation that this runtime did not report; GC is clamped to zero. Captures overlapped other validation processes: timing values are diagnostic, not an isolated performance comparison.'}
for version in ('Before','After'):
    with (root/version/'Motion.csv').open() as file:
        rows=list(csv.DictReader(file))
    frames=[r for r in rows if r['layer']=='0']
    ms=np.array([float(r['updateMs']) for r in frames])
    summary[version]={'samples':len(frames),'updateMedianMs':float(np.median(ms)), 'updateP95Ms':float(np.percentile(ms,95)), 'updateMaxMs':float(ms.max()), 'maxGcBytes':max(0,max(int(r['gc']) for r in frames))}
    summary[version]['minimumMiddleHalfSecondTipTravel']={}
    for clip in range(6):
        for layer in range(3):
            travel=[float(r['tipStep']) for r in rows if int(r['clip'])==clip and int(r['layer'])==layer]
            windows=np.convolve(travel,np.ones(10),'valid')[80:290]
            summary[version]['minimumMiddleHalfSecondTipTravel'][f'{clip}/{layer}']=float(windows.min())
for clip in range(6):
    before=sorted((root/'Before'/f'Clip{clip}').glob('*.jpg'))
    after=sorted((root/'After'/f'Clip{clip}').glob('*.jpg'))
    assert len(before)==len(after)==201
    writer=cv2.VideoWriter(str(root/f'Comparison{clip}.mp4'),cv2.VideoWriter_fourcc(*'mp4v'),10,(1280,360))
    assert writer.isOpened()
    for a,b in zip(before,after):
        images=[cv2.imread(str(a)),cv2.imread(str(b))]
        for im,label in zip(images,('Before','After')):
            cv2.putText(im,label,(15,25),cv2.FONT_HERSHEY_SIMPLEX,.6,(255,255,255),1,cv2.LINE_AA)
        writer.write(cv2.hconcat(images))
    writer.release()
    cap=cv2.VideoCapture(str(root/f'Comparison{clip}.mp4'));assert int(cap.get(cv2.CAP_PROP_FRAME_COUNT))==201;cap.release()
(root/'Summary.json').write_text(json.dumps(summary,indent=2),encoding='utf-8')
print(json.dumps({k:{a:b for a,b in v.items() if a!='minimumMiddleHalfSecondTipTravel'} for k,v in summary.items() if isinstance(v,dict)},indent=2))
