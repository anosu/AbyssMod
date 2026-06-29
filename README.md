# AbyssMod

> 🎮 鸡渊汉化MOD

本仓库适用于 **Windows 平台 DMM Game Player 端**

使用时如遇到问题请务必先阅读下面的[常见问题](#-常见问题)

---

## 📋 目录

- [功能特性](#-功能特性)
- [快速开始](#-快速开始)
- [配置项](#-配置项)
- [快捷键](#-快捷键)
- [翻译数据](#-翻译数据)
- [常见问题](#-常见问题)

---

## ✨ 功能特性

- 游戏界面&剧情翻译
- 关闭游戏内动态马赛克
- 跳过进游戏时的音量提醒
- 剧情角色语音不中断
- 关闭进游戏时的标题动画

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

### 5. 配置文件

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

### `[Translation]`

| 配置项     | 可选项                                       | 默认值                                                                                      | 说明                                                                    |
| ---------- | -------------------------------------------- | ------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------- |
| `Enabled`  | `true`（开启），`false`（关闭）              | `true`                                                                                      | 是否开启游戏内剧情翻译                                                  |
| `CDN`      | 任意有效的 CDN URL 地址                      | `https://raw.githubusercontent.com/anosu/dotabyss-translation/refs/heads/main/translations` | 翻译数据 CDN 地址                                                       |
| `Language` | `zh_Hans`（简体中文），`zh_Hant`（繁體中文） | `zh_Hans`                                                                                   | 翻译语言，支持 `zh_Hans` 简体中文与`zh_Hant` 繁體中文（群友"戀曲"提供） |

### `[Translation.Font]`

| 配置项            | 默认值                     | 说明                                                |
| ----------------- | -------------------------- | --------------------------------------------------- |
| `AssetBundlePath` | `AbyssMod/fonts/ttcuyuanj` | TMP 字体 AssetBundle 路径（相对插件目录或绝对路径） |

---

## ⌨️ 快捷键

| 快捷键 | 功能              |
| ------ | ----------------- |
| `F8`   | 开启/关闭剧情翻译 |
| `F9`   | 开启/关闭语音中断 |
| `F10`  | 热重载配置文件    |

---

## 📦 翻译数据

翻译文件托管在独立仓库中，与插件本体分离：

[dotabyss-translation](https://github.com/anosu/dotabyss-translation)

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

<details>


> 如果镜像站可用建议优先使用镜像站

</details>

<details>
<summary><b>更改成繁體中文</b></summary>
<code>BepInEx\config\AbyssMod.cfg</code> 中 <code>Language=zh_Hans</code> 改成 <code>Language=zh_Hant</code>

</details>

### 社群

- QQ群：[731843659](https://qm.qq.com/q/u80uVbzfNK)，[453330625](https://qm.qq.com/q/5ccunRbE0E)（二群）
- 海外詢問： 添加Discord好友 :.lienchu9420（Lienchu恋曲）

---

> 💬 有问题可以提交 [Issue](https://github.com/anosu/AbyssMod/issues) 或直接在 QQ 群里问
