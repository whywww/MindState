# 当前保留版本：选定试验版与中层非对称减凹

2026-11-09，用户确认第三轮弧度平滑试验比此前更好，要求保留并让瓣面不那么凹，左右无需对称。本轮按此选择恢复第三轮源码与缓存，再小幅减轻中层 6 片花瓣的横向内凹。

## 调整

以选定第三轮的连续曲面平滑为基础，侧边横向舒展系数由左右均 0.30 改为左 0.46、右 0.38，横向及纵向均平滑过渡。它是离线曲面拟合中的局部舒展系数，不是整片花瓣的旋转角，也不表示最终几何凹度按同比例减少。中层新增补偿仍沿用已选试验版的 0.16 上限；保持原有 33 帧插值与根部保护。

本轮只改中层形面，不改变花瓣整体位置、旋转、尺寸和动画节奏。ShallowerAudit.txt 已核对：原底部、外层尖角数据以及所有非中层的第三组补偿逐字节一致；完全盛开保持选定版。相对选定试验版，中层各片最大顶点变化约 0.03186–0.03315 单位。这次属于小幅弧度变化。

## 保存与对比

- 已选第三轮源码与完整缓存：Evidence/SmoothLap/SelectedThirdSource。
- 已选第三轮程序：Builds/SmoothLapPreview/MindState.exe，连同数据文件保留。
- 当前减凹版源码与完整缓存：Evidence/SmoothLap/ShallowerSource。
- 同视角斜视对比：Evidence/SmoothLap/ShallowerComparison.png；左为选定第三轮，右为本轮。
- 当前完整阶段对比截图：Evidence/SmoothLap/Shallower。
- 当前 Windows 程序：Builds/Windows/MindState.exe，需与同目录数据文件、DLL 一起运行。

之前 SmoothLapTrials.zh-CN.md 关于“不采用试验”的内容是本次用户选择之前的历史状态；当前已按用户选择保留第三轮并采用本轮微调。已知的小角及部分搭接露边仍不能宣称全部清除，本次不把弧度更浅等同于整花零穿插。

## 验证

关键阶段保护区域、根部、基础曲面与整瓣变换检查通过，见 Shallower/Checks.txt。新增曲面只在相应开合阶段启用，盛开新增位移归零。Windows 构建已完成；最终运行检查见本报告后附结果。


最终运行结果：ShallowerPlayer/Checks.txt 的 Windows 网格与播放回归 30 项通过；ShallowerDense/Checks.txt 的 249 个缓存与中点阶段未检出单瓣自相交。Build.log 与 ValidationBuild.log 均构建成功。该结果不是相邻瓣全部无穿插的声明；当前以用户选定试验版的造型为基准保留，并完成本轮小幅非对称减凹。
