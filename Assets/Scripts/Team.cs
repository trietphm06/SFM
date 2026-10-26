using UnityEngine;
using System.Collections.Generic;

namespace SFM.Core
{
    /// <summary>Bản ghi một cầu thủ bị thẻ đỏ (để hiện bảng điểm và trả về ô cũ khi bắt đầu trận mới).</summary>
    public class SentOffRecord
    {
        public FormationSlot Slot;
        public OutfieldPlayer Player;
        public PositionRole Role;
        public int Minute;
    }

    [System.Serializable]
    public class Team 
    {
        public string TeamName;
        public Goalkeeper Goalkeeper;
        
        // Cấu trúc đội hình tuyến trên đang thi đấu (10 người)
        public List<FormationSlot> OutfieldSlots = new List<FormationSlot>();

        public bool HasPossession = false;

        // ===== Thay người =====
        public const int MaxSubs = 5;
        public List<OutfieldPlayer> Bench = new List<OutfieldPlayer>();      // dự bị chưa vào sân
        public List<OutfieldPlayer> SubbedOff = new List<OutfieldPlayer>();  // đã bị thay ra: KHÔNG được vào lại
        public int SubsRemaining = MaxSubs;

        // ===== Thẻ đỏ =====
        // Đội còn 7 người (tính cả thủ môn) thì không đuổi thêm (khớp MIN_PLAYERS_ON_PITCH bên JS)
        public const int MinPlayersOnPitch = 7;
        [System.NonSerialized] public List<SentOffRecord> SentOffPlayers = new List<SentOffRecord>();

        // Thống kê trận của cả đội
        public int Shots, ShotsOnTarget, Passes;

        /// <summary>Số cầu thủ đang trên sân, tính cả thủ môn.</summary>
        public int PlayersOnPitch
        {
            get
            {
                int count = Goalkeeper != null ? 1 : 0;
                foreach (var slot in OutfieldSlots)
                    if (slot.Player != null) count++;
                return count;
            }
        }

        /// <summary>Điểm OVR trung bình thực tế của 11 người trên sân (có tính hệ số phạt vị trí).</summary>
        public float CalculateAverageOvr()
        {
            float total = 0f;
            int count = 0;
            if (Goalkeeper != null)
            {
                total += Goalkeeper.CalculateOVR();
                count++;
            }
            foreach (var slot in OutfieldSlots)
            {
                if (slot.Player != null)
                {
                    total += slot.Player.CalculateOVR() * slot.Player.PositionMultiplier;
                    count++;
                }
            }
            return count > 0 ? total / count : 0f;
        }

        public void SetupFormation(FormationData formationData, List<OutfieldPlayer> selectedPlayers)
        {
            OutfieldSlots.Clear();
            
            // Clone cấu trúc sơ đồ chiến thuật để không làm thay đổi bản gốc
            for (int i = 0; i < formationData.Slots.Count; i++)
            {
                var slot = new FormationSlot 
                {
                    Role = formationData.Slots[i].Role,
                    BasePosition = formationData.Slots[i].BasePosition,
                    Player = (i < selectedPlayers.Count) ? selectedPlayers[i] : null
                };

                OutfieldSlots.Add(slot);
            }

            RefreshPositionMultipliers();
        }

        /// <summary>Đá lệch vị trí -> bị giảm chỉ số (xem PositionFit). Gọi lại mỗi khi đổi người/sơ đồ.</summary>
        public void RefreshPositionMultipliers()
        {
            foreach (var slot in OutfieldSlots)
            {
                if (slot.Player != null)
                    slot.Player.PositionMultiplier = PositionFit.Evaluate(slot.Player, slot.Role).Multiplier;
            }
        }

        /// <summary>
        /// Xếp lại các cầu thủ đang có vào các slot sao cho tổng điểm phạt lệch vị trí nhỏ nhất
        /// (gọi sau khi đổi sơ đồ). Ưu tiên vị trí chính hơn vị trí phụ khi điểm phạt bằng nhau.
        /// Không xếp lại khi đang có người bị đuổi (sẽ làm ô của người bị đuổi có người đứng vào).
        /// </summary>
        public void AutoArrange()
        {
            if (SentOffPlayers.Count > 0) return;

            var players = new List<OutfieldPlayer>();
            foreach (var slot in OutfieldSlots)
                if (slot.Player != null) players.Add(slot.Player);

            int n = OutfieldSlots.Count;
            if (n == 0 || players.Count == 0) return;

            // Ma trận vuông n x n: hàng "giả" (chưa có người) có chi phí 0
            var cost = new float[n, n];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    if (i < players.Count)
                    {
                        var fit = PositionFit.Evaluate(players[i], OutfieldSlots[j].Role);
                        cost[i, j] = fit.Penalty + (fit.Level == FitLevel.Alt ? 0.1f : 0f);
                    }
                    else cost[i, j] = 0f;
                }
            }

            int[] assign = Hungarian.Solve(cost);

            foreach (var slot in OutfieldSlots) slot.Player = null;
            for (int i = 0; i < players.Count; i++)
                OutfieldSlots[assign[i]].Player = players[i];

            RefreshPositionMultipliers();
        }

        public void UpdateTeamStamina(float minutesPassed)
        {
            // Ít người hơn thì mỗi người chạy nhiều hơn: nhân 11 / số người trên sân (khớp bản JS)
            float manFactor = 11f / Mathf.Max(1, PlayersOnPitch);

            if (Goalkeeper != null)
            {
                Goalkeeper.UpdateStamina(minutesPassed * manFactor);
            }

            foreach (var slot in OutfieldSlots)
            {
                if (slot.Player != null)
                {
                    // Hao thể lực theo vị trí: CB ít, chạy cánh/bóng biên nhiều
                    slot.Player.UpdateStamina(minutesPassed * PositionFit.StaminaMultiplier(slot.Role) * manFactor);
                }
            }
        }

        // Các slot đang có cầu thủ (không tính thủ môn)
        public List<FormationSlot> GetActiveSlots()
        {
            return OutfieldSlots.FindAll(s => s.Player != null);
        }

        public List<Player> GetAllPlayers()
        {
            var all = new List<Player>();
            if (Goalkeeper != null) all.Add(Goalkeeper);
            foreach (var slot in OutfieldSlots)
                if (slot.Player != null) all.Add(slot.Player);
            return all;
        }

        public void ResetMatchStats()
        {
            Shots = ShotsOnTarget = Passes = 0;

            // Trận mới: người bị thẻ đỏ ở trận trước quay lại đúng ô cũ
            foreach (var rec in SentOffPlayers)
            {
                if (rec.Slot != null && rec.Slot.Player == null)
                    rec.Slot.Player = rec.Player;
            }
            SentOffPlayers.Clear();

            // Trận mới: người đã bị thay ra ở trận trước quay lại danh sách dự bị
            Bench.AddRange(SubbedOff);
            SubbedOff.Clear();
            SubsRemaining = MaxSubs;

            // Ô vừa được trả người -> tính lại hệ số vị trí
            RefreshPositionMultipliers();

            foreach (var p in GetAllPlayers()) p.ResetForMatch();
            foreach (var p in Bench) p.ResetForMatch();
        }

        /// <summary>
        /// Thay người: dự bị `incoming` vào ô `slot`. Người bị thay ra không được vào lại.
        /// Thủ môn không nằm trong OutfieldSlots nên chưa hỗ trợ thay thủ môn.
        /// Ô của người bị thẻ đỏ (Player == null) không được thay người bù.
        /// </summary>
        public bool Substitute(FormationSlot slot, OutfieldPlayer incoming, out string error)
        {
            error = null;
            if (SubsRemaining <= 0) { error = "Đã hết lượt thay người."; return false; }
            if (slot == null || !OutfieldSlots.Contains(slot) || slot.Player == null) { error = "Ô vị trí không hợp lệ (hoặc cầu thủ đã bị thẻ đỏ, không được thay người bù)."; return false; }
            if (incoming == null || !Bench.Contains(incoming)) { error = "Cầu thủ này không nằm trong danh sách dự bị (người đã bị thay ra không được vào lại)."; return false; }

            var outgoing = slot.Player;
            slot.Player = incoming;
            Bench.Remove(incoming);
            SubbedOff.Add(outgoing);
            SubsRemaining--;

            // Hệ số vị trí được cache trong Player -> phải tính lại cho người mới vào ô này
            RefreshPositionMultipliers();
            return true;
        }

        /// <summary>
        /// Đuổi cầu thủ ở ô `slot` (thẻ đỏ): ô để trống, không được thay người bù.
        /// Trả về false (-> chỉ là thẻ vàng) nếu ô không hợp lệ hoặc đội chỉ còn MinPlayersOnPitch người.
        /// </summary>
        public bool SendOff(FormationSlot slot, int minute)
        {
            if (slot == null || !OutfieldSlots.Contains(slot) || slot.Player == null) return false;
            if (PlayersOnPitch <= MinPlayersOnPitch) return false;

            var p = slot.Player;
            p.SentOff = true;
            SentOffPlayers.Add(new SentOffRecord { Slot = slot, Player = p, Role = slot.Role, Minute = minute });
            slot.Player = null;
            return true;
        }

        // Hàm tiện ích để lấy ngẫu nhiên 1 cầu thủ (dùng cho MatchManager giả lập)
        public OutfieldPlayer GetRandomOutfieldPlayer()
        {
            var active = GetActiveSlots();
            if (active.Count == 0) return null;
            return active[Random.Range(0, active.Count)].Player;
        }
    }
}
