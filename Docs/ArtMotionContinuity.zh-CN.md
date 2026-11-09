# 四态开合连续性修正

手动四态预览采用 18 秒有限时长缓动，冻结保留过渡进度，中途选态从当前权重转向新目标。灯光仍使用原来的 3 秒时间常数；模拟和外部脑电保留连续跟随，避免数据流每次更新都重启计时。

四态使用各层花苞及目标开放姿态之间的连续进度，层间最大轻微领先／滞后系数为 0.12。形变与整瓣变换采用同一层间权重，旋转不再混合一条已经在运动的完整开合路径。补偿采样按该层实际形变进度映射回原缓存时间。原始缓存生成器和 BottomOverlap.bytes 未修改；仅更新播放采样所在源文件的 provenance 哈希。开合重播仍使用原始分层延迟。

验证与证据位于本地 Evidence/ArtMotion：Before、After 为六组切换的固定步长录制，Comparison0–5.mp4 为同视角左右对比（左旧右新，依次澄明、漫游、幽微的进入／退出凝聚）。每组约 20 秒；Tools/VerifyArtMotion.py 可重建视频和 Summary.json。

ValidationBuild.log、Build.log 均构建成功。Checks/Checks.txt 覆盖有限时长、重定向、冻结继续、重置、分层无平台、凝聚与原始路径一致、输入及重播兼容性，以及六组切换逐帧根部连接。Checks/ArtContacts.csv 的 30 个实际渲染姿态未检出单瓣自相交，但仍存在同层和跨层接触，不能据此宣称全角度无穿插。WholeFlower/Validation.json 保存现有整花回归结果。

After/Motion.csv 的所有采样更新托管分配为零。Before 的原始 gc 列误减了 40 字节，实际观测为零；汇总已修正。录制与其他验证进程有重叠，耗时仅供诊断，不构成独立性能对比或稳定帧率保证。

复验：先运行 Tools/Build.ps1 -Validation，再启动 Builds/Validation/MindState.exe，参数 -verifyArtMotion -motionChecksOnly -motionOutput Evidence/ArtMotion/Checks；完整录制使用 -verifyArtMotion -motionChecks -motionOutput Evidence/ArtMotion/After。正式程序位于 Builds/Windows/MindState.exe。
