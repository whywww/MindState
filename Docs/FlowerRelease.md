# 当前版本：自然混色彩光

自然彩光已并入 main，正式入口 Builds/Windows/MindState.exe。详见 [集成说明](NaturalLight.md)。以下为此前发布记录。

# 已批准的根部裁切与花丝连接

2026-11-02：用户批准集成到main并提交推送。

原薄圆片下移到花底，移除球形封口，30根花丝延长并埋入圆片，保留花药高度。取消根部额外扩宽和回伸，使用材质裁切底部多余根瓣，不增加过渡曲面。四态、开合与脑电API保留。

候选版通过整花50项、单片97项、四态40项；main集成后再次通过整花50项并完成正式构建。既有瓣间间隙和局部搭接限制保留，不宣称全角度无穿插或稳定60fps。

运行Tools/Build.ps1生成Builds/Windows/MindState.exe；验证构建使用Tools/Build.ps1 -Validation。Builds按既有规则不纳入Git，本地正式程序已更新。截图视频保留本地，不包含在本次推送中。验证摘要见Evidence/RootTrim/IntegrationChecks.json。
