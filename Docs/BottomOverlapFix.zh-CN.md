# PolarEdgeFinal 底部局部搭接修正

2026-11-09。当前交付保留用户选定的 PolarEdgeFinal 基础造型，仅增加底部局部补偿。没有移动、旋转、缩放整片花瓣，没有修改底盘或隐藏花瓣。HeroPetal.cs 与 ExhibitionPeony.asset 和本轮修改前存档逐字节一致。

## 实现与逐处记录

从未修正曲面导出 33,384 条三角形相交记录，按花瓣对和开合阶段整理为 135 个 UV 接触范围。完整原始记录在 Evidence/BottomOverlap/FullTrace/AllContacts.csv；整理后的每处所有者、邻瓣、阶段及范围在 Evidence/BottomOverlap/CorrectionRegions.csv。层编号 0/1/2，花瓣编号从 0 开始；t 为根到尖的长度坐标，s 为左右坐标。范围用于定位候选区域，并不表示整个范围都产生位移。

同层按左内右外关系确定覆盖者，包括首尾接缝；跨层保留原层次。对每个候选顶点，沿局部曲面外法线查询邻瓣三角形，将陷入部分推到邻面外约 0.012 单位处，单点最大局部位移约 0.08。这里的外向由局部曲面决定，不沿全局竖轴统一抬升。高度带在基准局部空间约 1.24857–1.660473 间渐退，根部前 10% 固定，半径过小的连接内部不参与补偿。空间平滑后，在开合 0.25–0.65 间渐退，完全盛开归零。

18 片有接触的花瓣分别保存稀疏顶点补偿和独立工作网格，六份基础姿态缓存继续共享；每片补偿有 33 个连续插值姿态。只更新受影响区域及相邻顶点的法线、切线，侧壁同步，包围盒按当前两帧最大位移保守扩张。重复进度和冻结不会重新上传网格。运行时读取预烘焙 Resources 缓存，不运行昂贵的相交拟合；构建前核对相关源码与配置指纹，防止带入过期缓存。没有增加公共接口。

## 视觉结果与残余

- Comparison.png：Windows 程序中相同相机、画幅和光照的原基准/新修复低角度对比。
- Orbit.png、FinalPlayerImages/Bottom_Orbit_0.png 至 7.png：八向底部环绕。
- Residuals.png：仍可见区域标注。A 为右下搭接细露边；B 为左下局部露边；C 为根部连接小缝。没有把这些残余解释成已消除。
- Final/ 保存完整开合和凝聚进出、呼吸阶段截图；FinalPlayerImages/ 保存实际 Windows 程序关键姿态截图。

明显向外突出的底部三角瓣面已大幅收回正确搭接位置，整体花苞和上部形状保留。但本次不是“整花零穿插”的验收结果：仍有细边、根部小缝以及其他区域的花瓣间相交记录。保留这些位置，未扩大为整瓣改形。根部内部接触在 ContactSummary.csv 的 root 列独立统计；该列是诊断范围归类，不能单凭统计认定肉眼可见或不可见。

## 验证

| 检查 | 结果及证据 |
|---|---|
| 全部缓存姿态及相邻中点 | 249 个全局进度样本，未检出单瓣自相交；DenseValidation/Checks.txt 与 Contacts.csv。相邻瓣相交仍记录，不等于全部消除 |
| 凝聚进出与呼吸 | 57 个实际姿态样本未检出单瓣自相交；Final/ 下检查和阶段截图 |
| 实际独立工作网格固定区 | 关键进度下根部与未修正顶点一致；FinalPlayerImages/Checks.txt |
| 基础缓存、根部、盛开及变换 | FullBloom、Roots、FixedSurface、Transforms 与基准逐字节一致；该基础缓存检查与上项实际工作网格检查共同使用 |
| Windows 网格和播放回归 | 30 项通过，覆盖法线、切线、拓扑、边界、缓存插值、根部、播放和冻结；FinalPlayerValidation/Checks.txt |
| 实际程序关键截图检查 | FinalPlayerImages/Complete.txt，所有列出检查通过 |
| Windows 构建 | Builds/Windows/MindState.exe 与 Builds/Validation/MindState.exe 成功；Build.log、ValidationBuild.log |

## 性能与内存

Windows 验证程序在进度 0–0.25 往复更新 120 次的 CPU 测量，证据为 OptimizedPlayerPerformance/Performance.txt：

| 指标 | 基准 | 局部修正 |
|---|---:|---:|
| 每次更新 CPU 平均 | 5.64 ms | 17.25 ms |
| 每次更新 CPU 最大 | 10.71 ms | 21.39 ms |
| 最大单次 GC 分配 | 0 | 0 |

冻结/重复进度网格上传 0 次。稀疏位置缓存 18,152,640 字节（17.31 MiB），独立工作数据 39,365,202 字节（37.54 MiB），统计几何数据总量 234,834,642 字节（223.96 MiB）。这些是逻辑数据统计，不是进程总内存。CPU 测量不包括绘制，不能据此声称达到固定帧率；动态更新成本确有增加。

## 保存与复现

用户选定原版保留于 Evidence/ShingleOverlap/SelectedPolarSource；本轮修改前关键源码及配置在 Evidence/BottomOverlap/Baseline；当前交付源码、配置及烘焙缓存在 Evidence/BottomOverlap/DeliveredSource。此前试验目录不代表当前交付。

在工程根目录运行：

```powershell
./Tools/VerifyShingle.ps1 -Screens -Output Evidence/BottomOverlap/Recheck -Baseline Evidence/ShingleOverlap/ShapeReference
./Tools/VerifyShingle.ps1 -ContactsOnly -Output Evidence/BottomOverlap/RecheckDense
./Tools/Build.ps1 -Validation
./Tools/Build.ps1
```

修改曲面生成、配置或布局后应重新追踪接触范围并重新运行编辑器验证烘焙，不能仅复用旧二进制缓存。编辑器验证成功会保存缓存及指纹。Windows 程序必须连同 Builds/Windows 中的数据目录和 DLL 一起运行。
