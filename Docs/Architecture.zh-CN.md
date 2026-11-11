# 架构与维护

## 目录职责

| 目录 | 内容 |
|---|---|
| Assets/Flower/Runtime | 整花组装、标准开合、四态、彩光和界面 |
| Assets/Flower/Baking | 仅在 Editor 编译的完整补偿生成及统一来源校验 |
| Assets/Flower/Editor | 主场景创建、构建、Inspector 诊断 |
| Assets/Flower/Validation | 当前版本的回归与几何诊断 |
| Assets/HeroPetal | 整花使用的单瓣几何生成器、材质、纹理、后处理与着色器 |
| Assets/BCI/BrainLink | 输入适配、Python 桥接、安装脚本和协议测试 |
| Tools | 构建、烘焙与验证命令 |
| Docs | 当前开发说明与已知限制 |

`HeroPetal` 是整花的底层依赖，不是另一个保留版本。项目只有整花主场景进入正式构建。

## 开合与四态

`FlowerAssembler` 为每层建立确定性的曲面变体，每个变体采样 33 个姿态，运行时对顶点、法线和切线插值。标准花瓣变换和稀疏补偿共同使用一个全局开合进度。

四态决定开合目标：澄明 88%、凝聚 4%～10%（5 秒呼吸周期）、漫游 100%、幽微 70%。四态不再覆写静态倾角；原动态摆动叠加在标准姿态上，澄明保留整花旋转。手动四态切换为 9 秒，流式输入使用 3 秒指数平滑（约 9 秒收敛到 95%），灯光使用独立平滑。

Unity 主线程、`flower.Ready` 后调用：

```csharp
flower.SetBloomProgress(0.5f); // 退出四态，显示标准开合
flower.PlayBloom();           // 从闭合重播
flower.SetArtState(FlowerArtState.Clarity);
flower.SimulateArtBrain();
flower.WaitForArtBrain();
flower.SetBrainMetrics(attention01, relaxation01, signalValid: true);
flower.SetFrozen(true);
flower.ResetArt();
```

注意力 A、放松度 R 的目标权重为 AR、A(1−R)、(1−A)R、(1−A)(1−R)。无效值不刷新有效时间；外部输入五秒无有效数据后渐变到幽微，有效数据恢复后继续跟随。

## 缓存维护

造型以 `ExhibitionPeony.asset` 和 `HeroPetal.prefab` 为准，外观以 `TargetAppearance.asset` 为准。改变几何参数或几何源码后执行 `Tools/Bake.ps1`，然后验证、构建。单纯修改外观使用 `RefreshAppearance()`。

完整生成顺序：底部补偿、尖角、层间搭接、外层平滑及修正、中层修正及平滑、内层修正及平滑。每次从基础曲面开始，不读取历史报告或缓存快照，不把已有结果重复叠加。

缓存保留一个当前格式，源码签名由生成与构建共用。禁止只更新签名而跳过生成。编辑器、验证构建中的诊断代码不进入正式发布。

## 验证与产物

`Tools/Verify.ps1` 验证标准路径、四态切换、冻结与根部连接，并保留当前诊断入口。接触扫描是离散采样，不能据此宣称连续时间、所有角度零穿插；已接受的动态接触见当前问题文档。

验证结果、烘焙统计及日志统一写入 `Outputs`，重复执行覆盖本轮输出。正式程序仅保留 `Builds/Windows`，BrainLink 的本机环境保留在 `Builds/BrainLinkTools`。验证构建可以重新生成。
