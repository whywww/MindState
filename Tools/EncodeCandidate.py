from pathlib import Path
import cv2
r=Path(__file__).resolve().parents[1]/"Evidence/FlowerHeart"
for prefix,count in [("Motion"+str(i),400) for i in range(4)]+[("Transition"+str(a)+"_"+str(b),240) for a in range(4) for b in range(4) if a!=b]:
    files=[r/(prefix+"_"+str(i).zfill(4)+".jpg") for i in range(count)]
    if not all(p.exists() for p in files):raise RuntimeError("Incomplete clip: "+prefix)
    writer=cv2.VideoWriter(str(r/(prefix+".mp4")),cv2.VideoWriter_fourcc(*"mp4v"),20,(960,540))
    for p in files:writer.write(cv2.resize(cv2.imread(str(p)),(960,540),interpolation=cv2.INTER_AREA))
    writer.release()
    cap=cv2.VideoCapture(str(r/(prefix+".mp4")))
    if int(cap.get(cv2.CAP_PROP_FRAME_COUNT))!=count:raise RuntimeError(prefix)
    cap.release()
print("Encoded and verified 16 clips, 4480 frames, 20fps")
