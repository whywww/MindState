# MindState · 浮光花境

Unity 2022.3.62f3c1 / Windows x64。主场景是 `Assets/Flower/Scenes/FlowerGallery.unity`，当前配置为 18 片花瓣、三层（7/6/5），采用珍珠丝绸材质与自然混色彩光。

## 第一次使用

Windows x64，安装 Unity 2022.3.62f3c1（Unity 中国版）。

1. 克隆仓库并进入项目根目录：

```powershell
git clone https://github.com/whywww/MindState.git
cd MindState
```

2. 用 Unity Hub 添加这个目录并打开工程，等待首次导入。`Library`、`Builds`、`Outputs` 和播放缓存都不在仓库里，需要在本机生成。
3. 打开主场景按 Play 试看。第一次会先自动生成约 1.2 GB 播放缓存（`Assets/StreamingAssets`，不纳入 Git），画面卡住并显示进度条是正常的，生成一次后不再重复；改过花形、几何或相关生成代码后要手动运行 `./Tools/Bake.ps1`，否则按 Play 会被拦下弹窗。
4. 查找 Unity.exe 的完整路径。先读 Unity Hub 已登记的编辑器：

```powershell
(Get-Content "$env:APPDATA\UnityHub\editors-v2.json" -Raw | ConvertFrom-Json).data |
  Select-Object version, @{n='Unity.exe'; e={$_.location[0]}}
```

Hub 里没有记录时，在常见安装目录里搜：

```powershell
Get-ChildItem 'C:\Program Files','C:\Program Files (x86)' -Filter Unity.exe -Recurse -ErrorAction SilentlyContinue |
  Select-Object -ExpandProperty FullName
```

5. 打包 exe。先关闭占用本项目的 Unity 编辑器，把上一步查到的路径填进命令：

```powershell
./Tools/Build.ps1 -Unity "<Unity.exe 路径>"
```

输出 `Builds/Windows/MindState.exe`，整个 `Builds/Windows` 文件夹要一起拷走才能运行。第一次打包会自动补上缺失的播放缓存，终端会多等几分钟。

6. 双击 `MindState.exe` 运行，操作见文末「运行与操作」。要接脑机，先按 [BrainLink 使用说明](Assets/BCI/BrainLink/README.md) 准备 Python 环境。

## 运行与操作

启动 `Builds/Windows/MindState.exe`，同目录的 `MindState_Data`、`UnityPlayer.dll` 等文件要一起保留。

### 模拟脑机信号：用操作面板调试

BrainLink 连接运行时自动常开；没有启动 Python 数据程序时，花一直跟着手动操作走。操作面板默认隐藏，按 H 显示 / 隐藏。

- 四态按钮或数字键 1–4：澄明、凝聚、漫游、幽微，专注 / 放松滑条约 6 秒滑向目标值；Shift+1–4 直接跳到四个角点。
- 开合滑条直接控制 0%～100%；开合重播（同 R）、冻结 / 继续（同 Space）和视角预设（正面、3/4、侧面、重置）都可使用。
- 拖动专注、放松滑条可当作脑电输入，实时核对四态映射。
- 拖动鼠标环绕、滚轮缩放；L 显示帧率。
- F9 显示 / 隐藏 BrainLink 诊断面板。

### 脑机设备输入：连接 BrainLink

先按 [BrainLink 使用说明](Assets/BCI/BrainLink/README.md) 启动 Python 数据程序：没有设备用模拟器，实体设备用 `-Mode serial -SerialPort 实际COM号`。

- 连接运行时自动常开：不需要在 Inspector 勾选组件，构建前也不需要勾选并保存场景。
- 画面左下角常驻专注度、放松度两条只读数值条（进度条 + 数字），H 隐藏面板时仍可见；信号丢失（未连接、数据过期或接触不良）时变灰。
- 有可用数据（signal=0 且新鲜）时花跟着脑电走；按四态键 / 按钮、拖专注 / 放松滑条或点重置会立即手动优先，最后一次手动操作 5 秒后且有可用数据时自动交还脑电。开合滑条、开合重播、R、视角预设、冻结及 H/F9 始终可用。
- 没有可用数据时花一直跟着手动走并保持姿态，不会进入等待态；数据恢复可用后自动接管。Space 冻结时数据继续接收；F9 查看专注度、放松度、signal、数据年龄和连接状态。

- N 显示 / 隐藏花瓣编号，默认关闭，不受 H 影响。外层 L0-P0～P6、中层 L1-P0～P5、内层 L2-P0～P4；编号跟随同一片花瓣，反馈穿模时可直接报编号。
