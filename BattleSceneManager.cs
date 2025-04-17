using System.Collections;
using UnityEngine;
using DG.Tweening;
using TMPro;
using System.Collections.Generic;
using System;
using UnityEngine.UI;

public class BattleSceneManager : MonoBehaviour
{
    // BattleSceneManager.csの冒頭部分の修正

    public enum BattleState
    {
        FadeOut,               // フェードアウト中
        BattleStartText,       // バトル開始テキスト表示中
        PlayerCharacterSpawn,  // プレイヤーキャラクター出現中
        EnemySpawn,            // 敵キャラクター出現中 (新規追加)
        DeckPlacement,         // デッキ配置中
        PuzzleGenerating,      // パズル生成中（プレイヤー操作不可）
        PuzzleMatching,        // パズルマッチング中（プレイヤー操作可能）
        PlayerAttack,          // プレイヤー攻撃中
        EnemyAttack,           // 敵攻撃中
        EnemySkill,            // 敵スキル発動中（プレイヤー操作不可）
        SkillAnimation,  // スキルアニメーション中の状態
        SkillAfterProcess,     // スキル後処理中（新規追加）
        BattleInProgress       // バトル進行中（通常状態）
    }

   [SerializeField] private BattleState _currentState;

    // 読み取り専用プロパティを提供
    public BattleState currentState => _currentState;

   // [Header("Fade Panel Settings")]
   // [SerializeField] private GameObject fadePanel;
   // [SerializeField] private float fadeDuration = 1f;
   // private CanvasGroup fadeCanvasGroup;

    [Header("Battle UI & Text")]
    [SerializeField] private GameObject battleUI;
    [SerializeField] private TextMeshProUGUI battleStartText;

    [Header("Player Entrance Settings")]
    [SerializeField] private Transform[] playerSpawnPoints;
    [SerializeField] private GameObject[] playerCharacterPrefabs;
    [SerializeField] private float characterEntranceTime = 0.5f;

    [Header("Puzzle Manager")]
    [SerializeField] private PuzzleManager puzzleManager;

    [Header("Battle Information")]
    [SerializeField] private int battleCount = 1;

    [Header("Delays")]
    [SerializeField] private float delayAfterFade = 1f;
    [SerializeField] private float delayAfterBattleText = 0.3f;
    [SerializeField] private float delayBetweenAnimations = 0.5f;

    [Header("Puzzle/Match Manager")]
    [SerializeField] private MatchManager matchManager;

    [Header("Player Attack Manager")]
    [SerializeField] private PlayerAttackManager playerAttackManager;

    [Header("User Manager")]
    [SerializeField] private UserManager userManager;

    [Header("Scene Character Manager")]
    [SerializeField] private CharacterSceneManager characterSceneManager;

    [SerializeField] private Enemy enemy;
    [SerializeField] private EnemyUI enemyUI;

    // 入力クールダウン関連
    private bool isInputCooldown = false;
    private float inputCooldownDuration = 1.0f; // デフォルト1秒のクールダウン

    // デバッグフラグ
    [Header("Debug Options")]
    [SerializeField] private bool enableEnemyInitialSkill = true; // 敵の初期スキルを実行するかどうか

    // ターン管理用フラグ
    private bool isFirstTurn = true;
    // 初期スキル発動済みかどうかのフラグを追加
    private bool hasUsedInitialSkill = false;

    [Header("Enemy Entrance Settings")] // 新しいセクション
    [SerializeField] private EnemyEntranceManager enemyEntranceManager; // 新しいマネージャーへの参照



    // 1. クラス変数の宣言部分に以下を追加
    [Header("キャラクターUI")]
    [SerializeField] private CharacterUIUpdater characterUIUpdater;

    // BattleSceneManager.cs に追加するフィールド
    private bool isTurnTransitioning = false;  // ターン遷移中フラグ
    private float turnTransitionTimeout = 5.0f; // ターン遷移タイムアウト時間
    private Coroutine turnTransitionCoroutine;  // ターン遷移コルーチン
    // ユーザー操作中フラグ（新規追加）
    private bool isProcessingUserOperation = false;

    // タイムアウト時間（新規追加）
    private float userOperationTimeout = 10.0f;
    private float userOperationStartTime = 0f;

    // スキルアニメーション後のマッチ処理フラグ
    private bool isMatchingAfterSkill = false;

    // ユーザー操作開始時のタイムスタンプ
    private float lastUserOperationTime = 0f;
    // 同じ操作と見なす時間間隔（秒）
    private float operationDuplicationThreshold = 1.0f;

    // BattleSceneManager.cs に追加するフィールド
    private float turnTransitionStartTime = 0f;  // ターン遷移開始時刻

    // クラス変数として追加（他のフィールド宣言の近くに配置）
    private BattleState previousState;

    // タイムアウト監視用の変数を追加
    private float enemyAttackStartTime = 0;

    private bool isNewSystemActive = false;


    void Awake()
    {
       
      //  if (fadePanel == null)
        {
      //      Debug.LogError("Fade panel is not assigned!");
      //      return;
        }
      //  fadeCanvasGroup = fadePanel.GetComponent<CanvasGroup>();
       // if (fadeCanvasGroup == null)
       // {
           // fadeCanvasGroup = fadePanel.AddComponent<CanvasGroup>();
      //  }
    //    fadeCanvasGroup.alpha = 1f;
      //  fadePanel.SetActive(true);

        if (battleStartText != null)
        {
            battleStartText.alpha = 0f;
            battleStartText.gameObject.SetActive(false);
        }

        if (matchManager != null)
        {
            matchManager.OnMatchProcessCompleted += HandleMatchCompleted;
        }
        puzzleManager = FindAnyObjectByType<PuzzleManager>();
        userManager = FindAnyObjectByType<UserManager>();

        enemyUI = FindObjectOfType<EnemyUI>();

        if (characterSceneManager == null)
        {
            characterSceneManager = FindObjectOfType<CharacterSceneManager>();
        }

        if (characterUIUpdater == null)
        {
            characterUIUpdater = FindObjectOfType<CharacterUIUpdater>();
        }

        // EnemyEntranceManagerの取得
        if (enemyEntranceManager == null)
        {
            enemyEntranceManager = FindObjectOfType<EnemyEntranceManager>();
            if (enemyEntranceManager == null)
            {
                Debug.LogWarning("EnemyEntranceManagerが見つかりません。標準の敵登場演出が使用されます。");
            }
        }

        

        // BulletPool の初期化チェック
        CheckAndInitializeBulletPool();
        // メッセージャーの初期化
        InitializeEnemyDropActionMessenger();
        SetupSkillAnimationManager();
        // 新システムの検出
        isNewSystemActive = FindObjectOfType<NewBattleManager>() != null;

        if (isNewSystemActive)
        {
            Debug.Log("新バトルシステムが検出されました。旧システムはパッシブモードで動作します。");
            // クリティカルなイベント登録を行わない
            if (matchManager != null)
            {
                matchManager.OnMatchProcessCompleted -= HandleMatchCompleted;
            }
        }
    }

    void Start()
    {
        // 新システムが存在すれば旧システムの処理をスキップ
        if (FindObjectOfType<NewBattleManager>() != null)
        {
            Debug.Log("新バトルシステムが動作中のため、旧システムの初期化をスキップします");
            this.enabled = false; // Update()を呼ばないように
            return;
        }
        // シーン起動時にDropPoolの状態をチェック
        CheckDropPoolOnStart();
        StartCoroutine(StartBattleScene());

       
    }

    // ユーザー操作通知メソッド（新規追加）
    // ユーザー操作通知メソッド（修正版）
    public void NotifyUserOperation()
    {
        if (isNewSystemActive)
        {
            Debug.Log("新システムが動作中のため、旧システムの操作処理をスキップします");
            return;
        }
        // 入力クールダウン中は操作を無視
        if (isInputCooldown)
        {
            Debug.Log($"入力クールダウン中のため、操作をスキップします");
            return;
        }

        // スキル後のマッチ処理中は新しい操作を無視
        if (isMatchingAfterSkill)
        {
            Debug.Log("スキル後のマッチ処理中のため、新しい操作をスキップします");
            return;
        }

        // バトル状態がBattleInProgress以外の場合は操作を無視
        if (currentState != BattleState.BattleInProgress)
        {
            Debug.Log($"現在のバトル状態 ({currentState}) では操作を受け付けません");
            return;
        }

        // 短時間内の重複操作を防止
        float currentTime = Time.time;
        if (currentTime - lastUserOperationTime < operationDuplicationThreshold)
        {
            Debug.Log($"操作間隔が短すぎます ({currentTime - lastUserOperationTime:F2}秒)。重複操作を防止します。");
            return;
        }

        // 既に処理中ならスキップ
        if (isProcessingUserOperation || isTurnTransitioning)
        {
            Debug.LogWarning("すでにユーザー操作処理中またはターン遷移中です。重複操作を防止します。");
            return;
        }

        // 操作時間を記録
        lastUserOperationTime = currentTime;
        isProcessingUserOperation = true;
        userOperationStartTime = Time.time;

        Debug.Log("ユーザー操作を開始しました");

        // InputBlockerは現在のバトル状態に基づいて自動的に更新されるので
        // ここでは特に追加の操作は不要
    }
    // Update内でタイムアウトチェックを追加
    private void Update()
    {
        // 新システムアクティブ時はタイムアウト監視をスキップ
        if (isNewSystemActive)
        {
            return;
        }
        // ユーザー操作処理のタイムアウトチェック
        if (isProcessingUserOperation && (Time.time - userOperationStartTime) > userOperationTimeout)
        {
            Debug.LogWarning($"ユーザー操作がタイムアウトしました。状態をリセットします。");
            ResetBattleState();
        }

        // ターン遷移のタイムアウトチェック
        if (isTurnTransitioning && (Time.time - turnTransitionStartTime) > turnTransitionTimeout)
        {
            Debug.LogWarning($"ターン遷移処理がタイムアウトしました。状態をリセットします。");
            EnsureCorrectBattleState();
        }

      

        // EnemyAttack状態のタイムアウト監視
        if (currentState == BattleState.EnemyAttack)
        {
            // 初回のみ開始時間を記録
            if (enemyAttackStartTime <= 0)
            {
                enemyAttackStartTime = Time.time;
                Debug.Log("EnemyAttack状態の監視を開始: " + Time.time);
            }

            // タイムアウトチェック (10秒)
            if ((Time.time - enemyAttackStartTime) > 10.0f)
            {
                Debug.LogWarning($"EnemyAttack状態が10秒以上続いています。強制的にBattleInProgressに移行します。");
                TrySetBattleState(BattleState.BattleInProgress);
                enemyAttackStartTime = 0; // リセット

                // 入力ブロックがあれば解除
                InputBlocker inputBlocker = InputBlocker.Instance;
                if (inputBlocker != null)
                {
                    inputBlocker.SetForceInputBlocked(false);
                }
            }
        }
        else
        {
            // 状態が変わったらタイマーをリセット
            enemyAttackStartTime = 0;
        }

        // 状態変更を監視するために現在の状態を保存
        previousState = currentState;

        // 既存のUpdateロジック...
    }

    // 状態リセットメソッド（新規追加）
    private void ResetBattleState()
    {
        isProcessingUserOperation = false;
        isTurnTransitioning = false;

        // 入力ブロックを解除
        InputBlocker inputBlocker = InputBlocker.Instance;
        if (inputBlocker != null)
        {
            inputBlocker.SetForceInputBlocked(false);
        }

        // バトル状態をBattleInProgressに戻す（既にPlayerAttackやEnemyAttackなど進行中の場合を除く）
        if (currentState != BattleState.PlayerAttack &&
            currentState != BattleState.EnemyAttack &&
            currentState != BattleState.SkillAnimation)
        {
            TrySetBattleState(BattleState.BattleInProgress);
        }

        Debug.Log($"バトル状態をリセットしました。現在の状態: {currentState}");
    }

    private void CheckDropPoolOnStart()
    {
        Debug.Log("シーン起動時のDropPool状態チェック");

        // DropPoolが存在するか確認
        if (DropPool.Instance == null)
        {
            Debug.LogWarning("シーン起動時: DropPool.Instanceが存在しません。新規作成します。");
            GameObject newObj = new GameObject("DropPool_SceneStart");
            DropPool newInstance = newObj.AddComponent<DropPool>();
            DontDestroyOnLoad(newObj);

            // 初期化を待機
            StartCoroutine(WaitForDropPoolInitialization(newInstance));
        }
        else
        {
            // 既存のDropPoolをすべてのマネージャーにアサイン
            AssignDropPoolToAllManagers(DropPool.Instance);
        }
    }

    private IEnumerator WaitForDropPoolInitialization(DropPool pool)
    {
        // DropPoolの初期化を待機
        yield return new WaitForEndOfFrame();

        // すべてのマネージャーにアサイン
        AssignDropPoolToAllManagers(pool);
    }


    // StartBattleSceneコルーチンの修正 - 登場順序を変更
    IEnumerator StartBattleScene()
    {
        // フェードアウト処理
       // TrySetBattleState(BattleState.FadeOut);
      //  yield return StartCoroutine(HideFadePanelCoroutine());
        // DropPoolの状態確認
        CheckDropPool("フェードアウト後");
        yield return new WaitForSeconds(delayAfterFade);

        // メッセージャーが存在することを確認
        if (EnemyDropActionMessenger.Instance == null)
        {
            Debug.LogWarning("EnemyDropActionMessengerが初期化されていません。再初期化します。");
            InitializeEnemyDropActionMessenger();
        }

        // 戦闘開始テキスト表示
        TrySetBattleState(BattleState.BattleStartText);
        yield return StartCoroutine(ShowBattleStartTextCoroutine(battleCount));
        // パズル生成後にも確認
        CheckDropPool("パズル生成後");
        yield return new WaitForSeconds(delayAfterBattleText);

        // === 変更点1: プレイヤーキャラクター出現処理を先に実行 ===
        TrySetBattleState(BattleState.PlayerCharacterSpawn);
        if (characterSceneManager != null)
        {
            characterSceneManager.characterDeck = userManager.userData.characterDeck;
            characterSceneManager.PlaceCharactersInScene();
            yield return new WaitForSeconds(delayBetweenAnimations);
        }
        else
        {
            yield return StartCoroutine(SpawnPlayerCharactersCoroutine());
        }

        // キャラクターUIの初期化
        if (characterUIUpdater != null)
        {
            characterUIUpdater.InitializeUI();
            Debug.Log("キャラクターUIを初期化しました");
            yield return new WaitForSeconds(delayBetweenAnimations * 0.5f);
        }

        // HP UI 表示
        UserHPDisplayUI hpDisplay = FindObjectOfType<UserHPDisplayUI>();
        if (hpDisplay != null)
        {
            hpDisplay.gameObject.SetActive(true);
            yield return new WaitForSeconds(delayBetweenAnimations);
            hpDisplay.AnimateInitialHP();
        }
        else
        {
            Debug.LogWarning("UserHPDisplayUI not found in scene.");
        }

        yield return new WaitForSeconds(delayBetweenAnimations);

        // StartBattleSceneコルーチン内のエネミー出現処理部分を修正

        // エネミー出現処理
        TrySetBattleState(BattleState.EnemySpawn);

        // まず敵を生成（非表示状態で）
        EnemyManager enemyManager = FindObjectOfType<EnemyManager>();
        if (enemyManager != null)
        {
            enemyManager.SpawnEnemies();
        }
        else
        {
            Debug.LogError("BattleSceneManager: EnemyManagerが見つかりません。敵を生成できません。");
            yield break;
        }

        // 重要: 敵生成を確実にするため少し待機
        yield return new WaitForSeconds(0.1f);

        // EnemyEntranceManagerによる演出を開始
        if (enemyEntranceManager != null)
        {
            Debug.Log("EnemyEntranceManagerによる敵登場演出を開始");
            bool entranceComplete = false;

            enemyEntranceManager.PlayEnemyEntrance(() => {
                entranceComplete = true;
            }, true);

            // 演出完了を待機
            while (!entranceComplete)
            {
                yield return null;
            }

            Debug.Log("敵登場演出完了");
        }

        // エネミー参照を取得
        enemy = FindObjectOfType<Enemy>();

        // エネミーのUIをセットアップ
        Enemy[] enemies = FindObjectsOfType<Enemy>();
        foreach (Enemy e in enemies)
        {
            e.SetEnemyUI(enemyUI);
        }

        // UIデータの設定とアニメーション
        if (enemy != null && enemy.enemyData != null && enemyUI != null)
        {
            enemyUI.SetEnemyData(enemy.enemyData);
            yield return StartCoroutine(enemyUI.AnimateUIElements());
            Debug.Log($"エネミーUI初期化: HP={enemy.enemyData.hp}, AttackCount={enemy.enemyData.turnsUntilAttack}");
        }

        yield return new WaitForSeconds(delayBetweenAnimations);

        // === 変更点3: デッキ表示処理はそのまま維持 ===
        TrySetBattleState(BattleState.DeckPlacement);

        // パズルグリッド生成（状態をPuzzleGeneratingに変更）
        TrySetBattleState(BattleState.PuzzleGenerating);
        Debug.Log("パズルグリッド生成を開始します...");

        if (puzzleManager != null)
        {
            // InputBlockerを明示的にブロック状態に設定
            InputBlocker inputBlocker = InputBlocker.Instance;
            if (inputBlocker != null)
            {
                inputBlocker.SetForceInputBlocked(true);
            }

            yield return StartCoroutine(puzzleManager.GeneratePuzzleGrid());
            yield return new WaitForSeconds(delayBetweenAnimations);

            // ブロック解除はしない（この後の敵スキル処理まで継続）
        }

        // 敵の初期スキル発動（フラグチェックを追加）
        if (enemy != null && enableEnemyInitialSkill && !hasUsedInitialSkill)
        {
            // 敵スキル発動中に状態を変更
            TrySetBattleState(BattleState.EnemySkill);
            Debug.Log("敵が初期スキルを発動します...");
            yield return new WaitForSeconds(0.5f);

            yield return StartCoroutine(enemy.UseInitialSkill());

            // フラグをセット
            hasUsedInitialSkill = true;

            yield return new WaitForSeconds(delayBetweenAnimations);
        }

        // 全処理完了後にブロック解除
        InputBlocker blockInstance = InputBlocker.Instance;
        if (blockInstance != null)
        {
            blockInstance.SetForceInputBlocked(false);
        }
        enemy = FindObjectOfType<Enemy>();
        enemy.SaveOriginalPosition();
        isFirstTurn = true; // 変更: 初回操作時にカウントダウンをスキップするためtrue に設定

        // バトル進行中状態に移行
        TrySetBattleState(BattleState.BattleInProgress);
        Debug.Log("バトル進行中状態に移行しました");

        // 重要: ここでisFirstTurnを明示的に設定
        isFirstTurn = false; // 初回操作も通常ターンとして扱う
    }

    /// <summary>
    /// フォールバックとしてプレハブを直接生成する場合に呼び出されるコルーチン。
    /// </summary>
    IEnumerator SpawnPlayerCharactersCoroutine()
    {
        if (playerCharacterPrefabs == null || playerCharacterPrefabs.Length == 0)
        {
            Debug.LogWarning("playerCharacterPrefabs is empty. Cannot spawn characters.");
            yield break;
        }
        if (playerSpawnPoints == null || playerSpawnPoints.Length == 0)
        {
            Debug.LogWarning("playerSpawnPoints is empty. No spawn points available.");
            yield break;
        }

        for (int i = 0; i < playerCharacterPrefabs.Length; i++)
        {
            if (i < playerSpawnPoints.Length)
            {
                // 生成
                Transform spawnPoint = playerSpawnPoints[i];
                GameObject character = Instantiate(playerCharacterPrefabs[i], spawnPoint.position, Quaternion.identity);

                // 1) 画面下部へ初期配置
                float offScreenY = spawnPoint.position.y - 5f;
                character.transform.position = new Vector3(
                    spawnPoint.position.x,
                    offScreenY,
                    spawnPoint.position.z
                );

                // 2) スケールを0にする
                character.transform.localScale = Vector3.zero;

                // 3) 下から上へのイグ+0→1のスケールを同時に行う
                Sequence seq = DOTween.Sequence();
                seq.Join(character.transform.DOMoveY(spawnPoint.position.y, characterEntranceTime).SetEase(Ease.OutCubic));
                seq.Join(character.transform.DOScale(1f, characterEntranceTime).SetEase(Ease.OutBack));

                // 演出完了まで待つ
                yield return seq.WaitForCompletion();

                // 次のキャラ登場まで少し待つ
                yield return new WaitForSeconds(delayBetweenAnimations);
            }
        }
    }

    /*
    IEnumerator ShowFadePanelCoroutine()
    {
        fadePanel.SetActive(true);
        fadeCanvasGroup.alpha = 0f;
        yield return fadeCanvasGroup.DOFade(1f, fadeDuration).WaitForCompletion();
    }

    IEnumerator HideFadePanelCoroutine()
    {
        fadePanel.SetActive(true);
        fadeCanvasGroup.alpha = 1f;
        yield return fadeCanvasGroup.DOFade(0f, fadeDuration).WaitForCompletion();
        fadePanel.SetActive(false);
    }
    */
    IEnumerator ShowBattleStartTextCoroutine(int battleCount)
    {
        if (battleStartText == null)
            yield break;

        battleStartText.text = $"Battle {battleCount} Start";
        battleStartText.alpha = 0f;
        battleStartText.gameObject.SetActive(true);
        yield return battleStartText.DOFade(1f, 0.5f).WaitForCompletion();
        yield return new WaitForSeconds(1f);
        yield return battleStartText.DOFade(0f, 0.5f).WaitForCompletion();
        battleStartText.gameObject.SetActive(false);
    }

    // BattleSceneManager.cs に修正を追加

    private void HandleMatchCompleted()
    {
        if (isNewSystemActive) return;
        // 明示的にログを出力してデバッグ
        Debug.Log("MatchManager.OnMatchProcessCompleted イベント発火：PlayerAttackPhase を開始します");
        StartCoroutine(PlayerAttackPhase());
    }

    // BattleSceneManager.cs 内の PlayerAttackPhase メソッドの修正版

    // PlayerAttackPhase メソッドの修正（余裕を持たせる版）
    private IEnumerator PlayerAttackPhase()
    {
        if (isNewSystemActive) yield break;
        // 既にターン遷移中なら処理をスキップ
        if (isTurnTransitioning)
        {
            Debug.LogWarning("ターン遷移が既に進行中です。重複実行を防止しました。");
            yield break;
        }

        // ターン遷移中フラグをセット
        isTurnTransitioning = true;
        turnTransitionStartTime = Time.time; // タイムアウト計測用に開始時間を記録

        // ブロッカーを明示的に強制ブロック状態に設定
        InputBlocker inputBlocker = InputBlocker.Instance;
        if (inputBlocker != null)
        {
            inputBlocker.SetForceInputBlocked(true);
            Debug.Log("プレイヤー攻撃フェーズ: 入力をブロックしました");
        }

        // プレイヤー攻撃フェーズ開始
        TrySetBattleState(BattleState.PlayerAttack);
        Debug.Log("プレイヤー攻撃フェーズ開始");

        if (userManager?.userData != null)
        {
            if (playerAttackManager != null)
            {
                // 攻撃が完全に終わるまで待機（リンクアタックを含む）
                yield return StartCoroutine(playerAttackManager.ExecuteMatchedTypeCharactersAttackAndWait());
                Debug.Log("プレイヤーの攻撃アニメーションが完了しました");

                // 重要: 消去ドロップカウントをリセット
                if (puzzleManager != null)
                {
                    puzzleManager.ClearRemovedDropsCount();
                    Debug.Log("攻撃後に消去ドロップカウントをリセットしました");
                }

                // 攻撃完了後、スキル発動前に十分な余裕を持たせる
                yield return new WaitForSeconds(1.5f);
                Debug.Log("攻撃完了後の余裕時間が経過しました");

                // ここで予約されたスキルの発動をチェックして実行する処理を追加
                yield return StartCoroutine(CheckAndExecutePendingSkills());
            }
        }

        // エネミーターン前に余裕を持たせる
        yield return new WaitForSeconds(2.0f);
        Debug.Log("エネミーターン開始前の余裕時間が経過しました");

        // メインエネミーの攻撃処理
        Debug.Log("エネミー攻撃フェーズ開始");
        TrySetBattleState(BattleState.EnemyAttack);
        yield return StartCoroutine(HandleMainEnemyAttack());

      

        // エネミーターン終了後に余裕を持たせる
        yield return new WaitForSeconds(2.0f);
        Debug.Log("エネミーターン終了後の余裕時間が経過しました");

        // すべての処理完了後に強制ブロックを解除
        if (inputBlocker != null)
        {
            inputBlocker.SetForceInputBlocked(false);
            Debug.Log("プレイヤー攻撃フェーズ終了: 入力ブロックを解除しました");
        }

        // すべての処理完了後にフラグをリセット
        isTurnTransitioning = false;

        // 修正: 直接次のターンに移行するのではなく、現在の状態を維持
        // EnemyAttackがすでに終了しているため、次のユーザー操作を待つ状態へ
        Debug.Log("ターンの完了を維持し、次のユーザー操作を待機します");
        // currentState = BattleState.BattleInProgress; <-- この行を削除または以下に修正

        // スキルのチェックを行う（待機中のスキルがあれば実行）
        SkillManager skillManager = FindObjectOfType<SkillManager>();
        if (skillManager != null && skillManager.HasPendingSkills())
        {
            Debug.Log("待機中のスキルが見つかりました - スキル実行フェーズに移行");
            TrySetBattleState(BattleState.SkillAnimation);
            StartCoroutine(skillManager.ExecuteAllPendingSkills());
        }
        else
        {
            // スキルがない場合のみBattleInProgressに戻る
            Debug.Log("ターン完了 - 次のユーザー操作を待機します");
           // currentState = BattleState.BattleInProgress;
        }

        // 重要: 入力無視期間を設ける
        StartCoroutine(EnableInputAfterCooldown(1.0f));
    }

    // 新規追加: 入力無視クールダウンコルーチン
    private IEnumerator EnableInputAfterCooldown(float cooldownTime)
    {
        // 入力無視フラグを立てる（新しいフラグを追加する必要があります）
        isInputCooldown = true;
        Debug.Log($"入力無視クールダウン開始: {cooldownTime}秒");

        yield return new WaitForSeconds(cooldownTime);

        // クールダウン終了
        isInputCooldown = false;
        Debug.Log("入力無視クールダウン終了");
    }

    // 新しいメソッド: 予約されたスキルの発動をチェックして実行
    private IEnumerator CheckAndExecutePendingSkills()
    {
        // SkillManagerのインスタンスを取得
        SkillManager skillManager = FindObjectOfType<SkillManager>();
        if (skillManager == null || !skillManager.HasPendingSkills())
        {
            // スキルマネージャーがないか、予約スキルがない場合は何もしない
            yield break;
        }

        Debug.Log("予約されたスキルの発動処理を開始します");

        // スキルアニメーション状態に移行
        TrySetBattleState(BattleState.SkillAnimation);

        // すべての予約スキルが実行完了するまで待機
        yield return StartCoroutine(skillManager.ExecuteAllPendingSkills());

        // スキル実行後に少し待機
        yield return new WaitForSeconds(0.5f);

        Debug.Log("予約されたスキルの発動処理が完了しました");
    }

    public IEnumerator HandleMainEnemyAttack()
    {
        // 新システムが動作しているか確認
        bool isNewSystemActive = FindObjectOfType<NewBattleManager>() != null;

        if (isNewSystemActive)
        {
            Debug.Log("BattleSceneManager: 新バトルシステムが動作中のため、処理をスキップします");
            yield return new WaitForSeconds(0.5f);
            yield break;
        }

        // メインエネミーのターンとアタック処理
        if (enemy != null && enemy.enemyData != null && enemyUI != null)
        {
            // 第1ターン目かどうかで処理を分岐
            if (!isFirstTurn)
            {
                // 最初のターンでなければカウントダウンを行う
                enemy.DecrementTurnCounter();
                Debug.Log($"エネミーのターンカウントダウン: {enemy.CurrentTurnCounter}");
            }
            else
            {
                // 最初のターンはカウントダウンをスキップ
                isFirstTurn = false;
                Debug.Log("最初のターン: カウントダウンはスキップします");
            }

            yield return new WaitForSeconds(delayBetweenAnimations);

            // 被弾中なら待機
            if (enemy.IsTakingDamage)
            {
                Debug.Log("エネミーがまだ被弾中のため、攻撃を待機します...");

                // タイムアウト付きの待機処理
                bool waitComplete = false;
                float startWaitTime = Time.time;

                // 一度だけイベントハンドラを追加
                System.Action damageCompletedHandler = null;
                damageCompletedHandler = () =>
                {
                    waitComplete = true;
                    if (enemy != null)
                    {
                        enemy.OnDamageAnimationCompleted -= damageCompletedHandler;
                    }
                };

                enemy.OnDamageAnimationCompleted += damageCompletedHandler;

                // 被弾アニメーション完了またはタイムアウトまで待機
                while (!waitComplete && (Time.time - startWaitTime) < 3.0f)
                {
                    yield return null;
                }

                // タイムアウト時のクリーンアップ
                if (!waitComplete && enemy != null)
                {
                    Debug.LogWarning("エネミー被弾アニメーション待機がタイムアウトしました");
                    enemy.OnDamageAnimationCompleted -= damageCompletedHandler;
                }

                // 少し余裕を持たせる
                yield return new WaitForSeconds(0.2f);
            }

            // 攻撃タイミングの確認 - カウンターが0以下の場合のみ攻撃を実行
            if (enemy.CurrentTurnCounter <= 0)
            {
                yield return new WaitForSeconds(2f);
                TrySetBattleState(BattleState.EnemyAttack);
                enemy.Attack();

                // 攻撃完了を待機するためのタイムアウト付き待機
                float attackTimeout = 5.0f;
                float attackStartTime = Time.time;
                bool attackComplete = false;

                // 攻撃完了イベントの一時監視
                System.Action attackCompletedHandler = null;
                attackCompletedHandler = () =>
                {
                    attackComplete = true;
                };

                // 攻撃完了まで待機（タイムアウト付き）
                while (!attackComplete && (Time.time - attackStartTime) < attackTimeout)
                {
                    yield return null;
                }

                if (!attackComplete)
                {
                    Debug.LogWarning("エネミー攻撃完了待機がタイムアウトしました");
                }

                yield return new WaitForSeconds(delayBetweenAnimations);
            }
            else
            {
                // カウントダウンはしたが、まだ攻撃タイミングではないケース
                Debug.Log($"エネミーの攻撃カウントダウンが {enemy.CurrentTurnCounter} のため、攻撃をスキップします");
            }
        }

        // ★★★ 追加: ここで明示的に次の処理に進む ★★★
        Debug.Log("メインエネミー攻撃処理が完了しました。EnemyDropアクションを開始します。");



        // EnemyDropアクションを処理
        yield return StartCoroutine(HandleEnemyDropActions());

        // すべてのエネミー処理が完了した後に次の状態に移行
        // (PlayerAttackPhaseから呼ばれた場合でも、ここでの状態遷移が必要)
        Debug.Log("すべてのエネミー処理が完了しました。次の状態に移行します。");

        // BattleInProgressに移行して次のターンに備える
        TrySetBattleState(BattleState.BattleInProgress);
    }

    // 安全なターン遷移を保証するためのメソッドを追加
    public void EnsureCorrectBattleState()
    {
        // 一定時間経過後も状態が変わっていない場合に強制的にリセット
        if (currentState == BattleState.PlayerAttack ||
            currentState == BattleState.EnemyAttack ||
            currentState == BattleState.EnemySkill)
        {
            Debug.LogWarning($"バトル状態が {currentState} で停滞しているため、強制的にリセットします");

            // InputBlockerのリセット
            InputBlocker inputBlocker = InputBlocker.Instance;
            if (inputBlocker != null)
            {
                inputBlocker.SetForceInputBlocked(false);
            }

            // 状態をバトル進行中に戻す
            TrySetBattleState(BattleState.BattleInProgress);
            isTurnTransitioning = false;
        }
    }

    // BattleSceneManager.cs の HandleEnemyDropActions メソッドを修正
    private IEnumerator HandleEnemyDropActions()
    {
        Debug.Log("EnemyDropアクション処理を開始します");

        // すべてのEnemyDropインスタンスを検索
        EnemyDrop[] enemyDrops = FindObjectsOfType<EnemyDrop>();

        // nullやアクティブでないものをフィルタリング
        List<EnemyDrop> activeEnemyDrops = new List<EnemyDrop>();
        foreach (var drop in enemyDrops)
        {
            if (drop != null && drop.gameObject.activeInHierarchy)
            {
                activeEnemyDrops.Add(drop);
            }
        }

        Debug.Log($"アクティブなEnemyDrop: {activeEnemyDrops.Count}個");

        if (activeEnemyDrops.Count == 0)
        {
            Debug.Log("アクティブなEnemyDropがないため、処理をスキップします");
            yield break;
        }

        List<EnemyDrop> readyToAct = new List<EnemyDrop>();

        // 各エネミードロップのターン終了処理
        foreach (EnemyDrop drop in activeEnemyDrops)
        {
            bool isReady = drop.ProcessTurnEnd();
            Debug.Log($"EnemyDrop {drop.name} - ターン処理: アクション準備完了={isReady}");

            if (isReady)
            {
                readyToAct.Add(drop);
            }
        }

        // 行動準備ができているエネミードロップの処理
        if (readyToAct.Count > 0)
        {
            Debug.Log($"{readyToAct.Count}個のEnemyDropがアクションを実行します");

            foreach (EnemyDrop drop in readyToAct)
            {
                // 個別のエネミードロップアクション実行を別コルーチンに分離
                yield return StartCoroutine(ProcessSingleEnemyDropAction(drop));

                // 次の行動までの間隔
                yield return new WaitForSeconds(0.5f);
            }
        }
        else
        {
            Debug.Log("アクションを実行するEnemyDropがありません");
        }

        // 処理完了後のログ出力
        Debug.Log("EnemyDropアクション処理が完了しました");
    }

    // 新しく追加するヘルパーメソッド - 単一のエネミードロップアクション処理
    private IEnumerator ProcessSingleEnemyDropAction(EnemyDrop drop)
    {
        if (drop == null || !drop.gameObject.activeInHierarchy)
        {
            Debug.LogWarning($"非アクティブなEnemyDropアクションをスキップ");
            yield break;
        }

        Debug.Log($"EnemyDrop実行: {drop.name}");

        // アクション実行
        drop.ExecuteActionAndReset();

        // アクション完了待ち（タイムアウト付き）
        float startTime = Time.time;
        float timeout = 2.0f;
        bool actionComplete = false;

        // イベントハンドラ
        System.Action completionHandler = null;
        completionHandler = () => {
            actionComplete = true;
        };

        // イベントハンドラを登録
        drop.OnActionCompleted += completionHandler;

        // 完了またはタイムアウトまで待機
        while (!actionComplete && (Time.time - startTime) < timeout)
        {
            yield return null;
        }

        // イベントハンドラを必ず解除
        drop.OnActionCompleted -= completionHandler;

        // タイムアウト時のログ
        if (!actionComplete)
        {
            Debug.LogWarning($"EnemyDrop {drop.name} のアクション完了待機がタイムアウトしました");
        }
    }
    private void OnDestroy()
    {
        if (matchManager != null)
        {
            matchManager.OnMatchProcessCompleted -= HandleMatchCompleted;
        }
    }

    // メッセージャー初期化用の追加メソッド
    private void InitializeEnemyDropActionMessenger()
    {
        // メッセージャーが既に存在するか確認
        EnemyDropActionMessenger messenger = FindObjectOfType<EnemyDropActionMessenger>();
        if (messenger == null)
        {
            // 存在しなければ初期化（Instanceにアクセスするだけで自動的にシングルトンが生成される）
            Debug.Log("EnemyDropActionMessengerを初期化します");
            var instance = EnemyDropActionMessenger.Instance;

            if (instance != null)
            {
                Debug.Log("EnemyDropActionMessengerが正常に初期化されました");
            }
        }
        else
        {
            Debug.Log("EnemyDropActionMessengerは既に存在しています");
        }
    }


    // DropPoolの状態を確認して必要なら再生成し、アサインするヘルパーメソッド
    private void CheckDropPool(string checkpoint)
    {
        DropPool instance = DropPool.Instance;

        if (instance == null)
        {
            Debug.LogWarning($"チェックポイント「{checkpoint}」: DropPool.Instanceがnullです。再生成します。");
            GameObject newObj = new GameObject("DropPool_BattleManager");
            DropPool newInstance = newObj.AddComponent<DropPool>();
            DontDestroyOnLoad(newObj);
            instance = newInstance;
        }

        Debug.Log($"チェックポイント「{checkpoint}」: DropPool.Instance = {instance.name}");

        // すべてのマネージャーにDropPoolを自動アサイン
        AssignDropPoolToAllManagers(instance);
    }

    // すべてのマネージャーにDropPoolをアサイン
    private void AssignDropPoolToAllManagers(DropPool pool)
    {
        // DropManagerの更新
        DropManager[] dropManagers = FindObjectsOfType<DropManager>();
        foreach (var manager in dropManagers)
        {
            SetFieldValue(manager, "dropPool", pool);
            Debug.Log($"DropManager {manager.name} にDropPoolをアサインしました");
        }

        // DropRemoverの更新
        DropRemover[] dropRemovers = FindObjectsOfType<DropRemover>();
        foreach (var remover in dropRemovers)
        {
            SetFieldValue(remover, "dropPool", pool);
            Debug.Log($"DropRemover {remover.name} にDropPoolをアサインしました");
        }
    }

    // リフレクションを使用してフィールド値を設定するヘルパーメソッド
    private void SetFieldValue(object target, string fieldName, object value)
    {
        if (target == null) return;

        var type = target.GetType();

        // private/protectedフィールドを試す
        var field = type.GetField(fieldName,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic);

        if (field != null)
        {
            field.SetValue(target, value);
            return;
        }

        // publicフィールドを試す
        field = type.GetField(fieldName);
        if (field != null)
        {
            field.SetValue(target, value);
            return;
        }

        // SerializeFieldアトリビュートのフィールドも検索
        var allFields = type.GetFields(System.Reflection.BindingFlags.Instance |
                                     System.Reflection.BindingFlags.NonPublic |
                                     System.Reflection.BindingFlags.Public);

        foreach (var f in allFields)
        {
            // フィールドにSerializeFieldアトリビュートがあるか確認
            var attr = f.GetCustomAttributes(typeof(SerializeField), true);
            if (attr.Length > 0 && f.Name.EndsWith(fieldName, StringComparison.OrdinalIgnoreCase))
            {
                f.SetValue(target, value);
                return;
            }
        }

        Debug.LogWarning($"フィールド '{fieldName}' が {type.Name} クラスに見つかりませんでした");
    }

    // BattleSceneManager.cs に追加するメソッド
    // BattleSceneManager.cs の CheckAndInitializeBulletPool メソッドを強化

    // BattleSceneManager.cs の CheckAndInitializeBulletPool メソッドをシンプルにする

    private void CheckAndInitializeBulletPool()
    {
        // BulletPool がすでに存在するか確認
        BulletPool bulletPool = FindObjectOfType<BulletPool>();

        if (bulletPool == null)
        {
            Debug.Log("BulletPool が見つかりません。新規作成します。");

            // BulletPool オブジェクトを作成
            GameObject bulletPoolObj = new GameObject("BulletPool");
            bulletPool = bulletPoolObj.AddComponent<BulletPool>();

            // シーン間で破棄されないようにする
            DontDestroyOnLoad(bulletPoolObj);

            Debug.Log("BulletPool を作成しました: " + bulletPoolObj.name);
        }
        else
        {
            Debug.Log("既存の BulletPool を使用します: " + bulletPool.name);
        }
    }

    // SkillAnimationManagerを取得して相互参照を設定
    private void SetupSkillAnimationManager()
    {
        SkillAnimationManager skillAnimManager = SkillAnimationManager.Instance;
        if (skillAnimManager != null)
        {
            // private変数にSerializeFieldがついている場合は、リフレクションで設定する必要があります
            var field = skillAnimManager.GetType().GetField("battleSceneManager",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public);

            if (field != null)
            {
                field.SetValue(skillAnimManager, this);
                Debug.Log("BattleSceneManager: SkillAnimationManagerに自身の参照を設定しました");
            }
            else
            {
                Debug.LogWarning("BattleSceneManager: SkillAnimationManagerのbattleSceneManagerフィールドが見つかりません");
            }
        }
        else
        {
            Debug.LogWarning("BattleSceneManager: SkillAnimationManagerのインスタンスが見つかりません");
        }
    }

  

    // BattleSceneManagerに追加
    public void StartSkillAfterProcessMonitoring()
    {
        StartCoroutine(MonitorSkillAfterProcess());
    }

    // スキルアニメーション完了時に呼び出されるハンドラを修正
    private void OnSkillAnimationCompleted()
    {
        Debug.Log("スキルアニメーション完了イベントを受信しました");
        isMatchingAfterSkill = true;  // スキル後のマッチ処理フラグを設定

        // タイムアウト設定
        StartCoroutine(ResetMatchingAfterSkillFlag(3.0f));
    }

    // 9. スキル後のマッチ処理フラグをリセットするコルーチン
    // BattleSceneManager.cs に追加または修正

    // 既存のフラグリセットメソッドを改善
    private IEnumerator ResetMatchingAfterSkillFlag(float timeout)
    {
        // 指定された待機時間
        float originalTimeout = timeout;
        float startTime = Time.time;

        // MatchManagerのマッチング処理と空セル補充が完了するまで待機
        MatchManager matchManager = FindObjectOfType<MatchManager>();
        bool matchingComplete = false;
        bool fillingComplete = false;

        while (Time.time - startTime < timeout * 2) // 余裕を持って2倍の時間まで待機
        {
            // マッチング処理の状態確認
            if (matchManager != null)
            {
                matchingComplete = !IsMatchingInProgress();
                fillingComplete = !IsGridFillingInProgress();

                // すべての処理が完了したら待機終了
                if (matchingComplete && fillingComplete)
                {
                    Debug.Log("スキル後処理: マッチングと補充が完了しました");
                    break;
                }
            }
            else
            {
                // MatchManagerが見つからない場合は元の時間で待機
                if (Time.time - startTime > originalTimeout)
                {
                    Debug.LogWarning("スキル後処理: MatchManagerが見つからないため、タイムアウトします");
                    break;
                }
            }

            yield return null;
        }

        // さらに1秒待機（アニメーションなどの完了を保証）
        yield return new WaitForSeconds(2.0f);

        // すべての処理が完了したらフラグをリセット
        if (isMatchingAfterSkill)
        {
            Debug.Log("スキル後のマッチ処理フラグをリセットします");
            isMatchingAfterSkill = false;
        }

        // バトル状態を正常に戻す
        if (currentState == BattleState.SkillAnimation || currentState == BattleState.SkillAfterProcess)
        {
            // 重要: ここでBattleInProgressに戻さない。エネミーターンに直接移行
            TrySetBattleState(BattleState.EnemyAttack);
            Debug.Log("スキル処理完了後、エネミーターン状態に直接移行しました");
            yield return new WaitForSeconds(2f);

            // エネミーターン処理を開始
            StartCoroutine(HandleMainEnemyAttack());

            // InputBlockerを解除
            InputBlocker inputBlocker = InputBlocker.Instance;
            if (inputBlocker != null)
            {
                inputBlocker.SetForceInputBlocked(false);
                Debug.Log("スキル処理完了後、入力ブロックを解除しました");
            }
        }
    }
    // スキル処理開始時にも明示的にフラグを設定
    public void StartSkillProcessing()
    {
        isMatchingAfterSkill = true;
        TrySetBattleState(BattleState.SkillAnimation);
        Debug.Log("スキル処理を開始し、スキル後マッチングフラグを設定しました");
    }

    // イベント登録を追加
    private void OnEnable()
    {
        // スキルアニメーションイベントを購読
        SkillAnimationManager.OnSkillAnimationStarted += OnSkillAnimationStarted;
        SkillAnimationManager.OnSkillAnimationCompleted += OnSkillAnimationCompleted;
    }

    private void OnDisable()
    {
        // イベント購読解除
        SkillAnimationManager.OnSkillAnimationStarted -= OnSkillAnimationStarted;
        SkillAnimationManager.OnSkillAnimationCompleted -= OnSkillAnimationCompleted;
    }

    // スキルアニメーション開始時のハンドラ
    private void OnSkillAnimationStarted()
    {
        Debug.Log("スキルアニメーション開始イベントを受信しました");
        isMatchingAfterSkill = false;  // 念のためリセット
    }

    private IEnumerator MonitorSkillAfterProcess()
    {
        Debug.Log("スキル後処理の監視を開始しました [現在の状態: " + currentState + "]");

        // 状態を確実にSkillAfterProcessにする
        if (currentState != BattleState.SkillAfterProcess)
        {
            Debug.Log("スキル後処理: 状態を明示的にSkillAfterProcessに設定します");
            TrySetBattleState(BattleState.SkillAfterProcess);
        }

        // スキル後の盤面変化が落ち着くまで待機
        float startTime = Time.time;
        float timeout = 5.0f;

        // マッチ処理やドロップ補充が完了するまで待機
        while (Time.time - startTime < timeout)
        {
            // 現在の状態をチェック - 外部から変更された場合に検出
            if (currentState != BattleState.SkillAfterProcess)
            {
                Debug.LogWarning($"スキル後処理中に状態が変更されました: {currentState}。監視を中止します。");
                yield break;
            }

            // 処理が全て完了したかをチェック
            bool matchingCompleted = !IsMatchingInProgress();
            bool fillingCompleted = !IsGridFillingInProgress();

            if (matchingCompleted && fillingCompleted)
            {
                // 全処理完了
                Debug.Log("スキル後処理: マッチングと補充が完了しました");
                break;
            }

            yield return null;
        }

        // 念のためもう一度状態を確認
        if (currentState != BattleState.SkillAfterProcess)
        {
            Debug.LogWarning($"スキル後処理完了時に状態が変更されていました: {currentState}。以降の処理をスキップします。");
            yield break;
        }

        // 少し待機してからスキルマネージャーをチェック（安定化のため）
        yield return new WaitForSeconds(0.5f);

        // ここが重要: スキルマネージャーを取得して待機スキルの有無を確認
        SkillManager skillManager = FindObjectOfType<SkillManager>();

        if (skillManager != null && skillManager.HasPendingSkills())
        {
            Debug.Log("スキル後処理完了: 待機中のスキルが見つかりました。次のスキル実行へ移行します。");

            // SkillAnimation状態に戻す（重要）
            TrySetBattleState(BattleState.SkillAnimation);

            // 待機スキルの実行をスキルマネージャーに依頼
            yield return StartCoroutine(skillManager.ExecuteAllPendingSkills());

            // この後の状態遷移はスキルマネージャー側で処理するので終了
            yield break;
        }
        yield return new WaitForSeconds(2f);
        // 重要: この時点で直接エネミーアタックに移行し、BattleInProgressをスキップする
        Debug.Log("スキル後処理完了: 待機スキルがないため、エネミーターンに直接移行します");
        TrySetBattleState(BattleState.EnemyAttack);

        // エネミーターン処理を開始
        StartCoroutine(HandleMainEnemyAttack());

        // InputBlockerを解除
        InputBlocker inputBlocker = InputBlocker.Instance;
        if (inputBlocker != null)
        {
            inputBlocker.SetForceInputBlocked(false);
            Debug.Log("スキル後処理完了: 入力ブロックを解除しました");
        }
    }

    // マッチング処理中かどうかを確認するヘルパーメソッド
    public bool IsMatchingInProgress()
    {
        MatchManager matchManager = FindObjectOfType<MatchManager>();
        if (matchManager != null)
        {
            return matchManager.IsMatchingInProgress();
        }
        return false;
    }

    // グリッド補充中かどうかを確認するヘルパーメソッド
    private bool IsGridFillingInProgress()
    {
        // GridFillerの状態を確認するための実装
        MatchManager matchManager = FindObjectOfType<MatchManager>();
        if (matchManager != null)
        {
            // GridManagerを通じて空のセルがあるかを確認
            int rows = matchManager.GetRows();
            int cols = matchManager.GetColumns();

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    if (matchManager.GetDropItemAt(r, c) == null)
                    {
                        // 空のセルがある場合はまだ補充中
                        return true;
                    }
                }
            }
        }

        // 空のセルがない場合は補充完了とみなす
        return false;
    }

    // BattleSceneManager.cs に追加するメソッド
    public bool IsMatchingAfterSkill()
    {
        return isMatchingAfterSkill;
    }

    // BattleSceneManager.cs に追加
    public void SetMatchingAfterSkill(bool value)
    {
        if (value != isMatchingAfterSkill)
        {
            isMatchingAfterSkill = value;
            Debug.Log($"スキル後マッチングフラグを {value} に設定しました");

            if (value)
            {
                // フラグがtrueに設定された場合、タイムアウト付きリセットを開始
                StartCoroutine(ResetMatchingAfterSkillFlag(5.0f));
            }
        }
    }

    // BattleSceneManager.cs に追加するメソッド
    public IEnumerator ForceStateTransitionAfterTimeout(BattleState targetState, float timeout = 5.0f)
    {
        // 現在の状態を記録
        BattleState initialState = currentState;
        Debug.Log($"状態タイムアウト監視開始: {initialState} → {targetState}, タイムアウト: {timeout}秒");

        float startTime = Time.time;

        // タイムアウトまで待機
        while (Time.time - startTime < timeout && currentState == initialState)
        {
            yield return null;
        }

        // タイムアウトしても状態が変わっていない場合は強制的に変更
        if (currentState == initialState)
        {
            Debug.LogWarning($"状態遷移タイムアウト: {timeout}秒経過しても {initialState} から変化しないため、強制的に {targetState} に変更します");
            TrySetBattleState(targetState);

            // InputBlockerの解除
            InputBlocker inputBlocker = InputBlocker.Instance;
            if (inputBlocker != null)
            {
                inputBlocker.SetForceInputBlocked(false);
                Debug.Log("タイムアウトにより入力ブロックを解除しました");
            }
        }
    }

    // 入力クールダウン状態を外部から確認できるようにするメソッド
    public bool IsInputCooldown()
    {
        return isInputCooldown;
    }

    // 状態を変更するメソッド
    // BattleSceneManager.cs の TrySetBattleState メソッドを修正
    public bool TrySetBattleState(BattleState newState)
    {
        // 現在の状態と同じなら何もしない
        if (_currentState == newState) return true;

        // 遷移が許可されているかチェック
        if (!IsStateTransitionAllowed(_currentState, newState))
        {
            Debug.LogWarning($"不正な状態遷移: {_currentState} → {newState} は許可されていません");
            return false;
        }

        // 遷移を記録
        BattleState oldState = _currentState;
        _currentState = newState;

        // ログ出力
        Debug.Log($"[{Time.time:F2}] 状態変更: {oldState} → {newState}");

        // 特定の状態への遷移時の処理
        switch (newState)
        {
            case BattleState.PuzzleMatching:
                // PuzzleMatching状態に入る時に、MatchManagerの状態も確認
                MatchManager matchManager = FindObjectOfType<MatchManager>();
                if (matchManager != null && !matchManager.IsMatchingInProgress())
                {
                    // MatchManagerがマッチング中でない場合、状態を確認
                    Debug.LogWarning("PuzzleMatchingに移行しましたが、MatchManagerはマッチング中ではありません。");

                    // GridFillerもチェック
                    GridFiller gridFiller = FindObjectOfType<GridFiller>();
                    if (gridFiller != null && gridFiller.IsFillingInProgress())
                    {
                        Debug.Log("しかし、GridFillerはまだ補填中です。補填完了を待ちます。");
                    }
                    else
                    {
                        // マッチングも補填も行われていない場合、状態を修正
                        Debug.LogWarning("マッチングも補填も行われていないため、状態を修正します。");
                        StartCoroutine(ForceStateTransitionAfterTimeout(BattleState.BattleInProgress, 0.5f));
                    }
                }
                break;

                // 他の状態への遷移処理も必要に応じて追加
        }

        return true;
    }

    // 状態遷移が許可されているかチェック
    private bool IsStateTransitionAllowed(BattleState fromState, BattleState toState)
    {
        switch (fromState)
        {
            case BattleState.FadeOut:
                // フェードアウトからは、バトル開始テキスト表示のみ許可
                return toState == BattleState.BattleStartText;

            case BattleState.BattleStartText:
                // バトル開始テキストからは、プレイヤーキャラクター出現のみ許可
                return toState == BattleState.PlayerCharacterSpawn;

            case BattleState.PlayerCharacterSpawn:
                // プレイヤーキャラクター出現からは、敵キャラクター出現のみ許可
                return toState == BattleState.EnemySpawn;

            case BattleState.EnemySpawn:
                // 敵キャラクター出現からは、デッキ配置のみ許可
                return toState == BattleState.DeckPlacement;

            case BattleState.DeckPlacement:
                // デッキ配置からは、パズル生成のみ許可
                return toState == BattleState.PuzzleGenerating;

            case BattleState.PuzzleGenerating:
                // パズル生成からは、敵スキルか通常バトルへの遷移を許可
                return toState == BattleState.EnemySkill || toState == BattleState.BattleInProgress;

            case BattleState.PuzzleMatching:
                // パズルマッチングからは、プレイヤー攻撃か敵攻撃への遷移を許可
                return toState == BattleState.PlayerAttack || toState == BattleState.EnemyAttack;

            case BattleState.PlayerAttack:
                // プレイヤー攻撃からは、スキルアニメーションか敵攻撃への遷移を許可
                return toState == BattleState.SkillAnimation || toState == BattleState.EnemyAttack;

            case BattleState.EnemyAttack:
                // 敵攻撃からは、通常バトル状態への遷移のみ許可
                return toState == BattleState.BattleInProgress;

            case BattleState.EnemySkill:
                // 敵スキルからは、通常バトル状態への遷移のみ許可
                return toState == BattleState.BattleInProgress;

            case BattleState.SkillAnimation:
                // スキルアニメーションからは、スキル後処理のみ許可
                return toState == BattleState.SkillAfterProcess;

            case BattleState.SkillAfterProcess:
                // スキル後処理からは、スキルアニメーション(連続スキル用)かエネミーアタックのみ許可
                return toState == BattleState.SkillAnimation || toState == BattleState.EnemyAttack;

            case BattleState.BattleInProgress:
                // 通常バトル状態からは、パズルマッチングかスキルアニメーションへの遷移を許可
                return toState == BattleState.PuzzleMatching || toState == BattleState.SkillAnimation;

            default:
                // その他の状態遷移は制限なし（安全装置）
                Debug.LogWarning($"未定義の状態遷移: {fromState} → {toState}、許可します");
                return true;
        }
    }

    // BattleSceneManager.cs に追加

    // 新システム用に状態を同期するメソッド
    public void SyncStateWithNewSystem(NewBattleStateType newStateType)
    {
        if (!isNewSystemActive) return;

        // 新システムの状態を旧システムの状態に変換
        BattleState correspondingState = ConvertNewStateToOldState(newStateType);

        // 状態を同期（アニメーションなどUIのみ）
        _currentState = correspondingState;
    }

    // 状態変換ヘルパーメソッド
    private BattleState ConvertNewStateToOldState(NewBattleStateType newStateType)
    {
        switch (newStateType)
        {
            case NewBattleStateType.FadeOut: return BattleState.FadeOut;
            case NewBattleStateType.BattleStartText: return BattleState.BattleStartText;
            case NewBattleStateType.PlayerCharacterSpawn: return BattleState.PlayerCharacterSpawn;
            case NewBattleStateType.EnemySpawn: return BattleState.EnemySpawn;
            case NewBattleStateType.DeckPlacement: return BattleState.DeckPlacement;
            case NewBattleStateType.PuzzleGenerating: return BattleState.PuzzleGenerating;
            case NewBattleStateType.PuzzleMatching: return BattleState.PuzzleMatching;
            case NewBattleStateType.PlayerAttack: return BattleState.PlayerAttack;
            case NewBattleStateType.EnemyAttack: return BattleState.EnemyAttack;
            case NewBattleStateType.EnemySkill: return BattleState.EnemySkill;
            case NewBattleStateType.SkillAnimation: return BattleState.SkillAnimation;
            case NewBattleStateType.SkillAfterProcess: return BattleState.SkillAfterProcess;
            case NewBattleStateType.BattleInProgress: return BattleState.BattleInProgress;
            default: return BattleState.BattleInProgress;
        }
    }

    // BattleSceneManager.cs に追加

    // 新システムへの参照を設定
    public void SetNewSystemActive(bool active)
    {
        isNewSystemActive = active;

        if (isNewSystemActive)
        {
            // イベント登録を明示的に解除
            if (matchManager != null)
            {
                matchManager.OnMatchProcessCompleted -= HandleMatchCompleted;
            }

            // その他のイベント解除...

            Debug.Log("旧バトルシステムをパッシブモードに設定しました");
        }
    }

    // BattleSceneManager.cs に追加

    public void DisableTimeoutMonitoring()
    {
        // タイムアウト監視を無効化
        this.enabled = false; // Updateを呼ばないようにする
        Debug.Log("タイムアウト監視を無効化しました");
    }

    public void UnsubscribeFromSkillEvents()
    {
        // スキル関連イベントの購読を解除
        if (SkillAnimationManager.Instance != null)
        {
            SkillAnimationManager.OnSkillAnimationStarted -= OnSkillAnimationStarted;
            SkillAnimationManager.OnSkillAnimationCompleted -= OnSkillAnimationCompleted;
            Debug.Log("スキルアニメーションイベントの購読を解除しました");
        }
    }

    // 新システムから利用可能な公開メソッド
    public bool IsNewSystemActive() => isNewSystemActive;
}
