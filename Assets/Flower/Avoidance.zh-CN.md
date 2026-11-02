# 花瓣诊断

Play Mode下可在FlowerAssembler面板选择ID颜色、线框、显示/隐藏与Solo；检测使用各花瓣对应的共享变体，覆盖两种变体的自交检查。

离线扫描：

```powershell
powershell -ExecutionPolicy Bypass -File Tools/VerifyFlowerAvoidance.ps1 -Seeds 731 -Steps 201
```

输出默认位于Evidence/FlowerArt/Contacts。检测到相交返回非零；有限采样不是连续时间无穿插证明，根部命中不自动豁免。实际未通过项见[最新报告](../../Evidence/FlowerArt/报告.md)。

高级布局搜索不会自动替换正式Preset；Natural模式必须显式提供-NaturalBaselineJson。改结构、弯曲或布局后须重新检测，不复用历史结论。
