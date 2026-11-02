# MindState · 柔褶绽放

当前 main 已统一采用验收过的花瓣外观与CPU缓存/上传优化版本：14片（6/5/3）、宽度60%/50%/40%、主褶3/2/1，中外层有上下弯曲和不对称左右包卷。六组共享网格，每组33个姿态。

- Unity 2022.3.62f3c1，Built-in Render Pipeline。
- 正式场景：[FlowerGallery](Assets/Flower/Scenes/FlowerGallery.unity)。
- 正式配置：[ExhibitionPeony](Assets/Flower/Art/ExhibitionPeony.asset)。
- 唯一正式程序：[MindState.exe](Builds/Windows/MindState.exe)。运行时保留同目录Data、MonoBleedingEdge和DLL。
- [最新验收与性能报告](Evidence/FlowerPerformance/报告.md)。

操作：R重播，空格冻结/继续，H隐藏面板；拖动环绕，滚轮缩放，面板可手动调整进度。约15.6秒完全展开。

## 构建与复测

```powershell
powershell -ExecutionPolicy Bypass -File Tools/Build.ps1
```

默认生成非Development、无脚本调试和验证宏的发布程序。菜单 MindState / Build Windows Player 使用相同发布入口。

```powershell
powershell -ExecutionPolicy Bypass -File Tools/Build.ps1 -Validation
powershell -ExecutionPolicy Bypass -File Tools/VerifyFlower.ps1 -Seconds 60
powershell -ExecutionPolicy Bypass -File Tools/Verify.ps1 -Seconds 5
```

验证构建仅临时写入Builds/Validation，复测完成后可删除该目录；正式运行不依赖它。最新原始数据集中保存于Evidence/FlowerPerformance。

## 当前限制

外观已获用户通过，但性能未达到稳定60帧：最终10分钟测试平均53.63FPS、整帧CPU P95 26.40ms；几何搭接和穿插仍有未通过项。并入main不改变这些验收结论。详细条件、原始数据和限制见最新报告。

## 原单片与脑电接口

原单片场景Assets/HeroPetal/Scenes/HeroPetalGallery.unity和EEG接口保留。主线程调用：

```csharp
petal.SetShapeParameters(125f, 0.2f, 0.7f);
petal.SetBrainMetrics(attention01, relaxation01);
petal.RestartOpening();
```

展开角90–150°，卷曲和柔软度0–1；非有限输入忽略，越界值钳制。直接造型选择DirectShape，脑电输入选择ExternalBrain并保持最后样本；模拟模式每秒采样，参数平滑过渡。未接入真实BrainLink通信或3D屏幕SDK。

历史验证工程、重复程序、源码快照与回滚备份已按用户要求清理，仅保留main和最新验收证据。
