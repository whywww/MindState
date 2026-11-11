"""Compare fixed-view middle-petal animation and spatial/temporal bend measurements."""
from pathlib import Path
import argparse,json
import numpy as np
import cv2
ROOT=Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser();parser.add_argument('candidate');args=parser.parse_args()
r=ROOT/'Evidence/MiddleFlow';folders=[r/'Before',r/args.candidate]
phases=np.loadtxt(folders[0]/'Phases.txt');assert np.allclose(phases,np.loadtxt(folders[1]/'Phases.txt'))
arrays=[np.memmap(f/'Geometry.bin',dtype='<f4',mode='r',shape=(len(phases),6,81,129,3))for f in folders]
measure=[]
for name,v in zip(['Before',args.candidate],arrays):
    a=np.asarray(v[(phases>=.3)&(phases<=.44)],dtype=np.float64)
    along=np.linalg.norm(np.diff(a[:,:,16:81],n=2,axis=2),axis=-1)
    across=np.linalg.norm(np.diff(a[:,:,16:81],n=2,axis=3),axis=-1)
    temporal=np.linalg.norm(np.diff(a,n=2,axis=0),axis=-1)
    focus=np.asarray(v[(phases>=.34)&(phases<=.4),:,40:81,:65,:],dtype=np.float64)
    focus_bend=np.linalg.norm(np.diff(focus,n=2,axis=2),axis=-1)
    focus_time=np.linalg.norm(np.diff(focus,n=2,axis=0),axis=-1)
    measure.append(dict(focusAlongRms=float(np.sqrt(np.mean(focus_bend**2))),focusTemporalRms=float(np.sqrt(np.mean(focus_time**2))),version=name,alongRms=float(np.sqrt(np.mean(along**2))),alongP99=float(np.percentile(along,99)),acrossRms=float(np.sqrt(np.mean(across**2))),temporalRms=float(np.sqrt(np.mean(temporal**2))),temporalP99=float(np.percentile(temporal,99))))
(r/'Metrics.json').write_text(json.dumps(measure,indent=2))
video=cv2.VideoWriter(str(r/'Comparison.mp4'),cv2.VideoWriter_fourcc(*'mp4v'),20,(1440,760))
for k in list(range(51))+list(range(50,-1,-1)):
    canvas=np.zeros((760,1440,3),np.uint8)
    for col,f in enumerate(folders):
        im=cv2.imread(str(f/f'Frame_{k:03}.png'));assert im is not None
        im=cv2.resize(im,(720,720));canvas[40:,col*720:(col+1)*720]=im
        cv2.putText(canvas,('Before' if col==0 else 'After')+f'  {25+k*.5:.1f}%',(col*720+20,28),0,.7,(240,240,240),1)
    video.write(canvas)
video.release()
for k in [18,22,26,30]:
    out=np.hstack([cv2.imread(str(f/f'Frame_{k:03}.png'))for f in folders]);cv2.imwrite(str(r/f'Comparison_{25+k*.5:g}.png'),out)
print(json.dumps(measure,indent=2))
