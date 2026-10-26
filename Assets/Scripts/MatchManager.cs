using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace SFM.Core
{
    public enum MatchState 
    { 
        PreMatch, 
        FirstHalf, 
        HalfTime, 
        SecondHalf, 
        FullTime 
    }

    public class MatchManager : MonoBehaviour
    {
        [Header("Teams")]
        public Team HomeTeam;
        public Team AwayTeam;

        [Header("Match Info")]
        public int HomeScore = 0;
        public int AwayScore = 0;
        public int CurrentMinute = 0;                 // Tối đa 45 / 90 (bù giờ nằm ở MinuteLabel)
        public string MinuteLabel = "0'";             // Ví dụ: 12', 45+2', 90+3'
        public int ExtraTimeFirstHalf = 0;
        public int ExtraTimeSecondHalf = 0;
        public MatchState State = MatchState.PreMatch;

        [Header("Settings")]
        [Tooltip("Số giây đời thực để trôi qua 1 phút trong game")]
        public float SecondsPerMinute = 0.5f; 
        public float HalfTimeSeconds = 3f;

        /// <summary>UI có thể đăng ký để hiện log trận đấu.</summary>
        public event Action<string> OnMatchLog;

        // ===== Tỷ lệ các loại sự kiện mỗi phút (cộng dồn, khớp bản JS) =====
        // 0.00-0.45 triển khai bóng | -0.60 rê bóng | -0.75 chọc khe | -0.83 sút xa | -0.91 tạt bóng | còn lại: phòng ngự
        private const float ChanceBuildup = 0.45f;
        private const float ChanceDribble = 0.60f;
        private const float ChanceThroughPass = 0.75f;
        private const float ChanceLongShot = 0.83f;
        private const float ChanceCross = 0.91f;

        // ===== Tỷ lệ sút hỏng / bị chặn (khớp bản JS đã tune) =====
        private const float OneVOneOffTarget = 0.35f;
        private const float ThroughBallOffTarget = 0.40f;
        private const float LongShotBlocked = 0.35f;
        private const float LongShotOffTarget = 0.45f;
        private const float HeaderOffTarget = 0.50f;
        private const float RedCardChance = 0.10f;      // xác suất thẻ đỏ khi phạm lỗi kéo người (khớp RED_CARD_CHANCE bên JS)
        private const float ManAdvantage = 0.08f;       // mỗi người hơn = +8% xác suất giành quyền tấn công (khớp MAN_ADVANTAGE bên JS)

        private class Attack
        {
            public Team Atk;
            public Team Def;
            public List<FormationSlot> AtkSlots;
            public List<FormationSlot> DefSlots;
            public Goalkeeper Gk;
            public bool HomeAttacking;
        }

        public void StartMatch()
        {
            HomeScore = 0;
            AwayScore = 0;
            CurrentMinute = 0;
            MinuteLabel = "0'";
            HomeTeam.ResetMatchStats();
            AwayTeam.ResetMatchStats();

            ExtraTimeFirstHalf = UnityEngine.Random.Range(1, 6);   // 1..5 phút
            ExtraTimeSecondHalf = UnityEngine.Random.Range(1, 6);

            Log($"Trận đấu bắt đầu: {HomeTeam.TeamName} vs {AwayTeam.TeamName}");
            StartCoroutine(MatchLoop());
        }

        /// <summary>
        /// Chạy nhanh toàn bộ trận đấu không cần Coroutine (phục vụ mô phỏng hàng trăm trận để kiểm tra cân bằng).
        /// </summary>
        public (int homeScore, int awayScore) SimulateFullMatchInstant()
        {
            HomeScore = 0;
            AwayScore = 0;
            CurrentMinute = 0;
            MinuteLabel = "0'";
            HomeTeam.ResetMatchStats();
            AwayTeam.ResetMatchStats();

            ExtraTimeFirstHalf = UnityEngine.Random.Range(1, 6);
            ExtraTimeSecondHalf = UnityEngine.Random.Range(1, 6);

            State = MatchState.FirstHalf;
            int lastH1 = 45 + ExtraTimeFirstHalf;
            for (int m = 1; m <= lastH1; m++)
            {
                CurrentMinute = Mathf.Min(m, 45);
                MinuteLabel = (m > 45) ? $"45+{m - 45}'" : $"{m}'";
                OnMinutePassed();
            }

            State = MatchState.SecondHalf;
            int lastH2 = 90 + ExtraTimeSecondHalf;
            for (int m = 46; m <= lastH2; m++)
            {
                CurrentMinute = Mathf.Min(m, 90);
                MinuteLabel = (m > 90) ? $"90+{m - 90}'" : $"{m}'";
                OnMinutePassed();
            }

            State = MatchState.FullTime;
            return (HomeScore, AwayScore);
        }

        private IEnumerator MatchLoop()
        {
            State = MatchState.FirstHalf;
            yield return PlayHalf(1, 45, ExtraTimeFirstHalf);

            State = MatchState.HalfTime;
            Log($"--- HẾT HIỆP 1 (Tỷ số: {HomeScore} - {AwayScore}) ---");
            yield return new WaitForSeconds(HalfTimeSeconds);

            State = MatchState.SecondHalf;
            Log("--- HIỆP 2 BẮT ĐẦU ---");
            yield return PlayHalf(46, 90, ExtraTimeSecondHalf);

            State = MatchState.FullTime;
            Log($"--- HẾT GIỜ! TỶ SỐ CHUNG CUỘC: {HomeTeam.TeamName} {HomeScore} - {AwayScore} {AwayTeam.TeamName} ---");
        }

        private IEnumerator PlayHalf(int firstMinute, int lastRegularMinute, int extraTime)
        {
            int lastMinute = lastRegularMinute + extraTime;
            for (int m = firstMinute; m <= lastMinute; m++)
            {
                yield return new WaitForSeconds(SecondsPerMinute);

                CurrentMinute = Mathf.Min(m, lastRegularMinute);
                MinuteLabel = (m > lastRegularMinute) ? $"{lastRegularMinute}+{m - lastRegularMinute}'" : $"{m}'";

                if (m == lastRegularMinute)
                    Log($"Hiệp này có {extraTime} phút bù giờ.");

                OnMinutePassed();
            }
        }

        private void OnMinutePassed()
        {
            // 1. Trừ thể lực (theo vị trí và theo quân số) cho tất cả cầu thủ
            HomeTeam.UpdateTeamStamina(1f);
            AwayTeam.UpdateTeamStamina(1f);

            // 2. Mỗi phút có đúng 1 diễn biến (giống bản JS)
            SimulateEvent();
        }

        private void SimulateEvent()
        {
            // Lệch quân số và chênh lệch sức mạnh (OVR): đội mạnh hơn giành quyền kiểm soát bóng nhiều hơn
            int homeCount = HomeTeam.PlayersOnPitch;
            int awayCount = AwayTeam.PlayersOnPitch;
            float ovrHome = HomeTeam.CalculateAverageOvr();
            float ovrAway = AwayTeam.CalculateAverageOvr();
            float ovrDiff = ovrHome - ovrAway;
            float pHomeAttack = Mathf.Clamp(0.5f + ManAdvantage * (homeCount - awayCount) + ovrDiff * 0.015f, 0.15f, 0.85f);

            bool homeAttacking = UnityEngine.Random.value < pHomeAttack;
            var ctx = new Attack
            {
                HomeAttacking = homeAttacking,
                Atk = homeAttacking ? HomeTeam : AwayTeam,
                Def = homeAttacking ? AwayTeam : HomeTeam
            };
            ctx.AtkSlots = ctx.Atk.GetActiveSlots();
            ctx.DefSlots = ctx.Def.GetActiveSlots();
            ctx.Gk = ctx.Def.Goalkeeper;

            if (ctx.AtkSlots.Count == 0 || ctx.DefSlots.Count == 0 || ctx.Gk == null) return;

            float r = UnityEngine.Random.value;
            if (r < ChanceBuildup) EventBuildup(ctx);
            else if (r < ChanceDribble) EventDribble(ctx);
            else if (r < ChanceThroughPass) EventThroughPass(ctx);
            else if (r < ChanceLongShot) EventLongShot(ctx);
            else if (r < ChanceCross) EventCross(ctx);
            else EventDefensivePressure(ctx);
        }

        // ================= CÁC LOẠI SỰ KIỆN =================

        // A. Triển khai bóng ở tuyến giữa (không có cú sút)
        private void EventBuildup(Attack ctx)
        {
            var involved = PickN(ctx.AtkSlots, 3);
            var defender = Pick(ctx.DefSlots);
            int success = 0;

            foreach (var s in involved)
            {
                if (MatchEngine.Situation10_PossessionPass(s.Player, defender.Player))
                {
                    s.Player.Passes++;
                    success++;
                }
            }
            ctx.Atk.Passes += success;

            if (success >= 2)
                Log($"{ctx.Atk.TeamName} triển khai bóng chắc chắn: {JoinNames(involved)}.");
            else
            {
                defender.Player.Tackles++;
                Log($"{defender.Player.Name} ({ctx.Def.TeamName}) thu hồi bóng thành công.");
            }
        }

        // B. Rê bóng đột phá -> đua tốc độ -> đối mặt thủ môn
        private void EventDribble(Attack ctx)
        {
            var creator = Pick(ctx.AtkSlots);
            var d1 = Pick(ctx.DefSlots);
            var d2 = Pick(ctx.DefSlots);

            // Tình huống 2: qua người
            if (!MatchEngine.Situation2_DribbleVsDefend(creator.Player, d1.Player))
            {
                d1.Player.Tackles++;
                Log($"{d1.Player.Name} cướp bóng ngay trước mũi giày {creator.Player.Name}.");
                return;
            }

            creator.Player.Dribbles++;
            Log($"Đột phá! {creator.Player.Name} vượt qua {d1.Player.Name}!");

            // Tình huống 1: đua tốc độ với hậu vệ thứ hai
            if (MatchEngine.Situation1_PaceDuel(creator.Player, d2.Player))
            {
                RegisterShot(ctx, creator.Player);

                if (UnityEngine.Random.value < OneVOneOffTarget)
                {
                    Log($"{creator.Player.Name} đối mặt thủ môn nhưng dứt điểm chệch cột dọc!");
                    return;
                }

                ctx.Atk.ShotsOnTarget++;
                // Tình huống 6: 1v1
                if (MatchEngine.Situation6_1v1Breakaway(creator.Player, ctx.Gk))
                    RegisterGoal(ctx, creator.Player, null, "bấm bóng qua người thủ môn");
                else
                {
                    ctx.Gk.Saves++;
                    Log($"{ctx.Gk.Name} khép góc cản phá cú sút của {creator.Player.Name}!");
                }
            }
            else
            {
                // Tình huống 9: hậu vệ thua đua tốc độ có thể phạm lỗi
                if (MatchEngine.Situation9_FoulCheck(d2.Player))
                {
                    var fouler = d2.Player;   // giữ lại tham chiếu: sau SendOff thì d2.Player = null
                    fouler.Fouls++;

                    bool wantsRed = UnityEngine.Random.value < RedCardChance;
                    // Thẻ đỏ: đội phòng ngự mất người, ô bị bỏ trống. Nếu đội chỉ còn 7 người thì chỉ nhận thẻ vàng.
                    if (wantsRed && ctx.Def.SendOff(d2, CurrentMinute))
                    {
                        Log($"THẺ ĐỎ cho {fouler.Name} sau pha kéo người với {creator.Player.Name}! {ctx.Def.TeamName} còn {ctx.Def.PlayersOnPitch} người.");
                    }
                    else
                    {
                        Log($"THẺ VÀNG cho {fouler.Name} sau pha kéo người với {creator.Player.Name}!");
                    }
                }
                else
                {
                    d2.Player.Tackles++;
                    Log($"{d2.Player.Name} bọc lót chuẩn xác, giải nguy.");
                }
            }
        }

        // C. Chọc khe thông minh
        private void EventThroughPass(Attack ctx)
        {
            var passer = Pick(ctx.AtkSlots);
            var finisher = PickOther(ctx.AtkSlots, passer);
            var d1 = Pick(ctx.DefSlots);

            passer.Player.Passes++;
            ctx.Atk.Passes++;

            // Tình huống 3: chọc khe qua hậu vệ
            if (!MatchEngine.Situation3_PassVsIntercept(passer.Player, d1.Player))
            {
                d1.Player.Tackles++;
                Log($"{d1.Player.Name} cắt được đường chuyền của {passer.Player.Name}.");
                return;
            }

            Log($"Chọc khe! Đường chuyền của {passer.Player.Name} đưa {finisher.Player.Name} vào thế trống trải!");
            RegisterShot(ctx, finisher.Player);

            if (UnityEngine.Random.value < ThroughBallOffTarget)
            {
                Log($"Cú dứt điểm của {finisher.Player.Name} đi vọt xà ngang!");
                return;
            }

            ctx.Atk.ShotsOnTarget++;
            if (MatchEngine.ThroughBallFinish(finisher.Player, ctx.Gk))
                RegisterGoal(ctx, finisher.Player, passer.Player, "đệm lòng chuẩn xác vào góc xa");
            else
            {
                ctx.Gk.Saves++;
                Log($"{ctx.Gk.Name} bay người cản phá cú sút của {finisher.Player.Name}!");
            }
        }

        // D. Sút xa (tỷ lệ chuyển hoá thấp như đời thực)
        private void EventLongShot(Attack ctx)
        {
            var shooter = Pick(ctx.AtkSlots);
            var blocker = Pick(ctx.DefSlots);
            RegisterShot(ctx, shooter.Player);

            if (UnityEngine.Random.value < LongShotBlocked)
            {
                blocker.Player.Tackles++;
                Log($"{shooter.Player.Name} nã đại bác từ xa nhưng {blocker.Player.Name} lăn xả chặn bóng!");
                return;
            }
            if (UnityEngine.Random.value < LongShotOffTarget)
            {
                Log($"Cú sút xa của {shooter.Player.Name} đi sạt mép lưới ra ngoài.");
                return;
            }

            ctx.Atk.ShotsOnTarget++;
            // Tình huống 4: sút xa
            if (MatchEngine.Situation4_LongShot(shooter.Player, ctx.Gk))
                RegisterGoal(ctx, shooter.Player, null, "SIÊU PHẨM sút xa găm thẳng góc chữ A");
            else
            {
                ctx.Gk.Saves++;
                Log($"{ctx.Gk.Name} ôm gọn cú sút xa của {shooter.Player.Name}.");
            }
        }

        // E. Tạt bóng - đánh đầu
        private void EventCross(Attack ctx)
        {
            var crosser = Pick(ctx.AtkSlots);
            var header = PickOther(ctx.AtkSlots, crosser);
            var d1 = Pick(ctx.DefSlots);

            crosser.Player.Passes++;
            ctx.Atk.Passes++;

            if (!MatchEngine.CrossDelivery(crosser.Player, d1.Player))
            {
                d1.Player.Tackles++;
                Log($"{d1.Player.Name} đánh đầu phá bóng giải nguy.");
                return;
            }

            RegisterShot(ctx, header.Player);
            if (UnityEngine.Random.value < HeaderOffTarget)
            {
                Log($"{crosser.Player.Name} tạt bóng thuận lợi nhưng đầu của {header.Player.Name} đi vọt xà ngang.");
                return;
            }

            ctx.Atk.ShotsOnTarget++;
            if (MatchEngine.HeaderFinish(header.Player, ctx.Gk))
                RegisterGoal(ctx, header.Player, crosser.Player, "ĐÁNH ĐẦU bật cao đưa bóng vào lưới");
            else
            {
                ctx.Gk.Saves++;
                Log($"{ctx.Gk.Name} bắt dính cú đánh đầu của {header.Player.Name}.");
            }
        }

        // F. Phòng ngự ép sân, bẻ gãy đợt tấn công
        private void EventDefensivePressure(Attack ctx)
        {
            var defenders = PickN(ctx.DefSlots, 3);
            var attacker = Pick(ctx.AtkSlots);

            var mvp = defenders[0];
            foreach (var d in defenders)
                if (d.Player.GetActualDefending() > mvp.Player.GetActualDefending()) mvp = d;

            mvp.Player.Tackles++;
            foreach (var d in defenders)
                if (d != mvp) d.Player.Passes++;

            Log($"{JoinNames(defenders)} giăng bẫy phòng ngự, bẻ gãy đợt tấn công của {attacker.Player.Name}.");
        }

        /// <summary>Thay người trong lúc trận đang diễn ra (UI gọi hàm này). Trả về false nếu bị từ chối.</summary>
        public bool MakeSubstitution(Team team, FormationSlot slot, OutfieldPlayer incoming)
        {
            if (State == MatchState.PreMatch || State == MatchState.FullTime)
            {
                Debug.LogWarning("Chỉ được thay người khi trận đang diễn ra.");
                return false;
            }

            var outgoing = slot != null ? slot.Player : null;
            string error;
            if (!team.Substitute(slot, incoming, out error))
            {
                Debug.LogWarning(error);
                return false;
            }

            Log($"[Thay người {team.TeamName}] {outgoing.Name} (STA {Mathf.RoundToInt(outgoing.Stamina)}%) rời sân nhường chỗ cho {incoming.Name}! Còn {team.SubsRemaining} lượt.");
            return true;
        }

        // ================= TIỆN ÍCH =================

        private void RegisterShot(Attack ctx, OutfieldPlayer shooter)
        {
            shooter.Shots++;
            ctx.Atk.Shots++;
        }

        private void RegisterGoal(Attack ctx, OutfieldPlayer scorer, OutfieldPlayer assister, string description)
        {
            scorer.Goals++;
            if (assister != null) assister.Assists++;
            if (ctx.HomeAttacking) HomeScore++; else AwayScore++;

            string tag = scorer.Goals == 3 ? " HATTRICK!!!" : (scorer.Goals >= 4 ? " POKER!!!" : "");
            string assistText = assister != null ? $" (kiến tạo: {assister.Name})" : "";
            Log($"!!! VÀOOOOO !!! {scorer.Name} {description}{assistText}!{tag} [{HomeScore} - {AwayScore}]");
        }

        private void Log(string message)
        {
            string line = $"[{MinuteLabel}] {message}";
            Debug.Log(line);
            OnMatchLog?.Invoke(line);
        }

        private static T Pick<T>(List<T> list)
        {
            return list[UnityEngine.Random.Range(0, list.Count)];
        }

        // Chọn 1 phần tử khác `exclude` (nếu danh sách chỉ có 1 phần tử thì đành lấy chính nó)
        private static FormationSlot PickOther(List<FormationSlot> list, FormationSlot exclude)
        {
            if (list.Count <= 1) return list[0];
            FormationSlot chosen;
            do { chosen = Pick(list); } while (chosen == exclude);
            return chosen;
        }

        private static List<FormationSlot> PickN(List<FormationSlot> list, int n)
        {
            var copy = new List<FormationSlot>(list);
            var result = new List<FormationSlot>();
            int count = Mathf.Min(n, copy.Count);
            for (int i = 0; i < count; i++)
            {
                int idx = UnityEngine.Random.Range(0, copy.Count);
                result.Add(copy[idx]);
                copy.RemoveAt(idx);
            }
            return result;
        }

        private static string JoinNames(List<FormationSlot> slots)
        {
            var names = new List<string>();
            foreach (var s in slots) names.Add(s.Player.Name);
            return string.Join(", ", names);
        }
    }
}
