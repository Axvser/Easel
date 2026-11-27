# IndustrialWorkbench · 工业控件工作台

面向工业 / 上位机（HMI / SCADA）场景的 Easel XAML 示例。与 `XamlGallery` 的“目录”定位不同，它是一块可复用的**工业控件墙**：首页同时运行绝大多数控件，左侧目录可选中任一控件查看大图、属性、交互与 XAML 用法。

纯 WinForms + Easel，不引用 WPF；一个窗口一个 `VisualHost`，一条视觉树。

## 运行

```powershell
dotnet build Demo/IndustrialWorkbench.csproj -c Debug
dotnet run --project Demo/IndustrialWorkbench.csproj -c Debug
```

开发用开关：

| 开关 | 作用 |
|---|---|
| `--smoke` | 窗口显示 1.2 秒后自动关闭，用于 CI 冒烟 |
| `--capture <dir>` | 截图 `overview.png` 后关闭 |
| `--capture <dir> --entry <id>` | 截图某个控件详情页，如 `--entry control.knob` |
| `--selftest <dir>` | 注入指针拖动旋钮、点击拨杆/阀门/报警条，写 `selftest.txt` 并关闭 |

## 结构

```
Demo/
  App.xaml(.cs)                应用资源（工业色板 + StartupTheme）与入口
  MainForm.xaml(.cs)           Window 根，DataContext = WorkbenchViewModel
  WorkbenchShell.xaml(.cs)     顶栏 + 左侧目录 TreeView + 中间 PageHost
  OverviewView.xaml(.cs)       固定设计尺寸的控件墙，由 Viewbox 等比缩放
  ControlDetailView.xaml(.cs)  大图 + 实时属性列 + 可复制 XAML 片段
  Views/Controls/*View.xaml    每个控件的大尺寸实例
  Controls/                    自绘控件（GDI+，继承 VisualElement）
  Controls/StatusTile.xaml     XAML + code-behind 复合控件
  ViewModels/                  仿真模型、目录、ObservableObject / RelayCommand
```

## 控件清单

每个自绘控件都用 `DependencyProperty.Register(..., new PropertyMetadata(...) { AffectsRender = true })` 注册属性，可在 XAML 中直接赋值或绑定。

| 控件 | 类型 | 关键 DP | 交互 |
|---|---|---|---|
| `CircularGauge` | 自绘 | `Value/Minimum/Maximum/WarnThreshold/DangerThreshold/Unit/Title/Accent` | 只读 |
| `LedLamp` | 自绘 | `IsOn/Blink/LampColor/Label/Phase` | 只读 |
| `SevenSegmentDisplay` | 自绘 | `Value/Digits/Decimals/Unit/SegmentBrush` | 只读 |
| `TankLevel` | 自绘 | `Value/LowLimit/HighLimit/LiquidBrush/Phase` | 只读 |
| `PipeFlow` | 自绘 | `IsFlowing/FlowRate/Direction/Phase` | 只读 |
| `TrendChart` | 自绘 | `SeriesA/SeriesB(double[])/Minimum/Maximum` | 只读 |
| `AlarmBanner` | 自绘 | `Message/Severity/IsActive/IsAcknowledged/Count/Phase` | 点击确认（`Acknowledged`） |
| `Knob` | 自绘 | `Value/Minimum/Maximum/Unit/Title` | 拖动 / 滚轮 / 方向键 |
| `LinearGauge` | 自绘 | `Value/WarnThreshold/DangerThreshold/Unit/Title` | 只读 |
| `MotorFan` | 自绘 | `Angle/Speed/IsRunning/Label` | 只读 |
| `Valve` | 自绘 | `IsOpen/Title/OpenBrush/ClosedBrush` | 点击切换（`Toggled`） |
| `ToggleSwitch` | 自绘 | `IsOn/Title/OnBrush/OffBrush` | 点击 / 空格（`Toggled`） |
| `HazardStripe` | 自绘 | `Phase/StripeBrush/StripeWidth/Angle` | 只读装饰 |
| `StatusTile` | XAML + code-behind | `Title/Value/Caption/IsOn/LampBrush` | 只读复合卡片 |

## 仿真与交互

`WorkbenchViewModel` 用一个 150 ms 的 `System.Windows.Forms.Timer` 平滑推进压力、温度、流量、罐位、风扇角度与趋势样本。旋钮 `Setpoint`、拨杆 `PumpOn`、阀门 `ValveOpen` 通过 `Mode=TwoWay` 写回模型并影响后续仿真（例如关闭阀门后流量归零）。

自绘控件的值变了只失效自己（`AffectsRender`），不重绘整棵树。首页控件墙用固定设计尺寸（1056 宽）包在 `Viewbox` 里，任何窗口尺寸都能完整看到全部控件；详情页则按可用空间铺开。

## 已知取舍

- 控件墙在很小的窗口里会整体缩小以保持“同时可见”，文字随之变小；需要看清细节时进入详情页。
- 报警队列是演示用的循环队列，点击 `ACK` 后短暂显示“已确认”，随后自动产生下一条。
- `TrendChart` 的序列是每次 tick 新建的 `double[]`，实现简单，不追求零分配。
