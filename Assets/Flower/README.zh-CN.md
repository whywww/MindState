# 整花主版本

主场景为Scenes/FlowerGallery.unity，Preset为Art/ExhibitionPeony.asset，程序为项目根目录Builds/Windows/MindState.exe。

当前配置14片、6/5/3，宽度60%/50%/40%，主褶3/2/1，具有纵向闭合和左右不对称包卷。六组共享网格、33姿态缓存，连续40字节顶点数据由标量CPU插值后单次上传。没有新增依赖或降低网格精度。

操作、构建与脑电接口见[主说明](../../README.md)，实际验证与未通过项见[最新报告](../../Evidence/FlowerPerformance/报告.md)。

运行中修改造型或结构后需调用RebuildFlower重新生成缓存。SetBloomProgress为0–1，PlayBloom重播，SetFrozen冻结。原单片Legacy路径保留。

已知限制：未稳定60帧，搭接与穿插未全部通过；不要将外观验收与性能/几何验收混为一谈。
