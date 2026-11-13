# MindState · 浮光花境

Unity 2022.3.62f3c1 / Windows x64。主场景是 `Assets/Flower/Scenes/FlowerGallery.unity`，当前配置为 18 片花瓣、三层（7/6/5），采用珍珠丝绸材质与自然混色彩光。

## 运行与操作

启动 `Builds/Windows/MindState.exe`，保留同目录的数据与 DLL。

最终演示视频：[ClosingContinuity_10s.mp4](Outputs/ClosingContinuity_10s.mp4)，保留慢速版前 10 秒，1280×720、60 FPS。

- 四态按钮把虚拟脑电滑条在 9 秒内移到对应目标值，花朵实时跟随；1–4 等同按钮，Shift+1–4 直接注入四个角点。
- 拖动环绕、滚轮缩放；R 重播标准开合，空格冻结；面板默认隐藏，H 显示 / 隐藏。
- 开合滑条直接控制 0%～100%；F9 切换 BrainLink 诊断面板，L 显示帧率。
- 拖动专注/放松滑条作为脑电输入并核对四态映射；BrainLink 接管时滑条禁用，关闭组件后滑条重新生效。
- BrainLink 默认关闭，在相机 Inspector 启用组件后接管输入。

## 开发入口

在项目根目录运行以下 PowerShell 命令，运行批处理前关闭占用本项目的 Unity 编辑器：

```powershell
./Tools/Bake.ps1                 # 修改几何或生成源码后重新生成缓存
./Tools/Bake.ps1 -PlaybackOnly   # 仅重建播放缓存，保留已验证的底层补偿
./Tools/Verify.ps1               # 当前项目回归
./Tools/Build.ps1                # 正式 Windows 程序
./Tools/Build.ps1 -Validation    # 含诊断功能的测试程序
./Tools/Performance.ps1 -Reference # 原 CPU 路径性能基线（验证程序）
./Tools/Performance.ps1          # GPU 播放性能与四态截图
```

首次运行和修改几何后的处理见下文「第一次运行与缓存」。`Outputs` 当前仅保留最终演示视频；验证报告、日志和帧序列可按需重新生成。`Tools/CleanGenerated.ps1` 清理临时和历史产物时会保留这段视频；`Builds` 是生成程序与 BrainLink 工具环境，均不纳入 Git。

## 第一次运行与缓存

- 新拷贝的项目或删掉缓存后，第一次运行会自动生成播放缓存：编辑器里按 Play 会卡住并显示进度条，运行 `Tools/Build.ps1` 时终端会多等几分钟；完成后自动继续，不用重新按 Play。
- 生成过一次后不会重复。程序启动时要先加载缓存，所以刚启动会稍慢，进入画面后播放流畅。
- 改过花形、几何或相关生成代码后，需要手动运行 `Tools/Bake.ps1`；否则按 Play 会被拦下并弹窗提示。
- 自动生成失败时看 Console 或弹窗，最稳妥的重试方式是手动运行 `Tools/Bake.ps1`。
- 缓存位于 `Assets/StreamingAssets`，约 1.2 GB，和 `Outputs`、`Builds` 一样不纳入 Git。

- [架构、接口与维护](Docs/Architecture.zh-CN.md)
- [BrainLink 安装、模拟与设备接入](Assets/BCI/BrainLink/README.md)
- [当前限制与修复建议](Docs/CurrentIssues.zh-CN.md)
