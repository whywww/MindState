# 当前版本：PolarEdgeFinal + 底部与上部尖角局部修正

2026-11-09，按用户选择恢复 `Evidence/ShingleOverlap/PolarEdgeFinal/Bloom_0250.png` 对应版本。新截图与用户选图最大 RGB 差为 1/255，详情见 `Evidence/ShingleOverlap/SelectedPolarVerification/SelectedVersion.txt`。

选定原版源码及配置存档于 `Evidence/ShingleOverlap/SelectedPolarSource`。随后按用户批准计划增加底部逐瓣局部补偿，当前程序采用该补偿；实现、残余标注、验证及性能结果见 [底部修正报告](BottomOverlapFix.zh-CN.md)。

该版本由用户按外观选定；此前部分轮廓检查超出 2%，不能描述为全部轮廓检查通过或整花零穿插。其他试验目录不代表当前采用版本。

程序：`Builds/Windows/MindState.exe`，需与同目录数据文件一起使用。


2026-11-09，随后完成外层 7 片花瓣内壁尖角的局部外推，当前 Windows 程序包含该修正。前后近景、逐瓣记录及测试见 [上部尖角修正报告](UpperCornerFix.zh-CN.md)。此前底部补偿数据逐字节保留。


本轮另加入中层小角与外层中段的局部补偿：外层有改善，中层仍有达到限幅后保留的残余，当前程序不是两类问题全部解决的版本。见 [局部修正与残余报告](LayerOverlapFix.zh-CN.md)。修改前完整缓存保留于 Evidence/LayerOverlap/Baseline。


当前用户已选择保留第三轮弧度试验，并在此基础上减轻中层左右不对称内凹。当前状态、选定版备份及程序见 [非对称减凹报告](AsymmetricCupRelaxation.zh-CN.md)。旧报告的未采用状态为历史记录。


在已认可中层减凹版基础上，当前另加入外层 7 片的连续弧度平滑，中层与内层保持该版本。见 [外层弧度报告](OuterPetalFlow.zh-CN.md)。本轮开始前版本保留于 Evidence/OuterFlow/Baseline。


当前另加入 58% 展开附近外层覆盖瓣面的局部抬高，保留既有外层连续弧度及中内层缓存。对比、范围与复现见 [58% 外层修正报告](Outer58OverlapFix.zh-CN.md)。
