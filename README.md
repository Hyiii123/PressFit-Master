# PressFit-Master 智能产线多工位精密伺服压装监控与质量判定系统

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)
![WPF](https://img.shields.io/badge/GUI-WPF%20%2F%20MVVM-blue?logo=windows)
![Protocol](https://img.shields.io/badge/Protocol-Modbus%20TCP-orange)
![Database](https://img.shields.io/badge/Database-SQLite%20(WAL)-003B57?logo=sqlite&logoColor=white)
![Architecture](https://img.shields.io/badge/Architecture-Clean%20Architecture-brightgreen)
![License](https://img.shields.io/badge/License-MIT-green)

> **PressFit-Master** 是一套面向新能源汽车三电系统（电机定子/减速器/电池极柱）与精密装备制造的工业级工位上位机系统。采用 **C# (.NET 8) + WPF (MVVM)** 技术栈，实现了工业 Modbus TCP 毫秒级高频数采、异步流水线解耦、全行程位移-压力包络线（Envelope）质量判定、配方与条码追溯，并内置了具备故障注入能力的数字孪生仿真引擎。

---

## 🏗️ 系统架构设计 (Clean Architecture)

项目严格遵循依赖倒置与整洁架构设计原则，杜绝传统上位机“逻辑与界面揉杂”的面条代码，全解耦为 5 个独立工程：

`	ext
PressFit-Master/
├── src/
│   ├── PressFit.Core/              # [核心领域层] 纯业务实体、状态机枚举、包络线算法、接口契约
│   ├── PressFit.Communication/     # [通信协议层] Modbus TCP 原生编解码、自愈重连看门狗、Channels管道
│   ├── PressFit.Data/              # [数据持久层] Dapper + SQLite (WAL 预写日志高性能模式)
│   ├── PressFit.Simulator/         # [数字孪生层] 独立 Modbus 从站 (127.0.0.1:5020) + 物理动力学仿真与故障注入
│   └── PressFit.UI/                # [人机交互层] WPF 现代化暗色工业大屏、MVVM 响应式绑定、实时轨迹渲染
├── PressFitMaster.sln              # 解决方案工程配置
└── README.md                       # 技术白皮书与答辩题库
`

### 异步并发数据流水线拓扑

`	ext
[ 下位机 / 虚拟从站 ]
         │ (Modbus TCP 50Hz 轮询)
         ▼
[ ModbusTcpClientService ] ───(原始字节流解析)───► [ ConnectionWatchdog (心跳保活/指数退避) ]
         │
         ▼ (生产者推入)
┌────────────────────────────────────────────────────────┐
│  .NET 8 System.Threading.Channels (有界无锁环形管道)     │
└────────────────────────────────────────────────────────┘
         │
         ▼ (消费者按批次抽取 60FPS)
 ┌───────┴────────────────────────────┐
 │                                    │
 ▼                                    ▼
[ WPF UI 局部刷新 (零GC卡顿) ]      [ Dapper + SQLite WAL 批量持久化 ]
`

---

## ⚡ 四大核心技术亮点 (面试攻防点)

### 1. 高频数采与 UI 彻底解耦（防界面假死与 GC 抖动）
* **行业痛点**：传统上位机在 50~100Hz 采样率下，频繁使用 Dispatcher.Invoke 更新 ObservableCollection，导致 WPF 主线程卡死、界面掉帧严重，频繁触发垃圾回收（GC Gen 0/1 峰值）。
* **工程解法**：基于 .NET 8 的 System.Threading.Channels 实现了单写单读有界管道（SingleWriter = true, SingleReader = true）。采集线程只负责向管道高效推入；消费线程定时以 60FPS（~16ms）节奏批量抽取 64 点批次局部更新，**将 UI 渲染 CPU 占用压制在 3% 以下，彻底消除 GC 抖动**。

### 2. 工业级自愈通信引擎（Watchdog & Exponential Backoff）
* **行业痛点**：车间现场存在强电磁干扰、网线松动或 PLC 偶发重启，传统 Socket 往往陷入假死或重连死锁。
* **工程解法**：基于四状态机（Disconnected / Connecting / Connected / Degraded）构建了自愈看门狗。断线后自适应启用**指数退避重连算法（1s -> 2s -> 4s -> 8s -> 16s）**，避免网络风暴惊群；内置 500ms 心跳探测，实现网络自愈无感恢复。

### 3. 全行程位移-压力动态包络线（Envelope）质量判定
* **行业痛点**：传统质检仅判断“最终峰值压力”，极易漏判工件倾斜卡滞、壳体微裂或基座虚压。
* **工程解法**：实现了全过程动态包络公差带算法。基于工艺配方（Recipe）自动拟合非线性理论基准阻力曲面，生成动态上下公差边界，实时捕捉轨迹拐点违规（超限过盈、虚压配合、未到位提前卸载），判定耗时 < 5ms。

### 4. 高保真数字孪生仿真引擎与故障注入（零硬件开箱即用）
* **工程解法**：内置了基于原生 TcpListener 的高保真 Modbus Slave 仿真从站。不仅建立了伺服电机的动力学推进与测量白噪声物理模型，还在前台集成了**抽屉式故障注入控制台**：
  * 模拟网络电缆瞬间拔出 (Disconnect)
  * 模拟响应严重超时 (Timeout > 3s)
  * 模拟过盈卡滞压力超限 (Spike NG)
  * 模拟配合过松开裂 (Low NG)
  * 模拟伺服驱动器过载报警 (Alarm 0x5001)

---

## 🎯 面试核心问答速查表 (Cheat Sheet)

> **面试官问：你为什么从 Java 技术栈转到做 C# 上位机？你的优势是什么？**  
> **你的回答**：C# 与 Java 在底层 OOP、泛型、内存模型与垃圾回收上高度同构，且 .NET 8 的值类型内存优化（Span<T>, ValueTask, Channels）在工控高实时场景极具优势。更重要的是，国内很多传统上位机停留在 WinForms 单体和面条代码阶段；而我带来了**企业级软件架构素养**——严格的领域驱动分层、依赖倒置、无锁并发管道和健壮的状态机设计，能够开发出可长期无故障运行、易维护的大型工业软件。

> **面试官问：Modbus TCP 的 MBAP 报文头结构是怎样的？粘包半包如何处理？**  
> **你的回答**：MBAP 头共 7 个字节：事务元标识符（Transaction ID，2B）、协议标识符（Protocol ID = 0，2B）、后续长度（Length，2B）、单元标识符（Unit ID，1B）。在实现底层客户端时，因为 Modbus TCP 报文头部已经明确给出了后随数据长度 Length，我采用了**“先定长读取 7 字节头部 -> 解析出 Payload 长度 -> 循环读取直至达到精确字节数”**的状态机流式解析方案，从根本上杜绝了 Socket 粘包和半包问题。

> **面试官问：SQLite 在工业高频写库时会不会锁库导致上位机卡顿？**  
> **你的回答**：默认的 SQLite 使用回滚日志（Rollback Journal），在写事务时会加排他锁阻塞读操作。我在系统初始化时强制开启了 **WAL（Write-Ahead Logging）预写日志模式**（PRAGMA journal_mode = WAL;）与非严格同步（PRAGMA synchronous = NORMAL;）。在 WAL 模式下，写操作追加写入 wal 文件，读操作直接读取主库，**读写完全并发互不阻塞**，轻松承受每秒上百次压装结果归档。

---

## 🚀 快速启动指南

### 环境依赖
* Windows 10 / 11 操作系统
* [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
* Visual Studio 2022 (推荐) 或 Rider / VS Code

### 编译与运行
`ash
# 1. 克隆代码仓库
git clone https://github.com/Hyiii123/PressFit-Master.git
cd PressFit-Master

# 2. 编译整个解决方案
dotnet build PressFitMaster.sln

# 3. 运行上位机系统
dotnet run --project src/PressFit.UI/PressFit.UI.csproj
`

### 一分钟演示体验
1. 软件启动后，**内置虚拟从站已默认在后台 5020 端口启动并自动连接**。
2. 在左侧面板选择配方（如 REC-MOTOR-101 新能源驱动电机定子压装），点击 **“▶ 触发启动压装周期”**。
3. 中间区域将以 60FPS 实时绘制位移-压力轨迹，全行程自动比对公差包络线，周期结束后底部显示 **PASS (OK)**，同时将数据归档至本地 SQLite。
4. 展开底部抽屉勾选 **“⚠️ 注入过盈卡滞超限 (Spike NG)”**，再次点击启动，系统将实时拦截超限点并判定为 **NG**！
