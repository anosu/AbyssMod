# 构建与工程维护

项目入口：`src/AbyssMod/AbyssMod.csproj`。标准方案：`AbyssMod.slnx`。

## 准备

安装 global.json 指定的 .NET SDK、.NET 8 测试运行时、Python 3.10+、PowerShell 7。VS 使用 2022 17.14 或更新版本。

```sh
git submodule update --init --recursive
python shared/ModEngineering/scripts/mod.py check
python shared/ModEngineering/scripts/mod.py test
python shared/ModEngineering/scripts/mod.py build --configuration Debug
python shared/ModEngineering/scripts/mod.py build --configuration Release
```

引用只使用游戏/加载器必要 DLL；具体资源和游戏差异见 [dependencies](../dependencies/README.md)（若项目未提供该文件，以项目的 Reference 声明为准）。完整游戏导出和本机路径不提交。

## 本地共享源码联调

标准方案包含仓库固定的共享项目。需要编辑同级 Utility 时，将 `SharedDependencies.local.props.example` 复制为忽略 Git 的 `SharedDependencies.local.props`，调整路径后运行：

```sh
python shared/ModEngineering/scripts/mod.py solution --local
```

打开生成的 `AbyssMod.local.slnx`。已有本地覆盖不应被模板覆盖。CI 和 `UsePinnedSharedDependencies=true` 总是使用固定源码；标准 VS 方案也忽略本地覆盖。无需复制共享 DLL。

## 规范与升级

`mod.json` 是项目工程清单，声明平台、项目、测试及发行文件。重复构建逻辑来自固定的 ModEngineering 子模块。生成文件改动应在公共实现或清单中完成，然后执行 `mod.py sync`；`mod.py check` 检测漂移和格式问题。

更新工程用 `mod.py update --revision <commit>`，更新运行库增加 `--dependency Utility`。更新后验证并提交子模块指针。

详见 [公共规范](../shared/ModEngineering/docs/CONVENTIONS.md)。

## PC 构建与部署

将 Build.local.props.example 复制为 Build.local.props，填写 GameInteropDir 或 GameDir。纯编译只需 PC interop，默认输出 artifacts/bin/<项目>/<配置>/；不会写入游戏目录。

需要安装到本机游戏时运行 `scripts/deploy.ps1`，该命令构建并将 Mod/Utility DLL 复制到原有插件布局。字体、BepInEx 和其他安装资源仍按游戏文档管理。PC CI 检查规范和独立测试，不自动发布，也不依赖本机游戏进行完整编译。
