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
- [图片替换](#-图片替换)
- [常见问题](#-常见问题)

---

## ✨ 功能特性

- 游戏界面&剧情翻译
- 关闭游戏内动态马赛克
- 跳过进游戏时的音量提醒
- 剧情角色语音不中断
- 关闭进游戏时的标题动画
- 按住 `Ctrl` 滚动鼠标滚轮调整剧情 Live2D 大小
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
| `NovelLive2DScale`  | `1.0`   | 剧情 Live2D 缩放倍率（范围 `0.1` 至 `10.0`） |

### `[Translation]`

| 配置项     | 可选项                                       | 默认值                                                                                      | 说明                                                                    |
| ---------- | -------------------------------------------- | ------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------- |
| `Enabled`  | `true`（开启），`false`（关闭）              | `true`                                                                                      | 是否开启游戏内剧情翻译                                                  |
| `CDN`      | 任意有效的 CDN URL 地址                      | `https://raw.githubusercontent.com/anosu/dotabyss-translation/refs/heads/main/translations` | 翻译数据 CDN 地址                                                       |
| `Language` | `zh_Hans`（简体中文）                        | `zh_Hans`                                                                                   | 翻译语言，支持 `zh_Hans` 简体中文 |

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

### UI 文本格式

`translations/ui_texts/<language>.json` 按 TMP 组件的完整 Transform 路径分组，
组内使用精确原文映射译文：

```json
{
  "TopScene/Canvas/Menu/HomeButton/Label": {
    "ホーム": "主页"
  },
  "CharacterScene/Canvas/Profile/NameLabel": {
    "名前": "名字"
  }
}
```

路径或原文任一不匹配时，文本保持不变。

UI 文本和图片目标采集器已拆为独立插件，不包含在本仓库；`F11` 采集 UI 文本，
`F12` 生成图片替换 manifest 草稿。

---

## 🖼️ 图片替换

图片替换清单位于：

```text
BepInEx\plugins\AbyssMod\replacements\manifest.json
```

发布包中的 `manifest.example.json` 是格式示例；将它改名为 `manifest.json` 后才会启用。示例中的 ID、路径和图片文件都是占位内容，需要换成实际目标并自行放入对应图片。图片文件路径相对于 `replacements` 目录，支持 PNG、JPG 和 JPEG，不允许绝对路径或 `..` 跳出该目录；需要透明通道时使用 PNG。

```json
{
  "version": 1,
  "novelBackgrounds": {
    "BG001": "images/novel/BG001.png"
  },
  "spriteNames": {
    "Common_Item_1001_M": "images/items/Common_Item_1001_M.png"
  },
  "uiComponents": [
    {
      "transformPath": "HomeScene/Canvas/BannerArea/Banner/Image",
      "sourceSprite": "BannerS_10001",
      "file": "images/ui/home_banner.png"
    }
  ]
}
```

字段说明：

| 字段 | 类型 | 匹配方式 |
| ---- | ---- | -------- |
| `version` | 整数 | 当前固定为 `1`；缺失时也按 `1` 处理 |
| `novelBackgrounds` | 对象 | key 是区分大小写、不带扩展名的剧情背景 ID |
| `spriteNames` | 对象 | key 精确匹配 `Sprite.name`；这是全局兜底，同名 Sprite 会一起被替换 |
| `uiComponents` | 数组 | `transformPath` 精确匹配对象完整层级路径；可选的 `sourceSprite` 会再校验原始 `Sprite.name` |

同一图片命中多条规则时，优先级为 `uiComponents`、`novelBackgrounds`、`spriteNames`。所有匹配均区分大小写。

Mod 会在图片第一次显示时读取并缓存替换图，同时继承原 Sprite 的 pivot、pixels-per-unit 和九宫格 border；border 超过新图片尺寸时会自动收缩。建议替换图与原图保持相同尺寸和比例；尺寸不一致时日志会给出警告。单张图片最大 64 MiB，宽高均不能超过 8192 像素，总像素不能超过 16777216。单张图片读取或解码失败只会保留原图，不影响其他规则。

修改清单或图片后需要完全退出并重启游戏，`F10` 不会热重载图片。当前版本不支持用普通 PNG 替换 Prefab、Spine、Live2D、`RawImage`、材质贴图，也不支持按 SpriteAtlas 子资源地址直接匹配；从图集中取出的 Sprite 仍可能通过 `spriteNames` 匹配。

不知道背景 ID、Sprite 名或 UI 路径时，可以安装独立的 `AbyssMod.UiTextDumper`，在游戏内按 `F12` 采集。它会在自己的 `capture` 目录生成同结构草稿；先填写需要保留规则的空 `file` 字段并放置对应图片，再作为正式 manifest 使用。

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
