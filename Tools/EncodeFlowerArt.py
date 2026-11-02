"""Encode Unity-rendered evidence frames; requires the host OpenCV installation."""
from pathlib import Path
import cv2
root=Path(__file__).resolve().parent.parent/'Evidence'/'FlowerArt'
for name in ('Clarity','Gathering','Wandering','Quiet','EnterGathering','ExitGathering1','ExitGathering2','ExitGathering3'):
    frames=sorted((root/name).glob('*.jpg'))
    if len(frames)!=400: raise RuntimeError(f'{name}: expected 400 frames, got {len(frames)}')
    image=cv2.imread(str(frames[0])); h,w=image.shape[:2]
    writer=cv2.VideoWriter(str(root/(name+'.mp4')),cv2.VideoWriter_fourcc(*'mp4v'),20,(w,h))
    if not writer.isOpened(): raise RuntimeError('Video encoder unavailable')
    for frame in frames: writer.write(cv2.imread(str(frame)))
    writer.release()
    cap=cv2.VideoCapture(str(root/(name+'.mp4')))
    assert int(cap.get(cv2.CAP_PROP_FRAME_COUNT))==400
    cap.release()
    print(name, '20 seconds, 960x540, 20 fps')
