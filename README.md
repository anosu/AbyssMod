# AbyssMod

> 🎮 鸡渊汉化MOD

本仓库适用于 **Windows 平台 DMM Game Player 端**

开发环境、Utility 源码引用和代码格式化见 [构建说明](https://github.com/anosu/ModEngineering/blob/main/docs/CONVENTIONS.md)。

使用时如遇到问题请务必先阅读下面的[常见问题](#-常见问题)

---

## 📋 目录

- [功能特性](#-功能特性)
- [快速开始](#-快速开始)
- [配置项](#-配置项)
- [快捷键](#-快捷键)
- [翻译数据](#-翻译数据)
- [图片替换](#-图片替换)
- [常见问题](#-常见问题)

---

## ✨ 功能特性

- 游戏界面、图片与剧情翻译，可分别控制
- 按 `F10` 打开游戏内 MOD 设置菜单，无需手动修改配置文件
- 关闭游戏内动态马赛克
- 跳过进游戏时的音量提醒
- 剧情角色语音不中断
- 关闭进游戏时的标题动画
- H场景滤镜开关与尺寸调整，支持菜单设置和 `Ctrl` + 鼠标滚轮缩放
- 通过本地 `manifest.json` 替换剧情背景和 Sprite 图片

---

## 🚀 快速开始

### 1. 安装游戏客户端

确保已安装 DMM Game Player 版游戏，并知晓游戏可执行文件所在的目录

### 2. 下载插件

前往 [Releases](https://github.com/anosu/AbyssMod/releases) 页面，找到最新版本（带有绿色 `Latest` 标识），展开 `Assets` 下载 `AbyssMod.7z` 压缩包

> ⚠️ 不要下载 `Source code`，那是源码

### 3. 安装

将压缩包解压到游戏根目录（和游戏 `.exe` 同级），解压后目录结构大致如下：

```
游戏根目录/
├── 游戏.exe
├── winhttp.dll
└── BepInEx/
    ├── core/
    ├── plugins/
    │   └── AbyssMod/
    └── config/
```

### 4. 启动游戏

**正常启动游戏**（在DMMPlayer里启动），如果这是你第一次安装 BepInEx，启动时会自动下载适配当前 Unity 版本的补丁，期间只显示一个控制台窗口，稍等片刻即可

> ⚠️ 如果你用了加速器（如 ACGP），控制台可能出现红色报错，说明可能无法直连 BepInEx 官网，请开启代理/梯子后重试

### 5. 游戏内设置与配置文件

进入游戏界面后按 `F10` 打开 MOD 设置菜单，包含「翻译设置」「画面与声音」「高级选项」三个页面。菜单仅通过 `F10` 呼出，没有常驻按钮。

修改后点击「保存并应用」；「恢复默认」只填入默认值，仍需保存。有未保存修改时，`F10`、`Esc` 或顶部关闭按钮会先提示，也可选择「放弃并关闭」。手动编辑配置文件后，可在菜单中点击「从文件重读」。

菜单打开时，鼠标点击、拖拽和长按不会传给游戏，兼容快捷键暂停生效；关闭后会等待鼠标释放，再恢复游戏输入。标注「需重启」或「下次载入」的设置按菜单提示生效。

首次运行后，`BepInEx\config\` 目录下会生成两个配置文件：

| 文件           | 用途                                 |
| -------------- | ------------------------------------ |
| `BepInEx.cfg`  | BepInEx 框架配置（如隐藏控制台窗口） |
| `AbyssMod.cfg` | 插件功能配置（翻译、字体、马赛克等） |

---

## ⚙️ 配置项

### `[General]`

| 配置项              | 默认值  | 说明               |
| ------------------- | ------- | ------------------ |
| `DynamicMosaic`     | `false` | 是否启用动态马赛克 |
| `SoundCaution`      | `false` | 是否弹出音量提醒   |
| `VoiceInterruption` | `false` | 是否启用语音中断   |
| `TitleMovie`        | `true`  | 是否播放标题动画   |
| `NovelLive2DScale`  | `1.0`   | H场景尺寸大小的缩放倍率（范围 `0.1` 至 `10.0`） |
| `NovelStageVolume` | `true`  | H场景滤镜开关，控制附加泛光、色差，保留舞台基础效果 |

### `[Menu]`

| 配置项 | 默认值 | 说明 |
| ------ | ------ | ---- |
| `LegacyHotkeys` | `false` | 启用 `F6`、`F8`、`F9` 兼容快捷键；仅在菜单关闭时生效 |

### `[Translation]`

| 配置项     | 可选项                          | 默认值                                                                                      | 说明                              |
| ---------- | ------------------------------- | ------------------------------------------------------------------------------------------- | --------------------------------- |
| `Enabled`  | `true`（开启），`false`（关闭） | `true`                                                                                      | 剧情翻译可即时切换；MasterData 翻译按启动时的设置生效 |
| `UIEnabled` | `true`（开启），`false`（关闭） | `true` | 是否启用界面文字与图片翻译，修改后重启生效，不受剧情翻译开关影响 |
| `CDN`      | 任意有效的 CDN URL 地址         | `https://raw.githubusercontent.com/anosu/dotabyss-translation/refs/heads/main/translations` | 翻译数据 CDN 地址                 |
| `Language` | `zh_Hans`（简体中文）           | `zh_Hans`                                                                                   | 翻译语言，支持 `zh_Hans` 简体中文 |

### `[Translation.Cache]`

| 配置项             | 默认值           | 说明                                                                |
| ------------------ | ---------------- | ------------------------------------------------------------------- |
| `Directory`        | `AbyssMod/cache/translations` | 缓存目录；相对路径以 `BepInEx/plugins` 为基准，也可填写绝对路径 |
| `PreferLocalFiles` | `false`          | 本地文件存在时优先使用，不校验远程哈希；manifest 始终先尝试远程加载 |

### `[Translation.Font]`

| 配置项            | 默认值                     | 说明                                                |
| ----------------- | -------------------------- | --------------------------------------------------- |
| `AssetBundlePath` | `AbyssMod/fonts/ttcuyuanj` | TMP 字体 AssetBundle 路径（相对插件目录或绝对路径） |

---

## ⌨️ 快捷键

| 快捷键 | 功能              |
| ------ | ----------------- |
| `F10`  | 打开 / 关闭 MOD 设置菜单；有未保存修改时先提示 |
| `Esc`  | 关闭菜单；有未保存修改时先提示 |
| `Ctrl` + 鼠标滚轮 | 调整H场景尺寸大小，仅在菜单关闭时生效 |
| `F6`   | 开启/关闭H场景滤镜 |
| `F8`   | 开启/关闭剧情翻译 |
| `F9`   | 开启/关闭语音中断 |

`F6`、`F8`、`F9` 默认关闭，需要在「高级选项」中开启「兼容快捷键」，并且仅在菜单关闭时生效。`F10`、`Esc` 和 `Ctrl` + 鼠标滚轮不受此开关影响。

---

## 📦 翻译数据

翻译文件托管在独立仓库中，与插件本体分离：

[dotabyss-translation](https://github.com/anosu/dotabyss-translation)

缓存目录内部结构与该仓库的 `translations` 目录完全一致。可以直接将其内容放入缓存目录，或把 `Translation.Cache/Directory` 指向下载后的 `translations` 目录。

默认缓存统一放在 `BepInEx/plugins/AbyssMod/cache` 下：文本翻译使用 `translations` 子目录，图片替换使用 `replacements` 子目录。

旧配置若仍使用默认的 `AbyssMod/translations`，插件启动时会自动更新为 `AbyssMod/cache/translations`，并将旧文件复制到新目录中缺失的位置；不覆盖新目录中的已有文件，也不删除旧文件。自定义缓存路径保持不变。

MasterData 与 UI 翻译状态、翻译 CDN、语言、缓存目录、本地优先策略和字体路径均在插件启动时确定，修改后需要重启游戏。剧情翻译可以在菜单中即时切换，或在启用兼容快捷键后按 `F8` 切换；「从文件重读」只即时应用支持运行时更新的设置。

---

## 🖼️ 图片替换

图片替换随「界面与图片翻译」开关在启动时启用。插件从翻译 CDN 校验并同步替换清单和图片，保存到 `BepInEx/plugins/AbyssMod/cache/replacements`，下载失败时尝试使用已有本地缓存。

---

## ❓ 常见问题

<details>
<summary><b>启动时控制台窗口出现红色报错</b></summary>
通常是 BepInEx 无法连接其官网下载 Unity 补丁，请开启代理/梯子后重启游戏

也可能是初始化文件是网络波动导致下载的文件损坏，此时可以尝试删除Mod文件然后重新安装

</details>

<details>
<summary><b>如何隐藏控制台窗口</b></summary>
编辑 <code>BepInEx\config\BepInEx.cfg</code>，找到 <code>[Logging.Console]</code>，将 <code>Enabled</code> 设为 <code>false</code>
</details>

### 社群

- QQ群：[731843659](https://qm.qq.com/q/u80uVbzfNK)

---

> 💬 有问题可以提交 [Issue](https://github.com/anosu/AbyssMod/issues) 或直接在 QQ 群里问

## 开发

源码位于 `src/`，测试位于 `tests/`。项目配置由 `.csproj` 管理，依赖版本由 Git 子模块记录。构建、VS 联调和发布命令见[公共工程说明](https://github.com/anosu/ModEngineering/blob/main/docs/CONVENTIONS.md)。
