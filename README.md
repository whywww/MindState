# MindState · main 正式版本

当前主版本为四态脑电艺术呈现，凝聚完整复用原开合轨迹收拢为花苞，并在原全局进度0–0.04内作5秒周期轻微呼吸。默认进入澄明（慢旋转＋规则微颤），漫游错相摇曳，幽微保持直立并呈现柔和慢波。

- Unity 2022.3.62f3c1，Built-in Render Pipeline。
- 正式场景：[FlowerGallery](Assets/Flower/Scenes/FlowerGallery.unity)。
- 正式配置：[ExhibitionPeony](Assets/Flower/Art/ExhibitionPeony.asset)。
- 唯一正式程序：[MindState.exe](Builds/Windows/MindState.exe)。保留同目录Data、MonoBleedingEdge和DLL。
- [操作及接口](Assets/Flower/README.zh-CN.md) · [最新实机视频与验收](Evidence/FlowerArt/报告.md)。

面板选择四态、模拟脑电或外部输入；空格冻结/继续，H隐藏面板，拖动环绕，滚轮缩放，R进入原开合重播，重置恢复澄明。

## 构建与复测

```powershell
powershell -ExecutionPolicy Bypass -File Tools/Build.ps1
powershell -ExecutionPolicy Bypass -File Tools/Build.ps1 -Validation
powershell -ExecutionPolicy Bypass -File Tools/VerifyFlower.ps1 -Seconds 5
powershell -ExecutionPolicy Bypass -File Tools/Verify.ps1 -Seconds 5
```

默认生成正式发布程序。验证构建临时写入Builds/Validation，测试记录写入Evidence/FlowerArt下；复测完成后可清理验证构建。四态完整复测：运行验证程序加 -verifyArt，再用 Tools/EncodeFlowerArt.py 编码重新生成的逐帧图片。

## 当前验收

40项意象检查、50项整花与97项单片回归通过。十分钟混合循环83123帧，花组件更新P95为11.189ms，更新路径GC为零，网格和材质数量稳定。该数值不是整帧/GPU时间，不代表已证明1080p稳定60帧。凝聚沿原缓存曲面更新，旧版“四态零网格上传”结论不再适用。

原轨迹局部搭接及其他三态中心缝隙仍有未通过项，确认为main不改变这些限制。真实设备通信与3D屏幕SDK未接入，适配层须在主线程传入归一化指标和有效性。

原单片场景Assets/HeroPetal/Scenes/HeroPetalGallery.unity及其SetShapeParameters、SetBrainMetrics、RestartOpening接口继续保留。

工作区仅保留main正式程序和当前验收记录；临时验证程序、旧验收目录、已编码的逐帧图片及旧日志已清理。版本与清理记录见Evidence/FlowerArt/MainVersion.json、Cleanup.json。
