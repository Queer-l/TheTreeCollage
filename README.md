# TheTreeCollage

基于 Unity 2D 的剧情驱动型冒险解谜游戏。玩家扮演主角**小明**，在乡村环境中探索、调查并收集线索，逐步揭开围绕"树神/古神"的神秘故事。游戏包含多分支剧情、体力调查系统和打地鼠小游戏。

## 游戏玩法

- **剧情推进**：通过顺序对话和分支选择推进故事，玩家的选择会影响剧情走向和最终结局。
- **调查与线索**：消耗体力进行调查行动，解锁线索。线索可用于解锁后续分支选项。
- **体力与天数系统**：每天拥有 3 点体力上限，调查、获取线索、进入下一天均消耗体力。进入下一天后体力自动恢复。
- **打地鼠小游戏**：通过小游戏获取分数和额外线索奖励，支持简单/困难两种难度。
- **多结局**：根据玩家选择，剧情走向**真结局**、**普通结局**或**坏结局**。

## 主要角色

| 编号 | 角色 | 说明 |
|------|------|------|
| 0 | 小明 | 主角 |
| 1 | 父亲 | 小明的父亲 |
| 2 | 母亲 | 小明的母亲 |
| 3 | 陈爷爷/陈工 | 村中长者 |
| 4 | 陈奶奶 | 村中长者 |
| 5 | 陈春柏 | 陈家人 |
| 6 | 高中历史老师 | 提供历史背景 |
| 7 | 卫生所年轻医生 | 为小明提供帮助 |
| 8 | 疯疯癫癫的道士 | 神秘人物 |
| 9 | 村民群像 | 村庄的集体声音 |
| 10 | 旁白 | 叙述者 |
| 11 | 树神/古神 | 核心神秘角色 |

## 技术架构

### 核心技术

- **引擎**：Unity 2022.3+ (2D 模板)
- **语言**：C#
- **UI 框架**：UGUI + TextMeshPro
- **数据存储**：JSON 本地存档（3 个存档位）

### 主要系统

| 系统 | 核心脚本 | 功能 |
|------|----------|------|
| 剧情系统 | `StoryManager`, `StoryNodeSO` | 管理分支对话、角色立绘、CG 展示、前后对话导航 |
| 线索系统 | `ClueManager`, `ClueSO`, `ClueSlot`, `ClueInfoUI` | 线索收集、展示、条件判断 |
| 玩家数据 | `PlayerData` | 单例运行时数据（体力、天数、剧情进度、存档读档） |
| 日流程 | `DayManager`, `PlayerManager` | 体力消耗、调查行动、天数推进 |
| 小游戏 | `WhackGameController`, `WhackTarget`, `WhackGameConfigSO` | 打地鼠玩法、难度配置、分数奖励 |
| 主菜单 | `MainMenuManager`, `SaveSelectUI` | 新游戏、读档、关于我们 |
| UI 控制 | `UIControl` | 通用弹窗/面板打开关闭管理 |
| 编辑器工具 | `StoryNodeSOGeneratorWindow`, `StoryNodeDesignImporter`, `ClueSOGeneratorWindow` | 批量生成剧情节点和线索资产 |

### 项目结构

```
Assets/
├── 脚本/
│   ├── Editor/          # 编辑器扩展工具
│   ├── Item/            # 线索系统（ClueSO, ClueManager, ClueSlot）
│   ├── LittleGame/      # 打地鼠小游戏
│   ├── Player/          # 玩家数据与日流程
│   ├── UI/              # 主菜单、剧情、线索 UI 管理
│   └── 剧情线/          # 剧情节点 ScriptableObject 定义
├── Prefabs/             # UI 预制体（按钮、物品槽、小游戏 UI）
├── SOS/                 # ScriptableObject 剧情数据资产
└── Scenes/              # 游戏场景
```

### 存档系统

- 存档格式：JSON 文件
- 存档位置：`Application.persistentDataPath`
- 存档槽位：3 个（`1.json`, `2.json`, `3.json`）
- 存档内容：体力、天数、分数、已解锁线索 ID、剧情进度、剧情历史、分支标记

## 快速开始

1. 使用 Unity Hub 打开项目（需要 Unity 2022.3 或更高版本）
2. 在 Unity 中打开 `MainMenu` 场景
3. 点击 Play 运行游戏
4. 在主菜单中选择"开始游戏"或"读取存档"

## 剧情编辑

项目提供了编辑器工具用于快速创建剧情内容：

- **StoryNodeSO 生成器**：在 `Assets > Create > Game > Story Node` 创建剧情节点资产
- **ClueSO 生成器**：在 `Assets > Create > Game > ClueSO` 创建线索资产
- 剧情节点通过 `nodeID` 串联，支持顺序跳转（`nextNodeID`）和分支跳转（`choices`）
- 分支选项可配置**线索条件**（`requiredClue`）和**剧情标记条件**（`requiredFlag`），实现条件分支
