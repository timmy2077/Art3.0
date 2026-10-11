# Index 主界面

打开 `Assets/Project/Scenes/Index.unity`，所有界面节点都位于 `Canvas/UIMainview`，可直接在编辑器调整。文字均使用普通 Unity UI Text，不使用 TMP。

## 按钮

- 入画闯关：保留原按钮事件，进入 Select。
- 画像砖录：保留 RecordWindowLauncher，打开现有 RecordUI。
- 左上头像和名字：保留 PlayerNameWindow，打开原取名窗口并保存名字。
- 人物志：显示四个伙伴位置的相逢状态。
- 旅途设定：按 SettingsDesign 参考图制作。乐声、声响分别使用独立音量滑条，拖动立即生效并自动保存；滑到最左侧静音。
- 归画继续：关闭设置；暂别画境：退出游戏，在编辑器里停止运行。

## 伙伴配置

选中 UIMainview，在 IndexMainMenu 的 Companions 中配置 Completed Level、Portrait、Caption 和 Display Name。现在预留四位伙伴，都使用 baishen 占位；可直接替换对应 Image 的 Source Image。

当前项目通过“下一关已解锁”表示上一关完成，所以通关第 1 关并解锁第 2 关后，第一个伙伴出现。第一关默认解锁不会自动赠送伙伴。若以后加入独立通关数据，应将 IndexMainMenu.Refresh 中的判断改为该通关记录。当前四个位置并非无限扩展的自动布局。

## 美术与布局

以 1920 × 1080 横屏布局，CanvasScaler 同时匹配宽高。暂定标题为“砖上人间”，可直接修改 GameTitle 的 Text。

Art/UI/MainMenu/BaishenMenu.png 是 baishen.png 去除透明空白后的副本，原图未修改。InkHalo.png 是程序绘制的透明光晕。背景沿用现有纸张素材。

IndexMainMenuPreview.png 是根据场景坐标生成的布局预览，实际效果以 Unity Game 视图为准。Unity 中打开着旧场景时，应重新加载 Index；如有未保存的编辑，先另存备份，避免旧内容覆盖磁盘修改。

SettingsWindowPreview.png 是新版设置窗口的布局预览。设置面板位于 `UIMainview/ModalLayer/SettingsWindow/Panel`，比例为 540 × 628；所有标签都是普通 Text，两个控件为原生 Slider。背景、边框、按钮底图和圆形滑块位于 `Art/UI/Settings`。设置专用字体位于 `Fonts/SettingsKaiTi.ttf` 和 `Fonts/SettingsYaHei.ttf`。

首次读取旧版总音量存档时，会迁移到两个独立音量设置。新存档的音乐和音效初始值为 72% 和 82%，保留旧版音乐、音效开关状态。

## 调整滑条位置

选中 `MusicVolumeSlider` 或 `SoundVolumeSlider`，修改它们的 Pos Y，可将轨道、填充和圆形滑块一起移动。

每个滑条的结构为 Track、FillArea/Fill 和 HandleSlideArea/Handle。FillArea 高度为 5，HandleSlideArea 高度为 20；Fill 和 Handle 的锚点由 Slider 控制，保留它们的 Pos Y 为 0。单独调整填充的位置或厚度时修改 FillArea；单独调整圆形滑块的位置或高度时修改 HandleSlideArea。

## 主界面模块分层

UIMainview 下按绘制顺序分为 BackgroundLayer（背景装饰）、HeaderLayer（标题）、CharacterLayer（主角与伙伴）、StatusLayer（进度与页脚）、NavigationLayer（功能按钮）、PlayerLayer（头像与名字）、ModalLayer（取名、设置与人物志弹窗）。伙伴按 Companions/CompanionSlot1～4 分组，每组包含人物图片与说明文字。界面控制脚本仍挂在 UIMainview。

分组节点均为铺满父节点的 RectTransform，用于组织层级。弹窗在 ModalLayer 内排序，确保显示在主界面内容上方。

编辑器工具独立放在 `Assets/Project/Editor/Tools/UI/UIRectRoundingTool.cs`，运行时脚本留在 `Assets/Project/Scripts`。选中 UI 后，直接点击 Inspector 的 Rect Transform 组件底部“坐标与尺寸取整”，会将当前对象的位置及尺寸取整到整数。多选 UI 对象也可一次处理。受 Slider 或布局控制的 Rect Transform 会跳过。
