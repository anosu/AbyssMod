# 构建 AbyssMod（PC）

本项目使用 Windows 版 BepInEx IL2CPP。需要 .NET 8 或更新的 SDK，以及对应 PC 游戏由 BepInEx 生成的 interop DLL；游戏启动仍使用 BepInEx 自带的 .NET 6 运行时。

## 首次配置

```powershell
git clone --recurse-submodules https://github.com/anosu/AbyssMod.git
cd AbyssMod
Copy-Item Build.local.props.example Build.local.props
dotnet tool restore
```

在 `Build.local.props` 中填写 `GameDir`。先通过 BepInEx 启动一次游戏，确保生成 `BepInEx/interop`；也可以设置 `GameInteropDir` 指向单独保存的 **PC** interop 目录。完整游戏 DLL 不进入 Git。

## 共享源码

默认使用 `shared/Utility` 子模块中固定提交的源码，通过 `ProjectReference` 编译并复制 `Utility.dll`。PC 项目不依赖 Android Extension。

本地同时开发 Utility 时，执行：

```powershell
Copy-Item SharedDependencies.local.props.example SharedDependencies.local.props
```

示例指向同级的 `../Utility/Utility/Utility.csproj`，可按实际路径修改。之后修改 Utility 直接重新构建 Mod 即可，无需手动复制 DLL。两个 `*.local.props` 文件都忽略 Git 追踪。

```powershell
dotnet build AbyssMod/AbyssMod.csproj -c Release
# 忽略本地 Utility 源码覆盖，验证仓库中固定的依赖版本
dotnet build AbyssMod/AbyssMod.csproj -c Release -p:UsePinnedSharedDependencies=true
```

CI 忽略两个本地配置文件；在构建命令中显式传入 `-p:GameDir=...` 和必要的 `-p:GameInteropDir=...`。Utility 编译使用其仓库自带的最小 Unity 编译依赖，运行时使用游戏的 BepInEx interop。

共享库中间文件和输出隔离在本仓库 `artifacts/shared/local` 或 `artifacts/shared/pinned`，不会与其他 Mod 同时构建冲突。

## 输出与发布

保留原有输出位置：`$(GameDir)/BepInEx/plugins/AbyssMod/<Configuration>/net6.0/`。其中包含 Mod 和本次编译的 Utility DLL。

验证构建而不写入游戏目录时：

```powershell
dotnet build AbyssMod/AbyssMod.csproj -c Release -p:BaseOutputPath=../artifacts/build-check/
```

发布内容、字体资源、BepInEx 文件和打包方式继续由本项目单独维护。GitHub Actions 仅检查格式并运行不依赖游戏的翻译测试，不打包或发布，也不在云端下载游戏文件。

## 更新共享库版本

先在 Utility 仓库提交并推送修改，然后在本仓库更新固定提交：

```powershell
git -C shared/Utility fetch origin
git -C shared/Utility checkout <Utility提交SHA>
git add shared/Utility
git commit -m "Update Utility"
git push
```

协作者拉取后执行 `git submodule update --init --recursive`。仅修改本地覆盖路径不会更新仓库固定版本。

## 格式化

三个 PC 项目统一使用 CSharpier 1.3.0：4 空格、100 列、LF 换行。子模块、依赖 DLL、构建产物和本地配置不参与格式化。

```powershell
dotnet tool restore
dotnet csharpier format .
dotnet csharpier check .
dotnet test tests/AbyssMod.Tests/AbyssMod.Tests.csproj -c Release
```

## Visual Studio

标准解决方案已包含固定版本的 Utility 项目。在 VS 中打开标准解决方案时，不应用 `SharedDependencies.local.props` 的本地源码覆盖。

如果需要在 VS 中同时修改同级 Utility，先配置 `SharedDependencies.local.props`，然后在本仓库运行：

```powershell
pwsh -NoProfile -File shared/Utility/scripts/New-ModSolution.ps1 -Project AbyssMod/AbyssMod.csproj
```

打开生成的 `AbyssMod.local.slnx`（需要 VS 2022 17.14 或更新版本）。它包含实际引用的共享项目，忽略 Git 追踪；修改共享项目路径后重新运行该命令。不要只向标准解决方案添加本机路径后提交。

VS 使用共享项目自身的 `bin` / `obj` 输出，以保证解决方案构建和项目引用查找一致；命令行项目构建继续使用本仓库 `artifacts/shared` 下的隔离目录。
