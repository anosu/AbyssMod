# AbyssMod

<!-- hy-mt2-i18n:start -->
[中文](./README.md) | [English](./README_en.md) | [日本語](./README_ja.md) | **Español**
<!-- hy-mt2-i18n:end -->


> 🎮 MOD de traducción para Abyss

Este repositorio es compatible con la versión de **DMM Game Player en plataforma Windows**.

Si encuentra problemas al usarlo, asegúrese de leer primero las [Preguntas frecuentes](#-常见问题) a continuación.

Canal de feedback en Discord: https://discord.gg/RjsDFuEuBy

## 📋 Índice

- [Características](#-功能特性)
- [Iniciar rápidamente](#-快速开始)
- [Opciones de configuración](#-配置项)
- [Atajos de teclado](#-快捷键)
- [Datos de traducción](#-翻译数据)
- [Preguntas frecuentes](#-常见问题)

## ✨ Funcionalidades

## ✨ Características funcionales

- Traducción de la interfaz y la trama del juego
- Desactivar el mosaico dinámico dentro del juego
- Saltarse la notificación de volumen al iniciar el juego
- Mantener continuos los diálogos de los personajes de la trama
- Desactivar la animación de título al iniciar el juego
- Ajustar el tamaño de los elementos Live2D de la trama con el scroll del ratón manteniendo presionada `Ctrl`

## 🚀 Inicio rápido

## 🚀 Inicio rápido

### 1. Instalar el cliente del juego

Asegúrese de haber instalado la versión del juego para DMM Game Player y conocer la ubicación de la carpeta que contiene el archivo ejecutable del juego.

### 2. Descargar el complemento

Vaya a la página [Releases](https://github.com/anosu/AbyssMod/releases), encuentre la versión más reciente (con la etiqueta verde “Latest”), expanda “Assets” y descargue el paquete comprimido AbyssMod.7z.

> ⚠️ No descargues `Source code`, ese es el código fuente

### 3. Instalación

Descomprima el paquete comprimido en la carpeta raíz del juego (al mismo nivel que el archivo `.exe` del juego). La estructura de carpetas después de la descompresión será más o menos la siguiente:

Directorio raíz del juego/
├── juego.exe
├── winhttp.dll
└── BepInEx/
    ├── core/
    ├── plugins/
    │   └── AbyssMod/
    └── config/
```

### 4. Iniciar el juego

**Ejecutar el juego normalmente** (desde DMMPlayer). Si es la primera vez que instala BepInEx, se descargará automáticamente un parche compatible con la versión actual de Unity al iniciar; solo se mostrará una ventana de consola, y basta esperar unos instantes.

> ⚠️ Si utilizas un acelerador (como ACGP), es posible que aparezcan errores en rojo en la consola, lo que indica que quizás no puedas conectarte directamente al sitio oficial de BepInEx. Intenta nuevamente después de activar el proxy.

### 5. Archivos de configuración

Tras la primera ejecución, se generarán dos archivos de configuración en el directorio `BepInEx\config\`:

| Archivo         | Uso                                 |
| -------------- | ------------------------------------ |
| `BepInEx.cfg`  | Configuración del framework BepInEx (como ocultar la ventana de consola) |
| `AbyssMod.cfg` | Configuración de las funciones del plugin (traducción, fuentes, mosaico, etc.) |

## ⚙️ Parámetros de configuración

## ⚙️ Parámetros de configuración

### `[General]`

| Parámetro de configuración | Valor por defecto | Descripción               |
| --------------------------- | --------------- | ------------------ |
| `DynamicMosaic`            | `false`         | Habilitar el mosaico dinámico |
| `SoundCaution`             | `false`         | Mostrar advertencia de volumen |
| `VoiceInterruption`       | `false`         | Habilitar interrupciones de voz |
| `TitleMovie`                | `true`          | Reproducir la animación de título |
| `NovelLive2DScale`          | `1.0`           | Proporción de escala para Live2D narrativo (rango `0.1` a `10.0`) |

### `[Translation]`

| Parámetro     | Opciones                                      | Valor predeterminado                                                                              | Descripción                                                                    |
| ---------- | -------------------------------------------- | ------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------- |
| `Enabled`  | `true` (activado), `false` (desactivado)       | `true`                                                                                      | Indica si se activa la traducción de la trama dentro del juego.                 |
| `CDN`      | Cualquier dirección URL de CDN válida          | `https://raw.githubusercontent.com/anosu/dotabyss-translation/refs/heads/main/translations` | Dirección del CDN para los datos de traducción.                               |
| `Language` | `zh_Hans` (chino simplificado)                | `zh_Hans`                                                                                   | Idioma de traducción; se admite `zh_Hans` para chino simplificado.             |

### `[Translation.Font]`

| Parámetro de configuración | Valor predeterminado | Descripción                                        |
| ----------------------- | ------------------ | ---------------------------------------------------- |
| `AssetBundlePath`     | `AbyssMod/fonts/ttcuyuanj` | Ruta del AssetBundle de fuentes TMP (ruta relativa al directorio del plugin o ruta absoluta) |

## 📦 Datos de traducción

Los archivos de traducción se almacenan en un repositorio independiente, separado del propio plugin:

[dotabyss-translation](https://github.com/anosu/dotabyss-translation)

## ⌨️ Atajos de teclado

| Atajo     | Función               |
| ---------- | --------------------- |
| `F8`       | Activar/desactivar traducción de la trama |
| `F9`       | Activar/desactivar interrupciones de voz |
| `F10`      | Cargar nuevamente el archivo de configuración |

---

## 📦 Datos de traducción

Los archivos de traducción se almacenan en un repositorio independiente, separado del propio plugin.

[dotabyss-translation](https://github.com/anosu/dotabyss-translation)

---

## ❓ Preguntas frecuentes

<details>
<summary><b>Aparecen errores en rojo en la ventana de la consola al iniciar</b></summary>
Por lo general, se debe a que BepInEx no puede conectarse a su sitio oficial para descargar los parches de Unity; active un proxy o servidor VPN y reinicie el juego.

También podría ser que los archivos de inicialización estén dañados debido a fluctuaciones en la conexión a Internet durante su descarga; en ese caso, intente eliminar los archivos del mod y reinstalarlos.

</details>

<details>
<summary><b>Cómo ocultar la ventana de la consola</b></summary>
Edite <code>BepInEx\config\BepInEx.cfg</code>, busque <code>[Logging.Console]</code> y establezca <code>Enabled</code> en <code>false</code>.
</details>


### Comunidades

- Grupo de QQ: [731843659](https://qm.qq.com/q/u80uVbzfNK)

---

> 💬 Si tienes algún problema, puedes enviar un [Issue](https://github.com/anosu/AbyssMod/issues) o preguntar directamente en el grupo de QQ.
