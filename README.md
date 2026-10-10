# AbyssMod

> 🎮 鸡渊汉化MOD

本仓库适用于 **Windows 平台 DMM Game Player 端**

安卓端发布页:https://release.ntr.best/mod/dotabyss

开发环境、Utility 源码引用和代码格式化见 [构建说明](https://github.com/anosu/ModEngineering/blob/main/docs/CONVENTIONS.md)。

翻译会话在卸载时取消 manifest、静态资源和剧情请求，旧结果不会继续发布。补丁解除失败时保留运行依赖并拒绝卸载。字体可能被游戏文本持有，按进程生命周期保留，不支持热替换或强制销毁。

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
- 支持通过 BepInEx.ConfigurationManager 调整配置，无需手动修改配置文件
- 关闭游戏内动态马赛克
- 跳过进游戏时的音量提醒
- 剧情角色语音不中断
- 关闭进游戏时的标题动画
- 隐藏寝室 CG 角色眼镜
- H场景滤镜开关与尺寸调整，支持配置管理器和 `Ctrl` + 鼠标滚轮缩放
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

本项目适配 [anosu/BepInEx.ConfigurationManager](https://github.com/anosu/BepInEx.ConfigurationManager)。从该分支的 [Releases](https://github.com/anosu/BepInEx.ConfigurationManager/releases) 下载 **BepInEx 6 / IL2CPP** 版本，将 `ConfigurationManager.dll` 和 `BepInEx.KeyboardShortcut.dll` 一同安装到 `BepInEx/plugins`，升级时也同时替换这两个 DLL。

默认按 `F1` 打开配置管理器，在 AbyssMod 分组中修改配置。打开键以 ConfigurationManager 自身的设置为准。文本编辑按回车或失焦时提交，关闭窗口也会提交待保存编辑；`Esc` 放弃当前文本草稿。

配置变更由 BepInEx 自动保存；支持即时更新的项目在下一帧应用。将鼠标悬停在配置名称上可查看生效时机；CDN、缓存、字体、解密和调试项标记为高级设置，需要开启高级选项显示。

CDN、缓存目录和字体路径在提交时去除首尾空格；无效 CDN 或空资源路径使用默认值，启动读取和文件重载也执行同样的校验。资源配置仍需重启生效。开关变更显示提示，缩放和文本编辑只写日志，避免连续调整产生大量弹窗。

AbyssMod 不再提供独立设置窗口，也不再占用 `F10` 或拦截游戏输入。ConfigurationManager 是可选插件，未安装时仍可在关闭游戏后编辑配置文件。

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
| `HideNovelGlasses` | `false` | 是否隐藏寝室 CG 角色眼镜；下次载入 Live2D 场景时生效 |
| `TitleMovie`        | `true`  | 是否播放标题动画   |
| `NovelLive2DScale`  | `1.0`   | H场景尺寸大小的缩放倍率（范围 `0.1` 至 `10.0`） |
| `NovelStageVolume` | `true`  | H场景滤镜开关，控制附加泛光、色差，保留舞台基础效果 |

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

### `[Translation.Crypto]`（高级）

| 配置项 | 说明 |
| ------ | ---- |
| `Tag` | 翻译文本加密标签，默认 `ENC:`；留空则不解密 |
| `Key` | 翻译文本解密密钥；使用加密标签时不能为空，通常无需修改 |

解密设置只影响之后下载的资源，不会重新加载已缓存或已载入的翻译。

---

## ⌨️ 快捷键

| 快捷键 | 功能              |
| ------ | ----------------- |
| `F1`（ConfigurationManager 默认） | 打开 / 关闭配置管理器，需要另外安装 |
| `Ctrl` + 鼠标滚轮 | 调整H场景尺寸大小，停止滚动约 1 秒后保存 |
| `F6`   | 开启/关闭H场景滤镜 |
| `F8`   | 开启/关闭剧情翻译 |
| `F9`   | 开启/关闭语音中断 |

`F6`、`F8`、`F9` 可直接使用，和配置管理器修改同一配置项。旧版 `[Menu]/LegacyHotkeys` 已停用；旧配置文件中若仍有该项，可直接删除，其余现有配置键名和默认值保持兼容。

---

## 📦 翻译数据

翻译文件托管在独立仓库中，与插件本体分离：

[dotabyss-translation](https://github.com/anosu/dotabyss-translation)

缓存目录内部结构与该仓库的 `translations` 目录完全一致。可以直接将其内容放入缓存目录，或把 `Translation.Cache/Directory` 指向下载后的 `translations` 目录。

默认缓存统一放在 `BepInEx/plugins/AbyssMod/cache` 下：文本翻译使用 `translations` 子目录，图片替换使用 `replacements` 子目录。

旧配置若仍使用默认的 `AbyssMod/translations`，插件启动时会自动更新为 `AbyssMod/cache/translations`，并将旧文件复制到新目录中缺失的位置；不覆盖新目录中的已有文件，也不删除旧文件。自定义缓存路径保持不变。

MasterData 与 UI 翻译状态、翻译 CDN、语言、缓存目录、本地优先策略和字体路径均在插件启动时确定，修改后需要重启游戏。剧情翻译可通过配置管理器或 `F8` 即时切换，当前对话也会刷新；不会热切换已经加载的 MasterData、UI 或图片替换数据。

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

> 💬 有问题可以提交 [Issue](https://github.com/anosu/AbyssMod/issues).

## 开发

源码位于 `src/`，测试位于 `tests/`。项目配置由 `.csproj` 管理，依赖版本由 Git 子模块记录。构建、VS 联调和发布命令见[公共工程说明](https://github.com/anosu/ModEngineering/blob/main/docs/CONVENTIONS.md)。

源码按现有项目惯例组织：

- `Core/`：插件生命周期、配置定义、日志和 Unity 运行时入口。插件入口组装 HTTP 客户端、字体和翻译模块；所有配置统一在 `Config.cs` 绑定，`RuntimeController` 在主线程应用配置变更和快捷键。
- `Patches/`：游戏方法的 Harmony 钩子。补丁只连接游戏入口与功能逻辑，注册和清理由 `PatchManager` 管理。
- `Services/`：翻译加载与查询、缓存、图片替换，以及 Live2D 缩放和滤镜状态。翻译管理只接收缓存和启动选项；图片规则、路径、文件头校验和画布位置计算不依赖 Unity，纹理和 Sprite 生命周期集中在图片替换管理类。缩放保留原始尺寸与延迟保存，卸载时恢复场景状态。
- `Models/`：文本翻译和图片替换清单。

配置使用标准 `Config.Bind`、`ConfigDescription` 和 `AcceptableValueRange/List`；显示名称、顺序及高级标记通过 anosu 分支支持的 `ConfigurationManagerAttributes` 标签提供，无需引用 ConfigurationManager DLL。现有配置键名保持稳定，生效时机写入描述，运行时值和启动快照分别使用。

重构保留公共工程结构、共享缓存实现和资源协议。旧窗口的草稿、批量保存、字体和输入屏蔽层已删除，避免与 ConfigurationManager 维护两套配置行为。

图片本体/更新请求和文本资源使用独立 HTTP 客户端；翻译文本及图片 JSON 清单支持解密，图片本体保持原始字节。响应体读取统一限制大小，图片同步保留全量校验后提交、清单最后写入和失败回退；MasterData 只在实际写入译文时记录表级日志，不再为统计日志维护单独协程。

配置回归测试使用真实 BepInEx `ConfigFile`，验证旧配置读取与保存、资源值规范化与回退、连续编辑通知、启动快照、文件重读、快捷键和滚轮缩放的相互覆盖；Unity 场景操作使用最小替身，实际配置管理器显示和游戏画面仍需在游戏内验证。

图片测试覆盖匹配优先级、路径边界、PNG/JPEG 尺寸、SpriteAtlas 裁剪偏移与居中回退、缓存提交和下载大小限制；翻译测试覆盖启动选项、并发加载和解密请求隔离。GPU 纹理创建与实际游戏钩子仍由游戏内验证覆盖。
