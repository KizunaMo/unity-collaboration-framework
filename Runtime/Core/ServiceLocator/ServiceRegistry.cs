using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Collaboration.Framework.Core.ServiceLocator
{
    /// <summary>
    /// 服務註冊表 - 系統服務和模組的中央記錄系統
    /// 
    /// 這個組件展現了您在真實專案中對系統可觀測性的重視。
    /// ServiceRegistry 不僅僅是一個記錄工具，它是整個架構的"記憶體"，
    /// 保存了系統當前狀態的完整快照。
    /// 
    /// 為什麼需要註冊表？
    /// 在複雜的遊戲系統中，僅僅有服務和模組是不夠的。您需要能夠：
    /// 
    /// 1. 診斷問題：當系統出現問題時，快速了解當前註冊了哪些組件
    /// 2. 性能分析：追蹤哪些服務消耗了最多的初始化時間
    /// 3. 動態監控：在運行時查看系統狀態的變化
    /// 4. 配置驗證：確保所有必需的服務都已正確註冊
    /// 5. 開發調試：在開發過程中快速驗證系統配置
    /// 
    /// 設計哲學：
    /// 註冊表採用了"事件溯源"的思想 - 它不僅記錄當前狀態，
    /// 還記錄了達到當前狀態的所有操作歷史。這種設計在調試
    /// 複雜的初始化問題時特別有價值。
    /// 
    /// 實際應用價值：
    /// 在您的真實專案中，當玩家報告某個功能不工作時，
    /// 您可以通過註冊表快速檢查相關的服務是否正確載入，
    /// 大大加快問題診斷的速度。
    /// </summary>
    [CreateAssetMenu(fileName = "ServiceRegistry", menuName = "Framework/Service Registry")]
    public class ServiceRegistry : ScriptableObject
    {
        [Header("Service Registration Records")]
        [SerializeField] private List<ServiceEntry> registeredServices = new List<ServiceEntry>();
        
        [Header("Module Registration Records")]
        [SerializeField] private List<ModuleEntry> registeredModules = new List<ModuleEntry>();
        
        [Header("Registry Configuration")]
        [SerializeField] private bool enableDetailedLogging = true;
        [SerializeField] private bool trackRegistrationHistory = true;
        [SerializeField] private int maxHistoryEntries = 100;
        
        // 運行時數據（不序列化）
        [System.NonSerialized] private List<RegistrationEvent> registrationHistory = new List<RegistrationEvent>();
        [System.NonSerialized] private DateTime lastModified = DateTime.Now;

        /// <summary>
        /// 服務註冊記錄結構
        /// 
        /// 這個結構不僅記錄了基本的註冊資訊，還包含了運行時狀態和性能指標。
        /// 這種設計讓註冊表成為系統健康監控的重要工具。
        /// 
        /// 為什麼包含這麼多資訊？
        /// 在企業級系統中，僅僅知道"服務已註冊"是不夠的。
        /// 您需要知道服務的健康狀態、初始化時間、錯誤歷史等。
        /// 這些資訊在生產環境的故障排除中至關重要。
        /// </summary>
        [System.Serializable]
        public class ServiceEntry
        {
            [Header("Basic Information")]
            public string serviceName;
            public string serviceType;
            public string implementationType;
            
            [Header("Registration Details")]
            public DateTime registrationTime;
            public string registrationSource; // 記錄是從哪裡註冊的（用於調試）
            
            [Header("Runtime Status")]
            public ServiceStatus status = ServiceStatus.Registered;
            public float initializationTimeMs = 0f;
            public string lastError = "";
            public int errorCount = 0;
            
            [Header("Dependencies")]
            public List<string> dependencies = new List<string>();
            public List<string> dependents = new List<string>();

            public ServiceEntry()
            {
                registrationTime = DateTime.Now;
            }

            public ServiceEntry(string name, string type, string implementation = null)
            {
                serviceName = name;
                serviceType = type;
                implementationType = implementation ?? type;
                registrationTime = DateTime.Now;
                registrationSource = GetRegistrationSource();
            }

            /// <summary>
            /// 獲取註冊來源的調試資訊
            /// 
            /// 這個方法使用調用堆疊來確定服務是從代碼的哪個部分註冊的。
            /// 在調試複雜的註冊問題時，這個資訊非常有價值。
            /// </summary>
            private string GetRegistrationSource()
            {
                try
                {
                    var stackTrace = new System.Diagnostics.StackTrace(true);
                    var frames = stackTrace.GetFrames();
                    
                    // 跳過ServiceRegistry內部的方法，找到實際的調用者
                    for (int i = 0; i < frames.Length; i++)
                    {
                        var method = frames[i].GetMethod();
                        var declaringType = method.DeclaringType;
                        
                        if (declaringType != null && 
                            !declaringType.FullName.Contains("ServiceRegistry") &&
                            !declaringType.FullName.Contains("ServiceLocator"))
                        {
                            return $"{declaringType.Name}.{method.Name}";
                        }
                    }
                }
                catch
                {
                    // 如果獲取調用堆疊失敗，不影響主要功能
                }
                
                return "Unknown";
            }

            public override string ToString()
            {
                return $"{serviceName} ({serviceType}) - {status} - Errors: {errorCount}";
            }
        }

        /// <summary>
        /// 模組註冊記錄結構
        /// 
        /// 模組記錄比服務記錄更注重身份識別和生命週期管理。
        /// 由於模組支援多實例，UID成為了關鍵的識別資訊。
        /// </summary>
        [System.Serializable]
        public class ModuleEntry
        {
            [Header("Basic Information")]
            public string moduleName;
            public string moduleType;
            public string uid;
            
            [Header("Registration Details")]
            public DateTime registrationTime;
            public string registrationSource;
            
            [Header("Runtime Status")]
            public ModuleStatus status = ModuleStatus.Registered;
            public bool isActive = false;
            public string lastError = "";
            
            [Header("Module Metadata")]
            public string description = "";
            public string version = "";
            public List<string> tags = new List<string>();

            public ModuleEntry()
            {
                registrationTime = DateTime.Now;
            }

            public ModuleEntry(string uid, string name, string type)
            {
                this.uid = uid;
                moduleName = name;
                moduleType = type;
                registrationTime = DateTime.Now;
                registrationSource = GetRegistrationSource();
            }

            private string GetRegistrationSource()
            {
                // 實作與ServiceEntry類似的調用堆疊追蹤
                try
                {
                    var stackTrace = new System.Diagnostics.StackTrace(true);
                    var frames = stackTrace.GetFrames();
                    
                    for (int i = 0; i < frames.Length; i++)
                    {
                        var method = frames[i].GetMethod();
                        var declaringType = method.DeclaringType;
                        
                        if (declaringType != null && 
                            !declaringType.FullName.Contains("ServiceRegistry") &&
                            !declaringType.FullName.Contains("ModuleLocator"))
                        {
                            return $"{declaringType.Name}.{method.Name}";
                        }
                    }
                }
                catch { }
                
                return "Unknown";
            }

            public override string ToString()
            {
                return $"{moduleName} (UID: {uid}) - {status} - Active: {isActive}";
            }
        }

        /// <summary>
        /// 註冊事件記錄
        /// 
        /// 這個結構記錄了系統中發生的所有註冊/註銷事件。
        /// 這種事件溯源的方法讓您可以重現系統的狀態變化過程，
        /// 這在調試複雜的初始化問題時極其有價值。
        /// </summary>
        [System.Serializable]
        public class RegistrationEvent
        {
            public DateTime timestamp;
            public RegistrationEventType eventType;
            public string targetName;
            public string targetType;
            public string details;

            public RegistrationEvent(RegistrationEventType type, string name, string targetType, string details = "")
            {
                timestamp = DateTime.Now;
                eventType = type;
                targetName = name;
                this.targetType = targetType;
                this.details = details;
            }

            public override string ToString()
            {
                return $"[{timestamp:HH:mm:ss.fff}] {eventType}: {targetName} ({targetType}) - {details}";
            }
        }

        /// <summary>
        /// 服務狀態枚舉
        /// 
        /// 這個枚舉追蹤服務的完整生命週期。
        /// 在異步初始化的環境中，了解服務的當前狀態對於
        /// 故障診斷和性能分析都很重要。
        /// </summary>
        public enum ServiceStatus
        {
            Registered,      // 已註冊但未初始化
            Initializing,    // 正在初始化中
            Ready,          // 已初始化，可以使用
            Error,          // 初始化失敗或運行時錯誤
            Shutting_Down,  // 正在關閉中
            Shutdown        // 已關閉
        }

        /// <summary>
        /// 模組狀態枚舉
        /// </summary>
        public enum ModuleStatus
        {
            Registered,     // 已註冊但未初始化
            Initialized,    // 已初始化
            Error,         // 初始化失敗
            Shutdown       // 已關閉
        }

        /// <summary>
        /// 註冊事件類型枚舉
        /// </summary>
        public enum RegistrationEventType
        {
            ServiceRegistered,
            ServiceUnregistered,
            ServiceInitialized,
            ServiceError,
            ServiceShutdown,
            ModuleRegistered,
            ModuleUnregistered,
            ModuleInitialized,
            ModuleError,
            ModuleShutdown
        }

        #region Public API

        /// <summary>
        /// 註冊服務到註冊表
        /// 
        /// 這個方法不僅記錄註冊資訊，還啟動了服務的生命週期追蹤。
        /// 從這一刻開始，系統就會監控這個服務的各種狀態變化。
        /// </summary>
        public void RegisterService<T>(string implementationName)
        {
            var serviceType = typeof(T);
            var entry = new ServiceEntry(serviceType.Name, serviceType.FullName, implementationName);
            
            // 檢查是否已經註冊過同樣的服務
            var existingEntry = registeredServices.FirstOrDefault(s => s.serviceType == serviceType.FullName);
            if (existingEntry != null)
            {
                LogWarning($"Service {serviceType.Name} is already registered, updating entry");
                registeredServices.Remove(existingEntry);
            }
            
            registeredServices.Add(entry);
            
            AddRegistrationEvent(RegistrationEventType.ServiceRegistered, serviceType.Name, serviceType.FullName);
            
            if (enableDetailedLogging)
            {
                Debug.Log($"[ServiceRegistry] Registered service: {entry}");
            }
            
            MarkAsDirty();
        }

        /// <summary>
        /// 註冊模組到註冊表
        /// </summary>
        public void RegisterModule(string uid, string moduleName)
        {
            var entry = new ModuleEntry(uid, moduleName, moduleName);
            
            // 檢查UID衝突
            var existingEntry = registeredModules.FirstOrDefault(m => m.uid == uid);
            if (existingEntry != null)
            {
                LogWarning($"Module with UID {uid} is already registered, updating entry");
                registeredModules.Remove(existingEntry);
            }
            
            registeredModules.Add(entry);
            
            AddRegistrationEvent(RegistrationEventType.ModuleRegistered, moduleName, moduleName, $"UID: {uid}");
            
            if (enableDetailedLogging)
            {
                Debug.Log($"[ServiceRegistry] Registered module: {entry}");
            }
            
            MarkAsDirty();
        }

        /// <summary>
        /// 更新服務狀態
        /// 
        /// 這個方法讓MasterManager可以報告服務狀態的變化。
        /// 通過追蹤這些狀態變化，註冊表可以提供系統健康狀況的實時視圖。
        /// </summary>
        public void UpdateServiceStatus(Type serviceType, ServiceStatus status, float initTimeMs = 0f, string error = "")
        {
            var entry = registeredServices.FirstOrDefault(s => s.serviceType == serviceType.FullName);
            if (entry != null)
            {
                var oldStatus = entry.status;
                entry.status = status;
                
                if (initTimeMs > 0)
                    entry.initializationTimeMs = initTimeMs;
                    
                if (!string.IsNullOrEmpty(error))
                {
                    entry.lastError = error;
                    entry.errorCount++;
                }
                
                // 記錄狀態變化事件
                if (oldStatus != status)
                {
                    var eventType = status switch
                    {
                        ServiceStatus.Ready => RegistrationEventType.ServiceInitialized,
                        ServiceStatus.Error => RegistrationEventType.ServiceError,
                        ServiceStatus.Shutdown => RegistrationEventType.ServiceShutdown,
                        _ => RegistrationEventType.ServiceRegistered
                    };
                    
                    AddRegistrationEvent(eventType, entry.serviceName, serviceType.FullName, 
                        $"Status: {oldStatus} -> {status}");
                }
                
                MarkAsDirty();
            }
        }

        /// <summary>
        /// 更新模組狀態
        /// </summary>
        public void UpdateModuleStatus(string uid, ModuleStatus status, string error = "")
        {
            var entry = registeredModules.FirstOrDefault(m => m.uid == uid);
            if (entry != null)
            {
                var oldStatus = entry.status;
                entry.status = status;
                entry.isActive = status == ModuleStatus.Initialized;
                
                if (!string.IsNullOrEmpty(error))
                    entry.lastError = error;
                
                if (oldStatus != status)
                {
                    var eventType = status switch
                    {
                        ModuleStatus.Initialized => RegistrationEventType.ModuleInitialized,
                        ModuleStatus.Error => RegistrationEventType.ModuleError,
                        ModuleStatus.Shutdown => RegistrationEventType.ModuleShutdown,
                        _ => RegistrationEventType.ModuleRegistered
                    };
                    
                    AddRegistrationEvent(eventType, entry.moduleName, entry.moduleType, 
                        $"UID: {uid}, Status: {oldStatus} -> {status}");
                }
                
                MarkAsDirty();
            }
        }

        /// <summary>
        /// 獲取系統健康報告
        /// 
        /// 這個方法生成系統當前狀態的綜合報告。
        /// 在生產環境中，這個報告可以幫助快速識別系統問題。
        /// </summary>
        public SystemHealthReport GenerateHealthReport()
        {
            var report = new SystemHealthReport
            {
                GeneratedAt = DateTime.Now,
                TotalServices = registeredServices.Count,
                TotalModules = registeredModules.Count
            };
            
            // 統計服務狀態
            foreach (var service in registeredServices)
            {
                switch (service.status)
                {
                    case ServiceStatus.Ready:
                        report.HealthyServices++;
                        break;
                    case ServiceStatus.Error:
                        report.ErrorServices++;
                        report.ServiceErrors.Add($"{service.serviceName}: {service.lastError}");
                        break;
                    case ServiceStatus.Initializing:
                        report.InitializingServices++;
                        break;
                }
                
                report.TotalInitializationTime += service.initializationTimeMs;
            }
            
            // 統計模組狀態
            foreach (var module in registeredModules)
            {
                switch (module.status)
                {
                    case ModuleStatus.Initialized:
                        report.ActiveModules++;
                        break;
                    case ModuleStatus.Error:
                        report.ErrorModules++;
                        report.ModuleErrors.Add($"{module.moduleName} (UID: {module.uid}): {module.lastError}");
                        break;
                }
            }
            
            // 計算系統健康度分數 (0-100)
            var totalComponents = report.TotalServices + report.TotalModules;
            if (totalComponents > 0)
            {
                var healthyComponents = report.HealthyServices + report.ActiveModules;
                report.HealthScore = (int)((float)healthyComponents / totalComponents * 100);
            }
            
            return report;
        }

        /// <summary>
        /// 獲取最近的註冊事件
        /// 
        /// 這個方法對於調試初始化問題特別有用。
        /// 您可以查看最近發生了什麼註冊活動，幫助理解系統的當前狀態。
        /// </summary>
        public List<RegistrationEvent> GetRecentEvents(int count = 10)
        {
            return registrationHistory
                .OrderByDescending(e => e.timestamp)
                .Take(count)
                .ToList();
        }

        /// <summary>
        /// 清除所有註冊記錄
        /// 
        /// 這個方法主要用於測試或系統重置。
        /// 在生產環境中很少使用，因為註冊表的歷史資訊對故障排除很有價值。
        /// </summary>
        public void Clear()
        {
            registeredServices.Clear();
            registeredModules.Clear();
            registrationHistory.Clear();
            
            AddRegistrationEvent(RegistrationEventType.ServiceRegistered, "System", "Registry", "Registry cleared");
            
            MarkAsDirty();
        }

        #endregion

        #region Internal Methods

        private void AddRegistrationEvent(RegistrationEventType eventType, string name, string type, string details = "")
        {
            if (!trackRegistrationHistory)
                return;
                
            var registrationEvent = new RegistrationEvent(eventType, name, type, details);
            registrationHistory.Add(registrationEvent);
            
            // 限制歷史記錄的大小
            while (registrationHistory.Count > maxHistoryEntries)
            {
                registrationHistory.RemoveAt(0);
            }
        }

        private void LogWarning(string message)
        {
            if (enableDetailedLogging)
            {
                Debug.LogWarning($"[ServiceRegistry] {message}");
            }
        }

        private void MarkAsDirty()
        {
            lastModified = DateTime.Now;
            
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            #endif
        }

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            if (registrationHistory == null)
                registrationHistory = new List<RegistrationEvent>();
        }

        #endregion

        /// <summary>
        /// 系統健康報告結構
        /// 
        /// 這個結構提供了系統狀態的高級概覽。
        /// 它被設計成既能被程式讀取，也能被人類理解。
        /// </summary>
        [System.Serializable]
        public class SystemHealthReport
        {
            public DateTime GeneratedAt;
            public int HealthScore; // 0-100
            
            [Header("Services")]
            public int TotalServices;
            public int HealthyServices;
            public int InitializingServices;
            public int ErrorServices;
            public float TotalInitializationTime;
            
            [Header("Modules")]
            public int TotalModules;
            public int ActiveModules;
            public int ErrorModules;
            
            [Header("Error Details")]
            public List<string> ServiceErrors = new List<string>();
            public List<string> ModuleErrors = new List<string>();

            public bool IsHealthy => HealthScore >= 90 && ErrorServices == 0 && ErrorModules == 0;
            
            public override string ToString()
            {
                return $"System Health Report (Score: {HealthScore}/100)\n" +
                       $"Services: {HealthyServices}/{TotalServices} healthy, {ErrorServices} errors\n" +
                       $"Modules: {ActiveModules}/{TotalModules} active, {ErrorModules} errors\n" +
                       $"Total Init Time: {TotalInitializationTime:F2}ms";
            }
        }

        #region Editor Support

        #if UNITY_EDITOR
        [UnityEditor.MenuItem("Framework/Create Service Registry")]
        private static void CreateServiceRegistry()
        {
            var registry = CreateInstance<ServiceRegistry>();
            UnityEditor.AssetDatabase.CreateAsset(registry, "Assets/ServiceRegistry.asset");
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.EditorUtility.FocusProjectWindow();
            UnityEditor.Selection.activeObject = registry;
        }
        #endif

        #endregion
    }
}