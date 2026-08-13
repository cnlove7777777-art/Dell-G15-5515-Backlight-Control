# Dell G15 5515 键盘灯光控制器

[English README](README.md) · [下载 Releases](https://github.com/cnlove7777777-art/Dell-G15-5515-Backlight-Control/releases)

这是一个面向已验证 Dell G15 5515 锐龙版四区键盘的小型 Windows 托盘程序。它直接控制 AlienFX HID，不需要安装 AWCC，也不会在日常使用时弹出 PowerShell 窗口。

## 已验证范围

- Dell G15 5515，Ryzen 7 5800H 配置
- 四区控制器 `VID_187C&PID_0550`
- APIv4 灯区 ID `8、9、10、11`
- Windows 10/11 x64，.NET Framework 4.x

“同样叫 G15”并不代表控制器相同。单色键盘、APIv5、新型号或灯区映射不同的机器，安装器默认会拒绝安装。

## 安装与使用

下载 Release 压缩包并解压，双击 `Install.cmd`。安装器会检测硬件和 AWCC 冲突，把单个 EXE 放到 `%LOCALAPPDATA%\G15Backlight`，迁移旧设置，并注册隐藏的当前用户登录任务。

- `Fn+F5`：全亮 → 自定义暗亮度 → 关闭
- `Ctrl+Shift+F5`：备用快捷键
- 双击托盘图标：打开颜色和亮度设置
- 再次启动程序：唤出已经运行的设置窗口

从睡眠或休眠恢复后，程序会在电源恢复或会话解锁时重新打开 HID 控制器并恢复保存的灯光。恢复记录位于 `%LOCALAPPDATA%\G15Backlight\events.log`。

如果检测到 AWCC，安装会先停止。可运行：

```powershell
.\scripts\Remove-AWCC.ps1 -Mode Audit
```

只有明确加入 `-Mode UninstallRegistered -ConfirmAwccRemoval` 后，脚本才会卸载已注册的 MSI/Appx 包。它不会硬删驱动、服务、注册表或残留目录；错误 1612 之类的损坏安装需要逐机处理。

卸载时双击 `Uninstall.cmd`。完整技术说明、构建方式和许可信息见 [English README](README.md)。

如需报告其他机型兼容性，请运行 `scripts\Diagnose.ps1 -Json`，附上输出、准确机型和键盘类型。不要直接用 `-ForceUnsupportedController` 猜灯区映射——键盘灯倒不至于记仇，但控制未知 HID 没必要赌。
