# 横屏 UI 适配说明

项目的 UI 以 1920 x 1080（16:9 横屏）为参考尺寸。目前的适配由两个组件负责：

## AdaptiveCanvasScaler：屏幕比例缩放

把 `AdaptiveCanvasScaler` 加在已有 `Canvas Scaler` 的 Canvas 上，无需另建 Canvas。
组件会在运行时比较设备长宽比与 Canvas 的 `Reference Resolution`：

- 比 16:9 更宽：按高度缩放，画面左右留出更多空间。
- 比 16:9 更窄：按宽度缩放，画面上下留出更多空间。
- 屏幕尺寸发生变化时重新计算；不会修改场景中保存的参考分辨率。

目前已接入 `Start` 的 `VideoCanvas`、`Index` 的 `Canvas`、`Select` 的 `砖块Canvas`、
`Level2` 场景的 `Canvas`，以及 `Level1` 至 `Level5` 预制体的 `DialogueCanvas`。
`Select` 中全屏播放视频的 Canvas 不使用这个组件，以免视频边缘被额外缩放。

## SafeAreaButton：避开刘海和屏幕边缘

把 `SafeAreaButton` 加在需要避开设备左右安全区的 UI 对象上。
它保留该对象原本的 `RectTransform` 位置，仅在按钮超出 `Screen.safeArea` 时沿水平方向挪回安全区。
`Margin` 是以 Canvas 单位计算的额外边距，默认 24。当前已用于 `Select` 的“上一个”和“下一个”箭头。
该组件不负责上下边缘，也不会自动修正其他没有挂载此组件的控件。

## 验收方法

1. 在 Unity 安装并打开 `Device Simulator`，进入 Play 模式。
2. 分别检查 16:9、19.5:9、20:9 的横屏设备，以及带刘海或挖孔的设备。
3. 检查开始界面、选关界面、关卡对话和完成面板：控件不能出屏、互相遮挡，箭头仍可点击。
4. 检查选关进入动画、关卡预制体加载及下一关按钮流程没有变化。

这是一轮基础适配，不代表所有界面已经逐机型验收。关卡中未挂安全区组件的边缘控件、
非 Canvas 的世界空间对象，以及全屏图片的裁切情况仍需在 Device Simulator 和真机上检查。
如 Unity 提示场景在外部被修改，请先保存自己的未保存改动，再重新加载磁盘场景。
