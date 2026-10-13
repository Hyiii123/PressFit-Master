# PressFit-Master 智能产线多工位精密伺服压装监控与质量判定系统

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)
![WPF](https://img.shields.io/badge/GUI-WPF%20%2F%20MVVM-blue?logo=windows)
![Protocol](https://img.shields.io/badge/Protocol-Modbus%20TCP-orange)
![Database](https://img.shields.io/badge/Database-SQLite%20(WAL)-003B57?logo=sqlite&logoColor=white)
![Architecture](https://img.shields.io/badge/Architecture-Clean%20Architecture-brightgreen)
![Tests](https://img.shields.io/badge/Tests-xUnit%20100%25%20Pass-success)
![License](https://img.shields.io/badge/License-MIT-green)

> **PressFit-Master** 是一套面向新能源汽车三电系统（电机定子/减速器/电池极柱）与精密装备制造的工业级工位上位机系统。采用 **C# (.NET 8) + WPF (MVVM)** 技术栈，实现了工业 Modbus TCP 毫秒级高频数采、异步流水线解耦、全行程位移-压力包络线（Envelope）质量判定、工业多级权限（RBAC）、配方管理与参数下发、历史曲线复盘追溯、CSV 报表导出，并内置了数字孪生仿真引擎与 xUnit 自动化测试工程。

---

## 🏗️ 系统架构设计 (Clean Architecture)

项目严格遵循依赖倒置与整洁架构设计原则，解耦为 5 个核心模块与 1 个自动化测试工程：

```text
PressFit-Master/
├── src/
│   ├── PressFit.Core/              # [核心领域层] 纯业务实体、状态机枚举、包络线算法、用户角色权限契约
│   ├── PressFit.Communication/     # [通信协议层] Modbus TCP 原生编解码、自愈重连看门狗、Channels 管道
│   ├── PressFit.Data/              # [数据持久层] Dapper + SQLite (WAL 高性能模式)、CSV 报表导出器
│   ├── PressFit.Simulator/         # [数字孪生层] 独立 Modbus 从站 (127.0.0.1:5020) + 动力学拟合与故障注入
│   └── PressFit.UI/                # [人机交互层] WPF 现代化暗色工业大屏、MVVM 响应式绑定、曲线复盘与配方编辑器
├── tests/
│   └── PressFit.Tests/             # [单元测试层] xUnit 测试工程，覆盖核心算法、边界公差判定与协议映射
├── PressFitMaster.sln              # 统一解决方案配置
└── README.md                       # 技术白皮书与答辩题库
```

---

## ⚡ 六大核心技术亮点 (面试攻防与演示)

### 1. 高频数采与 UI 彻底解耦（防界面假死与 GC 抖动）
* 基于 .NET 8 的 `System.Threading.Channels` 实现了单写单读有界管道（`SingleWriter = true, SingleReader = true`）。
* 50Hz 底层高频采集与 60FPS UI 渲染完全解耦，批量抽帧局部刷新，CPU 占用率低于 3%，杜绝 GC 内存抖动。

### 2. 工业级自愈通信引擎（Watchdog & Exponential Backoff）
* 基于四状态机（`Disconnected` / `Connecting` / `Connected` / `Degraded`）构建了自愈看门狗。
* 断线后自适应启用**指数退避重连算法（1s -> 2s -> 4s -> 8s -> 16s）**，避免网络风暴惊群；内置心跳探测，实现网线重连自愈无感恢复。

### 3. 全行程位移-压力动态包络线（Envelope）质量判定
* 突破传统“仅看最终压力”的局限，基于工艺配方（Recipe）实时拟合动态上下公差边界。
* 全行程实时捕捉过盈卡滞、虚压配合、未到位提前卸载与轨迹违规拐点，判定耗时 `< 5ms`。

### 4. 生产履历追溯与历史位移-压力曲线复盘回放 (Traceability & Replay)
* 生产记录（含全过程数百个采样点 JSON）异步落盘本地 SQLite（WAL 预写日志无锁并发模式）。
* 提供独立的**“生产履历追溯与曲线复盘系统”**：支持按日期范围、条码关键字检索，**点击任意历史记录即可 1:1 重新渲染出当时的物理压装轨迹与包络线，并高亮诊断超限原因**。
* 支持一键将检索记录导出为带 UTF-8 BOM 的标准 **CSV 质检报表**，供 Excel 无缝打开。

### 5. 工业三级角色权限与操作防呆 (RBAC) + 配方在线标定下发
* **产线操作员 (Operator)**：免密操作，仅允许扫码、选择配方与触发启停，配方编辑权限强力防呆置灰。
* **工艺工程师 (Engineer)**：密码授权（默认 `1234`），拥有**工艺配方管理控制台**，支持在线修改压深、公差带与压力阈值，并支持**通过 Modbus 0x10 功能码一键将新配方下发至下位机寄存器**。
* **系统管理员 (Administrator)**：密码授权（默认 `8888`），最高管理权限。

### 6. 自动化 xUnit 单元测试保障 (`PressFit.Tests`)
* 针对核心公差带算法与协议映射编写了全面的单元测试：
  - `Evaluate_WhenTrajectoryConforms_ReturnsOK`（标准平滑轨迹合格判定）
  - `Evaluate_WhenPeakForceExceedsMaxLimit_ReturnsNG`（压力超限过盈卡滞拦截）
  - `Evaluate_WhenPeakForceBelowMinLimit_ReturnsNG`（压力偏低虚压拦截）
  - `Evaluate_WhenFinalDisplacementShort_ReturnsNG`（未到位提前停机拦截）
  - `ModbusAddressMap_RegistersAndCoils_AreConfiguredCorrectly`（寄存器地址契约校验）

---

## 🎯 面试核心问答速查表 (Cheat Sheet)

> **面试官问：数据存进 SQLite 后，现场质检员（QA）怎么调出历史记录？能看当时的压装曲线吗？**  
> **你的回答**：系统在落盘时不仅存储了最大压力、终点位移等元数据，还将采样轨迹时序点压缩序列化保存。我们在上位机中开发了**全过程追溯与曲线复盘回放引擎**，品保人员可以随时按日期和工件条码查询，点击任意一行记录即可在右侧画布上以 1:1 坐标系完整复原当时的“位移-压力”物理曲线，并叠加显示参考包络线公差带，准确展示异常发生的拐点位置。同时还支持一键导出符合质检台账标准的 CSV 报表。

> **面试官问：车间普通工人如果误改了工艺参数导致大批量报废怎么办？**  
> **你的回答**：系统严格遵循了工业自动化**三级角色权限控制（RBAC）与操作防呆**机制。默认处于免密操作员权限，操作员只能扫码与启动，所有配方修改与下发按钮全部处于不可触达状态。只有工艺工程师输入授权密码后，才能进入配方标定控制台调整公差，并在下发前执行参数上下限逻辑互斥校验，彻底杜绝违规参数下发至设备。

> **面试官问：你们有做自动化测试吗？如何保证核心算法不会因代码迭代出 Bug？**  
> **你的回答**：解决方案中独立包含 `PressFit.Tests` 测试工程，使用 xUnit 对核心的 `EnvelopeDetector` 包络线算法进行了全覆盖单元测试。我们构建了理论合格曲线、峰值超限波形、虚压波形、行程不足波形等多种边界工况数据集进行自动化断言，保证在没有现场真机的情况下，质检算法与协议映射始终 100% 可靠。

---

## 🚀 快速启动与测试

```bash
# 1. 运行所有自动化单元测试 (100% 通过)
dotnet test PressFitMaster.sln

# 2. 编译并启动 WPF 上位机主程序
dotnet run --project src/PressFit.UI/PressFit.UI.csproj
```
