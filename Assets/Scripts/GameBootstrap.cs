using UnityEngine;
using System.Collections.Generic;
using System.IO;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace SFM.Core
{
    /// <summary>
    /// Khởi tạo và chạy thử nghiệm game SFM:
    /// 1. Nạp CSV cơ sở dữ liệu cầu thủ.
    /// 2. Tạo FormationData (4-4-2, 4-3-3, 3-5-2) bằng code với vị trí đã tinh giản (CB, LB, RB).
    /// 3. Dựng 2 đội qua DraftManager (đảm bảo luôn có GK, không trùng cầu thủ khác năm).
    /// 4. Chạy thử 1 trận hoặc hàng trăm trận không chờ (instant) để kiểm tra cân bằng ~2.4 - 2.7 bàn/trận.
    /// 5. Đảm bảo UI EventSystem sử dụng InputSystemUIInputModule.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [Header("Data Source")]
        public TextAsset PlayerCsvAsset;
        public string CsvRelativePath = "Assets/Data/PlayersData.csv";

        [Header("Simulation Settings")]
        public bool RunBatchSimulationOnStart = true;
        public int BatchMatchCount = 500;
        public bool RunSingleMatchOnStart = false;

        [Header("References")]
        public PlayerDatabase Database;
        public MatchManager MatchManagerInstance;

        private DraftManager _draftManager = new DraftManager();
        private FormationData _form442;
        private FormationData _form433;
        private FormationData _form352;

        private void Awake()
        {
            Initialize();
        }

        public void Initialize()
        {
            EnsureInputSystemUI();
            InitializeDatabase();
            CreateFormationsByCode();
        }

        private void Start()
        {
            if (RunBatchSimulationOnStart)
            {
                RunBatchSimulation(BatchMatchCount);
            }
            else if (RunSingleMatchOnStart)
            {
                RunSingleMatch();
            }
        }

        /// <summary>
        /// Đảm bảo EventSystem sử dụng InputSystemUIInputModule theo chuẩn Unity 6 / New Input System.
        /// </summary>
        public static void EnsureInputSystemUI()
        {
            var eventSystem = FindFirstObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                var esGo = new GameObject("EventSystem");
                eventSystem = esGo.AddComponent<EventSystem>();
                esGo.AddComponent<InputSystemUIInputModule>();
                Debug.Log("[GameBootstrap] Đã tạo EventSystem kèm InputSystemUIInputModule.");
            }
            else
            {
                var legacyModule = eventSystem.GetComponent<StandaloneInputModule>();
                if (legacyModule != null)
                {
                    Destroy(legacyModule);
                }
                if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
                {
                    eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
                }
            }
        }

        private void InitializeDatabase()
        {
            if (Database == null)
            {
                Database = gameObject.AddComponent<PlayerDatabase>();
            }

            string csvText = "";
            if (PlayerCsvAsset != null)
            {
                csvText = PlayerCsvAsset.text;
            }
            else
            {
                string fullPath = Path.Combine(Application.dataPath, "..", CsvRelativePath);
                if (File.Exists(fullPath))
                {
                    csvText = File.ReadAllText(fullPath);
                }
                else
                {
                    Debug.LogError($"[GameBootstrap] Không tìm thấy file CSV tại: {fullPath}");
                    return;
                }
            }

            Database.LoadPlayersFromCSV(csvText);
            Debug.Log($"[GameBootstrap] Đã nạp thành công {Database.AllPlayers.Count} cầu thủ vào Database.");
        }

        /// <summary>
        /// Tạo FormationData 4-4-2, 4-3-3 và 3-5-2 bằng code thuần túy.
        /// Các vị trí trung vệ LCB/RCB được quy về CB; LWB/RWB quy về LB/RB cho đơn giản.
        /// </summary>
        public void CreateFormationsByCode()
        {
            // Sơ đồ 4-4-2 (10 cầu thủ tuyến trên: LB, CB, CB, RB, LM, CM, CM, RM, ST, ST)
            _form442 = ScriptableObject.CreateInstance<FormationData>();
            _form442.FormationName = "4-4-2";
            _form442.Slots = new List<FormationSlot>
            {
                new FormationSlot { Role = PositionRole.LB, BasePosition = new Vector2(-0.85f, -0.65f) },
                new FormationSlot { Role = PositionRole.CB, BasePosition = new Vector2(-0.35f, -0.70f) },
                new FormationSlot { Role = PositionRole.CB, BasePosition = new Vector2(0.35f, -0.70f) },
                new FormationSlot { Role = PositionRole.RB, BasePosition = new Vector2(0.85f, -0.65f) },

                new FormationSlot { Role = PositionRole.LM, BasePosition = new Vector2(-0.85f, -0.10f) },
                new FormationSlot { Role = PositionRole.CM, BasePosition = new Vector2(-0.30f, -0.15f) },
                new FormationSlot { Role = PositionRole.CM, BasePosition = new Vector2(0.30f, -0.15f) },
                new FormationSlot { Role = PositionRole.RM, BasePosition = new Vector2(0.85f, -0.10f) },

                new FormationSlot { Role = PositionRole.ST, BasePosition = new Vector2(-0.35f, 0.65f) },
                new FormationSlot { Role = PositionRole.ST, BasePosition = new Vector2(0.35f, 0.65f) }
            };

            // Sơ đồ 4-3-3 (10 cầu thủ tuyến trên: LB, CB, CB, RB, CM, CDM, CM, LW, ST, RW)
            _form433 = ScriptableObject.CreateInstance<FormationData>();
            _form433.FormationName = "4-3-3";
            _form433.Slots = new List<FormationSlot>
            {
                new FormationSlot { Role = PositionRole.LB, BasePosition = new Vector2(-0.85f, -0.65f) },
                new FormationSlot { Role = PositionRole.CB, BasePosition = new Vector2(-0.35f, -0.70f) },
                new FormationSlot { Role = PositionRole.CB, BasePosition = new Vector2(0.35f, -0.70f) },
                new FormationSlot { Role = PositionRole.RB, BasePosition = new Vector2(0.85f, -0.65f) },

                new FormationSlot { Role = PositionRole.CM, BasePosition = new Vector2(-0.45f, -0.10f) },
                new FormationSlot { Role = PositionRole.CDM, BasePosition = new Vector2(0f, -0.30f) },
                new FormationSlot { Role = PositionRole.CM, BasePosition = new Vector2(0.45f, -0.10f) },

                new FormationSlot { Role = PositionRole.LW, BasePosition = new Vector2(-0.80f, 0.60f) },
                new FormationSlot { Role = PositionRole.ST, BasePosition = new Vector2(0f, 0.70f) },
                new FormationSlot { Role = PositionRole.RW, BasePosition = new Vector2(0.80f, 0.60f) }
            };

            // Sơ đồ 3-5-2 (10 cầu thủ tuyến trên: CB, CB, CB, LB, CM, CDM, CM, RB, ST, ST)
            _form352 = ScriptableObject.CreateInstance<FormationData>();
            _form352.FormationName = "3-5-2";
            _form352.Slots = new List<FormationSlot>
            {
                new FormationSlot { Role = PositionRole.CB, BasePosition = new Vector2(-0.55f, -0.70f) },
                new FormationSlot { Role = PositionRole.CB, BasePosition = new Vector2(0f, -0.72f) },
                new FormationSlot { Role = PositionRole.CB, BasePosition = new Vector2(0.55f, -0.70f) },

                new FormationSlot { Role = PositionRole.LB, BasePosition = new Vector2(-0.88f, -0.10f) },
                new FormationSlot { Role = PositionRole.CM, BasePosition = new Vector2(-0.35f, -0.15f) },
                new FormationSlot { Role = PositionRole.CDM, BasePosition = new Vector2(0f, -0.35f) },
                new FormationSlot { Role = PositionRole.CM, BasePosition = new Vector2(0.35f, -0.15f) },
                new FormationSlot { Role = PositionRole.RB, BasePosition = new Vector2(0.88f, -0.10f) },

                new FormationSlot { Role = PositionRole.ST, BasePosition = new Vector2(-0.30f, 0.65f) },
                new FormationSlot { Role = PositionRole.ST, BasePosition = new Vector2(0.30f, 0.65f) }
            };
        }

        /// <summary>
        /// Tạo và chuẩn bị 2 đội đấu nhau qua hệ thống Draft:
        /// - redFormation: Sơ đồ đội Đỏ (mặc định 4-4-2)
        /// - blueFormation: Sơ đồ đội Xanh (mặc định 4-3-3)
        /// </summary>
        public (Team red, Team blue) SetupTeams(FormationData redFormation = null, FormationData blueFormation = null, bool redBest = true, bool blueBest = true)
        {
            if (redFormation == null) redFormation = _form442;
            if (blueFormation == null) blueFormation = _form433;

            _draftManager.Reset();

            var redTeam = new Team { TeamName = "Đội Đỏ" };
            var blueTeam = new Team { TeamName = "Đội Xanh" };

            // Draft đội Đỏ trước, rồi đến đội Xanh từ pool cầu thủ
            _draftManager.AutoDraftTeam(redTeam, redFormation, Database.AllPlayers, redBest);
            _draftManager.AutoDraftTeam(blueTeam, blueFormation, Database.AllPlayers, blueBest);

            return (redTeam, blueTeam);
        }

        /// <summary>
        /// Chạy mô phỏng hàng trăm trận tức thì để so sánh thống kê với chuẩn ~2.4 - 2.7 bàn/trận.
        /// </summary>
        public void RunBatchSimulation(int matches = 500)
        {
            if (MatchManagerInstance == null)
            {
                MatchManagerInstance = gameObject.AddComponent<MatchManager>();
            }

            int redWins = 0, blueWins = 0, draws = 0;
            int totalRedGoals = 0, totalBlueGoals = 0;
            int zeroZeroCount = 0;
            int highScoringCount = 0;

            for (int i = 0; i < matches; i++)
            {
                // Thay phiên đổi lượt pick trước để đảm bảo tính công bằng
                var (red, blue) = SetupTeams(_form442, _form433, true, true);
                MatchManagerInstance.HomeTeam = red;
                MatchManagerInstance.AwayTeam = blue;

                var (gr, gb) = MatchManagerInstance.SimulateFullMatchInstant();

                totalRedGoals += gr;
                totalBlueGoals += gb;

                if (gr > gb) redWins++;
                else if (gb > gr) blueWins++;
                else draws++;

                if (gr + gb == 0) zeroZeroCount++;
                if (gr + gb >= 7) highScoringCount++;
            }

            float totalGoals = totalRedGoals + totalBlueGoals;
            float avgGoalsPerMatch = totalGoals / matches;
            float redWinRate = (float)redWins / matches * 100f;
            float blueWinRate = (float)blueWins / matches * 100f;
            float drawRate = (float)draws / matches * 100f;
            float zeroZeroRate = (float)zeroZeroCount / matches * 100f;
            float highScoringRate = (float)highScoringCount / matches * 100f;

            Debug.Log($"<color=#38a169><b>================ KẾT QUẢ MÔ PHỎNG {matches} TRẬN C# (UNITY) ================</b></color>\n" +
                      $"Bàn thắng trung bình: <b>{avgGoalsPerMatch:F2}</b> bàn/trận (Đỏ {((float)totalRedGoals/matches):F2} - Xanh {((float)totalBlueGoals/matches):F2})\n" +
                      $"Đội Đỏ thắng: <b>{redWinRate:F1}%</b> | Hoà: <b>{drawRate:F1}%</b> | Đội Xanh thắng: <b>{blueWinRate:F1}%</b>\n" +
                      $"Tỷ số 0-0: <b>{zeroZeroRate:F1}%</b> | Trận đấu >= 7 bàn: <b>{highScoringRate:F1}%</b>\n" +
                      $"Chuẩn cân bằng JS: 2.2 - 3.3 bàn/trận => Trạng thái: {(avgGoalsPerMatch >= 2.2f && avgGoalsPerMatch <= 3.3f ? "<color=green>ĐẠT CHUẨN CÂN BẰNG</color>" : "<color=orange>CẦN TUNE LẠI</color>")}");
        }

        /// <summary>
        /// Khởi động 1 trận đấu thông thường có diễn tiến theo thời gian.
        /// </summary>
        public void RunSingleMatch()
        {
            if (MatchManagerInstance == null)
            {
                MatchManagerInstance = gameObject.AddComponent<MatchManager>();
            }

            var (red, blue) = SetupTeams(_form442, _form433, true, true);
            MatchManagerInstance.HomeTeam = red;
            MatchManagerInstance.AwayTeam = blue;
            MatchManagerInstance.OnMatchLog += (log) => Debug.Log($"[MatchLog] {log}");

            MatchManagerInstance.StartMatch();
        }
    }

#if UNITY_EDITOR
    public static class GameBootstrapEditor
    {
        [UnityEditor.MenuItem("SFM/Run Batch Simulation (500 Matches)")]
        public static void RunBatchFromEditor()
        {
            var go = new GameObject("BootstrapBatchTest");
            var boot = go.AddComponent<GameBootstrap>();
            boot.RunBatchSimulationOnStart = false;
            boot.RunSingleMatchOnStart = false;
            boot.Initialize();
            boot.RunBatchSimulation(500);
            Object.DestroyImmediate(go);
        }
    }
#endif
}
