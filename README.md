# PearlColor · 自然混色彩光主版本

当前版本采用珍珠丝绸底色与三盏真实彩色聚光灯，基于平滑噪声缓慢改变照射位置、方向、范围及混合颜色。脑电控制冷暖光的相对强度。自然彩光版本已集成main。

启动 Builds/Windows/MindState.exe。

BrainLink 接入与新电脑安装见 [BrainLink 使用说明](Assets/BCI/BrainLink/README.md)：读取专注度、放松度和佩戴信号质量；在 Flower Gallery Camera 的 Inspector 勾选 Brain Link Flower Input 后启用，F9 切换默认隐藏的实时诊断面板，Console 搜索 `[BrainLink]` 查看日志。

当前记录：Evidence/NaturalLight/报告.md。历史色带截图保留于 Evidence/NaturalLight/PreviousBands.jpg。

构建：Tools/Build.ps1 -Validation；场景配置：MindState.Editor.ConfigureColorVariant.Build。采集：验证程序 -verifyColor；编码：python Tools/EncodeNaturalEvidence.py。

花瓣根部连接修正已并入 main：14 片花瓣统一平移接入底部圆盘，保留开合与四态动作。验收见 Evidence/RootConnection/报告.md。
