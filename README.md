# MindState · 浮光花境

Unity 2022.3.62f3c1 / Windows x64。主场景是 `Assets/Flower/Scenes/FlowerGallery.unity`，当前配置为 18 片花瓣、三层（7/6/5），采用珍珠丝绸材质与自然混色彩光。

## 运行与操作

启动 `Builds/Windows/MindState.exe`，保留同目录的数据与 DLL。

- 四态按钮把虚拟脑电滑条在 9 秒内移到对应目标值，花朵实时跟随；1–4 等同按钮，Shift+1–4 直接注入四个角点。
- 拖动环绕、滚轮缩放；R 重播标准开合，空格冻结；面板默认隐藏，H 显示 / 隐藏。
- 开合滑条直接控制 0%～100%；F9 切换 BrainLink 诊断面板，L 显示帧率。
- 拖动专注/放松滑条作为脑电输入并核对四态映射；BrainLink 接管时滑条禁用，关闭组件后滑条重新生效。
- BrainLink 默认关闭，在相机 Inspector 启用组件后接管输入。

## 开发入口

在项目根目录运行以下 PowerShell 命令，运行批处理前关闭占用本项目的 Unity 编辑器：

```powershell
./Tools/Bake.ps1                 # 修改几何或生成源码后重新生成缓存
./Tools/Verify.ps1               # 当前项目回归
./Tools/Build.ps1                # 正式 Windows 程序
./Tools/Build.ps1 -Validation    # 含诊断功能的测试程序
```

烘焙会检查来源，构建拒绝过期缓存。`Outputs` 是可删除的本轮验证产物；`Builds` 是生成程序与 BrainLink 工具环境，均不纳入 Git。

- [架构、接口与维护](Docs/Architecture.zh-CN.md)
- [BrainLink 安装、模拟与设备接入](Assets/BCI/BrainLink/README.md)
- [当前限制与修复建议](Docs/CurrentIssues.zh-CN.md)
