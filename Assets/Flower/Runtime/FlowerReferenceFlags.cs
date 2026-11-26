#if UNITY_EDITOR || FLOWER_VALIDATION
namespace MindState {
public sealed partial class FlowerAssembler {
 internal bool forceCpuReference;
#if UNITY_EDITOR
 internal bool clipReverseEcho;
 internal float clipEchoBloom;
#endif
}
}
#endif