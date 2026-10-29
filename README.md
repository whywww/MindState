# MindState · 柔褶绽放

单片 Fan-shaped Petal：窄根、宽阔波浪外缘、7 条几何宽褶皱和默认 32 条放射细纹。完整花瓣从卷起状态连续舒展，使用珍珠白 / 淡紫非金属丝绸材质。

## 打开工程

项目根目录就是 **E:\Unity\MindState**，使用 **Unity 2022.3.62f3c1 + Built-in Render Pipeline**。

1. Unity Hub 添加上述目录。
2. 打开 Assets/HeroPetal/Scenes/HeroPetalGallery.unity，按 **Play**。
3. 场景进入约 12 秒的完整薄片展开动画；编辑状态不生成运行时网格。
4. 也可双击 Builds/Windows/MindState.exe。请保留旁边的 Data、MonoBleedingEdge 与 DLL 文件。

不需要外部模型、OpenBrush、付费插件或 URP/HDRP。未接入真实 BrainLink 通信与 3D 屏幕 SDK。

## 控制面板与操作

| 操作 | 作用 |
|---|---|
| 模拟脑电 | 每秒生成一次注意力、放松度目标值 |
| 外部脑电 | 停止模拟，等待并保持 SetBrainMetrics 的外部输入 |
| 三参数 | 独立控制扇面角度、卷曲量与褶皱柔软度 |
| 卷起 / 半开 / 盛放 | 平滑切换到对应的三参数造型 |
| 重新展开 / R | 复位展开进度；完整网格始终存在 |
| 空格 / 冻结造型 | 冻结或继续造型与展开进度；材质轻微呼吸仍继续 |
| 灰模 | 隐藏细纹、颜色和丝绸装饰，查看真实曲面 |
| Bloom | 默认关闭，可切换比较 |
| 正面 / 侧面 / 背面 | 固定观察角度 |
| 左键拖动 / 滚轮 | 环绕 / 缩放 |
| H | 隐藏或显示面板 |
| 重置 | 恢复默认镜头、模拟脑电并重新展开 |

默认参数为展开角 **125°**、卷曲量 **0.2**、褶皱柔软度 **0.7**。模拟脑电会持续改变目标值，三参数模式保持手动输入。面板显示目标与平滑后的实际参数。

## 三参数接口

必须在 **Unity 主线程**调用场景中 MindState.HeroPetal：

    petal.SetShapeParameters(
        openingAngleDegrees: 125f,
        curlAmount01: 0.2f,
        pleatSoftness01: 0.7f);

| 参数 | 范围 | 含义 |
|---|---|---|
| OpeningAngle | 90–150 度 | 未卷曲时的基础扇面展开角 |
| CurlAmount | 0–1 | 径向卷曲、横向包卷、左右局部翻折的组合强度；0 仍有自然曲率 |
| PleatSoftness | 0–1 | 宽褶皱深浅与圆润程度；不改变 7 条宽褶皱数量 |

调用后自动切到 DirectShape，模拟脑电不会覆盖输入。每个参数独立保存目标与当前值，默认时间常数 2 秒的指数平滑，不叠加第二层几何滤波。超范围值被限制，NaN/Infinity 被忽略。三个只读属性返回实际平滑值。

finePleatCount 可在 Inspector 中设为 20–40，默认 32；改变细纹数量，不改变宽褶皱网格。

## 脑电接口

    // 接受归一化的 0–1 数值，并自动选择 ExternalBrain。
    petal.SetBrainMetrics(attention01, relaxation01);

默认艺术映射：

- OpeningAngle = 90 + 60 × Relaxation
- CurlAmount = 0.85 - 0.75 × Relaxation
- PleatSoftness = 0.9 - 0.65 × Attention

放松时更舒展，专注时宽褶皱更清楚。这是艺术映射，不是脑状态的物理定义。设备数值若为 0–100，由适配层除以 100；接触质量及设备无效值由适配层判断。后台线程数据请先转交主线程。

外部输入保持最后目标，不自动切回模拟模式。三参数模式下参数独立；脑电模式下两个指标有意映射成三个相关联的参数。

## 展开动画与旧接口兼容

- RestartOpening()：进度置 0 并解除冻结，约 12 秒展开到当前造型目标。
- OpeningProgress：0–1 的动画进度，独立于三个造型目标。
- RestartGrowth()：兼容旧调用，转接 RestartOpening()。
- Growth：兼容旧读取，返回展开进度，**不再表示逐段生成长度**。
- automatic=true/false：分别选择模拟脑电 / 外部脑电。
- SetShapeImmediate(angle,curl,softness,progress)：诊断和确定性截图用的即时接口。
- SetImmediate(attention,relaxation,growth)：旧诊断接口；第三参数现在也是展开进度。

实时设备应使用目标接口，不要每帧调用 Immediate 方法。动画开始时仍保留完整薄片，通过真实弯折与卷曲展开，不使用裁切或透明渐显。动画完成后继续响应实时输入。

## 曲面与材质

主体是 **径向 80 段 × 角向 128 段** 的封闭薄壳，21,730 顶点、41,792 三角形。前后表面有独立法线和切线手性，侧缘封闭，根部略厚、外缘渐薄。

几何包含七条不完全规则的放射宽褶皱、非线性径向弯折、横向包卷、左右不同的局部翻折，以及多尺度软波浪外缘。根部保留非零尺寸，外缘不向中央尖端收拢。复用网格和工作数组。

材质使用 Built-in StandardSpecular、宽高光和低强度掠射 sheen。细纹为覆盖完整扇面 UV 的非均匀放射法线，包含轻微弯曲、疏密变化和弱二级纤维；主细纹数量仍可设为 20–40。根部、远景和掠射角减弱细纹，启用 mipmap、Trilinear 和各向异性过滤。已移除中央叶脉、金色分叉脉及羽枝。

轻量视觉优化另用一张打包遮罩提供 Smoothness Variation、微弱淡粉色差和背光权重，新增一次纹理采样，不增加渲染 Pass。主褶皱振幅为此前扇形版本的 80%；拓扑、卷绕、翻折、动画与脑机接口不变。法线与遮罩均为 1024² 线性数据纹理，实际材质额外纹理内存约 5.3 MiB（RGBA32 含 mipmap）。

采用不透明深度写入，以薄片背光近似表现透光；**不是真实折射或物理次表面散射**。默认关闭 Bloom 和装饰粒子，使用中性主光、淡紫补光和冷色轮廓光。

## 重建与验证

关闭占用工程的 Unity 编辑器后，在根目录运行：

    # 构建 Windows x64 开发版
    powershell -ExecutionPolicy Bypass -File .\Tools\Build.ps1

    # 重生成材质、纹理、Prefab、场景，再构建；会覆盖这些生成资产
    powershell -ExecutionPolicy Bypass -File .\Tools\Build.ps1 -RecreateArt

    # 三参数 27 组 × 3 个展开阶段检查、截图、短时回归
    powershell -ExecutionPolicy Bypass -File .\Tools\Verify.ps1 -Seconds 10

    # A/B 截图与 Profiler 三轮对比：冻结/动态两类，各三组 A/B、每段 60 秒
    powershell -ExecutionPolicy Bypass -File .\Tools\CompareSilk.ps1

    # 只生成 A/B 与展开阶段截图
    powershell -ExecutionPolicy Bypass -File .\Tools\CompareSilk.ps1 -ScreensOnly

编辑器菜单：MindState > Rebuild Gallery Assets / Build Windows Player。

当前轻量优化结果写入 **Evidence/SilkMicroDetail**。Regression 子目录为原有网格、接口与展开回归；A_ / B_ 图片分别为优化前后，拍摄时相机、灯光、曝光、造型目标一致，关闭 Bloom 并固定材质呼吸。旧版 Evidence/FanPetal 和 Evidence/Final 保留供对照。

ProfilerComparison.json 保存每轮均值和 P95，CSV 为逐帧原始计数器数据；四个代表性 .raw 文件可在 Unity Profiler 的 Load 功能中打开。采样关闭 Deep Profile、VSync 和限帧，冻结及动态各三轮，轮换 A/B 顺序，使用同一 Development Build。每段先预热 10 秒再记录 60 秒。CPU 提交耗时不等于 GPU 时间；没有返回有效值的 GPU/渲染线程帧指标用 -1 表示，并保存编辑器 GPU Profiler 的复查结果。

A 基线资源仅由开发诊断工具显式加载，不会额外绘制一个花瓣；正式场景继续使用原有 Pearl 材质资产。生产版本的微纹全部由 Shader 实现，不添加逐纹几何或额外 CPU 网格更新。开发版的诊断振幅切换保留 A=100%、B=80%，Release 版本固定使用 80%。

验证器在隐藏窗口中每帧显式 Camera.Render 到 1920×1080、4× MSAA RenderTexture，实际执行 GPU 渲染。相机截图不含操作面板。Validation.json 保存性能、内存、材质及检查结果；GPU 计时 -1 表示未返回有效数据。GC 指标仅覆盖曲面和材质参数更新，不包含 IMGUI、截图与日志。

单相机结果不代表 3D 屏幕多视角性能。旧版截图与报告保留供对照，不作为新扇形花瓣的验证结果。
