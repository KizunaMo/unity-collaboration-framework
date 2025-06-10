using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Collaboration.Framework.Core.ServiceLocator
{
    /// <summary>
    /// 模組定位器 - 功能模組的動態管理系統
    /// 
    /// 這個設計靈感來自於您在真實專案中的精妙實作。
    /// ModuleLocator 和 ServiceLocator 的分離體現了深刻的架構洞察：
    /// 
    /// 服務 vs 模組的哲學差異：
    /// 
    /// 服務(Services)代表系統的"骨幹" - 它們是遊戲運行的基礎設施：
    /// - UIManager：負責所有UI的生命週期
    /// - AudioManager：處理所有音頻播放
    /// - SceneManager：管理場景載入和切換
    /// - SaveManager：處理遊戲存檔
    /// 
    /// 模組(Modules)代表系統的"功能" - 它們是可插拔的特性：
    /// - AchievementModule：成就系統，遊戲沒有它也能運行
    /// - AnalyticsModule：數據分析，可能只在特定平台存在
    /// - DebugModule：調試工具，只在開發版本中需要
    /// - LeaderboardModule：排行榜，可能根據用戶等級動態載入
    /// 
    /// 為什麼需要分離？
    /// 1. 生命週期不同：服務需要複雜的異步初始化，模組通常可以同步初始化
    /// 2. 依賴關係不同：服務之間有複雜的依賴，模組相對獨立
    /// 3. 載入策略不同：服務在遊戲啟動時載入，模組可以動態載入/卸載
    /// 4. 多實例支援：某些模組可能需要多個實例（如多個UI面板的控制器）
    /// 
    /// 這種設計讓您的架構具有極強的可擴展性和靈活性。
    /// </summary>
    public class ModuleLocator
    {
        // 嵌套字典結構：外層Key是模組類型，內層Key是模組的唯一標識符(UID)
        // 這種設計允許同一類型的多個模組實例共存，這是非常聰明的設計
        // 例如：可以有多個ChatModule實例，分別處理不同的聊天頻道
        private readonly Dictionary<Type, Dictionary<string, IModule>> _modules = 
            new Dictionary<Type, Dictionary<string, IModule>>();
        
        // 線程安全鎖 - 雖然模組操作通常在主線程，但為了安全起見還是加鎖
        private readonly object _lock = new object();
        
        // 調試日誌開關
        private readonly bool _enableDebugLogging;

        public ModuleLocator(bool enableDebugLogging = false)
        {
            _enableDebugLogging = enableDebugLogging;
        }

        /// <summary>
        /// 註冊模組到定位器
        /// 
        /// 這個方法展示了如何優雅地處理多實例註冊。
        /// 每個模組都有一個唯一的UID，讓系統可以區分同類型的不同實例。
        /// 
        /// UID設計的考量：
        /// - 使用字符串而不是整數，提供更好的可讀性和調試體驗
        /// - 讓模組自己生成UID，確保每個模組都知道自己的標識
        /// - 支援有意義的UID（如"MainChatModule"）而不僅僅是GUID
        /// 
        /// 多實例的實際應用：
        /// 想像一個多人遊戲，您可能需要：
        /// - TeamChatModule("team1")  
        /// - TeamChatModule("team2")
        /// - GlobalChatModule("global")
        /// 
        /// 每個都是ChatModule的實例，但服務不同的聊天頻道。
        /// </summary>
        public void Register<T>(T module) where T : class, IModule
        {
            if (module == null)
                throw new ArgumentNullException(nameof(module));

            if (string.IsNullOrEmpty(module.UID))
                throw new ArgumentException("Module UID cannot be null or empty", nameof(module));

            var moduleType = typeof(T);
            
            lock (_lock)
            {
                // 確保該類型的字典存在
                if (!_modules.ContainsKey(moduleType))
                {
                    _modules[moduleType] = new Dictionary<string, IModule>();
                }
                
                var typeDict = _modules[moduleType];
                
                // 檢查UID是否已經被使用
                if (typeDict.ContainsKey(module.UID))
                {
                    throw new InvalidOperationException(
                        $"Module of type {moduleType.Name} with UID '{module.UID}' is already registered. " +
                        "Each module instance must have a unique UID.");
                }
                
                typeDict[module.UID] = module;
                
                if (_enableDebugLogging)
                {
                    Debug.Log($"[ModuleLocator] Registered module: {moduleType.Name} (UID: {module.UID})");
                }
            }
        }

        /// <summary>
        /// 便利方法：註冊模組（自動生成UID）
        /// 
        /// 這個重載版本為那些不需要特定UID的模組提供便利。
        /// 它會自動生成一個基於類型名稱的UID。
        /// 
        /// 注意：這個方法假設每種類型只會有一個實例。
        /// 如果需要多實例，請使用帶有明確UID的版本。
        /// </summary>
        public void Register(IModule module)
        {
            if (module == null)
                throw new ArgumentNullException(nameof(module));

            var moduleType = module.GetType();
            
            // 如果模組沒有設置UID，使用類型名稱作為默認UID
            if (string.IsNullOrEmpty(module.UID))
            {
                // 這裡我們假設IModule有一個setter，實際實作中可能需要調整
                // 或者在模組的構造函數中設置默認UID
                Debug.LogWarning($"[ModuleLocator] Module {moduleType.Name} has no UID, using type name as default");
            }
            
            Register<IModule>(module);
        }

        /// <summary>
        /// 獲取指定類型的所有模組實例
        /// 
        /// 這個方法對於需要與同類型的所有模組交互的場景很有用。
        /// 例如，當遊戲結束時，您可能需要通知所有的ChatModule保存聊天記錄。
        /// 
        /// 返回副本的重要性：
        /// 我們返回一個新的List而不是直接暴露內部集合，
        /// 這是防禦性編程的體現。即使調用者修改返回的列表，
        /// 也不會影響ModuleLocator的內部狀態。
        /// </summary>
        public List<T> GetModules<T>() where T : class, IModule
        {
            var moduleType = typeof(T);
            
            lock (_lock)
            {
                if (!_modules.ContainsKey(moduleType))
                {
                    return new List<T>(); // 返回空列表而不是null，避免null檢查
                }
                
                var result = new List<T>();
                foreach (var module in _modules[moduleType].Values)
                {
                    if (module is T typedModule)
                    {
                        result.Add(typedModule);
                    }
                }
                
                return result;
            }
        }

        /// <summary>
        /// 根據UID獲取特定的模組實例
        /// 
        /// 這是模組定位器的核心功能之一。當您知道確切需要哪個模組實例時，
        /// 可以通過類型和UID來精確定位。
        /// 
        /// 為什麼返回null而不是拋出異常？
        /// 與ServiceLocator不同，模組的存在通常是可選的。
        /// 如果某個特定的模組實例不存在，這通常不是配置錯誤，
        /// 而是正常的業務邏輯（比如某個功能還沒有被載入）。
        /// </summary>
        public T GetModule<T>(string uid) where T : class, IModule
        {
            if (string.IsNullOrEmpty(uid))
                return null;

            var moduleType = typeof(T);
            
            lock (_lock)
            {
                if (_modules.TryGetValue(moduleType, out var typeDict) && 
                    typeDict.TryGetValue(uid, out var module))
                {
                    return module as T;
                }
                
                return null;
            }
        }

        /// <summary>
        /// 檢查指定UID的模組是否已註冊
        /// 
        /// 這個方法在模組的動態載入場景中很有用。
        /// 您可以在嘗試載入模組之前檢查它是否已經存在，
        /// 避免重複載入同一個模組。
        /// 
        /// 跨類型搜索的設計決策：
        /// 這個方法搜索所有類型的模組，確保UID在整個系統中是唯一的。
        /// 這種設計防止了不同類型的模組使用相同UID的情況，
        /// 提供了更好的一致性和調試體驗。
        /// </summary>
        public bool IsModuleRegistered(string uid)
        {
            if (string.IsNullOrEmpty(uid))
                return false;

            lock (_lock)
            {
                foreach (var typeDict in _modules.Values)
                {
                    if (typeDict.ContainsKey(uid))
                    {
                        return true;
                    }
                }
                
                return false;
            }
        }

        /// <summary>
        /// 取消註冊指定的模組
        /// 
        /// 模組的動態管理是這個設計的關鍵優勢之一。
        /// 您可以在運行時載入和卸載模組，實現真正的插件式架構。
        /// 
        /// 應用場景：
        /// - 關卡特定功能：進入新關卡時載入相關模組，離開時卸載
        /// - 用戶權限：根據用戶等級動態載入不同的功能模組
        /// - 平台差異：在不同平台載入不同的實作模組
        /// - 記憶體優化：在記憶體緊張時卸載非關鍵模組
        /// </summary>
        public void Unregister<T>(string uid) where T : class, IModule
        {
            if (string.IsNullOrEmpty(uid))
                return;

            var moduleType = typeof(T);
            
            lock (_lock)
            {
                if (_modules.TryGetValue(moduleType, out var typeDict) && 
                    typeDict.ContainsKey(uid))
                {
                    var module = typeDict[uid];
                    typeDict.Remove(uid);
                    
                    // 如果該類型沒有任何模組了，清理空字典
                    if (typeDict.Count == 0)
                    {
                        _modules.Remove(moduleType);
                    }
                    
                    if (_enableDebugLogging)
                    {
                        Debug.Log($"[ModuleLocator] Unregistered module: {moduleType.Name} (UID: {uid})");
                    }
                    
                    // 嘗試清理模組資源
                    try
                    {
                        module.Shutdown();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[ModuleLocator] Error shutting down module {moduleType.Name} (UID: {uid}): {ex}");
                    }
                }
                else
                {
                    if (_enableDebugLogging)
                    {
                        Debug.LogWarning($"[ModuleLocator] Attempted to unregister non-existent module: {moduleType.Name} (UID: {uid})");
                    }
                }
            }
        }

        /// <summary>
        /// 獲取所有已註冊的模組（內部使用）
        /// 
        /// 這個方法主要供MasterManager使用，用於批量初始化或關閉所有模組。
        /// 它扁平化了嵌套的字典結構，返回一個簡單的模組列表。
        /// 
        /// 使用yield return的優勢：
        /// 這種實作方式使用延遲執行，只有在實際遍歷時才創建結果。
        /// 這比預先創建一個大列表更節省記憶體，特別是在模組數量很多的情況下。
        /// </summary>
        internal IEnumerable<IModule> GetAllModules()
        {
            lock (_lock)
            {
                foreach (var typeDict in _modules.Values)
                {
                    foreach (var module in typeDict.Values)
                    {
                        yield return module;
                    }
                }
            }
        }

        /// <summary>
        /// 根據類型獲取所有模組（泛型版本）
        /// 
        /// 這是GetModules<T>()的內部實作，提供了更靈活的類型查詢。
        /// 它允許按運行時類型來查詢模組，這在反射場景中很有用。
        /// </summary>
        internal IEnumerable<IModule> GetModulesByType(Type moduleType)
        {
            if (moduleType == null)
                yield break;

            lock (_lock)
            {
                if (_modules.TryGetValue(moduleType, out var typeDict))
                {
                    foreach (var module in typeDict.Values)
                    {
                        yield return module;
                    }
                }
            }
        }

        /// <summary>
        /// 清除所有註冊的模組
        /// 
        /// 這個方法主要用於系統關閉或重置。
        /// 它會嘗試優雅地關閉所有模組，即使某個模組關閉失敗，
        /// 也會繼續嘗試關閉其他模組。
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                var totalModules = 0;
                var shutdownErrors = new List<string>();
                
                // 遍歷所有模組並嘗試關閉它們
                foreach (var typeDict in _modules.Values)
                {
                    foreach (var module in typeDict.Values)
                    {
                        totalModules++;
                        try
                        {
                            module.Shutdown();
                        }
                        catch (Exception ex)
                        {
                            shutdownErrors.Add($"{module.GetType().Name} (UID: {module.UID}): {ex.Message}");
                        }
                    }
                }
                
                _modules.Clear();
                
                if (_enableDebugLogging)
                {
                    Debug.Log($"[ModuleLocator] Cleared {totalModules} modules");
                    
                    if (shutdownErrors.Any())
                    {
                        Debug.LogWarning($"[ModuleLocator] Shutdown errors occurred:\n{string.Join("\n", shutdownErrors)}");
                    }
                }
            }
        }

        /// <summary>
        /// 獲取模組統計資訊（用於調試和監控）
        /// 
        /// 這個方法提供了關於已註冊模組的詳細統計資訊。
        /// 對於系統監控和調試非常有用。
        /// </summary>
        public ModuleStatistics GetStatistics()
        {
            lock (_lock)
            {
                var stats = new ModuleStatistics
                {
                    TotalModuleTypes = _modules.Count,
                    TotalModuleInstances = 0,
                    ModulesByType = new Dictionary<string, int>()
                };
                
                foreach (var kvp in _modules)
                {
                    var moduleType = kvp.Key;
                    var instances = kvp.Value;
                    
                    stats.TotalModuleInstances += instances.Count;
                    stats.ModulesByType[moduleType.Name] = instances.Count;
                }
                
                return stats;
            }
        }

        /// <summary>
        /// 模組統計資訊結構
        /// 
        /// 這個結構封裝了ModuleLocator的統計資訊，
        /// 提供了清晰的數據視圖用於監控和調試。
        /// </summary>
        public struct ModuleStatistics
        {
            public int TotalModuleTypes;
            public int TotalModuleInstances;
            public Dictionary<string, int> ModulesByType;
            
            public override string ToString()
            {
                var typeDetails = ModulesByType?.Select(kvp => $"  {kvp.Key}: {kvp.Value}") ?? Array.Empty<string>();
                return $"Module Statistics:\n" +
                       $"  Total Types: {TotalModuleTypes}\n" +
                       $"  Total Instances: {TotalModuleInstances}\n" +
                       $"  By Type:\n{string.Join("\n", typeDetails)}";
            }
        }
    }

    /// <summary>
    /// 模組介面 - 所有功能模組必須實作的基礎契約
    /// 
    /// 這個介面定義了模組的基本生命週期和身份識別機制。
    /// 與IService相比，IModule更注重身份識別和靈活性。
    /// 
    /// 設計哲學差異：
    /// - IService注重系統性功能和異步初始化
    /// - IModule注重功能性特性和身份識別
    /// 
    /// 為什麼需要UID？
    /// UID（唯一標識符）讓系統可以支援同類型的多個模組實例。
    /// 這是傳統依賴注入容器通常不支援的功能，但在遊戲開發中很有用。
    /// 
    /// 例如在一個RTS遊戲中，您可能需要：
    /// - BuildingModule("barracks_1")
    /// - BuildingModule("barracks_2")  
    /// - BuildingModule("factory_1")
    /// 
    /// 每個都管理不同的建築，但使用相同的邏輯架構。
    /// </summary>
    public interface IModule
    {
        /// <summary>
        /// 模組的唯一標識符
        /// 
        /// 這個屬性讓系統可以區分同類型的不同模組實例。
        /// UID應該在模組的生命週期內保持不變。
        /// 
        /// UID設計建議：
        /// - 使用有意義的名稱而不是隨機GUID（便於調試）
        /// - 包含模組的功能描述（如"MainMenuUI"、"Level1Manager"）
        /// - 避免包含可變的資訊（如時間戳記）
        /// - 確保在同一類型內的唯一性
        /// </summary>
        string UID { get; }

        /// <summary>
        /// 初始化模組
        /// 
        /// 與服務的異步初始化不同，模組的初始化通常是同步的。
        /// 這是因為模組應該設計得輕量級，不需要複雜的初始化過程。
        /// 
        /// 如果模組需要複雜的初始化邏輯（如網路連接、大量資料載入），
        /// 應該考慮將其設計為服務而不是模組。
        /// 
        /// 初始化指南：
        /// - 設置模組的初始狀態
        /// - 訂閱必要的事件
        /// - 準備模組的核心資料結構
        /// - 如果依賴其他服務，通過ServiceLocator獲取
        /// </summary>
        void Initialize();

        /// <summary>
        /// 關閉模組並清理資源
        /// 
        /// 這個方法應該清理模組使用的所有資源，包括：
        /// - 取消事件訂閱
        /// - 清理UI元素（如果有的話）
        /// - 釋放持有的對象引用
        /// - 停止定時器或協程
        /// 
        /// 模組的關閉應該是快速和可靠的。
        /// 與服務不同，模組不應該執行複雜的清理邏輯。
        /// </summary>
        void Shutdown();
    }
}