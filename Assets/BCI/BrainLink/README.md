# BrainLink 使用说明 · MindState

本模块将 BrainLink 的专注度、放松度和信号质量接入整花场景。Python 数据程序与 Unity 需要同时运行，游戏不会自动启动 Python。

## 1. 读取哪些数据

| 数据 | 字段 | 本项目用途 |
|---|---|---|
| 专注度 | `attention`，0–100 | 除以 100 后作为花朵的注意力输入 A |
| 放松度 | `meditation`，0–100 | 除以 100 后作为花朵的放松度输入 R |
| 佩戴信号质量 | `signal`，0–200 | 仅为 0 时接受本条数据用于控制 |
| 数据来源 | `mode` | `simulate` 表示模拟，`serial` 表示实体串口；这是本项目添加的字段 |

四态目标权重为：澄明 AR、凝聚 A(1−R)、漫游 (1−A)R、幽微 (1−A)(1−R)。花朵姿态和灯光沿用现有平滑过渡，不是收到数据就瞬间跳转。专注度和放松度均无玩家移动阈值。

当前实际只转发上述三个设备指标。官方 SDK 另提供八频段功率（delta、theta、lowAlpha、highAlpha、lowBeta、highBeta、lowGamma、highGamma）、原始脑电 raw，以及运动、RR 和扩展字段；本项目没有转发、显示或记录这些数据。字段及设备支持以[官方 Python SDK](https://github.com/Macrotellect/BrainLinkParser-Python/tree/066af71079bc9eb87a6b9dcd4517b8817715ef38) 为准，不应将本项目指标接入等同于全部 SDK 功能已接入。

```text
BrainLink → Windows 蓝牙出站 COM → Python + 官方 SDK
         → 本机 TCP 127.0.0.1:13856 → BrainLinkFlowerInput → 花朵姿态与彩光
```

无设备时用模拟器生成设备格式的数据包，仍经过官方 SDK 解析再发送给 Unity。这些是设定的测试数值，不是人体脑电。

## 2. 新电脑首次安装

需要 Windows x64、可用的蓝牙适配器（实体设备模式）、联网环境。只运行游戏不需要 Unity；修改或构建工程使用 Unity 2022.3.62f3c1。

1. 从 [Miniforge 官方发布页](https://github.com/conda-forge/miniforge/releases/latest) 下载 `Miniforge3-Windows-x86_64.exe`，按默认的当前用户方式安装。已有 Conda 可以复用，无须再安装。
2. 将 PowerShell 当前目录切到本项目根目录。以下所有命令均在项目根目录执行；部署包则在第 6 节所示的部署根目录执行。
3. 运行准备脚本：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\Assets\BCI\BrainLink\Python\Setup-BrainLink.ps1"
```

默认寻找 `%USERPROFILE%\miniforge3\Scripts\conda.exe`。Conda 安装在其他位置时指定**实际 conda.exe 的完整路径**，例如：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\Assets\BCI\BrainLink\Python\Setup-BrainLink.ps1" -Conda "D:\Miniforge3\Scripts\conda.exe"
```

脚本在 `Builds/BrainLinkTools/python-env` 创建独立 Python 3.11 环境及 pyserial 3.5，在 `Builds/BrainLinkTools/sdk` 下载固定版本的 `BrainLinkParser.pyd` 并检查 SHA-256。不会修改系统默认 Python，也不需要手动 activate 环境。首次创建可能需要等待 Conda 获取索引和下载依赖。

准备成功必须出现：

```text
BrainLink SDK / Python 3.11 x64 / pyserial ready
```

可重复运行脚本检查环境。不直接复制其他电脑的 python-env；迁移后重新创建。

## 3. 无设备测试

### 启动模拟器

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\Assets\BCI\BrainLink\Python\Start-BrainLink.ps1" -Attention 70 -Meditation 50
```

保持该 PowerShell 窗口打开。默认每秒发送一次 `attention=70`、`meditation=50`、`signal=0`、`mode=simulate`。同一端口只运行一个数据程序；与侏罗纪项目同时测试时也要避免占用相同端口。

### 在 Unity 中启用

1. 打开 `Assets/Flower/Scenes/FlowerGallery.unity`。
2. 在 Hierarchy 中选择 **Flower Gallery Camera**。
3. 在 Inspector 中找到 **Brain Link Flower Input**，勾选组件标题左侧的启用框。Flower 已绑定主场景花朵，Host / Port 保持 `127.0.0.1` / `13856`。
4. 进入 Play；BrainLink 会自动连接。项目保存的默认状态是**未勾选**，仅启动 Python 不会改变设备选择。
5. 按 **F9** 显示 BrainLink 诊断面板，检查来源、专注度、放松度、signal 和数据年龄；再次 F9 隐藏。该面板默认隐藏，独立于 H 控制的原有操作面板。
6. Console 搜索 `[BrainLink]` 查看每秒摘要和状态变化。

组件启用期间，四态预览、模拟脑电、外部输入按钮、开合重播、开合滑条及 R 快捷键锁定，避免覆盖设备数据。视角、冻结及 H/F9 保持可用；重置仅重置视角。关闭组件会释放连接并返回澄明预览。面板中的“模拟脑电”是 Unity 自带模拟功能，与 Python 的 SDK 链路模拟是两种不同入口。

### 修改模拟数据

在模拟器 PowerShell 窗口中输入命令，回车执行：

| 命令 | 预期 |
|---|---|
| `a 100`、`m 100` | 专注与放松均高，渐变至澄明 |
| `a 100`、`m 0` | 专注高、放松低，渐变至凝聚 |
| `a 0`、`m 100` | 专注低、放松高，渐变至漫游 |
| `a 0`、`m 0` | 两项均低，渐变至幽微 |
| `s 200` | 显示接触不佳，不再将数据用于控制 |
| `s 0` | 下一个完整有效样本恢复控制 |
| `pause` | 停止发送；距最后有效控制数据超过五秒后，花朵提示重新感知并渐变至幽微 |
| `resume` | 恢复发送 |
| `drop` | 断开 TCP；Unity 约两秒后自动重连，等待新数据 |
| `quit` | 退出模拟器 |

专注度和放松度可独立修改。进入自动测试后，输入 `manual` 可回到手动测试。自动模式会覆盖四态组合、信号差、数据暂停和断线：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\Assets\BCI\BrainLink\Python\Start-BrainLink.ps1" -Auto
```

自动模式每阶段约四秒；需要清楚观察五秒超时时，请用手动 `pause` 等待至少六秒。花朵被冻结时仍接收与显示数据，恢复后继续表现。

## 4. 实体 BrainLink 接入

1. 开启设备，按设备说明佩戴。
2. 在 Windows 蓝牙设置中添加设备。官方 SDK 示例提示 BrainLink Pro 选择耳机图标对应的设备。
3. 在蓝牙更多设置的 COM 端口列表中查找设备的**出站（Outgoing）端口**，记下实际端口，例如 `COM7`。若没有出站 COM，先检查配对和设备连接模式。
4. 关闭其他占用该串口的软件，停止模拟器。一个串口不能同时被两个程序读取。
5. 用实际 COM 号启动，以下 COM7 仅为例子：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\Assets\BCI\BrainLink\Python\Start-BrainLink.ps1" -Mode serial -SerialPort COM7
```

6. 保持窗口打开，确认出现 `Serial connected` **以及持续输出的三个指标**。串口打开成功本身不代表已收到有效脑电。
7. 在 Unity 勾选 Brain Link Flower Input，进入 Play；F9 确认 `mode=serial`、指标持续更新、signal 为 0。

数据程序使用 115200 波特率；此方式不依赖 ThinkGear Connector 或额外虚拟串口驱动。非零 signal 的数据仍显示以便排查，但不会刷新花朵的最后有效时间。超过五秒没有有效控制数据时渐变至幽微；重连后必须收到新样本才恢复。

## 5. 实时诊断与日志

- F9 面板显示专注度、放松度、信号、来源、连接状态及最新完整样本年龄。未收到或数据非法时显示“暂无数据”，过期样本标记“已过期”。连接正常也可能处于等待数据、接触不佳或超时状态。
- Console 每秒输出 `[BrainLink]` 摘要，连接、断开、信号及超时变化立即记录。日志包含最近一次三个数值和数据年龄，不记录原始波形。
- Windows exe 的同类日志写入 `%USERPROFILE%\AppData\LocalLow\<CompanyName>\<ProductName>\Player.log`，CompanyName / ProductName 对应项目 Player Settings。也可启动时显式指定日志文件：

```powershell
.\Builds\Windows\MindState.exe -logFile .\MindState-BrainLink.log
```

## 6. 构建及迁移到新电脑

### 构建

1. 在主场景勾选 Brain Link Flower Input，**保存场景**。构建使用保存的启用状态；默认交付场景未勾选，需要脑控的构建应先勾选。
2. 在本项目根目录执行 `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tools\Build.ps1`，或 Unity Build Settings 选择 Windows x64、以 FlowerGallery 为首个场景构建。
3. 启动 Python 模拟器或实体串口程序，再打开 `Builds/Windows/MindState.exe`，按 F9 检查数据。

### 只部署程序

复制完整游戏构建目录（含 exe、MindState_Data、UnityPlayer.dll 及其他运行文件），以及本项目的 Python 文件夹和本说明。保留下面的相对结构：

```text
部署根目录/
  Assets/BCI/BrainLink/Python/    # Setup、Start、brainlink_bridge.py
  Assets/BCI/BrainLink/README.md
  Builds/Windows/               # 完整 Unity 游戏构建目录
```

目标电脑安装 Conda 后，在部署根目录运行第 2 节 Setup 命令，创建该电脑自己的 `Builds/BrainLinkTools`。首次准备需联网。随后重新完成蓝牙配对、确认实际出站 COM，运行 serial 命令，再启动游戏。不要把原电脑的 COM 号和 python-env 当作可直接迁移的配置。

无 Unity 的部署电脑通过已启用 BrainLink 的构建接入，不在 exe 中提供 Inspector 勾选框。正常运行不要添加 `-verifyBrainLink`；该参数仅供验证程序使用，测试后会自动退出。

## 7. 排查顺序

| 现象 | 检查 |
|---|---|
| `Conda not found` | 安装 Conda 或用 `-Conda` 指定实际 conda.exe |
| Conda 长时间获取索引或下载失败 | 检查 conda-forge 网络访问，等待或修复网络后重试 Setup |
| `Run Setup-BrainLink.ps1 first` | 先在当前机器运行 Setup |
| SDK SHA-256 或 import 失败 | 检查下载，确认 Python 3.11 x64；重新运行 Setup |
| 13856 端口占用 | 关闭重复的桥接程序，包括侏罗纪项目的桥接程序 |
| Python 有数值，Unity 无数值 | 组件是否勾选；Host / Port 是否匹配；F9 查看状态 |
| 实体设备却显示 simulate | 停止模拟器，改用 `-Mode serial -SerialPort 实际COM号` |
| 串口不可用 | 是否为当前电脑实际出站 COM，设备是否开机，是否被其他软件占用 |
| 串口连接成功却无数值 | 先排查设备输出和配对；此时不能判断 Unity 接收正常 |
| 数值更新但花朵不响应 | signal 是否为 0；是否冻结；平滑过渡需要时间 |
| 打包后没有接入 | 构建前是否勾选并保存场景；查看 Player.log 或指定日志 |

先确认 Python 持续输出，再确认 Unity 收到完整数据，最后确认接触质量与冻结状态。

## 8. 验证与限制

自动验证使用模拟数据，没有实体设备验收。软件测试通过不代表蓝牙连接、实际佩戴、设备持续输出或生理指标准确性已通过验证。

准备环境后可运行 Python 测试：

```powershell
.\Builds\BrainLinkTools\python-env\python.exe .\Assets\BCI\BrainLink\Tests\test_brainlink.py
```

Unity 菜单 `MindState/BrainLink/Run Acceptance` 验证协议、五秒过期、网络重连、线程释放和场景默认配置。测试会打开主场景，先保存当前工作。集成验收可由 `MindState.BCI.BrainLink.Tests.BrainLinkAcceptance.PlaySmoke` 进入 Play，输出在 `Outputs/BrainLink/Editor`。

Windows 测试统一使用 `Tools/Build.ps1 -Validation` 生成的 `Builds/Validation/MindState.exe`，运行时加 `-verifyBrainLink`，输出在项目的 `Outputs/BrainLink/Windows`。此构建与正式构建使用相同几何缓存来源检查。正常使用无需验证参数。
