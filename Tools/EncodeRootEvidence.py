from pathlib import Path
import cv2
r=Path(__file__).resolve().parents[1]/"Evidence/RootConnection"
for kind in ["Baseline","Translation"]:
 for folder in sorted((r/kind).iterdir()):
  if not folder.is_dir():continue
  files=sorted(folder.glob("*.jpg"))
  if not files:continue
  rate=10 if folder.name.startswith("State") else 5
  out=folder.with_suffix(".mp4")
  writer=cv2.VideoWriter(str(out),cv2.VideoWriter_fourcc(*"mp4v"),rate,(960,540))
  for f in files:writer.write(cv2.imread(str(f)))
  writer.release()
  cap=cv2.VideoCapture(str(out));assert int(cap.get(cv2.CAP_PROP_FRAME_COUNT))==len(files);cap.release()
rows=[]
for state in range(4):
 pair=[]
 for kind in ["Baseline","Translation"]:
  img=cv2.imread(str(r/kind/f"State{state}_-35.jpg"));img=cv2.resize(img,(640,360));cv2.putText(img,kind,(15,25),0,.7,(255,255,255),1);pair.append(img)
 rows.append(cv2.hconcat(pair))
cv2.imwrite(str(r/"Comparison.jpg"),cv2.vconcat(rows))
print("Encoded and verified 32 videos")
