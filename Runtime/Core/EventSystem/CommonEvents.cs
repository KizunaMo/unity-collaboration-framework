using UnityEngine;

namespace Collaboration.Framework.Core.EventSystem
{
    /// <summary>
    /// 常用遊戲事件定義
    /// 
    /// 這些事件類型展示了良好的事件設計原則：
    /// 1. 不可變性(immutable) - 所有屬性都是readonly
    /// 2. 清楚的命名約定
    /// 3. 包含所有必要的上下文資訊
    /// 4. 避免包含複雜的引用類型
    /// 
    /// 事件設計哲學：
    /// 好的事件設計應該提供足夠的信息讓接收者做出明智的決定，
    /// 但又不應該包含太多細節以至於造成緊耦合。
    /// 
    /// 例如，PlayerHealthChangedEvent不只包含新的血量值，
    /// 還包含最大血量和之前的血量，這讓接收者可以計算百分比、
    /// 傷害量等衍生資訊，而不需要依賴其他系統。
    /// </summary>
    
    /// <summary>
    /// 玩家血量變化事件
    /// 用於通知UI、音效、特效等系統更新
    /// 
    /// 這個事件展示了如何設計包含計算屬性的事件。
    /// HealthPercentage和DamageAmount是計算屬性，
    /// 它們基於基本資料提供便利的訪問方式。
    /// 這種設計讓事件接收者可以直接使用這些常用的計算結果，
    /// 而不需要重複實作相同的邏輯。
    /// </summary>
    public readonly struct PlayerHealthChangedEvent : IEvent
    {
        public readonly float NewHealth;
        public readonly float MaxHealth;
        public readonly float PreviousHealth;
        public readonly bool IsDamage;
        
        public PlayerHealthChangedEvent(float newHealth, float maxHealth, float previousHealth)
        {
            NewHealth = newHealth;
            MaxHealth = maxHealth;
            PreviousHealth = previousHealth;
            IsDamage = newHealth < previousHealth;
        }
        
        /// <summary>
        /// 血量百分比，常用於血條顯示
        /// </summary>
        public float HealthPercentage => MaxHealth > 0 ? NewHealth / MaxHealth : 0f;
        
        /// <summary>
        /// 傷害量，用於傷害數字顯示等
        /// </summary>
        public float DamageAmount => PreviousHealth - NewHealth;
    }
    
    /// <summary>
    /// 遊戲狀態變化事件
    /// 用於管理遊戲的整體狀態流程
    /// 
    /// 這個事件展示了如何處理狀態機相關的事件。
    /// 它不只包含新狀態，還包含之前的狀態和變化原因，
    /// 這樣接收者可以根據狀態轉換的具體情況做出不同的反應。
    /// 
    /// 例如，從Playing到Paused可能是玩家主動暫停，
    /// 但從Playing到GameOver可能需要播放失敗音效和動畫。
    /// </summary>
    public readonly struct GameStateChangedEvent : IEvent
    {
        public readonly GameState PreviousState;
        public readonly GameState NewState;
        public readonly string Reason;
        
        public GameStateChangedEvent(GameState previousState, GameState newState, string reason = "")
        {
            PreviousState = previousState;
            NewState = newState;
            Reason = reason;
        }
    }
    
    /// <summary>
    /// 遊戲狀態枚舉
    /// 
    /// 使用枚舉而不是字符串常量有幾個好處：
    /// 1. 編譯時檢查，減少拼寫錯誤
    /// 2. IDE的自動完成支援
    /// 3. 更好的性能（枚舉比較比字符串比較快）
    /// 4. 容易重構和維護
    /// </summary>
    public enum GameState
    {
        Loading,
        MainMenu,
        Playing,
        Paused,
        GameOver,
        Victory
    }
    
    /// <summary>
    /// 資源載入事件
    /// 用於顯示載入進度和狀態
    /// 
    /// 這個事件展示了如何處理異步操作的進度報告。
    /// 在Unity中，資源載入通常是異步的，
    /// 特別是使用Addressables系統時。
    /// 這個事件讓UI系統可以顯示準確的載入進度，
    /// 提升用戶體驗。
    /// </summary>
    public readonly struct ResourceLoadEvent : IEvent
    {
        public readonly string ResourcePath;
        public readonly float Progress;
        public readonly bool IsComplete;
        public readonly string ErrorMessage;
        
        public ResourceLoadEvent(string resourcePath, float progress, bool isComplete = false, string errorMessage = "")
        {
            ResourcePath = resourcePath;
            Progress = Mathf.Clamp01(progress);  // 確保進度值在0-1範圍內
            IsComplete = isComplete;
            ErrorMessage = errorMessage;
        }
    }
    
    /// <summary>
    /// 場景切換事件
    /// 用於協調場景切換過程中的各種系統
    /// 
    /// 場景切換是Unity遊戲中的重要操作，通常需要協調多個系統：
    /// - 保存遊戲狀態
    /// - 清理當前場景的資源
    /// - 載入新場景
    /// - 初始化新場景的系統
    /// 
    /// 這個事件通過不同的階段(Phase)讓各個系統知道
    /// 場景切換的當前狀態，並做出相應的反應。
    /// </summary>
    public readonly struct SceneTransitionEvent : IEvent
    {
        public readonly string FromScene;
        public readonly string ToScene;
        public readonly SceneTransitionPhase Phase;
        public readonly float Progress;
        
        public SceneTransitionEvent(string fromScene, string toScene, SceneTransitionPhase phase, float progress = 0f)
        {
            FromScene = fromScene;
            ToScene = toScene;
            Phase = phase;
            Progress = progress;
        }
    }
    
    /// <summary>
    /// 場景切換階段
    /// 
    /// 這個枚舉定義了場景切換的不同階段。
    /// 每個階段都可能觸發不同的系統操作：
    /// - Started: 開始場景切換，可能需要顯示載入畫面
    /// - LoadingNewScene: 正在載入新場景，更新載入進度
    /// - UnloadingOldScene: 卸載舊場景，清理資源
    /// - Complete: 場景切換完成，隱藏載入畫面
    /// </summary>
    public enum SceneTransitionPhase
    {
        Started,
        LoadingNewScene,
        UnloadingOldScene,
        Complete
    }
    
    /// <summary>
    /// 輸入事件基類
    /// 展示如何設計可擴展的事件層次結構
    /// 
    /// 這個事件展示了如何統一處理不同類型的輸入。
    /// 在現代遊戲中，玩家可能使用觸控、鼠標、鍵盤等多種輸入方式。
    /// 統一的輸入事件讓遊戲邏輯可以用相同的方式處理所有輸入，
    /// 而不需要為每種輸入類型寫不同的邏輯。
    /// </summary>
    public readonly struct InputEvent : IEvent
    {
        public readonly InputType Type;
        public readonly Vector2 Position;
        public readonly float Timestamp;
        
        public InputEvent(InputType type, Vector2 position)
        {
            Type = type;
            Position = position;
            Timestamp = Time.time;  // 記錄事件發生的時間
        }
    }
    
    /// <summary>
    /// 輸入類型枚舉
    /// 
    /// 這個枚舉涵蓋了常見的輸入類型。
    /// 在實際項目中，你可能需要根據具體需求添加更多類型，
    /// 比如手柄輸入、VR控制器輸入等。
    /// </summary>
    public enum InputType
    {
        TouchStart,
        TouchEnd,
        TouchMove,
        MouseClick,
        KeyPress
    }
    
    /// <summary>
    /// 物品收集事件
    /// 展示如何處理遊戲中的物品系統事件
    /// 
    /// 這個事件展示了如何為遊戲邏輯設計事件。
    /// 當玩家收集物品時，可能需要：
    /// - 更新背包UI
    /// - 播放收集音效
    /// - 顯示獲得物品的提示
    /// - 檢查是否完成任務
    /// - 更新統計數據
    /// 
    /// 通過事件系統，這些操作可以由不同的系統獨立處理，
    /// 而不需要在收集邏輯中硬編碼所有這些行為。
    /// </summary>
    public readonly struct ItemCollectedEvent : IEvent
    {
        public readonly string ItemId;
        public readonly int Quantity;
        public readonly Vector3 CollectionPosition;
        public readonly string CollectorId;
        
        public ItemCollectedEvent(string itemId, int quantity, Vector3 position, string collectorId)
        {
            ItemId = itemId;
            Quantity = quantity;
            CollectionPosition = position;
            CollectorId = collectorId;
        }
    }
    
    /// <summary>
    /// 關卡完成事件
    /// 展示如何設計包含豐富資訊的遊戲事件
    /// 
    /// 關卡完成是遊戲中的重要時刻，通常需要：
    /// - 計算得分和獎勵
    /// - 更新玩家進度
    /// - 解鎖新內容
    /// - 顯示結算畫面
    /// - 保存遊戲進度
    /// 
    /// 這個事件包含了所有這些系統可能需要的資訊。
    /// </summary>
    public readonly struct LevelCompletedEvent : IEvent
    {
        public readonly string LevelId;
        public readonly float CompletionTime;
        public readonly int Score;
        public readonly int CollectedItems;
        public readonly bool IsNewRecord;
        public readonly LevelCompletionRank Rank;
        
        public LevelCompletedEvent(string levelId, float completionTime, int score, int collectedItems, bool isNewRecord, LevelCompletionRank rank)
        {
            LevelId = levelId;
            CompletionTime = completionTime;
            Score = score;
            CollectedItems = collectedItems;
            IsNewRecord = isNewRecord;
            Rank = rank;
        }
    }
    
    /// <summary>
    /// 關卡完成評級
    /// 
    /// 使用枚舉來表示評級系統，
    /// 這樣可以很容易地添加新的評級或修改現有評級
    /// </summary>
    public enum LevelCompletionRank
    {
        Bronze,
        Silver,
        Gold,
        Perfect
    }
}