using System;
using System.Collections.Generic;
using UnityEngine;
using XLua;

namespace Collaboration.Framework.XLua
{
    /// <summary>
    /// XLua 橋接管理器
    /// 
    /// 這個類是 C# 和 Lua 之間的主要通信橋樑。
    /// 
    /// XLua 的基本概念：
    /// XLua 是一個為 Unity 設計的 Lua 解釋器，它允許你在 C# 應用程式中執行 Lua 腳本。
    /// 這帶來了幾個重要的好處：
    /// 
    /// 1. 熱更新能力：Lua 腳本可以在不重新編譯整個應用程式的情況下更新
    /// 2. 快速迭代：遊戲邏輯的修改不需要重新編譯，提高開發效率
    /// 3. 模組化：複雜的遊戲邏輯可以用 Lua 編寫，保持 C# 代碼的簡潔
    /// 4. 安全性：Lua 運行在沙盒環境中，相對更安全
    /// 
    /// 設計原則：
    /// 1. 安全性優先：所有 Lua 調用都應該有適當的錯誤處理
    /// 2. 性能考慮：頻繁調用的函數應該緩存 Lua 引用
    /// 3. 記憶體管理：適當地管理 Lua 對象的生命週期
    /// 4. 調試友好：提供清晰的錯誤信息和調試工具
    /// </summary>
    public class LuaBridge : PersistentSingleton<LuaBridge>
    {
        [Header("Lua Configuration")]
        [SerializeField] private string luaScriptPath = "LuaScripts/";
        [SerializeField] private bool enableDebugMode = true;
        [SerializeField] private bool enableLuaProfiler = false;
        
        // XLua 核心組件
        private LuaEnv _luaEnv;
        
        // 註冊的 C# 函數緩存
        // 這個字典存儲了所有註冊給 Lua 使用的 C# 函數
        // 使用緩存可以避免重複查找，提高性能
        private readonly Dictionary<string, Delegate> _registeredFunctions = new Dictionary<string, Delegate>();
        
        // Lua 函數引用緩存
        // 當我們需要從 C# 調用 Lua 函數時，每次都查找會影響性能
        // 緩存這些引用可以顯著提高調用速度
        private readonly Dictionary<string, LuaFunction> _luaFunctionCache = new Dictionary<string, LuaFunction>();
        
        // 事件系統集成
        // 將我們的事件系統暴露給 Lua，讓 Lua 腳本也能參與事件驅動的架構
        private LuaFunction _luaEventHandler;

        protected override void OnSingletonInitialized()
        {
            InitializeLuaEnvironment();
            RegisterCoreFunctions();
            RegisterEventSystemIntegration();
            
            if (enableDebugMode)
            {
                Debug.Log("[LuaBridge] Lua environment initialized successfully");
            }
        }

        /// <summary>
        /// 初始化 Lua 環境
        /// 
        /// 這是整個 XLua 集成的基礎。LuaEnv 是 XLua 提供的核心類，
        /// 它創建了一個 Lua 虛擬機實例。在這個虛擬機中，
        /// 我們可以執行 Lua 腳本，註冊 C# 函數，調用 Lua 函數等。
        /// 
        /// 重要的是，每個 LuaEnv 實例都是獨立的，
        /// 它們之間不會互相影響。這讓我們可以為不同的用途
        /// 創建不同的 Lua 環境，比如一個用於遊戲邏輯，一個用於UI。
        /// </summary>
        private void InitializeLuaEnvironment()
        {
            _luaEnv = new LuaEnv();
            
            // 設置 Lua 載入器
            // 這告訴 XLua 如何找到和載入 Lua 腳本文件
            _luaEnv.AddLoader(CustomLuaLoader);
            
            // 如果啟用了性能分析，設置相關配置
            if (enableLuaProfiler)
            {
                // 這裡可以添加 Lua 性能分析的配置
                // 在實際項目中，你可能會使用專門的性能分析工具
            }
        }

        /// <summary>
        /// 自定義 Lua 載入器
        /// 
        /// 這個函數告訴 XLua 如何載入 Lua 腳本。
        /// 預設情況下，XLua 會從 Resources 文件夾載入腳本，
        /// 但我們可以自定義載入邏輯來支援更複雜的需求，
        /// 比如從網路載入、從加密文件載入等。
        /// 
        /// 這個實作從 Resources 文件夾載入腳本，
        /// 這是最簡單也是最常用的方式。
        /// </summary>
        /// <param name="filepath">Lua 腳本的路徑</param>
        /// <returns>腳本內容的字節數組</returns>
        private byte[] CustomLuaLoader(ref string filepath)
        {
            string fullPath = luaScriptPath + filepath + ".lua";
            TextAsset luaScript = Resources.Load<TextAsset>(fullPath);
            
            if (luaScript != null)
            {
                return luaScript.bytes;
            }
            else
            {
                if (enableDebugMode)
                {
                    Debug.LogWarning($"[LuaBridge] Lua script not found: {fullPath}");
                }
                return null;
            }
        }

        /// <summary>
        /// 註冊核心 C# 函數給 Lua 使用
        /// 
        /// 這個方法展示了如何將 C# 函數暴露給 Lua。
        /// 這是 XLua 集成的核心部分之一：讓 Lua 腳本能夠調用 C# 的功能。
        /// 
        /// 我們註冊了一些基本的 Unity 功能，比如日誌輸出、遊戲對象查找等。
        /// 這些功能讓 Lua 腳本能夠與 Unity 的核心系統互動。
        /// </summary>
        private void RegisterCoreFunctions()
        {
            // 註冊日誌函數
            // 這讓 Lua 腳本可以輸出日誌信息，對於調試很重要
            RegisterFunction("Debug_Log", new Action<string>(Debug.Log));
            RegisterFunction("Debug_LogWarning", new Action<string>(Debug.LogWarning));
            RegisterFunction("Debug_LogError", new Action<string>(Debug.LogError));
            
            // 註冊時間相關函數
            // 遊戲邏輯經常需要訪問時間信息
            RegisterFunction("Time_GetTime", new Func<float>(() => Time.time));
            RegisterFunction("Time_GetDeltaTime", new Func<float>(() => Time.deltaTime));
            
            // 註冊遊戲對象查找函數
            // 這讓 Lua 腳本能夠找到和操作 Unity 的遊戲對象
            RegisterFunction("GameObject_Find", new Func<string, GameObject>(GameObject.Find));
            RegisterFunction("GameObject_FindWithTag", new Func<string, GameObject>(GameObject.FindWithTag));
            
            // 註冊向量運算函數
            // 3D 遊戲中的數學運算是基礎需求
            RegisterFunction("Vector3_Distance", new Func<Vector3, Vector3, float>(Vector3.Distance));
            RegisterFunction("Vector3_Lerp", new Func<Vector3, Vector3, float, Vector3>(Vector3.Lerp));
        }

        /// <summary>
        /// 註冊事件系統集成
        /// 
        /// 這個方法將我們之前建立的事件系統暴露給 Lua，
        /// 讓 Lua 腳本也能參與事件驅動的架構。
        /// 
        /// 這是一個非常強大的功能：它讓 Lua 腳本能夠監聽和發布事件，
        /// 與 C# 系統無縫集成。例如，當玩家血量改變時，
        /// Lua 腳本可以接收到事件並執行相應的邏輯。
        /// </summary>
        private void RegisterEventSystemIntegration()
        {
            // 註冊事件發布函數
            // 這個函數讓 Lua 能夠發布事件，通知其他系統
            RegisterFunction("Event_Publish", new Action<string, object>((eventName, data) =>
            {
                try
                {
                    // 這裡可以實作將 Lua 事件轉換為 C# 事件的邏輯
                    // 實際實作會根據具體的事件類型來決定
                    if (enableDebugMode)
                    {
                        Debug.Log($"[LuaBridge] Lua published event: {eventName}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[LuaBridge] Error publishing event from Lua: {ex.Message}");
                }
            }));
            
            // 設置 Lua 事件處理器
            // 這讓 C# 事件能夠被 Lua 腳本接收
            try
            {
                _luaEnv.DoString(@"
                    -- Lua 端的事件處理器表
                    _event_handlers = {}
                    
                    -- 註冊事件處理器
                    function register_event_handler(event_name, handler)
                        if not _event_handlers[event_name] then
                            _event_handlers[event_name] = {}
                        end
                        table.insert(_event_handlers[event_name], handler)
                    end
                    
                    -- 處理來自 C# 的事件
                    function handle_csharp_event(event_name, event_data)
                        local handlers = _event_handlers[event_name]
                        if handlers then
                            for _, handler in ipairs(handlers) do
                                pcall(handler, event_data)  -- 使用 pcall 來捕獲錯誤
                            end
                        end
                    end
                ");
                
                _luaEventHandler = _luaEnv.Global.Get<LuaFunction>("handle_csharp_event");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LuaBridge] Failed to setup Lua event integration: {ex.Message}");
            }
        }

        /// <summary>
        /// 註冊 C# 函數給 Lua 使用
        /// 
        /// 這是一個通用的函數註冊方法。它將 C# 的委託（Delegate）
        /// 註冊到 Lua 的全局命名空間中，讓 Lua 腳本能夠調用。
        /// 
        /// 使用緩存的原因是避免重複註冊同一個函數，
        /// 並且提高查找效率。
        /// </summary>
        /// <param name="functionName">在 Lua 中的函數名稱</param>
        /// <param name="function">要註冊的 C# 函數</param>
        public void RegisterFunction(string functionName, Delegate function)
        {
            if (_luaEnv == null)
            {
                Debug.LogError("[LuaBridge] Lua environment not initialized");
                return;
            }

            try
            {
                _registeredFunctions[functionName] = function;
                _luaEnv.Global.Set(functionName, function);
                
                if (enableDebugMode)
                {
                    Debug.Log($"[LuaBridge] Registered function: {functionName}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LuaBridge] Failed to register function {functionName}: {ex.Message}");
            }
        }

        /// <summary>
        /// 執行 Lua 腳本
        /// 
        /// 這個方法執行 Lua 腳本字符串。它包含了適當的錯誤處理，
        /// 確保 Lua 腳本中的錯誤不會導致整個應用程式崩潰。
        /// 
        /// 這對於動態載入和執行遊戲邏輯很有用，
        /// 比如載入關卡腳本、AI 行為腳本等。
        /// </summary>
        /// <param name="luaScript">要執行的 Lua 腳本</param>
        /// <returns>執行是否成功</returns>
        public bool ExecuteLuaScript(string luaScript)
        {
            if (_luaEnv == null)
            {
                Debug.LogError("[LuaBridge] Lua environment not initialized");
                return false;
            }

            try
            {
                _luaEnv.DoString(luaScript);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LuaBridge] Lua script execution failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 調用 Lua 函數
        /// 
        /// 這個方法讓 C# 代碼能夠調用 Lua 函數。
        /// 它使用緩存來提高性能，避免每次調用都查找函數。
        /// 
        /// 這是 C# 和 Lua 互操作的另一個重要方面：
        /// 不僅 Lua 可以調用 C# 函數，C# 也可以調用 Lua 函數。
        /// </summary>
        /// <param name="functionName">Lua 函數名稱</param>
        /// <param name="args">函數參數</param>
        /// <returns>函數返回值</returns>
        public object[] CallLuaFunction(string functionName, params object[] args)
        {
            if (_luaEnv == null)
            {
                Debug.LogError("[LuaBridge] Lua environment not initialized");
                return null;
            }

            try
            {
                // 嘗試從緩存中獲取函數引用
                if (!_luaFunctionCache.TryGetValue(functionName, out LuaFunction luaFunction))
                {
                    luaFunction = _luaEnv.Global.Get<LuaFunction>(functionName);
                    if (luaFunction != null)
                    {
                        _luaFunctionCache[functionName] = luaFunction;
                    }
                }

                if (luaFunction != null)
                {
                    return luaFunction.Call(args);
                }
                else
                {
                    if (enableDebugMode)
                    {
                        Debug.LogWarning($"[LuaBridge] Lua function not found: {functionName}");
                    }
                    return null;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LuaBridge] Failed to call Lua function {functionName}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 向 Lua 發送事件
        /// 
        /// 這個方法將 C# 事件轉發給 Lua 腳本。
        /// 這是事件系統集成的一部分，讓 Lua 腳本能夠響應 C# 中發生的事件。
        /// </summary>
        /// <param name="eventName">事件名稱</param>
        /// <param name="eventData">事件資料</param>
        public void SendEventToLua(string eventName, object eventData)
        {
            if (_luaEventHandler != null)
            {
                try
                {
                    _luaEventHandler.Call(eventName, eventData);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[LuaBridge] Failed to send event to Lua: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 定期執行 Lua 垃圾回收
        /// 
        /// Lua 有自己的垃圾回收器，但在遊戲環境中，
        /// 我們需要定期手動觸發垃圾回收以保持性能。
        /// 
        /// 這個方法應該定期調用，比如每隔幾秒或在場景切換時。
        /// </summary>
        private void Update()
        {
            // 定期執行 Lua 垃圾回收
            if (_luaEnv != null && Time.frameCount % 60 == 0)  // 每60幀執行一次
            {
                _luaEnv.Tick();
            }
        }

        protected override void OnSingletonDestroyed()
        {
            // 清理 Lua 環境
            if (_luaEnv != null)
            {
                // 清理緩存的函數引用
                foreach (var kvp in _luaFunctionCache)
                {
                    kvp.Value?.Dispose();
                }
                _luaFunctionCache.Clear();
                
                _luaEventHandler?.Dispose();
                _registeredFunctions.Clear();
                
                _luaEnv.Dispose();
                _luaEnv = null;
                
                if (enableDebugMode)
                {
                    Debug.Log("[LuaBridge] Lua environment disposed");
                }
            }
        }
    }
}