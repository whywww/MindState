from pathlib import Path
import cv2,json,numpy as np
root=Path(__file__).resolve().parent.parent;r=root/'Evidence'/'NaturalLight'
summary={}
for name,p in [('longStates',r/'LongStateChecks.txt'),('naturalLights',r/'Checks.txt'),('art',root/'Evidence/FlowerHeart/ArtRegression/Checks.txt')]:
 lines=p.read_text().splitlines();summary[name]={'pass':sum(x.startswith('PASS') for x in lines),'fail':sum(x.startswith('FAIL') for x in lines)}
for name,p in [('flower',root/'Evidence/FlowerArt/LegacyWholeBud/Validation.json'),('petal',root/'Evidence/FlowerArt/LegacySingleBud/Validation.json')]:
 lines=json.loads(p.read_text(encoding='utf-8-sig'))['checks'];summary[name]={'pass':sum(x.startswith('PASS') for x in lines),'fail':sum(x.startswith('FAIL') for x in lines)}
assert all(x['fail']==0 for x in summary.values())
videos=list(r.glob('*.mp4'));assert len(videos)==20
for file in videos:
 c=cv2.VideoCapture(str(file));assert int(c.get(7))==(1200 if file.stem.startswith('State') else 240);assert int(c.get(3))==1920 and int(c.get(4))==1080;c.release()
before=cv2.resize(cv2.imread(str(r/'PreviousBands.jpg')),(960,540));after=cv2.resize(cv2.imread(str(r/'Clarity.jpg')),(960,540))
cv2.putText(before,'Previous: surface color bands',(18,35),0,.7,(240,240,240),1);cv2.putText(after,'Now: soft overlapping colored lights',(18,35),0,.7,(240,240,240),1)
cv2.imwrite(str(r/'BeforeAfter.jpg'),np.hstack((before,after)))
(r/'ValidationSummary.json').write_text(json.dumps(summary,indent=2))
text=f'''# 自然混色与缓慢随机彩光 · 独立副本

2026-11-03，仅修改 E:/Unity/MindState/Variants/PearlColor；原工程与正式程序未修改，未提交推送。

## 本轮改变

取消目标材质中的UV颜色输送带、彩虹循环和高自发光。花瓣采用低饱和珍珠丝绸底色，以三盏真实Unity Spot Light叠加青蓝、淡紫和粉桃彩光。颜色随法线、曲面朝向和细纹自然混合，不再为每瓣指定一条上下移动的固定色带。旧LUT及相位接口留作兼容，当前外观分支不再使用该纹理给花瓣换色。

三灯分别使用不同种子的Perlin平滑噪声，独立改变水平位置、垂直高度、照射目标、锥角、颜色细混与强度。没有每帧抽随机数、固定单向位移或短周期全色轮播。seed=271，可重复复查；随机感来自连续噪声轨迹，不是每次启动重新抽签。

彩光基本强度2.8、锥角48度且缓慢变化±7度、漂移速度0.012。偏移包含左右、上下和纵深，色彩混合变化更慢。脑电平滑调节冷暖灯相对强度，响应时间常数由0.7秒改为3秒；形态权重仍为4秒。稳定输入时光斑继续缓慢漂移，冻结同时停止灯光的变换与颜色。

背景纯黑，曝光0.92，Bloom关闭。主光强度0.40，中性冷补光0.12，冷轮廓光0.22。花瓣几何、开合轨迹、四态动作、根部裁切与花丝不变。

## 预览

- [前后对比](BeforeAfter.jpg) · [四态](FourStates.jpg) · [混合输入](Mixed.jpg)
- [澄明60秒](State0.mp4) / [凝聚60秒](State1.mp4) / [漫游60秒](State2.mp4) / [幽微60秒](State3.mp4)
- [单瓣澄明](Petal0.mp4) / [单瓣凝聚](Petal1.mp4) / [单瓣漫游](Petal2.mp4) / [单瓣幽微](Petal3.mp4)
- 十二种TransitionA_B.mp4，每段12秒；Temporal0..3.jpg为五秒间隔抽样。

以上为Unity真实1920×1080、20fps固定时间步渲染编码，四态每段1200帧；近景及过渡每段240帧。视频时长表示模拟时间，不等同墙钟录制耗时。稳定纯态片段在放缓脑电响应前采集，纯态结果不受该时间常数影响；过渡、混合和最终回归使用3秒响应构建。

## 验证

{json.dumps(summary,ensure_ascii=False,indent=2)}

新增检查确认三盏真实彩灯存在、冻结时实际Light变换和颜色不变、运行时实际灯光沿连续噪声轨迹移动。四态外观开关前后，顶点/逐瓣变换/根部裁切哈希完全一致。旧相位兼容检查仍保留，不把旧相位推进当成新彩灯移动的证据。

整花50项、单片97项与四态40项回归通过。黑背景检查通过，纹理/网格/材质稳定，更新路径GC为0。

## 性能与边界

{(r/'Performance.txt').read_text()}

真实彩灯增加了逐像素照明渲染成本；彩灯不启用阴影，保留现有主光阴影。GPU计时不可用，不宣称已证明稳定60fps。原模型已有空隙与搭接保留。效果属于现有网格上的实时照明，不是对参考图造型与全部细节的逐像素复刻。

## 使用

启动 Builds/Validation/MindState.exe。调节 Assets/Flower/Art/TargetAppearance.asset 中 coloredLightIntensity、coloredLightAngle、lightDriftSpeed和seed；RefreshAppearance手动刷新。配色仍取自原有五色资产。原来stateColorWeights/flowPeriods等参数属于上一轮表面色带，当前彩光不使用它们。

构建 Tools/Build.ps1 -Validation；采集验证程序 -verifyColor；仅重录过渡与额外灯光检查用 -verifyColor -flowTransitionOnly。编码 python Tools/EncodeNaturalEvidence.py；报告 python Tools/SummarizeNatural.py。
'''
(r/'报告.md').write_text(text,encoding='utf-8');print(summary);print('Verified 20 natural-light videos')
