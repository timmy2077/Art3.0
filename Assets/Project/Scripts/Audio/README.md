# 音频系统使用说明

`GameAudio` 会在进入任意场景前自动创建，切换场景时不会销毁，并读取
`Assets/Project/Resources/AudioLibrary.asset` 中的音频配置。不需要在每个场景里手动添加 `GameAudio`。

## 添加音频素材

1. 将音频文件导入项目，例如放到 `Assets/Project/Audio` 下。
2. 在 Inspector 中打开 `AudioLibrary.asset`，在 `Music`（背景音乐）或 `Sound Effects`（音效）列表添加条目。
3. 为条目填写 `Id`，把音频文件拖到 `Clip`，并按需调整 `Volume`。同一列表中的 ID 不要重复。

填写的 ID 找不到或没有绑定 Clip 时不会播放；开启播放日志后，Console 会显示跳过原因。

## 背景音乐

在场景中一个始终激活的物体上添加 `AudioSceneMusic`，将 `Music Id` 填为音频库中 `Music` 条目的 ID。
进入该场景后会播放背景音乐。单场景切换时，旧场景的音乐会停止；新场景没有配置背景音乐时保持安静。

## 按钮与动画音效

所有 `Button` 与 `IPointerClickHandler` 点击已由 `UiClickSoundInstaller` 自动挂载 `UiClickSound`，
统一播放音频库中 ID 为 `ui_click` 的音效（当前为 `button-dry.mp3`），新按钮无需手动绑定。
详见 `Documentation/按钮点击音效使用说明.md`。

其他一次性的音效触发：在物体上添加 `AudioCueTrigger`，将 `Sound Id` 填为音频库中 `Sound Effects` 条目的 ID。
动画可在 Animator 的动画事件中调用 `Play()`。
代码也可以调用 `GameAudio.Instance.PlaySound("id")` 或 `GameAudio.Instance.PlayMusic("id")`。

## 音量、开关与调试

`DataSystem.SaveSettingsVolume`、`SaveSettingsMusic`、`SaveSettingsSFX` 修改后立即生效。
现有的 `enablePersistence` 勾选框决定这些设置是否在重新运行游戏后保留。
音效最多同时播放 8 个，超过时本次音效会被跳过。

`AudioLibrary` 中的 `Log Playback` 默认开启。成功开始播放时，Unity Console 会显示 ID 和 Clip 名称；
未找到音频或无法播放时会显示警告。Unity 无法检测电脑系统是否静音，因此有播放日志不代表一定能听到声音。
