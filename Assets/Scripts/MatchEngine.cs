using UnityEngine;

namespace SFM.Core
{
    public static class MatchEngine 
    {
        // ===== Hệ số cân bằng (đã tune ở bản JS, chỉnh ở đây cho gọn) =====
        public const float BuildupDefenderFactor = 0.75f;  // Chuyền ngắn: hậu vệ ép yếu hơn
        public const float CrossDefenderFactor = 0.9f;     // Tạt bóng
        public const float OneVOneGkBuff = 2.4f;           // Thủ môn trong tình huống 1v1 (TUNING.gk1v1)
        public const float ThroughBallGkBuff = 2.5f;       // Sút sau đường chọc khe (TUNING.gkThrough)
        public const float HeaderGkBuff = 2.6f;            // Đánh đầu sau tạt (TUNING.gkHeader)
        public const float LongShotFactor = 0.75f;         // Sút xa: cả 2 vế nhân 0.75
        public const float LongShotGkBuff = 2.1f;          // Thủ môn khi bị sút xa (TUNING.gkLong)

        /// <summary>
        /// Công thức Vạn năng (The Master Formula)
        /// Tỷ lệ chiến thắng của A (%) = (A / (A + B)) * 100
        /// </summary>
        public static bool ResolveContest(float statA, float statB)
        {
            if (statA + statB <= 0f) 
            {
                // Fallback nếu cả 2 chỉ số bị debuff về 0
                return Random.Range(0, 2) == 0; 
            }

            float winProbabilityA = (statA / (statA + statB)) * 100f;
            float roll = Random.Range(1f, 100f);
            
            // Nếu R <= Tỷ lệ thắng -> A thắng
            return roll <= winProbabilityA;
        }

        // Tình huống 1: Tiền đạo đua tốc độ với hậu vệ (có bóng)
        public static bool Situation1_PaceDuel(OutfieldPlayer attacker, OutfieldPlayer defender)
        {
            float A = attacker.GetActualPace(hasBall: true);
            float B = defender.GetActualPace(hasBall: false);
            return ResolveContest(A, B);
        }

        // Tình huống 2: Qua người / Tranh chấp tay đôi (Dribbling vs Defending)
        public static bool Situation2_DribbleVsDefend(OutfieldPlayer attacker, OutfieldPlayer defender)
        {
            float A = attacker.GetActualDribbling();
            float B = defender.GetActualDefending();
            return ResolveContest(A, B);
        }

        // Tình huống 3: Tiền vệ chọc khe / Chuyền dài (Passing vs Defending)
        public static bool Situation3_PassVsIntercept(OutfieldPlayer passer, OutfieldPlayer defender)
        {
            float A = passer.GetActualPassing();
            float B = defender.GetActualDefending();
            return ResolveContest(A, B);
        }

        // Tình huống 4: Sút xa ngoài vòng cấm
        // Khớp bản JS: shooter * 0.75 vs (Positioning + Diving) * 0.75 * 2.1
        public static bool Situation4_LongShot(OutfieldPlayer attacker, Goalkeeper gk)
        {
            float A = attacker.GetActualShooting() * LongShotFactor;
            float B = (gk.GetActualPositioning() + gk.GetActualDiving()) * LongShotFactor * LongShotGkBuff;
            return ResolveContest(A, B);
        }

        // Tình huống 5: Sút cận thành trong vòng cấm
        public static bool Situation5_CloseShot(OutfieldPlayer attacker, Goalkeeper gk)
        {
            float A = attacker.GetActualShooting();
            float B = gk.GetActualReflexing();
            return ResolveContest(A, B);
        }

        // Tình huống 6: Đối mặt thủ môn (1v1 Breakaway)
        public static bool Situation6_1v1Breakaway(OutfieldPlayer attacker, Goalkeeper gk)
        {
            float A = attacker.GetActualShooting();
            float B = ((gk.GetActualReflexing() * 0.7f) + (gk.GetActualPositioning() * 0.3f)) * OneVOneGkBuff;
            return ResolveContest(A, B);
        }

        // Sút sau đường chọc khe (Shooting vs Reflexing * 1.2)
        public static bool ThroughBallFinish(OutfieldPlayer finisher, Goalkeeper gk)
        {
            float A = finisher.GetActualShooting();
            float B = gk.GetActualReflexing() * ThroughBallGkBuff;
            return ResolveContest(A, B);
        }

        // Tình huống 7 (tạm thời): Đánh đầu sau tạt - chờ chỉ số Heading riêng, hiện dùng 50% Physical + 50% Shooting
        public static bool HeaderFinish(OutfieldPlayer header, Goalkeeper gk)
        {
            float A = header.GetActualPhysical() * 0.5f + header.GetActualShooting() * 0.5f;
            float B = (gk.GetActualReflexing() * 0.6f + gk.GetActualHandling() * 0.4f) * HeaderGkBuff;
            return ResolveContest(A, B);
        }

        // Tạt bóng thành công (Passing vs Defending * 0.9)
        public static bool CrossDelivery(OutfieldPlayer crosser, OutfieldPlayer defender)
        {
            float A = crosser.GetActualPassing();
            float B = defender.GetActualDefending() * CrossDefenderFactor;
            return ResolveContest(A, B);
        }

        // Tình huống 8: Phát bóng của thủ môn
        public static bool Situation8_GKKicking(Goalkeeper gk, float distanceInUnityUnits)
        {
            // Số chia 10 là placeholder, tune lại theo kích thước sân
            float distancePenalty = distanceInUnityUnits / 10f; 
            float successRate = gk.GetActualKicking() - distancePenalty;
            
            float roll = Random.Range(1f, 100f);
            return roll <= successRate;
        }

        // Tình huống 9: Phạm lỗi khi tắc bóng (Kích hoạt khi Hậu vệ thua Tình huống 2)
        public static bool Situation9_FoulCheck(OutfieldPlayer defender)
        {
            float foulProbability = 100f - defender.GetActualDefending();
            float roll = Random.Range(1f, 100f);
            return roll <= foulProbability;
        }

        // Tình huống 10: Chuyền ngắn / Giữ bóng (hậu vệ ép bị giảm 25%)
        public static bool Situation10_PossessionPass(OutfieldPlayer passer, OutfieldPlayer pressingDefender)
        {
            float A = passer.GetActualPassing();
            float B = pressingDefender.GetActualDefending() * BuildupDefenderFactor;
            return ResolveContest(A, B);
        }
    }
}
