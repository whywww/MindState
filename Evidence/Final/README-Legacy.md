# MindState · 珠光心瓣

一片由注意力与放松度连续塑形的三维花瓣。象牙白珠光、香槟金羽枝与深色展厅照明均由本工程生成，不需要下载模型或购买插件。

## 打开与运行

1. Unity Hub → **Add / 从磁盘添加**，选择 **`E:\Unity\MindState`**。这就是项目根目录。
2. 使用 **Unity 2022.3.62f3c1** 打开。工程采用 **Built-in Render Pipeline**，请勿执行 URP/HDRP 材质转换。
3. 打开 `Assets/HeroPetal/Scenes/HeroPetalGallery.unity`，按 **Play**。编辑状态不生成运行时花瓣，进入 Play 后自动生长约 12 秒。
4. 已构建的独立程序为 `Builds/Windows/MindState.exe`，可直接双击运行。请保留旁边的 Data、MonoBleedingEdge 和 DLL 文件。

推荐 1920×1080。界面中明确标识当前为 **模拟脑电**，不是设备实时数据。右侧花瓣可拖动旋转、滚轮缩放；左侧提供自动/手动输入、三组预设、正侧背视角和 Bloom 开关。

| 操作 | 效果 |
|---|---|
| 拖动鼠标左键 | 围绕花瓣旋转 |
| 滚轮 | 缩放 |
| `H` | 隐藏/显示控制面板 |
| `R` | 从根部重新生长 |
| 空格 | 冻结/继续几何响应 |
| 静谧 / 专注 / 凝聚 | 切换三组艺术映射目标值 |
| 重置展示 | 恢复默认视角、自动模拟并重新生长 |

## 艺术与渲染

- 主体为 128×48 网格的双层封闭曲面，包含独立侧缘，13,346 顶点、25,280 三角形；中央脊线、非对称肩部、尖端卷曲、扭转和边缘波浪由参数生成。
- 2048² 羽状纹理和微法线离线生成，带 mipmap 和各向异性过滤。纹理负责细密纤维，真实网格负责宏观曲率，实例网格负责凸起金色羽枝。
- 珠光采用 Built-in Standard 光照基础上的低饱和视角色偏、微法线和薄边背光近似。主体不透明、写入深度；**并非真实玻璃折射或物理次表面散射**。
- 金色羽枝的候选锚点最多 384 个，`detailCount` 默认为 192，以固定随机种子分布并平滑改变可见密度；实际可见数量随注意力和生长进度变化。
- 连续金色主脉与 26 条侧脉另用约 6,480 三角形的曲面绑定网格表现，随主体共用同一参数域；短羽枝每实例 80 三角形。预算中的约 2.5 万三角形指花瓣实体主体，不包含这些装饰。
- Linear / Forward / HDR / 4× MSAA；自生成软箱反射环境，暖主光、冷补光与金色轮廓光。后处理为轻量阈值 Bloom、ACES 拟合色调映射及轻微暗角。Bloom 可关闭，色调映射仍保留。
- 局部通透为艺术化背光近似，避免整片透明带来的排序问题。3D 屏幕多视角输出、后处理兼容性和实机性能需要在下一阶段验证。

## 接入 BrainLink

在 **Unity 主线程**调用场景中 `MindState.HeroPetal` 的接口：

```csharp
petal.automatic = false;
petal.SetBrainMetrics(attention01, relaxation01);
```

输入是已归一化的 `0–1` 指标。若设备给出 `0–100`，由适配层除以 100；设备的无效值和接触质量也应在适配层处理。超范围输入会被限制，NaN/Infinity 被忽略。串口/网络后台线程的数据必须先转交主线程。

接口只设置目标，视觉逐帧平滑：注意力时间常数 2 秒，放松度 4 秒。注意力高时略收窄、边缘更规整、金色细节更密；放松度高时舒展、尖端卷曲减弱。它们是艺术映射，不表示脑状态天然具有这些几何含义。

`RestartGrowth()` 重播 12 秒生长；`frozen=true` 固定当前造型，但材质呼吸仍继续。`SetImmediate()` 用于确定性的截图/极值测试，真实脑电接入应使用 `SetBrainMetrics()`。

## 重建与验证

关闭占用此工程的 Unity 编辑器后，在项目根目录运行：

```powershell
# 重新构建独立程序
powershell -ExecutionPolicy Bypass -File .\Tools\Build.ps1

# 重新生成纹理、材质、Prefab、展示场景，再构建；会覆盖本工程的这些生成资产
powershell -ExecutionPolicy Bypass -File .\Tools\Build.ps1 -RecreateArt

# 完整验证：截图、四组极值、输入与冻结检查，以及 900 秒反复生长
powershell -ExecutionPolicy Bypass -File .\Tools\Verify.ps1 -Seconds 900
```

也可使用编辑器菜单 `MindState > Rebuild Gallery Assets` 或 `Build Windows Player`。重生成器只处理 `Assets/HeroPetal` 下的生成资产与展示设置。

验证输出位于 `Evidence/Final`。隐藏窗口验证采用 **1920×1080、4× MSAA 的 RenderTexture，并每帧显式 Camera.Render**，保证截图与长时测试真正执行 GPU 渲染。截图为实际相机输出，不包括屏幕空间操作面板。普通运行模式显示完整面板。

长时测试以 60fps 为目标上限，每分钟环绕花瓣一周、持续改变观察距离，每 30 秒重新生长。`SoakProgress.txt` 为逐分钟记录；`Evidence/EditorPlayMode.txt` 为指定编辑器进入 Play Mode 后的运行结果。`Evidence/BuildManifest.json` 记录交付源码与美术资产的 SHA-256。

`Validation.json` 保存设备、帧率、花瓣 CPU 更新耗时、GPU 帧耗时、内存、材质数量及测试结果。GPU 时间为 `-1` 表示驱动/运行环境未返回有效测量，不能解释为零耗时。花瓣 GC 指标仅覆盖形变、材质参数更新与实例绘制，不包含诊断写文件、截图或 IMGUI 控制面板。

## 源码布局

- `Assets/HeroPetal/Runtime`：曲面、信号映射、画廊交互、后处理及按命令行启用的验证工具。
- `Assets/HeroPetal/Shaders`：珠光纤维、香槟金、Bloom 与色调映射。
- `Assets/HeroPetal/Editor`：确定性美术资源生成器、Windows 构建入口、编辑器 Play Mode 验证。
- `Assets/HeroPetal/Art`：已生成的纹理、材质、羽枝网格和反射环境，正常打开工程无需重新生成。

本轮集中于单片花瓣。尚未接入真实脑电设备、3D 屏幕 SDK、整朵花生成或模型导出。
