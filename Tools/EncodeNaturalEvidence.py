"""Encode Unity's fixed-timestep renders and summarize visible warm-color coverage."""
from pathlib import Path
import cv2,numpy as np,json,sys
r=Path(__file__).resolve().parent.parent/'Evidence'/'NaturalLight'
jobs=[('State'+str(i),1200) for i in range(4)]+[('Petal'+str(i),240) for i in range(4)]+[(f'Transition{a}_{b}',240) for a in range(4) for b in range(4) if a!=b]
if '--transitions-only' in sys.argv: jobs=[job for job in jobs if job[0].startswith('Transition')]
for name,count in ([] if '--analyze-only' in sys.argv else jobs):
 frames=sorted((r/name).glob('*.jpg'))
 if len(frames)!=count:raise RuntimeError(f'{name}: expected {count}, got {len(frames)}')
 writer=cv2.VideoWriter(str(r/(name+'.mp4')),cv2.VideoWriter_fourcc(*'mp4v'),20,(1920,1080))
 if not writer.isOpened():raise RuntimeError('Encoder unavailable')
 for p in frames:writer.write(cv2.imread(str(p)))
 writer.release(); cap=cv2.VideoCapture(str(r/(name+'.mp4')));assert int(cap.get(cv2.CAP_PROP_FRAME_COUNT))==count;cap.release()
 print(name,count/20,'seconds',flush=True)
canvas=np.zeros((1080,1920,3),np.uint8)
for i,name in enumerate(('Clarity','Gathering','Wandering','Quiet')):
 a=cv2.resize(cv2.imread(str(r/(name+'.jpg'))),(960,540));cv2.putText(a,name,(24,40),0,.8,(240,240,240),1);canvas[i//2*540:(i//2+1)*540,i%2*960:(i%2+1)*960]=a
cv2.imwrite(str(r/'FourStates.jpg'),canvas)
rows=[]
for state in range(4):
 strip=[]
 for n in range(0,1200,100):
  a=cv2.imread(str(r/f'State{state}'/f'{n:04}.jpg')); small=cv2.resize(a,(480,270));hsv=cv2.cvtColor(small,cv2.COLOR_BGR2HSV)
  mask=hsv[:,:,2]>45
  # OpenCV hue 0..179: peach and rose; neutral white excluded by saturation.
  warm=mask&(hsv[:,:,1]>35)&((hsv[:,:,0]<22)|(hsv[:,:,0]>155))
  rows.append(dict(state=state,seconds=n/20,visiblePixels=int(mask.sum()),warmFraction=float(warm.sum()/max(1,mask.sum()))))
  cv2.putText(small,str(n/20)+'s',(12,24),0,.6,(240,240,240),1);strip.append(small)
 cv2.imwrite(str(r/f'Temporal{state}.jpg'),np.vstack([np.hstack(strip[k:k+4]) for k in range(0,12,4)]))
(r/'VisibleColorCoverage.json').write_text(json.dumps(rows,indent=2))
for s in range(4):
 vals=[x['warmFraction'] for x in rows if x['state']==s];print('state',s,'mean warm',np.mean(vals),'max warm',max(vals),flush=True)
