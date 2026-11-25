# 项目备忘（万方商贸管家 / 方可送货单软件）

## 构建与发布

```bash
export PATH=$HOME/dotnet:$PATH DOTNET_NOLOGO=1
dotnet publish src/FangKeDelivery/FangKeDelivery.csproj -c Release \
  -r win-x64 --self-contained true -p:PublishSingleFile=true -o dist
```

产物 `dist/WanfangTrade.exe`（约 67 MB，自包含单文件）。命名空间 `FangKeDelivery` 是历史遗留，不要改，改了会断数据目录迁移逻辑。

## Wine 下验证（无桌面环境必须用 xvfb-run）

```bash
export WINEDEBUG=-all WINEPREFIX=$HOME/.wine XDG_RUNTIME_DIR=/tmp/xdg
xvfb-run -a wine WanfangTrade.exe --selftest  <dir>   # 业务逻辑
xvfb-run -a wine WanfangTrade.exe --uitest     <dir>   # 真实控件流程
xvfb-run -a wine WanfangTrade.exe --dump-layout <dir>  # 布局缺陷
xvfb-run -a wine WanfangTrade.exe --demo               # 造演示单据
xvfb-run -a wine WanfangTrade.exe --shot       <dir>   # 真实抓屏
```

**顺序很重要**：`--selftest` 会清库。要检查预览页/审核页这类需要数据的窗口，必须 `--demo` 之后再跑，否则预览页会被跳过（现在会显式打印 SKIPPED + 汇总行）。

## 踩过的坑

- **WinForms 控件不设 `Dock` 时默认 200×100**。打印预览主图容器就栽在这，整页单据被裁掉大半。
- **`FlowLayoutPanel` 里控件自带 3px `Margin`**。34px 高的按钮放进 34px 高容器会溢出 6px，必须显式清零。
- **页面缓存复用必须配刷新**。`MainForm.ShowPage` 缓存页面实例，不重读数据就会一直显示旧内容。
- **布局校验要能自证覆盖范围**。曾经空库时静默跳过预览页，报告少一行却全绿，看起来和全过一样。校验器必须显式报告跳过项 + 总数。
- **绝对定位的标签容易和输入框重叠**。标签宽度只包住文字时右对齐，文字位置会和输入框左边缘对不上。统一用「满宽 + `TextAlign=MiddleLeft`」。
- `dist/lay*/` 等测试输出目录用通配忽略，`--dump-layout` 的目录名不固定。
- **缺中文字体时中文全渲染成豆腐块**，且不同联次（如「红联：客户联」「黄联：结算联」，字数相同）渲染结果**逐像素完全相同**，自检会报「三联内容应各不相同」。这是环境问题不是代码问题，装 `fonts-wqy-zenhei` 即可。判断方法：两张渲染图 hash 相同，且差异 bbox 只有几像素宽。
- **联次模式的布尔量语义要一次看对**。`ThreePly=true` 曾表示「走三遍纸」，读代码时极易看反，正是「打一次出三张」的成因。现已改名 `CarbonCopy`（true = 复写纸 = 走一遍纸）。
- **默认值决定老用户升级后是否还犯同样的错**。设置键缺失时的 fallback 必须等于修复后的期望行为，否则用户升级了照样打三遍。自检里有一条专门建「老库」（无新键）验证默认值。

## 提交与发布

- 开发分支 `release/v1.3.0`，PR #1 指向 `main`。
- 发版：`gh release create <tag> dist/WanfangTrade.exe --notes-file <md> --target release/v1.3.0`
- 换资源要先 `gh release delete-asset <tag> <name> -y` 再 `gh release upload`。
- 发布后务必下载回来核对 SHA256，并在干净目录跑一遍自检——本地通过不等于上传无损。
