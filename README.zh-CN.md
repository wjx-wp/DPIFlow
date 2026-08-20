# DPIFlow

Windows 的按显示器、按应用自动缩放管理器。

> 当前状态：早期 MVP。核心原则是保守和可恢复：**绝不修改 Windows 全局 DPI**。Windows 自己负责每显示器基础缩放，DPIFlow 只给明确配置过的应用增加“第二层”自动缩放。

## 适合什么场景

例如：14 英寸 1080p 笔记本内屏像素密度很高，而 24 英寸 1080p 外接屏像素密度较低。Windows 可以把内屏设为 125%、外屏设为 100%，但 Chrome 等软件还有自己的内容缩放。DPIFlow 的目标就是让窗口跨屏后自动切这一层。

## 当前 MVP

- Windows 10/11 托盘程序。
- .NET Framework 4.8；正常使用无需管理员权限。
- WinEvent 事件驱动，不靠高频轮询。
- 自动识别窗口、进程、当前显示器、有效 DPI。
- 显示器信息缓存，仅在显示配置变化时刷新。
- 规则保存在 `%LOCALAPPDATA%\DPIFlow\settings.json`。
- 设置界面可对“最后使用的应用”一键生成所有显示器规则。
- `Observe`：不操作，让 Windows/应用自身处理 DPI。
- `ChromiumKeyboardZoom`：Chrome/Edge 的无插件兜底方案，跨屏时用 Ctrl+0 / Ctrl+± 切网页缩放。
- 可选开机自启，仅写当前用户启动项。
- 单实例保护和本地诊断日志。
- GitHub Actions 在 Windows runner 上真实编译 EXE。
- 根目录 `VERSION` 控制发布版本；合并到 `main` 后，如该版本尚未发布，会自动创建 Tag/Release，并附带 EXE、SHA-256 与浏览器 Companion ZIP。

## 推荐用法

1. Windows 先设置基础缩放，例如外屏 100%、笔记本内屏 125%。
2. 运行 `DPIFlow.exe`。
3. 正常使用 Chrome，然后从托盘打开 DPIFlow Settings。
4. 点击 **Quick-add last app**。
5. 如果内屏 DPI 明显高于外屏，Chrome 内屏规则默认 90%，低 DPI 外屏默认 100%。
6. 保存。以后跨屏自动执行。

## Chrome / Edge 的“完美模式”

仓库里的 `browser-extension/` 是 Manifest V3 Companion。它不是模拟快捷键，而是通过 Chrome 官方 `windows/system.display/tabs` API 判断浏览器窗口在哪块显示器，再对每个标签页设置 per-tab Zoom；跨屏、切标签、移动标签与页面导航后都会重新应用。

当前 Companion 和 EXE 暂时独立配置。后续可通过 Native Messaging 统一设置。即使不装 Companion，EXE 的 `ChromiumKeyboardZoom` 仍可作为轻量兜底。

## 安全原则

- 不改全局 DPI 注册表。
- 默认不碰未知应用。
- 不要求管理员权限。
- 配置只存在本机。
- Adapter 失败时宁可不操作，也不修改系统显示配置。

## 开源

MIT License。欢迎提交新 Adapter、问题报告和兼容性测试。
