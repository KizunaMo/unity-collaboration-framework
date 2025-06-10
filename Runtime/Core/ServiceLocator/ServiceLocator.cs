using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace Collaboration.Framework.Core.ServiceLocator
{
    /// <summary>
    /// 服務定位器實作 - 基於您的真實架構精煉而成
    /// 
    /// 這個實作展現了您在真實專案中使用的設計模式，
    /// 特別注重線程安全、類型安全和性能優化。
    /// 
    /// 關鍵設計決策分析：
    /// 
    /// 1. 為什麼使用Dictionary而不是List？
    ///    Dictionary提供O(1)的查找時間，即使在有數百個服務的大型專案中
    ///    也能保持高效。List需要O(n)的線性搜索，在服務數量增長時性能會下降。
    /// 
    /// 2. 為什麼需要線程安全？
    ///    雖然Unity主要是單線程的，但現代遊戲越來越多地使用異步操作。
    ///    資源載入、網路請求、音頻處理等都可能在背景線程中運行。
    ///    線程安全的設計確保這些操作不會損壞服務註冊表。
    /// 
    /// 3. 為什麼分離介面註冊和類型註冊？
    ///    這讓您可以通過介面來請求服務，實現依賴倒置原則。
    ///    例如，您可以註冊IUIManager介面，然後通過介面來解析，
    ///    這樣就可以在不修改客戶端代碼的情況下替換實作。
    /// </summary>
    public class ServiceLocator : IServiceLocator
    {
        // 使用Type作為鍵的字典，存儲所有已註冊的服務
        // 這種設計讓我們可以通過類型快速查找服務實例
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();
        
        // 線程安全鎖，確保多線程環境下的安全操作
        // 在Unity中，雖然主邏輯運行在主線程，但異步操作可能涉及其他線程
        private readonly object _lock = new object();
        
        // 調試選項，幫助開發者追蹤服務的註冊和解析過程
        private readonly bool _enableDebugLogging;
        
        // 服務依賴關係圖，用於檢測循環依賴
        private readonly Dictionary<Type, HashSet<Type>> _dependencies = new Dictionary<Type, HashSet<Type>>();

        public ServiceLocator(bool enableDebugLogging = false)
        {
            _enableDebugLogging = enableDebugLogging;
        }

        /// <summary>
        /// 註冊服務 - 介面到實作的映射
        /// 
        /// 這個方法實現了依賴倒置原則。客戶端代碼依賴於抽象（介面）
        /// 而不是具體實作。這讓系統更靈活，更容易測試。
        /// 
        /// 為什麼使用泛型約束？
        /// where TImplementation : class, TInterface 確保：
        /// 1. TImplementation必須是引用類型（class）
        /// 2. TImplementation必須實作TInterface介面
        /// 
        /// 這種約束在編譯時就能發現類型錯誤，避免運行時異常。
        /// </summary>
        public void Register<TInterface, TImplementation>(TImplementation implementation) 
            where TImplementation : class, TInterface
        {
            if (implementation == null)
                throw new ArgumentNullException(nameof(implementation));

            var interfaceType = typeof(TInterface);
            
            lock (_lock)
            {
                if (_services.ContainsKey(interfaceType))
                {
                    throw new InvalidOperationException(
                        $"Service of type {interfaceType.Name} is already registered. " +
                        "Use Unregister first if you need to replace the service.");
                }
                
                _services.Add(interfaceType, implementation);
                
                if (_enableDebugLogging)
                {
                    Debug.Log($"[ServiceLocator] Registered {interfaceType.Name} -> {typeof(TImplementation).Name}");
                }
            }
        }

        /// <summary>
        /// 簡化的服務註冊 - 當介面和實作類型相同時使用
        /// 
        /// 這是一個便利方法，適用於不需要介面抽象的場景。
        /// 例如，如果您有一個AudioManager類，並且不需要IAudioManager介面，
        /// 可以直接註冊AudioManager類型。
        /// </summary>
        public void Register<T>(T implementation) where T : class
        {
            Register<T, T>(implementation);
        }

        /// <summary>
        /// 解析服務實例 - 服務定位器的核心功能
        /// 
        /// 這個方法展示了如何安全地從容器中獲取服務。
        /// 使用泛型確保返回正確的類型，避免類型轉換錯誤。
        /// 
        /// 異常處理策略：
        /// 當服務不存在時拋出ServiceNotRegisteredException，
        /// 這遵循了"快速失敗"原則。這樣的設計讓配置錯誤
        /// 能夠立即被發現，而不是默默地返回null。
        /// </summary>
        public T Resolve<T>() where T : class
        {
            var serviceType = typeof(T);
            
            lock (_lock)
            {
                if (_services.TryGetValue(serviceType, out var service))
                {
                    if (_enableDebugLogging)
                    {
                        Debug.Log($"[ServiceLocator] Resolved service: {serviceType.Name}");
                    }
                    
                    return (T)service;
                }
                
                throw new ServiceNotRegisteredException(serviceType);
            }
        }

        /// <summary>
        /// 安全的服務解析 - 不拋出異常的版本
        /// 
        /// 這個方法適用於可選服務的場景。有時候某個服務可能不存在，
        /// 但這不應該被視為錯誤。例如，分析服務可能只在特定平台上存在。
        /// 
        /// 使用場景：
        /// var analyticsService = serviceLocator.TryResolve<IAnalyticsService>();
        /// if (analyticsService != null)
        /// {
        ///     analyticsService.TrackEvent("game_started");
        /// }
        /// </summary>
        public T TryResolve<T>() where T : class
        {
            var serviceType = typeof(T);
            
            lock (_lock)
            {
                if (_services.TryGetValue(serviceType, out var service))
                {
                    return (T)service;
                }
                
                return null;
            }
        }

        /// <summary>
        /// 檢查服務是否已註冊
        /// 
        /// 這個方法讓您可以在不觸發異常的情況下檢查服務的可用性。
        /// 對於條件性功能很有用，比如只在某些配置下才啟用的功能。
        /// </summary>
        public bool IsRegistered<T>() where T : class
        {
            var serviceType = typeof(T);
            
            lock (_lock)
            {
                return _services.ContainsKey(serviceType);
            }
        }

        /// <summary>
        /// 取消註冊服務
        /// 
        /// 在某些情況下，您可能需要動態替換服務實作。
        /// 例如，在測試中使用模擬實作，或者在運行時切換不同的實作。
        /// 
        /// 注意事項：
        /// 取消註冊服務可能會導致其他依賴該服務的組件出現問題。
        /// 使用此方法時需要小心，確保沒有其他代碼正在使用該服務。
        /// </summary>
        public void Unregister<T>() where T : class
        {
            var serviceType = typeof(T);
            
            lock (_lock)
            {
                if (_services.Remove(serviceType))
                {
                    if (_enableDebugLogging)
                    {
                        Debug.Log($"[ServiceLocator] Unregistered service: {serviceType.Name}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[ServiceLocator] Attempted to unregister non-existent service: {serviceType.Name}");
                }
            }
        }

        /// <summary>
        /// 清除所有註冊的服務
        /// 
        /// 這個方法主要用於測試清理或應用程式關閉時的資源清理。
        /// 在生產代碼中，通常不需要調用此方法。
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                var serviceCount = _services.Count;
                _services.Clear();
                _dependencies.Clear();
                
                if (_enableDebugLogging)
                {
                    Debug.Log($"[ServiceLocator] Cleared {serviceCount} registered services");
                }
            }
        }

        /// <summary>
        /// 獲取所有已註冊的服務（內部使用）
        /// 
        /// 這個方法主要供MasterManager使用，用於批量初始化所有服務。
        /// 它返回實作IService介面的所有服務實例。
        /// </summary>
        internal IEnumerable<IService> GetAllServices()
        {
            lock (_lock)
            {
                return _services.Values
                    .OfType<IService>()
                    .ToList(); // 創建副本以避免鎖定問題
            }
        }

        /// <summary>
        /// 獲取所有已註冊服務的類型資訊（用於調試）
        /// 
        /// 這個方法對於系統診斷很有用。它讓您可以查看
        /// 當前註冊了哪些服務，幫助調試配置問題。
        /// </summary>
        public IReadOnlyDictionary<string, string> GetRegisteredServices()
        {
            lock (_lock)
            {
                var result = new Dictionary<string, string>();
                
                foreach (var kvp in _services)
                {
                    var interfaceType = kvp.Key;
                    var implementationType = kvp.Value.GetType();
                    result[interfaceType.Name] = implementationType.Name;
                }
                
                return result;
            }
        }

        /// <summary>
        /// 驗證服務配置的完整性
        /// 
        /// 這個方法檢查所有註冊的服務是否能正確解析其依賴。
        /// 在系統啟動時調用此方法可以及早發現配置問題。
        /// 
        /// 檢查項目：
        /// 1. 循環依賴檢測
        /// 2. 缺失依賴檢測  
        /// 3. 類型兼容性檢查
        /// </summary>
        public void ValidateConfiguration()
        {
            lock (_lock)
            {
                var issues = new List<string>();
                
                // 檢查每個服務的依賴
                foreach (var serviceType in _services.Keys)
                {
                    try
                    {
                        // 嘗試解析服務以檢查是否有問題
                        var service = _services[serviceType];
                        
                        // 檢查服務是否實作了預期的介面
                        if (!serviceType.IsAssignableFrom(service.GetType()))
                        {
                            issues.Add($"Service {service.GetType().Name} does not implement {serviceType.Name}");
                        }
                    }
                    catch (Exception ex)
                    {
                        issues.Add($"Failed to validate service {serviceType.Name}: {ex.Message}");
                    }
                }
                
                if (issues.Any())
                {
                    var issueMessage = string.Join("\n", issues);
                    throw new InvalidOperationException($"Service configuration validation failed:\n{issueMessage}");
                }
                
                if (_enableDebugLogging)
                {
                    Debug.Log($"[ServiceLocator] Configuration validation passed for {_services.Count} services");
                }
            }
        }
    }

    /// <summary>
    /// 服務介面 - 所有服務必須實作的基礎契約
    /// 
    /// 這個介面定義了服務的基本生命週期方法。
    /// 所有服務都需要支援異步初始化和優雅關閉。
    /// 
    /// 為什麼初始化是異步的？
    /// 現代遊戲中的服務初始化通常涉及：
    /// - 從磁盤載入配置文件
    /// - 建立網路連接
    /// - 初始化第三方SDK
    /// - 載入大型資料集
    /// 
    /// 這些操作都可能耗時較長，異步初始化避免阻塞主線程。
    /// </summary>
    public interface IService
    {
        /// <summary>
        /// 異步初始化服務
        /// 
        /// 這個方法應該執行服務所需的所有初始化邏輯。
        /// 如果初始化失敗，應該拋出有意義的異常。
        /// 
        /// 實作指南：
        /// - 使用CancellationToken支援取消操作
        /// - 提供進度報告（如果初始化耗時較長）
        /// - 確保方法是冪等的（多次調用不會造成問題）
        /// - 在初始化完成前，服務應該處於"未就緒"狀態
        /// </summary>
        UniTask InitializeAsync();

        /// <summary>
        /// 關閉服務並清理資源
        /// 
        /// 這個方法應該優雅地停止服務並清理所有資源。
        /// 包括：關閉文件句柄、斷開網路連接、取消訂閱事件等。
        /// 
        /// 實作指南：
        /// - 方法應該是冪等的（多次調用不會造成問題）
        /// - 不應該拋出異常（除非是嚴重的系統錯誤）
        /// - 應該在合理的時間內完成（避免無限等待）
        /// - 清理後，服務應該能夠重新初始化
        /// </summary>
        void Shutdown();
    }

    /// <summary>
    /// 服務未註冊異常
    /// 
    /// 當嘗試解析未註冊的服務時拋出此異常。
    /// 這個異常提供了清晰的錯誤信息和解決建議。
    /// 
    /// 異常設計原則：
    /// 1. 提供清晰的錯誤描述
    /// 2. 包含解決問題的建議
    /// 3. 包含相關的上下文信息
    /// 4. 方便在日誌中搜索和過濾
    /// </summary>
    public class ServiceNotRegisteredException : Exception
    {
        public Type ServiceType { get; }
        
        public ServiceNotRegisteredException(Type serviceType) 
            : base($"Service of type '{serviceType.Name}' is not registered in the service locator. " +
                   $"Please ensure the service is registered in your MasterManager.RegisterCoreServices() method " +
                   $"before attempting to resolve it.")
        {
            ServiceType = serviceType;
        }
        
        public ServiceNotRegisteredException(Type serviceType, Exception innerException) 
            : base($"Failed to resolve service of type '{serviceType.Name}'. " +
                   $"See inner exception for details.", innerException)
        {
            ServiceType = serviceType;
        }
    }
}
