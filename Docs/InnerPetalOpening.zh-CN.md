# 最内层花瓣闭合留小口

2026-11-09，按用户选图（图 1 为正确，图 2 过度）只调整最内层 5 片花瓣的合拢幅度：合到顶端接近封口、留一个小口，不再互相压叠。只改合拢过程，盛开姿态不动。

## 原因与改动

- 合拢姿态下 `curl` 沿瓣长累计约 69°，`closureBendDegrees` 再叠加 62°，合计约 131°，5 片尖端越过花轴后互相压叠。
- `Assets/Flower/Art/ExhibitionPeony.asset` 第 3 层（`Inner / 浅紫花杯`）`closureBendDegrees` 62 → 42，合计约 111°，尖端停在轴前。
- 侧向闭合角 45/42、`closedTilt 50`、`maximumCurl 0.62`、`curl 0.36`、`openingAngle 130`、`budNarrow 0.42`、`budSpanNarrow 0.4` 与 `petalOffsets` 均未改；中层、外层不动。
- `closureRelease` 在 `OpeningProgress ≥ 0.7` 时归零，所以修改只作用于合拢区间。

## 重烘链补齐

强制重烘（`-rebuildBottom`）原先只重跑 `BuildUpperCorners()`。预设改动会让所有历史基准缓存的配置哈希失效，重烘于是从零重建并只补尖角，静默丢掉外层弧度与中层补偿。现按已验收顺序重跑：

```csharp
BuildUpperCorners();
BuildLayerPatches();
SmoothOuterPatches();
BuildOuterResidualPatches();
```

只影响编辑器离线烘焙。预设未变时仍走原有基准缓存快路径。

## 验证

- 接触（progress 0）：总数 52 → 47，同层 21 → 16，跨层 17 不变，self 0。内层尖端接触 10 → 5 条，接触点半径 0.065–0.099 → 0.218–0.278，即尖端不再越过中心，只在小口周围相接。
- 其他相位：0.25 由 42 降到 37，0.5、0.75、1 与修改前相同。
- 重烘回归 80 项全 PASS；`FullBloom.bin`、`Transforms.bin` 与修改前逐字节一致；`Roots.bin` 仅根部前 8 行有不超过 1.2e-7 的浮点差；`FixedSurface.bin` 因内层形面改变而不同。
- 缓存审计：18 个局部补偿块，最大局部位移仍约 0.08，底部过渡带 1.2486–1.6605 未变；仅内层 5 片的补偿量减少，另中层 5 号的底部采样数有小幅变化。
- 验证版出图 30 项 PASS；四态过渡接触扫描 452 项 PASS；`-verifyArt` 检查 PASS。
- 证据：`Evidence/InnerClosure`（`Before`、`Rebake`、`FinalContacts`、`VerifyFinal`、`topzoom_*`）。`10_AllTop_000.png` 为修改后顶端视图，`Bloom_0000_Top.png` 为同一视角的回归出图。
- 验证出图新增进度 0 的 `10_AllTop_000` / `10_AllView_000`，便于对比闭合口。

## 说明

- 未宣称整花零穿插：仍有同层与跨层接触（0.25 相位 37 条）。中层、外层外观未改动。
- 程序：`Builds/Windows/MindState.exe`（交付版，17:53 构建）与 `Builds/Validation/MindState.exe`，均需与同目录数据文件一起运行。
