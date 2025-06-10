using System;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace Collaboration.Framework.Core.ServiceLocator
{
    /// <summary>
    /// 主管理器 - 現代遊戲架構的核心協調系統
    /// 
    /// 這個設計基於真實的企業級Unity專案架構，展現了如何優雅地解決
    /// 複雜遊戲系統中的服務管理和初始化挑戰。
    /// 
    /// 設計靈感和教學價值：
    /// 這個實作展示了一個關鍵的架構洞察：在複雜的遊戲專案中，
    /// 您需要區分"核心服務"和"功能模組"。核心服務是遊戲運行的基礎
    /// （如UIManager、AudioManager），而功能模組是可選的增強功能
    /// （如成就系統、分析模組）。
    /// 
    /// 雙重定位器模式的優勢：
    /// 1. 服務定位器處理有生命週期依賴的核心系統
    /// 2. 模組定位器處理可動態載入/卸載的功能
    /// 3. 兩者都支援異步初始化，確保依賴順序正確
    /// 4. 統一的服務發現機制，降低系統耦合度
    /// 
    /// 實際應用場景：
    /// - 遊戲啟動時：先初始化核心服務，再載入功能模組
    /// - 場景切換時：保持核心服務，重新配置場景特定模組  
    /// - 熱更新時：可以動態替換模組而不影響核心功能
    /// - 平台差異：在不同平台載入不同的功能模組
    /// </summary>
    public class MasterManager : PersistentSingleton<MasterManager>
    {
        [Header("Service Configuration")]
        [SerializeField] private bool enableDebugLogging = true;
        [SerializeField] private float serviceInitTimeout = 30f;
        
        // 雙重定位器系統 - 分離核心服務和功能模組的關注點
        private ServiceLocator serviceLocator;
        private ModuleLocator moduleLocator;
        private ServiceRegistry serviceRegistry;
        
        // 初始化狀態追蹤
        private bool isInitialized = false;
        private readonly List<string> initializationLog = new List<string>();

        protected override void OnSingletonInitialized()
        {
            InitializeLocators();
            StartCoroutine(InitializeSystemAsync());
        }

        /// <summary>
        /// 初始化定位器系統
        /// 
        /// 這裡我們創建了兩個獨立的定位器：
        /// - ServiceLocator：管理實作IService介面的核心系統
        /// - ModuleLocator：管理實作IModule介面的功能模組
        /// 
        /// 為什麼要分離？
        /// 服務通常有複雜的初始化依賴關係，需要異步初始化。
        /// 模組通常是獨立的功能，可以同步初始化。
        /// 這種分離讓我們可以為不同類型的系統應用不同的管理策略。
        /// </summary>
        private void InitializeLocators()
        {
            serviceLocator = new ServiceLocator();
            moduleLocator = new ModuleLocator();
            serviceRegistry = new ServiceRegistry();
            
            LogInitialization("Locators initialized");
        }

        /// <summary>
        /// 異步系統初始化
        /// 
        /// 這個方法展示了現代Unity開發中的最佳實踐：
        /// 使用async/await來處理複雜的初始化序列。
        /// 
        /// 初始化順序很重要：
        /// 1. 註冊所有服務（但不初始化）
        /// 2. 註冊所有模組（但不初始化）  
        /// 3. 按依賴順序初始化服務
        /// 4. 初始化模組
        /// 5. 執行後初始化配置
        /// 
        /// 這種順序確保了當服務開始初始化時，
        /// 所有的依賴關係都已經註冊並可以解析。
        /// </summary>
        private async System.Collections.IEnumerator InitializeSystemAsync()
        {
            try
            {
                LogInitialization("Starting system initialization");
                
                // 第一階段：註冊所有服務和模組
                RegisterCoreServices();
                RegisterFeatureModules();
                
                // 第二階段：異步初始化核心服務
                var serviceInitTask = InitializeServicesAsync();
                yield return new WaitUntil(() => serviceInitTask.IsCompleted);
                
                if (serviceInitTask.Exception != null)
                {
                    Debug.LogError($"Service initialization failed: {serviceInitTask.Exception}");
                    yield break;
                }
                
                // 第三階段：初始化功能模組
                InitializeModules();
                
                // 第四階段：後初始化配置
                await PostInitializationSetup();
                
                isInitialized = true;
                LogInitialization("System initialization completed successfully");
                
                // 通知其他系統初始化完成
                OnSystemInitialized();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Critical error during system initialization: {ex}");
                throw;
            }
        }

        /// <summary>
        /// 註冊核心服務
        /// 
        /// 這個方法展示了依賴注入容器的基本使用模式。
        /// 我們註冊服務的介面和實作，讓系統可以通過介面來解析依賴。
        /// 
        /// 註冊順序的考量：
        /// 雖然服務會異步初始化，但註冊順序仍然重要，
        /// 因為某些服務可能在註冊階段就需要訪問其他服務。
        /// </summary>
        private void RegisterCoreServices()
        {
            LogInitialization("Registering core services");
            
            // 註冊事件管理器 - 通常是第一個註冊的服務
            // 因為其他服務可能需要在初始化時發布事件
            RegisterService<IEventManager>(EventManager.Instance);
            
            // 註冊場景管理器 - 負責場景載入和切換
            RegisterService<ISceneManager>(new SceneManager());
            
            // 註冊UI管理器 - 需要事件管理器支援
            RegisterService<IUIManager>(new UIManager());
            
            // 註冊音頻管理器 - 獨立系統，無依賴
            RegisterService<IAudioManager>(new AudioManager());
            
            // 註冊輸入管理器 - 需要事件管理器支援
            RegisterService<IInputManager>(new InputManager());
            
            // 註冊存檔管理器 - 通常最後註冊，因為其他系統可能需要載入數據
            RegisterService<ISaveManager>(new SaveManager());
        }

        /// <summary>
        /// 註冊功能模組
        /// 
        /// 模組系統的設計允許您為遊戲添加可選功能，
        /// 而不需要修改核心架構。每個模組都有唯一的UID，
        /// 支援同類型的多個實例。
        /// 
        /// 模組的優勢：
        /// - 可以根據平台或配置動態載入
        /// - 容易進行A/B測試（載入不同的模組實作）
        /// - 支援熱更新（替換模組而不重啟遊戲）
        /// - 降低主程序的複雜度
        /// </summary>
        private void RegisterFeatureModules()
        {
            LogInitialization("Registering feature modules");
            
            // 成就系統模組 - 可選功能
            RegisterModule(new AchievementModule());
            
            // 分析模組 - 可能根據平台不同而不同
            #if UNITY_ANALYTICS
            RegisterModule(new AnalyticsModule());
            #endif
            
            // 調試模組 - 只在開發版本中啟用
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            RegisterModule(new DebugModule());
            #endif
        }

        /// <summary>
        /// 異步初始化所有服務
        /// 
        /// 這個方法展示了如何安全地並行初始化多個異步服務。
        /// 使用UniTask.WhenAll確保所有服務都完成初始化，
        /// 同時提供超時保護避免無限等待。
        /// 
        /// 錯誤處理策略：
        /// 如果任何服務初始化失敗，整個初始化過程應該停止。
        /// 這遵循了"快速失敗"原則，讓問題儘早暴露。
        /// </summary>
        private async UniTask InitializeServicesAsync()
        {
            LogInitialization("Initializing services asynchronously");
            
            var services = serviceLocator.GetAllServices();
            var initTasks = new List<UniTask>();
            
            foreach (var service in services)
            {
                initTasks.Add(service.InitializeAsync());
            }
            
            // 並行初始化所有服務，但設置超時保護
            try
            {
                await UniTask.WhenAll(initTasks).Timeout(TimeSpan.FromSeconds(serviceInitTimeout));
                LogInitialization($"All {services.Count()} services initialized successfully");
            }
            catch (TimeoutException)
            {
                throw new SystemException($"Service initialization timed out after {serviceInitTimeout} seconds");
            }
        }

        /// <summary>
        /// 初始化功能模組
        /// 
        /// 模組的初始化通常是同步的，因為它們應該設計得輕量級。
        /// 如果模組需要複雜的初始化邏輯，應該考慮將其設計為服務。
        /// </summary>
        private void InitializeModules()
        {
            LogInitialization("Initializing feature modules");
            
            var modules = moduleLocator.GetAllModules();
            foreach (var module in modules)
            {
                try
                {
                    module.Initialize();
                    LogInitialization($"Module {module.GetType().Name} initialized");
                }
                catch (Exception ex)
                {
                    // 模組初始化失敗不應該阻止整個系統
                    Debug.LogError($"Module {module.GetType().Name} initialization failed: {ex}");
                }
            }
        }

        /// <summary>
        /// 後初始化設置
        /// 
        /// 某些配置只能在所有服務都初始化完成後進行。
        /// 例如：UI預製體路徑配置、跨服務的事件訂閱等。
        /// </summary>
        private async UniTask PostInitializationSetup()
        {
            LogInitialization("Performing post-initialization setup");
            
            // 示例：配置UI管理器的預製體路徑
            // 這需要在UI管理器初始化完成後進行
            // var uiManager = GetService<IUIManager>();
            // uiManager.ConfigurePrefabPaths();
            
            await UniTask.CompletedTask;
        }

        /// <summary>
        /// 系統初始化完成事件
        /// 
        /// 當整個系統初始化完成時，通知其他系統可以開始工作。
        /// 這是一個重要的生命週期事件，其他系統可以監聽此事件
        /// 來執行需要完整系統支援的操作。
        /// </summary>
        private void OnSystemInitialized()
        {
            var eventManager = GetService<IEventManager>();
            eventManager?.Publish(new SystemInitializedEvent(initializationLog.ToArray()));
        }

        #region Public API
        
        /// <summary>
        /// 註冊服務到服務定位器
        /// 
        /// 這個方法提供了類型安全的服務註冊。
        /// 泛型約束確保只有實作IService介面的類可以被註冊為服務。
        /// </summary>
        public void RegisterService<TInterface, TImplementation>(TImplementation implementation) 
            where TImplementation : class, IService, TInterface
        {
            serviceLocator.Register<TInterface, TImplementation>(implementation);
            serviceRegistry.RegisterService<TInterface>(implementation.GetType().Name);
            
            if (enableDebugLogging)
            {
                Debug.Log($"[MasterManager] Registered service: {typeof(TInterface).Name} -> {typeof(TImplementation).Name}");
            }
        }

        /// <summary>
        /// 註冊模組到模組定位器
        /// </summary>
        public void RegisterModule(IModule module)
        {
            moduleLocator.Register(module);
            serviceRegistry.RegisterModule(module.UID, module.GetType().Name);
            
            if (enableDebugLogging)
            {
                Debug.Log($"[MasterManager] Registered module: {module.GetType().Name} (UID: {module.UID})");
            }
        }

        /// <summary>
        /// 解析服務實例
        /// 
        /// 這是系統中其他組件獲取服務的主要方式。
        /// 通過泛型和約束，我們確保只能請求已註冊的服務類型。
        /// </summary>
        public T GetService<T>() where T : class
        {
            if (!isInitialized)
            {
                Debug.LogWarning("[MasterManager] Attempting to resolve service before system initialization is complete");
            }
            
            return serviceLocator.Resolve<T>();
        }

        /// <summary>
        /// 獲取模組實例
        /// </summary>
        public T GetModule<T>(string uid = null) where T : class, IModule
        {
            return moduleLocator.GetModule<T>(uid);
        }

        /// <summary>
        /// 獲取所有指定類型的模組
        /// </summary>
        public List<T> GetModules<T>() where T : class, IModule
        {
            return moduleLocator.GetModules<T>();
        }

        /// <summary>
        /// 檢查系統是否已完全初始化
        /// </summary>
        public bool IsSystemReady => isInitialized;

        /// <summary>
        /// 獲取初始化日誌（用於調試）
        /// </summary>
        public IReadOnlyList<string> GetInitializationLog() => initializationLog.AsReadOnly();

        #endregion

        #region Lifecycle Management

        protected override void OnSingletonDestroyed()
        {
            LogInitialization("Shutting down master manager");
            
            // 先關閉模組（它們可能依賴服務）
            var modules = moduleLocator?.GetAllModules();
            if (modules != null)
            {
                foreach (var module in modules)
                {
                    try
                    {
                        module.Shutdown();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error shutting down module {module.GetType().Name}: {ex}");
                    }
                }
            }
            
            // 然後關閉服務
            var services = serviceLocator?.GetAllServices();
            if (services != null)
            {
                foreach (var service in services)
                {
                    try
                    {
                        service.Shutdown();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error shutting down service {service.GetType().Name}: {ex}");
                    }
                }
            }
        }

        #endregion

        #region Utility Methods

        private void LogInitialization(string message)
        {
            string logEntry = $"[{Time.time:F2}] {message}";
            initializationLog.Add(logEntry);
            
            if (enableDebugLogging)
            {
                Debug.Log($"[MasterManager] {logEntry}");
            }
        }

        #endregion
    }

    /// <summary>
    /// 系統初始化完成事件
    /// 
    /// 當整個遊戲系統初始化完成時發布此事件。
    /// 其他系統可以監聽此事件來執行需要完整系統支援的操作。
    /// </summary>
    public readonly struct SystemInitializedEvent : Core.EventSystem.IEvent
    {
        public readonly string[] InitializationLog;
        
        public SystemInitializedEvent(string[] initializationLog)
        {
            InitializationLog = initializationLog;
        }
    }
}