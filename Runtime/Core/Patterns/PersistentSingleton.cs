using UnityEngine;

namespace Collaboration.Framework.Core.Patterns
{
    /// <summary>
    /// 持久化單例模式基類，適用於需要在場景切換時保持存在的管理類
    /// 
    /// 設計特點：
    /// 1. 線程安全的實例創建
    /// 2. 自動處理 DontDestroyOnLoad
    /// 3. 防止重複實例化
    /// 4. 提供清理機制
    /// 
    /// 使用場景：遊戲管理器、音效管理器、資料管理器等
    /// 
    /// 為什麼這個實作比標準單例更適合Unity：
    /// - 考慮了Unity的特殊生命週期（場景切換、應用退出等）
    /// - 自動處理MonoBehaviour的特殊需求
    /// - 提供了適合遊戲開發的錯誤處理機制
    /// - 支援在Inspector中監控單例狀態
    /// </summary>
    /// <typeparam name="T">繼承此類的具體類型</typeparam>
    public abstract class PersistentSingleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T _instance;
        private static readonly object _lock = new object();
        private static bool _applicationIsQuitting = false;

        /// <summary>
        /// 取得單例實例
        /// 使用雙重檢查鎖定模式確保線程安全
        /// 
        /// 這個實作解決了傳統單例模式在Unity中的問題：
        /// 1. 避免在應用程式結束時創建新實例
        /// 2. 處理Unity的非標準對象銷毀時機
        /// 3. 提供清晰的錯誤信息來幫助調試
        /// </summary>
        public static T Instance
        {
            get
            {
                // 應用程式結束時不創建新實例
                if (_applicationIsQuitting)
                {
                    Debug.LogWarning($"[{typeof(T)}] Instance accessed during application quit. Returning null.");
                    return null;
                }

                // 雙重檢查鎖定模式
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = FindObjectOfType<T>();

                            // 如果場景中沒有現有實例，創建新的
                            if (_instance == null)
                            {
                                GameObject singletonObject = new GameObject($"{typeof(T).Name} (Singleton)");
                                _instance = singletonObject.AddComponent<T>();
                                DontDestroyOnLoad(singletonObject);
                            }
                        }
                    }
                }

                return _instance;
            }
        }

        /// <summary>
        /// 初始化方法，在實例創建時調用
        /// 子類可以重寫此方法進行初始化邏輯
        /// 
        /// 重要：如果子類重寫Awake，必須調用base.Awake()
        /// </summary>
        protected virtual void Awake()
        {
            // 確保只有一個實例存在
            if (_instance == null)
            {
                _instance = this as T;
                DontDestroyOnLoad(gameObject);
                OnSingletonInitialized();
            }
            else if (_instance != this)
            {
                Debug.LogWarning($"[{typeof(T)}] Duplicate instance detected. Destroying duplicate.");
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 單例初始化完成時調用
        /// 子類可以重寫此方法進行自定義初始化
        /// 
        /// 這個方法在Awake之後調用，確保單例狀態已經穩定
        /// 適合進行需要單例狀態的初始化邏輯
        /// </summary>
        protected virtual void OnSingletonInitialized()
        {
            // 子類可以在此進行初始化邏輯
        }

        /// <summary>
        /// 應用程式結束時的清理
        /// 
        /// Unity在應用程式結束時會以不可預測的順序銷毀對象
        /// 這個方法確保我們能夠優雅地處理單例的清理
        /// </summary>
        protected virtual void OnApplicationQuit()
        {
            _applicationIsQuitting = true;
            OnSingletonDestroyed();
        }

        /// <summary>
        /// 單例銷毀時調用
        /// 子類可以重寫此方法進行清理邏輯
        /// 
        /// 在這裡進行資源清理、事件取消訂閱等操作
        /// </summary>
        protected virtual void OnSingletonDestroyed()
        {
            // 子類可以在此進行清理邏輯
        }

        /// <summary>
        /// 手動銷毀單例實例
        /// 主要用於測試或特殊情況下的清理
        /// 
        /// 警告：在正常遊戲流程中通常不需要手動銷毀單例
        /// 主要用於單元測試中的清理工作
        /// </summary>
        public static void DestroyInstance()
        {
            if (_instance != null)
            {
                Destroy(_instance.gameObject);
                _instance = null;
            }
        }

        /// <summary>
        /// 檢查實例是否已經存在
        /// 
        /// 這個屬性很有用，因為它允許代碼檢查單例是否存在
        /// 而不觸發自動創建機制
        /// </summary>
        public static bool HasInstance => _instance != null;
    }
}