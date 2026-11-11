# BrainLink 接入验收记录

日期：2026-11-11。环境：Windows x64，Unity 2022.3.62f3c1。

## 已实现

- 整花主场景相机新增 Brain Link Flower Input，默认关闭；Inspector 组件启用框控制接入与释放。
- 读取 attention、meditation、signal，前两者归一化后输入现有花朵四态与灯光逻辑；仅 signal 为 0 且完整、新鲜的样本参与控制。
- F9 控制独立诊断面板，默认隐藏；每秒输出 Console 日志，状态变化即时输出。H 仍控制原有操作面板。
- 启用 BrainLink 时锁定界面中的输入切换、开合重播及滑条、R 快捷键，保留冻结与视角操作；关闭后返回澄明。
- 新电脑安装、模拟测试、实体蓝牙出站 COM 接入、部署与排查见 [使用说明](../Assets/BCI/BrainLink/README.md)。

## 自动验证

| 检查 | 结果 | 证据 |
|---|---|---|
| 官方 SDK 与 Python 桥接 | 7 项测试通过 | `Assets/BCI/BrainLink/Tests/test_brainlink.py` |
| Unity 协议与网络验收 | 161 项检查通过 | `BrainLinkAcceptance.log` |
| Editor Play 集成 | 18 项检查通过 | `Evidence/BrainLink/Editor/Checks.txt`、`Complete.txt` |
| Windows x64 接入测试构建 | 构建成功 | `BrainLinkBuild.log`；`Builds/BrainLinkValidation/MindState.exe` |
| Windows 集成 | 18 项检查通过 | `Evidence/BrainLink/Windows/Checks.txt`、`Complete.txt`、`BrainLinkWindows.log` |
| 本项目 Python 环境及启动脚本 | Python 3.11 x64 / pyserial / SDK 加载成功；模拟输出 70 / 85 / 0 | `BrainLinkSetup.log`；执行 Start 脚本验证 |

Python 测试覆盖固定 SDK SHA-256、专注与放松独立解析、逐字节与所有拆包位置、粘包与重同步、校验和错误、raw 不冒充摘要、TCP 模拟指令、暂停与重连。

Unity 验收覆盖字段缺失与非法值、零值边界、拆包与粘包、过长行恢复、五秒过期、信号差、连接恢复后等待新回调，以及连接/阻塞读取/重试期间 Dispose 清理线程。

两种运行环境的集成检查使用本机 TCP 测试发送端，覆盖双指标分别驱动凝聚与漫游、输入所有权、默认隐藏与诊断显示切换、无数据等待、信号差导致五秒超时、恢复、暂停超时、重连、关闭回到澄明及反复启停清理。Python 官方 SDK 链路单独验证；这些检查均为软件测试。

显示切换通过同一运行时开关验证，未自动注入物理 F9 按键。隐藏窗口下的屏幕截图为黑帧，未作为界面视觉验收证据；可见窗口下的面板布局和键盘操作仍需人工核对。

本机 Conda 在线解析依赖耗时较长；较小的 current_repodata 索引缺少 libpython 依赖，未作为最终方案保留。验收环境通过本机已有环境的显式包清单、离线包缓存重新安装到本项目独立目录，未直接复制 python-env。随后运行本项目 Setup，联网下载并校验 SDK、确认加载成功，并在该独立环境重新运行全部七项 Python 测试。新电脑默认安装流程仍使用完整 conda-forge 索引，完整在线安装需在目标电脑验证。

## 正式构建与硬件限制

执行 `Tools/Build.ps1 -Validation` 时，现有构建入口报告：`Bottom overlap cache provenance differs from current geometry sources`。工作区已有其他花瓣与缓存修改，本次未修改、重新生成或伪造其签名，因此没有更新 `Builds/Windows/MindState.exe` 正式程序。

单独生成的 BrainLink 接入测试构建使用当前场景和代码，仅验证接入；没有通过原有几何发布校验，不应替代正式交付程序。正式构建需先由相关几何工作完成缓存更新与验收，再按原构建流程执行；脑控版本构建前应勾选组件并保存场景。

没有实体设备测试：蓝牙配对、实际出站串口、佩戴接触及设备持续输出尚未验收。软件模拟通过不表示硬件或生理指标准确性已确认。
