# 四种意象与脑电艺术呈现

默认进入澄明：90秒一圈慢旋转，叠加±0.2°规则微颤。凝聚沿原开合轨迹收拢为花苞，并在全局进度0–0.04内以5秒周期轻微呼吸，漫游为逐瓣7–11秒错相摇曳，幽微为14秒柔和慢波；幽微的整花竖轴始终直立。面板选择四态、模拟脑电、外部输入，空格冻结，重置回到澄明，R进入原开合重播。

本轮依据用户确认的第一组四态参考图实现。保留14瓣/6-5-3、原有丝绸网格与材质。凝聚完整复用原开合的33帧缓存、分层时差、根部变换、纵向旋转、尺寸和补偿，不使用独立倾角或整体缩放。凝聚权重G、呼吸进度B对应曲面进度1-G×(1-B)。其余三态倾角使用计划值。

接口（Unity主线程，FlowerAssembler.Ready之后）：

```csharp
flower.SetArtState(FlowerArtState.Clarity); // Gathering / Wandering / Quiet
flower.SimulateArtBrain();
flower.WaitForArtBrain();
flower.SetBrainMetrics(attention01, relaxation01, signalValid: true);
flower.SetFrozen(true);
flower.ResetArt();
```

外部输入为0–1，设备传输与信号有效性由适配层负责。有限数值限制到0–1；无效数据不刷新最后有效时间。5秒未收到有效数据则提示重新感知并渐变到幽微，收到有效数据自动恢复。预览/模拟不会伪装成外部脑电。四态权重为AR、A(1-R)、(1-A)R、(1-A)(1-R)，4秒指数时间常数连续混合。

凝聚及与凝聚的混合会沿原缓存曲面插值与上传；进度不变时停止上传。纯澄明、漫游、幽微恢复盛放曲面后只更新14个花瓣变换与MaterialPropertyBlock。原先“四态零上传”的结论不再适用。Shader使用独立暂停时钟；切换时旋转速度平滑改变并保留朝向。SetBloomProgress/PlayBloom退出意象，保留原开合接口。默认Shader权重为零，原单片路径保持原有材质行为。

实机截图、四态及凝聚进出过程的八段20秒MP4、完整旋转抽样和验收记录见项目根目录 Evidence/FlowerArt。复测：Tools/Build.ps1 -Validation，然后运行验证程序加 -verifyArt；该检查包含十分钟显式渲染循环。Tools/EncodeFlowerArt.py使用本机OpenCV将Unity输出帧编码为视频；Unity项目无新增包依赖。

已知：原花瓣搭接和中心背景缝隙仍可见，不能将接口/性能测试通过视为无穿插或目标图逐像素复现。

---

# 整花主版本

主场景为Scenes/FlowerGallery.unity，Preset为Art/ExhibitionPeony.asset，程序为项目根目录Builds/Windows/MindState.exe。

当前配置14片、6/5/3，宽度60%/50%/40%，主褶3/2/1，具有纵向闭合和左右不对称包卷。六组共享网格、33姿态缓存，连续40字节顶点数据由标量CPU插值后单次上传。没有新增依赖或降低网格精度。

操作、构建与脑电接口见[主说明](../../README.md)，实际验证与未通过项见[最新报告](../../Evidence/FlowerArt/报告.md)。

运行中修改造型或结构后需调用RebuildFlower重新生成缓存。SetBloomProgress为0–1，PlayBloom重播，SetFrozen冻结。原单片Legacy路径保留。

已知限制：未稳定60帧，搭接与穿插未全部通过；不要将外观验收与性能/几何验收混为一谈。
