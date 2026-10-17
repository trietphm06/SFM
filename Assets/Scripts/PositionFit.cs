using UnityEngine;
using System.Collections.Generic;

namespace SFM.Core
{
    public enum FitLevel { Exact, Alt, OutOfPosition }

    public struct PositionFitResult
    {
        public FitLevel Level;
        public float Penalty;      // điểm phạt lệch vị trí (0 = đúng/phụ)
        public float Multiplier;   // hệ số nhân lên mọi chỉ số: max(0.30, 1 - Penalty/150)
    }

    /// <summary>
    /// Khớp với checkPositionFit bên bản JS. Mỗi vị trí có toạ độ (độ sâu, bề ngang).
    /// Đá lệch vị trí -> điểm phạt theo khoảng cách -> hệ số nhân chỉ số (xa càng nặng, nhưng vẫn "đá được xíu").
    /// Thủ môn đá sân (và ngược lại) bị phạt cực nặng.
    /// </summary>
    public static class PositionFit
    {
        // ===== Các hằng số để tune (khớp FIT_CFG bên JS) =====
        public const float DepthWeight = 12f;          // mỗi bậc độ sâu = 12 điểm phạt
        public const float WidthWeight = 8f;           // mỗi bậc bên cánh = 8 điểm phạt
        public const float MaxOutfieldPenalty = 60f;   // trần phạt cho cầu thủ sân
        public const float GoalkeeperMismatchPenalty = 100f;
        public const float FreeTolerance = 8f;         // lệch rất nhỏ (CF<->ST, LB<->LWB...) coi như vị trí phụ
        public const float PenaltyDivisor = 150f;      // hệ số = 1 - phạt/150 (phạt 12 -> x0.92, 60 -> x0.60)
        public const float MinMultiplier = 0.30f;      // sàn hệ số

        // x = độ sâu (0 = thủ môn ... 6 = tiền đạo cắm), y = bề ngang (-1 trái, 0 giữa, +1 phải)
        private static readonly Dictionary<string, Vector2> Coords = new Dictionary<string, Vector2>
        {
            { "GK",  new Vector2(0f, 0f) },
            { "CB",  new Vector2(1f, 0f) },
            { "LB",  new Vector2(1.5f, -1f) }, { "RB",  new Vector2(1.5f, 1f) },
            { "LWB", new Vector2(2f, -1f) },   { "RWB", new Vector2(2f, 1f) },
            { "CDM", new Vector2(2.5f, 0f) },
            { "CM",  new Vector2(3.5f, 0f) },
            { "LM",  new Vector2(4f, -1f) },   { "RM",  new Vector2(4f, 1f) },
            { "CAM", new Vector2(4.5f, 0f) },
            { "LAM", new Vector2(4.6f, -1f) }, { "RAM", new Vector2(4.6f, 1f) },
            { "LW",  new Vector2(5.2f, -1f) }, { "RW",  new Vector2(5.2f, 1f) },
            { "CF",  new Vector2(5.6f, 0f) },
            { "ST",  new Vector2(6f, 0f) },
        };

        /// <summary>Slot trái/phải của vị trí trung tâm quy về vị trí gốc (LCB -> CB, LST -> ST...).</summary>
        public static string Canonical(string role)
        {
            string r = (role ?? "").Trim().ToUpperInvariant();
            switch (r)
            {
                case "LCB": case "RCB": return "CB";
                case "LST": case "RST": return "ST";
                case "LDM": case "RDM": return "CDM";
                case "LCM": case "RCM": return "CM";
                default: return r;
            }
        }

        public static float MultiplierFromPenalty(float penalty)
        {
            return Mathf.Max(MinMultiplier, 1f - penalty / PenaltyDivisor);
        }

        private static float Penalty(string a, string b)
        {
            Vector2 ca, cb;
            if (!Coords.TryGetValue(a, out ca)) ca = Coords["CM"];
            if (!Coords.TryGetValue(b, out cb)) cb = Coords["CM"];
            float pen = Mathf.Abs(ca.x - cb.x) * DepthWeight + Mathf.Abs(ca.y - cb.y) * WidthWeight;
            // Math.round của JS (làm tròn .5 lên), không dùng Mathf.Round (làm tròn về số chẵn)
            return Mathf.Min(MaxOutfieldPenalty, Mathf.Floor(pen + 0.5f));
        }

        private static PositionFitResult Make(FitLevel level, float penalty)
        {
            return new PositionFitResult { Level = level, Penalty = penalty, Multiplier = MultiplierFromPenalty(penalty) };
        }

        public static PositionFitResult Evaluate(string mainPos, IList<string> altPos, string slotRole)
        {
            string slot = Canonical(slotRole);
            string main = Canonical(mainPos);

            if (main == slot) return Make(FitLevel.Exact, 0f);

            var alts = new List<string>();
            if (altPos != null)
                foreach (var a in altPos) alts.Add(Canonical(a));

            if (alts.Contains(slot)) return Make(FitLevel.Alt, 0f);

            if ((slot == "GK") != (main == "GK"))
                return Make(FitLevel.OutOfPosition, GoalkeeperMismatchPenalty);

            // Lấy vị trí gần slot nhất trong số vị trí chính + phụ
            float best = Penalty(main, slot);
            foreach (var a in alts)
            {
                if (a == "GK") continue;
                best = Mathf.Min(best, Penalty(a, slot));
            }

            if (best <= FreeTolerance) return Make(FitLevel.Alt, 0f);
            return Make(FitLevel.OutOfPosition, best);
        }

        public static PositionFitResult Evaluate(Player player, PositionRole slotRole)
        {
            return Evaluate(player.Position, player.AltPositions, slotRole.ToString());
        }

        /// <summary>Hệ số hao thể lực theo vị trí (CB ít, chạy cánh/bóng biên nhiều) - khớp bản JS.</summary>
        public static float StaminaMultiplier(PositionRole role)
        {
            string r = role.ToString();
            if (r == "GK") return 0.25f;
            if (r == "LAM" || r == "RAM") return 1.0f;
            switch (Canonical(r))
            {
                case "CB": return 0.70f;
                case "ST": case "CF": return 0.85f;
                case "CDM": return 1.05f;
                case "CM": case "CAM": return 1.0f;
                default: return 1.18f; // LB, RB, LWB, RWB, LM, RM, LW, RW
            }
        }
    }
}
