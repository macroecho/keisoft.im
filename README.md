<h1 align="center">Keisoft.IM —— 高性能 .NET 即时通讯底座</h1>

<p align="center">
  <strong>「海量连接，毫秒必达」 —— 为 .NET 重新定义实时通信的性能边界</strong>
</p>
<p align="center">
<a href="https://dotnet.microsoft.com/"><img src="https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet&logoColor=white" alt=".NET 9"></a>
<a href="https://learn.microsoft.com/dotnet/core/deploying/native-aot/"><img src="https://img.shields.io/badge/AOT-Enabled-5C2D91?style=flat&logo=.net&logoColor=white" alt="AOT"></a>
<a href="https://www.mysql.com"><img src="https://img.shields.io/badge/MySQL-005C84?logo=mysql&logoColor=white" alt="MySQL"></a>
<a href="https://www.microsoft.com/zh-cn/sql-server"><img src="https://img.shields.io/badge/SQL%20Server-2022-CC2927?logo=microsoft%20sql%20server&logoColor=white" alt="SQLServer"></a>
<a href="https://redis.io"><img src="https://img.shields.io/badge/Redis-DD0031?logo=redis&logoColor=white" alt="Redis"></a>
<img src="https://img.shields.io/badge/Docker-Enabled-2496ED?logo=docker&logoColor=white" alt="Docker">
<a href="https://nginx.org"><img src="https://img.shields.io/badge/nginx-009639?logo=nginx&logoColor=white" alt="Nginx"></a>
<a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-green.svg" alt="License: MIT"></a>
<a href="https://github.com/macroecho/keisoft.im/stargazers"><img src="https://img.shields.io/github/stars/macroecho/keisoft.im?style=social" alt="Stars"></a>
</p>


**简体中文** | [English](README_en.md)

----



## 📖 项目简介

&emsp;&emsp;**Keisoft.IM** 是一款专注于处理**海量 TCP 长连接**、**心跳保活**、**高性能消息路由与转发**的底层通信系统。本项目作为纯粹的通信底座，**不包含任何业务代码**，旨在为上层应用提供极致的连接管理和消息传输能力。系统采用分层解耦设计，分为 **WebAPI 层**（负责离线消息与状态同步）和 **TCP 长连接层**（负责连接维持与实时推送）。**TCP 长连接层**深度参考了 **Netty** 的核心设计哲学，彻底摒弃了 .NET 原生 `Socket` 在处理高并发时的弊端（如频繁上下文切换、锁竞争严重），能够轻松支撑 **10万+ 并发连接**，消息延迟控制在 **1-100ms**。

&emsp;&emsp;本项目始于 **2018 年**，最初作为 **[KeiChat](#-相关仓库)** 项目的底层通信引擎同步研发。在开源之前，**Keisoft.IM** 长期作为企业内部通信工具的核心驱动引擎，支撑着数千名企业员工日常的即时消息收发、文件传输与在线协作。**Keisoft.IM**并非一蹴而就的半成品，而是经过生产环境反复打磨、数十次性能调优和架构迭代，才造就了今天这个稳定、高效、久经考验的通信底座。

> Keisoft.IM 专注于海量 TCP 长连接治理 · 心跳保活 · 消息路由转发 · 零业务逻辑耦合 · 🧩 业务系统可基于此底座进行二次构建。

---



## 🏗️ 系统架构概览

项目由两个核心模块组成：

```text
.
├── WebAPI              # 🌐 HTTP 接口服务（离线消息 & 同步校验）
└── TcpServer           # 📡 TCP 长连接服务（通信核心）
```

![整体架构图](Images/overall-architecture.jpg)

---



## 🌐 WebAPI —— 离线消息与同步校验

WebAPI 不参与实时通信，仅提供辅助能力：

1. **主要职责**
   
   - 📥 离线消息拉取（Pull 模式）。
   
   - 🔁 消息同步校验（防丢失、防乱序） 。
   
   - 🧾 消息状态确认（已送达 / 已同步）。
   
2. **设计原则**
   
   - 无状态设计，易于横向扩展。
   - 仅与存储层交互，不感知连接。
   - 为弱网 / 断线重连场景提供最终一致性保障。

----



## 📡 TCP —— 高性能  TCP 长连接服务

基于 **.NET 9** 控制台程序，并采用 **AOT（Ahead-of-Time，预先编译）** 编译模式发布为 **AOT 原生镜像**，即将 C# 源码通过 Roslyn 编译为 IL 后，再由 Native AOT 编译器（ILC）直接编译为特定平台的原生机器码，生成独立的、无需依赖 .NET 运行时的可执行文件。

![TCP 架构图](Images/tcp-architecture.jpg)

#### 💡 TCP 服务在底层设计上深度参考了 Netty 框架的核心思想，并引入了 Libuv 跨平台异步 I/O 库

1. **Reactor 线程模型**
   
   - **Boss 线程（Main Reactor）**：基于 Libuv 的 `uv_tcp_t` 监听句柄，专职接受客户端连接。连接建立后，通过 `uv_accept` 获取新套接字，并以轮询或一致性哈希策略注册到 Worker 线程。
   - **Worker 线程池（Sub Reactor）**：每个 Worker 运行独立的 Libuv Event Loop，绑定一组客户端连接，负责该连接的全部 I/O 读写、心跳检测、协议编解码。线程数默认与 CPU 核心数对齐，最大化多核利用率，避免上下文切换开销。
   
2. **零拷贝缓冲区管理**
   
   - 使用 `ArrayPool<byte>` 预分配内存池，结合 Libuv 的 `uv_buf_t` 直接引用托管内存的固定区域（通过 `GCHandle` 固定），避免非托管拷贝。
   - 利用 .NET `Pipe` 管道在编解码阶段实现零拷贝消息流转（`ReadOnlySequence<byte>`）。
   - 缓冲区按连接生命周期自动伸缩，突发流量下不分配新内存。
   
3. **高效事件驱动**
   
   - Libuv Event Loop 基于操作系统最优就绪通知机制（Linux epoll / macOS kqueue / Windows IOCP），事件触发即处理，无空轮询。
   - 文件类阻塞操作（如 Journal 日志写入）自动卸载到 Libuv 内部线程池，I/O 事件循环永不阻塞。
   - 避免为每个连接创建独立线程，内存占用恒定。
   
   

#### 🛡️  消息过滤

在即时通讯系统中，内容安全是不可逾越的底线，但深度风控逻辑（如语义分析、上下文理解、外部 API 调用）往往耗时较高，若同步执行会直接拖慢消息投递延迟。为此，**Keisoft.IM.Server** 设计了一套**两阶段消息过滤机制**，在**安全**与**性能**之间取得精准平衡：**Keisoft.IM.Server** 内仅执行轻量级敏感词扫描，重逻辑风控异步解耦，既保住了消息投递的极低延迟，又不放掉任何安全隐患。

1. **第一阶段：TCP 服务内轻量级敏感词扫描（同步，延迟敏感）**

   消息经由路由引擎确定目标后，在写入发送队列之前，先经过轻量级过滤器：

   - **算法**：基于 **双数组 Trie（Double-Array Trie）** 构建敏感词字典树，对消息内容进行 `O(n)` 线性扫描，时间复杂度与文本长度成正比，与词典大小无关。
   - **性能**：单次扫描耗时稳定在 **微秒级（μs）**，在 Worker 线程内同步执行，不阻塞 I/O 事件循环，不影响端到端延迟。
   - **拦截动作**：命中违规词时，消息根据敏感词库的 `actionType` 执行具体动作，然后丢到异步队列中上报到风控系统。

2. **第二阶段：异步深度风控（异步，安全兜底）**

   对于需要复杂逻辑判断的内容，由独立的风控服务集群异步消费处理：

   - **解耦**：风控服务与 TCP 服务完全解耦，可独立扩缩容，不影响长连接主路径。
   - **深度分析**：风控服务执行 NLP 语义分析、正则引擎、上下文关联、第三方安全 API 调用等重逻辑。
   - **事后处置**：若判定违规，对用户执行禁言/封禁等操作。

3. **配置文件示例**

   ``` json
   {
      // ... 省略
       
     // 消息过滤配置选项。
     "MessageFilterOptions": {
       // 敏感词过滤配置选项。
       "SensitiveOptions": {
         // 启用敏感词扫描功能为 true。
         "IsEnabled": true,
         // 启用本地扫描为 true。
         // 1.设为 true，先在本地根据敏感词库扫描一次，根据敏感词库要求执行具体动作，然后异步上报给远程服务。
         // 2.设为 false，每次都需要调用接口去扫描，然后等接口返回结果，这样性能可能会下降。
         "IsUseLocalScan": true,
   
         /* 
          * 加载敏感词的接口地址（GET 请求方式）。
          * 这个接口自定义，需要遵守以下规则：
          * 
          * 1.标准的 RESTful API，成功 HTTP 状态码为 200，并返回：
          * 	[{"wordText":"敏感词","matchType": 1,"actionType":1}] 无需再次包装 code、message、data 等结构。
          *  失败 HTTP 状态码非 200，并返回：
          *	{"code": "errorCode", "message": "具体错误消息"}
          * 
          * 2.参数说明：
          * 	matchType: 1=精确、2=模糊,、3=正则
          * 	actionType: 0=无动作、1=记录、2=替换、4=拦截
          * 	组合动作使用位运算：None = 0, Record = 1 << 0, Replace = 1 << 1, Block = 1 << 2，例如：actionType = Record | Replace;
         */
         "LoadUrl": "http://localhost:6006/sensitiveWord/list?X-API-KEY=osaViiBrVTKgF04HlLRBXX4ETy5I2s56",
         "LoadApiKye": "osaViiBrVTKgF04HlLRBXX4ETy5I2s56",
   
         /* 
          * 扫描敏感词的接口地址（POST 请求方式）。
          * 这个接口自定义，需要遵守以下规则：
          * 
          * 1.请求数据格式为“application/json”，示例：
          *	{"userId":10000,"sourceId": "10200900","sourceType":1,"sourceContent":"消息内容","clientIp":"127.0.0.1"}
          * 
          * 2.请求参数说明：
          * 	userId: 用户编号
          * 	sourceId: 消息编号
          * 	sourceType: 来源类型，1=私聊、2=群聊
          * 	sourceContent: 消息内容
          * 	clientIp: 客户端网络地址
          *
          * 3.返回要求，成功 HTTP 状态码为 200，并返回：{"action":2,"filteredText":"你***"}
          * 失败 HTTP 状态码非 200，并返回：{"code": "errorCode", "message": "具体错误消息"}
          * 
          * 4.返回参数说明：
          * 	action: 执行的动作，0=无动作、1=记录、2=替换、4=拦截
          * 	filteredText: 过滤后的内容
         */
         "ScanUrl": "http://localhost:6006/riskScanner/sensitiveScan?X-API-KEY=osaViiBrVTKgF04HlLRBXX4ETy5I2s56",
         "ScanApiKye": "osaViiBrVTKgF04HlLRBXX4ETy5I2s56"
       }
     }
   }
   ```

   

#### 📨 消息路由

1. 基于内存维护全局连接路由表（UserId → ServerInstance → WorkerId）。
2. 消息到达后，路由引擎查询目标连接所在节点，投递到用户回话的消息队列中，然后交给**消息分发**去处理。
3. 目标客户端不在线时，消息自动转入离线存储，上线后通过 HTTP 主动拉取离线消息。



#### 📤 消息分发

在海量长连接场景下，消息从路由引擎确定目标连接后，如何高效、公平的推送到客户端，是决定系统整体延迟和稳定性的关键。TCP 服务提供了两种消息分发模式，并引入了基于客户端处理能力的自适应流控机制，确保慢速客户端不会拖累整体服务性能。

1. **模式一：轮询分发（Round-Robin Dispatching）**

   - **公平调度**：在多个目标客户端之间采用轮询策略，依次将消息写入各个连接的发送队列，确保每个客户端都能公平地获得消息处理机会，避免某个客户端因连接顺序靠后而长期饥饿。
   - **无差别推送**：假设所有客户端具有均等的处理能力，按照迭代顺序进行分发，实现简单高效。

2. **模式二：最大活跃数策略分发（Maximum Active Count Dispatching）**

   在真实生产环境中，客户端的网络状况和处理能力差异巨大。如果服务端以统一速率向所有客户端推送消息，网络不佳或处理能力弱的客户端会导致服务端发送缓冲区积压，引发频繁的 GC 或线程阻塞，进而影响其他正常客户端的消息到达率（即“头部阻塞” Head-of-Line Blocking 问题）。

   为此，我们参考了 Netty 的 `ChannelOutboundBuffer` 与水位线（WriteBufferWaterMark）流控思想，实现了**三级自适应速率分发机制**：

   - **客户端能力评估维度**：
     1. TCP 发送缓冲区水位（Socket Send Buffer 占用率）。
     2. 消息推送后的客户端 ACK 延迟（RTT）。
     3. 心跳响应时间。
     4. 连续发送超时/失败次数。
     5. Channel 的 `IsWritable` 可写状态。
   - **三级队列隔离（Fast / Medium / Slow Tracks）**：
     1. 🟢 **快速队列 (Fast Track)**：客户端网络良好，处理能力强，发送缓冲区长期处于低位。系统以最大速率（甚至批量合并）向其推送消息，端到端延迟低至 1-30ms，让正常客户端享受极速体验。
     2. 🟡 **中速队列 (Medium Track)**：客户端出现偶发性网络抖动，发送缓冲区开始积压。系统自动降低该客户端的推送速率，减少单次推送的消息数量，给予客户端缓冲时间，防止其进一步恶化。
     3. 🔴 **慢速队列 (Slow Track)**：客户端网络极差或应用层卡顿，发送缓冲区持续高位（或频繁触发 TCP 零窗口）。系统将其移入慢速队列，大幅降低推送频率，甚至暂时挂起发送（背压机制 Backpressure），直到客户端缓冲区释放或心跳恢复正常，从而**不影响正常客户端的消息分发**。
   - **动态升降级**：
     1. 客户端的状态并非一成不变。系统通过后台探测线程实时监控每个连接的指标变化。
     2. 当慢速客户端网络恢复、缓冲区排空后，自动从慢速队列升级至中速或快速队列，恢复正常的消息接收速率。
     3. 这种机制实现了完美的**性能隔离（Performance Isolation）**，确保“慢速客户端不会拖累快速客户端”。

3. **配置文件示例**

   ``` json
   {
     // ... 省略
       
     // 消息分发配置选项
     "MessageDispatchOptions": {
       // 分发模式
       // RoundRobin: 轮训的消息分发。
       // UnFair: 
       //    根据客户端处理消息的能力来决定分发速度。
       //    由快速、中速、慢速分发组成， 客户端处理消息快的会放到快速队列中，让整正常的客户端能及时收到消息，
       //    客户端消息处理慢（说明网络不佳）将会放到慢速队列中，从而不影响正常的客户端分发消息。
       "Model": "UnFair",
       // UnFair 快速间隔（单位：秒）。
       "MinInterval": 5,
       // UnFair 中速间隔（单位：秒）。
       "MediumInterval": 15,
       // UnFair 慢速间隔（单位：秒）。
       "MaxInterval": 25
     }
   }
   ```




#### 📬 消息可靠投递与防丢失机制（TCP + HTTP 协同）

在即时通讯系统中，网络闪断、客户端崩溃、服务端重启等异常不可避免。为确保消息在客户端与服务端之间**不丢失、不重复、有序到达**，**Keisoft.IM** 构建了一套 **应用层可靠投递协议**，将 TCP 长连接的实时性与 HTTP 短连接的可靠性补偿完美结合。

1. **TCP 实时通道：应用层 ACK 确认**

   TCP 传输层虽能保证数据包的可靠传输，但无法感知应用层的消息处理状态。因此，系统在应用层引入了端到端的确认机制：

   - **推送与确认**：服务端通过 TCP 长连接将消息推送给客户端后，客户端必须在指定时间内返回**应用层 ACK**（包含消息的唯一序列号）。
   - **超时重传**：若服务端在超时窗口内未收到 ACK，会根据三级队列分发机制重新推送（可能降低速率）。连续多次未确认，则判定客户端离线或异常，触发消息离线转储。
   - **去重机制**：客户端根据消息唯一序列号进行幂等处理，防止网络抖动导致的重复推送。

2. **HTTP 补偿通道：离线拉取与同步校验**

   当 TCP 连接不可用（客户端离线、网络切换、服务端重启）时，HTTP 接口成为消息可靠性的最后保障：

   - **离线消息拉取**：客户端上线后，首先通过 HTTP 接口拉取离线消息（基于消息序列号增量拉取），确保不遗漏任何历史消息。
   - **消息同步校验**：客户端上报本地已接收消息的最大序列号，以及上一次同步的序列号到当前最大消息序列号的消息总数量，比对服务端存储的序列号、消息数量，返回缺失的消息列表。这确保了即使 TCP 通道短暂中断，消息也能通过 HTTP 通道最终补齐。
   - **确认删除**：客户端成功处理离线消息后，可通知 HTTP 服务器删除已拉取的离线记录（客户端上报的最大消息序列号做为条件），防止重复下发。




#### 💾 消息存储

在消息持久化方面提供了同步和异步高可靠的存储方案，确保消息在路由转发过程中的可靠性与系统的高性能。在数据库写入之前引入 Journal 文件作为缓冲层。支持 **MySQL** 和 **SQL Server** 两种关系型数据库，可通过配置文件 `RepositoryOptions:Type` 自由切换。

1. **同步与异步存储模式**

   - **同步存储**：消息到达后立即写入数据库，保证强一致性，但会增加消息处理延迟，适用于对数据可靠性要求极高的特殊场景。
   - **异步存储（默认）**：
     1. **Journal 文件**：消息首先以追加写（Append-Only）的方式顺序写入本地磁盘的 Journal 文件。顺序写磁盘的性能极高，几乎不影响消息处理的主流程。
     2. **批量异步入库**：后台线程定期（默认 60s）或当 Journal 文件积累到一定批次（默认 5000 条）时，批量将消息写入 MySQL 或 SQL Server。
     3. **崩溃恢复**：若服务意外宕机，重启时系统会自动读取 Journal 文件中未入库的消息，重新进行持久化，确保消息零丢失。

2. **群聊消息存储**

   在大规模即时通讯系统中，群聊消息的存储面临**写扩散**与**读扩散**的经典架构取舍：

   - **写扩散模型**：每条群聊消息向群内每个成员写入一条离线消息记录。优点在于读取时只需一次查询即可获取全部消息；但在大群（如 500 人、2000 人）场景下，单条消息会触发数百甚至上千次数据库写入操作，产生严重的**写放大**问题，数据库 IOPS 压力剧增，成为系统瓶颈。
   - **读扩散模型**：群聊消息仅作为**一条群消息**持久化到数据库，同时维护群消息的**引用计数**与**时间戳 (TimeStamp)**。当客户端拉取群消息时，服务端根据客户端已同步的消息编号和成员入群的时间戳，从群消息表中读取增量消息并实时分发给群内在线成员。
   - 所以 **Keisoft.IM** 在群聊消息存储上采用了**读扩散模型**。

3. **配置文件示例** 

   ``` json
   {
     // ... 省略
   
     "RepositoryOptions": {
       // 可选：MySql、SqlServer
       "Type": "MySql",
       // MySql
       "MySqlOptions": {
         "ConnectionString": "Server=127.0.0.1;Port=3306;Uid=root;Pwd=example@123;DataBase=keisoft_im;CharSet=utf8mb4;",
         // 消息序号起始值。如果迁移了数据库，这个值必须大于历史数据中的消息序号。
         "StartSequenceNumber": 0
       },
       // SqlServer
       "SqlServerOptions": {
         "ConnectionString": "Data Source=.;Initial Catalog=Keisoft.IM;Uid=example;Pwd=example@123;TrustServerCertificate=true;",
         // 消息序号起始值。如果迁移了数据库，这个值必须大于历史数据中的消息序号。
         "StartSequenceNumber": 735000
       }
     },
     "MessageStorageOptions": {
       // 可选：Sync、Async
       // Sync: 直接持久化到数据库中，落库成功后就返回消息发送成功。
       // Async: 先存储到 data/msg.db 磁盘文件中确保消息不丢失，然后再异步批量持久化到数据库，从而减轻数据库压力。
       "Model": "Async",
       // 多久批量存储一次数据库（单位：秒）。
       "Interval": 60,
       // 或者消息超过 5000 条存储一次数据库。
       "MaxNumber": 5000,
       // 使用 AES-GCM 加密存储消息为 true。
       "UseEncrypt": true,
       // AES-GCM 密钥（128-bit / 16 bytes）。
       "SecretKey": "CGkJri5mbIo9VGxogWwVig=="
     }
   }
   ```

----



## 🧪 开发环境要求

- Windows 10/11
- .NET 9 SDK
- Visual Studio 2022+

---



##  🚀 一键部署（Docker Compose）

#### 1. 快速启动

- 📦 安装 [Docker](https://www.docker.com/) 与 [Docker Compose](https://docs.docker.com/compose/)

- 💡 确保默认的`TCP 通讯服务` `18168` 端口未被占用（也可以在 docker-compose.yml 重新指定新的端口）。

- ⚠️ 部署前必改 `docker-compose.yml` 以下配置：

  - `TcpServiceAddress__0`: 设置 TCP 通讯服务的外网地址。

  ```bash
  # 1. 克隆仓库
  git clone https://github.com/macroecho/keisoft.im.git
  cd Keisoft.IM
  
  # 2.解压 Keisoft.IM.Server
  tar -xzf Keisoft.IM.Server/x64-1.0.8.tar.gz -C Keisoft.IM.Server
  
  # 3. 一键启动所有服务（后台运行）
  docker compose up -d
  ```

#### 2. Nginx 配置 (`nginx.conf`)

- ⚠️ 修改 `Keisoft.IM.Http/nginx.conf` 等配置文件的域名或证书，然后再添加到 `nginx.conf`中。

  ```nginx
  http
  {
      server
      {
      	listen 888;
        	# ... 省略
      }
      
      # ... 省略
      # 加载 keisoft.IM 的配置文件
      include /www/wwwroot/Keisoft.IM/Keisoft.IM.Http/nginx.conf;
  }
  ```

---



## 🧭 客户端使用指南

**Keisoft.IM** 提供了 .NET 客户端 SDK `Keisoft.IM.Client`，可通过 NuGet 快速集成到的项目中。

1. **安装 NuGet 包**

   ```  c#
   dotnet add package Keisoft.IM.Client
   ```

2. **建立 TCP 长连接**

   ``` c#
   var clientOptions = new ClientOptions
   {
       // 这里填写 Keisoft.IM.Http 网关地址，如果是 http 默认 im、https 默认 ims
       ServerHost = "ims://example.com", 
       // Keisoft.IM.Http 网关端口，如果是 Http 默认 80、Https 默认 443。（除自定义端口以外）
       ServerPort = 18169, 
       // 实现 Keisoft.IM.Client.Logger.ILogger 接口编写一个记录日志的类。
       Logger = new IMServiceLogger(), 
       // 存储本地消息的数据库文件路径。
       DbPath = "msg.db", 
       // 存储本地消息的数据库密码。
       DbPassword = "example", 
       // 默认消息过滤器。
       MessageContentFilter = new SimpleMessageContentFilter(), 
       // 用于 TLS 加密通信的公钥证书，由 Keisoft.IM.Server 绑定的 PFX 证书（.pfx）导出，两者属于同一密钥对。
       Certificate = X509CertificateLoader.LoadCertificateFromFile("im.crt")
   };
   
   // 初始化。
   IMClient.Init(clientOptions); 
   // 连接响应事件。
   IMClient.SetOnConnection(response => { }); 
   // 接收服务推送过来的消息集合。
   IMClient.SetOnReceiveMessage(messageItems => { }); 
   
   // 连接 Keisoft.IM.Server 服务。
   // userId: 导入到 Keisoft.IM 系统的用户编号。
   // token: 用户身份令牌，一般由业务系统的登录接口返回。后端业务系统的登录接口使用 Keisoft.IM.Http 中的 /IdentityRpc/Token 接口生成令牌。
   await IMClient.ConnectAsync(userId: 10000, token: "202CB962AC59075B964B07152D234B70");
   ```

3. **发送消息**

   ``` c#
   // 全局监听消息发送结果，就不用在发送的时候传递一个回调函数了。
   // IMClient.SetOnSendMessageResponse(messageItems => { });
   
   var subType = (byte)MessageSubTypeEnum.TextMessage;
   // 构建文本消息。
   var sendMessage = await IMClient.MakeMessageAsync(to: 10001, type: MessageType.Private, subType: subType, content: "text content");
   // 发送消息到服务上。
   await IMClient.SendMessageAndFlushAsync(sendMessage, response =>
   {
       // 发送状态 response.Status
   });
   ```


----



## ⚖️使用条款与免责声明

**1.合法使用**：本系统的使用受以下条件限制：您确认并同意仅将本系统用于合法目的，并遵守所有适用的法律法规，包括但不限于您所在司法管辖区、行为发生地及目标影响地的相关法律。您不得利用本系统从事任何侵犯他人合法权益、违反法律规定或危害网络安全的行为。

> 若您违反上述任何条件，本授权立即终止，您必须立即停止使用本系统并删除所有相关副本。因您违反本声明导致的任何法律责任，由您自行承担，本项目维护者保留追究法律责任的权利。

**2.开源性质**：本项目以开源形式提供，供学习、研究与技术交流之用。开发者不对本系统的功能完整性、安全性、稳定性或适用性作任何明示或暗示的保证。

**3.无担保声明**：在法律允许的最大范围内，开发者不对因使用或无法使用本系统所导致的任何直接、间接、附带、特殊或后果性损害承担责任，包括但不限于数据丢失、业务中断或其他商业损害。

**4.风险自担**：您理解并同意：使用本系统所产生的所有风险由您自行承担，包括但不限于系统漏洞、第三方依赖风险及运行环境风险。

**5.知识产权**：本系统及相关代码、文档的知识产权归**原作者所有**。未经书面许可，不得将本系统用于商业目的或擅自篡改、再分发。

---



## 💬 技术交流群

欢迎加入 KeiChat 技术交流群，一起交流 **.NET、WPF、Avalonia、Xamarin、WebRTC、实时音视频通信、即时通讯架构、多进程设计** 等话题 👇

![QQ 群](Images/qq.png)

- **QQ 群号**：283798566
- **入群方式**：扫码加入或通过 QQ 搜索群号申请加入
- **交流内容**：.NET、C#、实时音视频通信、即时通讯架构、项目 Issue 讨论

> 💡 没有 QQ 的朋友也可以在 GitHub Issues 中留言交流。

---



## 📄 许可证

MIT License

---

<p align="center">
  ⭐ 如果 Keisoft.IM 对你学习即时通讯有帮助，欢迎 Star 支持！
</p>

