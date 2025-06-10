using System;
using System.Collections.Generic;
using UnityEngine;

namespace Collaboration.Framework.Core.EventSystem
{
    /// <summary>
    /// 高性能事件管理系統
    /// 
    /// 設計特點：
    /// 1. 類型安全的事件處理
    /// 2. 自動清理機制防止記憶體洩漏
    /// 3. 支援優先級排序
    /// 4. 性能優化的事件分發
    /// 
    /// 為什麼使用事件系統：
    /// 事件系統是解耦不同系統的最佳實踐。在傳統的做法中，當玩家受傷時，
    /// 可能需要直接調用UI系統更新血條、音效系統播放傷害音效、特效系統播放傷害特效。
    /// 這種直接耦合讓代碼難以維護和測試。
    /// 
    /// 使用事件系統後，當玩家受傷時，只需要發布一個PlayerHealthChanged事件，
    /// 所有關心這個事件的系統都會自動收到通知並作出相應的反應。
    /// 這樣的設計讓系統之間保持獨立，便於維護和擴展。
    /// 
    /// 使用原則：
    /// - 事件類型應該是不可變的(immutable)
    /// - 避免在事件處理中拋出異常
    /// - 長時間運行的邏輯應該異步處理
    /// </summary>
    public class EventManager : PersistentSingleton<EventManager>
    {
        // 使用字典存儲不同類型的事件處理器
        // 這種設計允許我們為每種事件類型維護獨立的處理器列表
        private readonly Dictionary<Type, List<IEventHandler>> _eventHandlers = new Dictionary<Type, List<IEventHandler>>();
        
        // 事件統計，用於性能分析
        // 在開發過程中，了解哪些事件被頻繁觸發對於性能優化很重要
        private readonly Dictionary<Type, int> _eventStats = new Dictionary<Type, int>();
        
        // 性能監控開關
        // 在發布版本中可以關閉以減少性能開銷
        [SerializeField] private bool _enablePerformanceMonitoring = false;
        
        /// <summary>
        /// 訂閱事件
        /// 
        /// 這個方法使用泛型確保類型安全。當你訂閱PlayerHealthChanged事件時，
        /// 編譯器會確保你提供的處理函數能夠接收正確的事件類型。
        /// 這樣可以在編譯時就發現類型錯誤，而不是在運行時。
        /// </summary>
        /// <typeparam name="T">事件類型</typeparam>
        /// <param name="handler">事件處理函數</param>
        /// <param name="priority">優先級，數值越小優先級越高</param>
        public void Subscribe<T>(Action<T> handler, int priority = 0) where T : IEvent
        {
            Type eventType = typeof(T);
            
            if (!_eventHandlers.ContainsKey(eventType))
            {
                _eventHandlers[eventType] = new List<IEventHandler>();
            }
            
            var eventHandler = new EventHandler<T>(handler, priority);
            _eventHandlers[eventType].Add(eventHandler);
            
            // 依照優先級排序
            // 這確保高優先級的處理器會先執行
            // 例如，關鍵的遊戲邏輯應該在UI更新之前處理
            _eventHandlers[eventType].Sort((a, b) => a.Priority.CompareTo(b.Priority));
            
            Debug.Log($"[EventManager] Subscribed to {eventType.Name}, Total handlers: {_eventHandlers[eventType].Count}");
        }
        
        /// <summary>
        /// 取消訂閱事件
        /// 
        /// 正確地取消訂閱是防止記憶體洩漏的關鍵。
        /// 當一個GameObject被銷毀時，如果它的事件處理器沒有被正確移除，
        /// 就會形成懸空引用，導致記憶體洩漏和潛在的空引用異常。
        /// </summary>
        /// <typeparam name="T">事件類型</typeparam>
        /// <param name="handler">要移除的事件處理函數</param>
        public void Unsubscribe<T>(Action<T> handler) where T : IEvent
        {
            Type eventType = typeof(T);
            
            if (_eventHandlers.ContainsKey(eventType))
            {
                _eventHandlers[eventType].RemoveAll(h => 
                    h is EventHandler<T> eventHandler && 
                    eventHandler.Handler.Equals(handler));
                    
                Debug.Log($"[EventManager] Unsubscribed from {eventType.Name}, Remaining handlers: {_eventHandlers[eventType].Count}");
            }
        }
        
        /// <summary>
        /// 發布事件
        /// 
        /// 這是事件系統的核心方法。當你調用這個方法時，
        /// 所有訂閱了該事件類型的處理器都會按優先級順序被調用。
        /// 
        /// 性能考慮：
        /// - 我們複製處理器列表以避免迭代過程中的修改問題
        /// - 使用try-catch確保一個處理器的異常不會影響其他處理器
        /// - 提供可選的性能監控來幫助優化
        /// </summary>
        /// <typeparam name="T">事件類型</typeparam>
        /// <param name="eventData">事件資料</param>
        public void Publish<T>(T eventData) where T : IEvent
        {
            Type eventType = typeof(T);
            
            // 性能監控
            if (_enablePerformanceMonitoring)
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                PublishInternal(eventType, eventData);
                stopwatch.Stop();
                
                Debug.Log($"[EventManager] Published {eventType.Name} in {stopwatch.ElapsedMilliseconds}ms");
            }
            else
            {
                PublishInternal(eventType, eventData);
            }
            
            // 更新統計
            if (!_eventStats.ContainsKey(eventType))
            {
                _eventStats[eventType] = 0;
            }
            _eventStats[eventType]++;
        }
        
        /// <summary>
        /// 內部事件發布邏輯
        /// 
        /// 這個方法被分離出來是為了保持代碼的清晰性。
        /// 主要的Publish方法處理性能監控和統計，
        /// 而這個方法專注於核心的事件分發邏輯。
        /// </summary>
        private void PublishInternal<T>(Type eventType, T eventData) where T : IEvent
        {
            if (_eventHandlers.ContainsKey(eventType))
            {
                // 複製處理器列表以避免迭代過程中的修改問題
                // 這是一個重要的安全措施：如果在事件處理過程中有處理器被添加或移除，
                // 我們不希望這影響當前的事件分發
                var handlers = new List<IEventHandler>(_eventHandlers[eventType]);
                
                foreach (var handler in handlers)
                {
                    try
                    {
                        if (handler is EventHandler<T> typedHandler)
                        {
                            typedHandler.Handle(eventData);
                        }
                    }
                    catch (Exception ex)
                    {
                        // 記錄錯誤但不讓異常中斷其他處理器的執行
                        // 這確保了一個錯誤的事件處理器不會破壞整個事件系統
                        Debug.LogError($"[EventManager] Error handling event {eventType.Name}: {ex.Message}");
                    }
                }
            }
        }
        
        /// <summary>
        /// 清除所有事件處理器
        /// 主要用於場景切換或遊戲重置
        /// 
        /// 在場景切換時清除所有事件處理器是一個好習慣，
        /// 因為舊場景中的處理器可能已經不再有效
        /// </summary>
        public void ClearAllHandlers()
        {
            _eventHandlers.Clear();
            Debug.Log("[EventManager] All event handlers cleared");
        }
        
        /// <summary>
        /// 清除特定類型的事件處理器
        /// 
        /// 有時候你可能只想清除特定類型的事件處理器，
        /// 比如在某個系統重新初始化時
        /// </summary>
        public void ClearHandlers<T>() where T : IEvent
        {
            Type eventType = typeof(T);
            if (_eventHandlers.ContainsKey(eventType))
            {
                _eventHandlers[eventType].Clear();
                Debug.Log($"[EventManager] Cleared all handlers for {eventType.Name}");
            }
        }
        
        /// <summary>
        /// 獲取事件統計資訊
        /// 
        /// 這個方法返回每種事件類型被觸發的次數。
        /// 在開發和調試階段，這些資訊有助於：
        /// 1. 發現性能瓶頸（哪些事件被過度觸發）
        /// 2. 驗證遊戲邏輯（確保關鍵事件被正確觸發）
        /// 3. 優化事件處理器的實作
        /// </summary>
        public Dictionary<Type, int> GetEventStats()
        {
            return new Dictionary<Type, int>(_eventStats);
        }
        
        /// <summary>
        /// 設置性能監控開關
        /// 
        /// 在開發階段開啟性能監控可以幫助識別性能問題，
        /// 但在發布版本中應該關閉以減少開銷
        /// </summary>
        public void SetPerformanceMonitoring(bool enabled)
        {
            _enablePerformanceMonitoring = enabled;
        }
        
        protected override void OnSingletonDestroyed()
        {
            ClearAllHandlers();
            _eventStats.Clear();
        }
    }
    
    /// <summary>
    /// 事件介面，所有事件類型必須實作此介面
    /// 
    /// 這個介面目前是空的，但它提供了未來擴展的可能性。
    /// 例如，我們可以添加時間戳記、事件ID等通用屬性。
    /// 更重要的是，它作為類型約束確保只有實作了IEvent的類型
    /// 才能被用作事件。
    /// </summary>
    public interface IEvent
    {
        // 可以添加通用的事件屬性，如時間戳記等
    }
    
    /// <summary>
    /// 內部事件處理器介面
    /// 
    /// 這個介面允許我們在不知道具體事件類型的情況下
    /// 管理事件處理器，主要用於優先級排序
    /// </summary>
    internal interface IEventHandler
    {
        int Priority { get; }
    }
    
    /// <summary>
    /// 泛型事件處理器實作
    /// 
    /// 這個類將用戶提供的Action包裝成我們的內部處理器格式，
    /// 同時添加了優先級支援
    /// </summary>
    internal class EventHandler<T> : IEventHandler where T : IEvent
    {
        public Action<T> Handler { get; }
        public int Priority { get; }
        
        public EventHandler(Action<T> handler, int priority)
        {
            Handler = handler;
            Priority = priority;
        }
        
        public void Handle(T eventData)
        {
            Handler?.Invoke(eventData);
        }
    }
}