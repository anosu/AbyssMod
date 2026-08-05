# AbyssMod

<!-- hy-mt2-i18n:start -->
[中文](./README.md) | **English** | [日本語](./README_ja.md) | [Español](./README_es.md)
<!-- hy-mt2-i18n:end -->


> 🎮 Kikuen Localization MOD

This repository is compatible with the **DMM Game Player version on Windows**.

If you encounter any issues while using it, be sure to read the [FAQ](#-常见问题) below first.

Translation feedback channel (Discord): https://discord.gg/RjsDFuEuBy

## 📋 Contents

- [Features](#-功能特性)
- [Getting Started](#-快速开始)
- [Configuration Options](#-配置项)
- [Shortcuts](#-快捷键)
- [Translation Data](#-翻译数据)
- [Frequently Asked Questions](#-常见问题)

## ✨ Features

## ✨ Features

- Translation of game interface and storyline
- Disable in-game dynamic mosaic
- Skip volume prompt when starting the game
- Prevent interruption of voice lines for story characters
- Turn off title animation upon game launch
- Adjust Live2D size of story content by holding `Ctrl` and scrolling the mouse wheel

## 🚀 Getting Started

## 🚀 Getting Started

### 1. Install the game client

Make sure you have installed the DMM Game Player version of the game and know the directory where the game executable file is located.

### 2. Download the Plugin

Go to the [Releases](https://github.com/anosu/AbyssMod/releases) page, find the latest version (marked with a green “Latest” label), expand “Assets”, and download the “AbyssMod.7z” archive.

> ⚠️ Do not download `Source code`, as that is the source code.

### 3. Installation

Extract the compressed package to the game’s root directory (at the same level as the game’s `.exe` file). The directory structure after extraction will be roughly as follows:

Game root directory/
├── Game.exe
├── winhttp.dll
└── BepInEx/
    ├── core/
    ├── plugins/
    │   └── AbyssMod/
    └── config/

### 4. Launch the Game

**Launch the game normally** (start it through DMMPlayer). If this is your first time installing BepInEx, it will automatically download patches compatible with the current Unity version during startup. Only a single console window will appear, and it will take just a moment.

> ⚠️ If you're using an accelerator (such as ACGP), red error messages may appear in the console, indicating that direct connection to BepInEx's official website might be unavailable. Please enable a proxy and try again.

### 5. Configuration Files

After the first run, two configuration files will be generated in the `BepInEx\config\` directory:

| File            | Purpose                              |
| -------------- | ------------------------------------ |
| `BepInEx.cfg`  | BepInEx framework configuration (e.g., hiding the console window) |
| `AbyssMod.cfg` | Plugin feature configuration (translation, fonts, mosaic, etc.) |

## ⚙️ Configuration Options

## ⚙️ Configuration Options

### `[General]`

| Configuration Item      | Default Value | Description               |
| ----------------------- | ------------- | -------------------------- |
| `DynamicMosaic`       | `false`       | Whether to enable dynamic mosaic |
| `SoundCaution`         | `false`       | Whether to show volume reminder |
| `VoiceInterruption`   | `false`       | Whether to enable voice interruption |
| `TitleMovie`          | `true`        | Whether to play the title animation |
| `NovelLive2DScale`    | `1.0`         | Scaling factor for story Live2D (range `0.1` to `10.0`) |

### `[Translation]`

| Configuration Item | Options                                      | Default Value                                                                                 | Description                                                              |
|-------------------|----------------------------------------------|---------------------------------------------------------------------------------------------|-------------------------------------------------------------------------|
| `Enabled`         | `true` (enabled), `false` (disabled)          | `true`                                                                                        | Whether to enable in-game story translation                                |
| `CDN`             | Any valid CDN URL address                      | `https://raw.githubusercontent.com/anosu/dotabyss-translation/refs/heads/main/translations` | CDN address for translation data                                           |
| `Language`        | `zh_Hans` (Simplified Chinese)                 | `zh_Hans`                                                                                    | Translation language; `zh_Hans` supports Simplified Chinese                |

### `[Translation.Font]`

| Configuration Item | Default Value               | Description                                        |
| ----------------- | -------------------------- | --------------------------------------------------- |
| `AssetBundlePath` | `AbyssMod/fonts/ttcuyuanj` | Path to the TMP font AssetBundle (relative to the plugin directory or absolute path) |

## 📦 Translation Data

## ⌨️ Keyboard Shortcuts

| Shortcut | Function              |
| ------ | ----------------- |
| `F8`   | Enable/Disable story translation |
| `F9`   | Enable/Disable voice interruption |
| `F10`  | Hot-reload configuration file    |

---

## 📦 Translation Data

The translation files are hosted in a separate repository, independent of the main plugin.

[dotabyss-translation](https://github.com/anosu/dotabyss-translation)

---

## ❓ Frequently Asked Questions

<details>
<summary><b>Red error messages appear in the console at startup</b></summary>
This is usually because BepInEx cannot connect to its official website to download Unity patches. Please enable a proxy/VPN and restart the game.

It could also be that the initialization file was corrupted due to network fluctuations during download. In this case, you can try deleting the Mod file and reinstalling it.

</details>

<details>
<summary><b>How to hide the console window</b></summary>
Edit <code>BepInEx\config\BepInEx.cfg</code>, locate <code>[Logging.Console]</code>, and set <code>Enabled</code> to <code>false</code>.
</details>


### Communities

- QQ Group: [731843659](https://qm.qq.com/q/u80uVbzfNK)

---

> 💬 If you have any issues, you can submit an [Issue](https://github.com/anosu/AbyssMod/issues) or ask directly in the QQ group.
