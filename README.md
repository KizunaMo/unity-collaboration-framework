# Unity Collaboration Framework

一個專為 Unity 開發者設計的綜合性框架，提供進階 C# 設計模式、XLua 整合、Shader 工具和性能優化解決方案。

## 🎯 設計理念

本框架基於以下核心原則構建：
- **模組化架構**: 每個組件都可以獨立使用，遵循開放封閉原則
- **性能優先**: 所有組件都經過性能測試和優化
- **可維護性**: 清楚的命名約定和完整的文檔
- **漸進式學習**: 從基礎概念到進階應用的完整學習路徑

## 📦 安裝方式

### Unity Package Manager 安裝

1. 打開Unity，進入 Package Manager
2. 點擊左上角的 "+" 按鈕
3. 選擇 "Add package from git URL"
4. 輸入: `https://github.com/KizunaMo/unity-collaboration-framework.git`

### 或者作為 Git Submodule

```bash
git submodule add https://github.com/KizunaMo/unity-collaboration-framework.git Assets/Plugins/CollaborationFramework
```

## 🏗️ 框架結構

```
├── Core/                    # 核心架構組件
│   ├── Patterns/           # 設計模式實作
│   ├── EventSystem/        # 高性能事件系統
│   └── Utilities/          # 通用工具類
├── XLua/                   # XLua 整合模組
│   ├── Bindings/          # C# 與 Lua 綁定
│   ├── Examples/          # 使用範例
│   └── Utilities/         # XLua 工具類
├── Shaders/               # Shader 開發工具
│   ├── Common/            # 通用 Shader 函數
│   ├── Examples/          # Shader 範例
│   └── Tools/             # Shader 開發工具
├── Performance/           # 性能優化工具
│   ├── Profiling/         # 性能分析工具
│   ├── Memory/            # 記憶體管理
│   └── Optimization/      # 優化建議
└── Examples/              # 完整應用範例
    ├── BasicUsage/        # 基礎使用範例
    ├── AdvancedPatterns/  # 進階模式範例
    └── Integration/       # 整合應用範例
```

## 🚀 快速開始

### 基礎事件系統使用

```csharp
// 註冊事件監聽
EventManager.Instance.Subscribe<PlayerHealthChanged>(OnPlayerHealthChanged);

// 發布事件
EventManager.Instance.Publish(new PlayerHealthChanged(newHealth));
```

### XLua 整合範例

```csharp
// C# 端註冊 Lua 可調用的方法
LuaBridge.RegisterFunction("GetPlayerPosition", GetPlayerPosition);

// Lua 端調用 C# 方法
-- local playerPos = GetPlayerPosition()
```

## 📚 文檔和教學

- [核心架構指南](Documentation/Core-Architecture.md)
- [XLua 整合教學](Documentation/XLua-Integration.md)
- [Shader 開發指南](Documentation/Shader-Development.md)
- [性能優化策略](Documentation/Performance-Optimization.md)

## 🤝 貢獻指南

本框架採用協作開發模式，歡迎提供反饋和建議。請參考 [貢獻指南](CONTRIBUTING.md) 了解詳細資訊。

## 📄 授權

MIT License - 詳見 [LICENSE](LICENSE) 文件
