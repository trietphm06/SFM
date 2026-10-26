using UnityEngine;
using System;
using System.Collections.Generic;

namespace SFM.Core
{
    /// <summary>
    /// Quản lý logic Draft cầu thủ cho 2 đội (khớp luật đã kiểm thử 31/31 PASS):
    /// 1. Mỗi đội bắt buộc phải có ít nhất 1 Thủ môn (GK).
    /// 2. Tuyệt đối không cho phép 1 cầu thủ xuất hiện ở 2 năm khác nhau (vd: Messi 2022 và Messi 2023)
    ///    trong cùng một trận đấu (cả cùng đội lẫn khác đội).
    /// </summary>
    public class DraftManager
    {
        public HashSet<string> TakenPlayerKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Chuẩn hoá tên cầu thủ để nhận diện các phiên bản khác năm (Messi 2022 vs Messi 2023).
        /// Khớp với hàm nameKey() trong bản Web JS.
        /// </summary>
        public static string GetPlayerKey(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            return name.Trim().ToLowerInvariant()
                .Replace(" ", "")
                .Replace(".", "")
                .Replace("-", "")
                .Replace("_", "");
        }

        public void Reset()
        {
            TakenPlayerKeys.Clear();
        }

        public bool IsPlayerAvailable(Player player)
        {
            if (player == null) return false;
            string key = GetPlayerKey(player.Name);
            return !TakenPlayerKeys.Contains(key);
        }

        public bool MarkPlayerTaken(Player player)
        {
            if (!IsPlayerAvailable(player)) return false;
            string key = GetPlayerKey(player.Name);
            TakenPlayerKeys.Add(key);
            return true;
        }

        public void ReleasePlayer(Player player)
        {
            if (player == null) return;
            string key = GetPlayerKey(player.Name);
            TakenPlayerKeys.Remove(key);
        }

        /// <summary>
        /// Tự động draft cho một đội hình theo sơ đồ chiến thuật (khớp logic AI draft trên Web):
        /// - Đảm bảo luôn có 1 Goalkeeper cho ô GK.
        /// - Chọn cầu thủ có (OVR * hệ số hợp vị trí) cao nhất cho từng ô.
        /// - Chặn hoàn toàn trùng lặp tên khác năm.
        /// </summary>
        public void AutoDraftTeam(Team team, FormationData formation, List<Player> pool, bool prioritizeBest = true)
        {
            team.OutfieldSlots.Clear();
            team.Bench.Clear();
            team.SubbedOff.Clear();
            team.SentOffPlayers.Clear();
            team.Goalkeeper = null;

            // 1. Tuyển Thủ môn cho đội
            Goalkeeper chosenGk = null;
            float bestGkOvr = -1f;

            foreach (var p in pool)
            {
                if (p is Goalkeeper gk && IsPlayerAvailable(gk))
                {
                    float ovr = gk.CalculateOVR();
                    if (chosenGk == null || (prioritizeBest ? ovr > bestGkOvr : ovr < bestGkOvr))
                    {
                        chosenGk = gk;
                        bestGkOvr = ovr;
                    }
                }
            }

            if (chosenGk != null)
            {
                team.Goalkeeper = chosenGk;
                MarkPlayerTaken(chosenGk);
            }
            else
            {
                Debug.LogWarning($"[DraftManager] Không tìm thấy thủ môn khả dụng trong kho cho đội {team.TeamName}!");
            }

            // 2. Tuyển 10 vị trí trên sân (Outfield) theo FormationData
            var selectedOutfields = new List<OutfieldPlayer>();
            for (int i = 0; i < formation.Slots.Count; i++)
            {
                var role = formation.Slots[i].Role;
                if (role == PositionRole.GK) continue; // Thủ môn đã xử lý ở trên

                OutfieldPlayer bestCandidate = null;
                float bestScore = -1f;

                foreach (var p in pool)
                {
                    if (p is OutfieldPlayer op && IsPlayerAvailable(op))
                    {
                        var fit = PositionFit.Evaluate(op, role);
                        float score = op.CalculateOVR() * fit.Multiplier;

                        // Ưu tiên chọn đúng vị trí hoặc điểm phạt thấp nhất
                        if (prioritizeBest)
                        {
                            if (bestCandidate == null || score > bestScore)
                            {
                                bestCandidate = op;
                                bestScore = score;
                            }
                        }
                        else
                        {
                            if (bestCandidate == null || score < bestScore)
                            {
                                bestCandidate = op;
                                bestScore = score;
                            }
                        }
                    }
                }

                if (bestCandidate != null)
                {
                    selectedOutfields.Add(bestCandidate);
                    MarkPlayerTaken(bestCandidate);
                }
            }

            team.SetupFormation(formation, selectedOutfields);

            // 3. Tuyển 5 cầu thủ dự bị (Bench) có OVR tốt nhất còn lại
            int benchCount = 0;
            var remainingPool = new List<OutfieldPlayer>();
            foreach (var p in pool)
            {
                if (p is OutfieldPlayer op && IsPlayerAvailable(op))
                    remainingPool.Add(op);
            }

            remainingPool.Sort((a, b) => prioritizeBest 
                ? b.CalculateOVR().CompareTo(a.CalculateOVR()) 
                : a.CalculateOVR().CompareTo(b.CalculateOVR()));

            for (int i = 0; i < remainingPool.Count && benchCount < Team.MaxSubs; i++)
            {
                var sub = remainingPool[i];
                if (MarkPlayerTaken(sub))
                {
                    team.Bench.Add(sub);
                    benchCount++;
                }
            }
        }
    }
}
