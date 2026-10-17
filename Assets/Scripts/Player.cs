using UnityEngine;
using System.Collections.Generic;

namespace SFM.Core
{
    public abstract class Player 
    {
        public string Name;
        public string Position;
        public List<string> AltPositions = new List<string>(); // Vị trí phụ (không bị phạt)
        public string Year = "2023";
        public string Season = "Base";

        public float IQ = 70f;
        public float Stamina = 100f;
        public float MaxStamina = 100f;

        // Hệ số nhân mọi chỉ số khi đá lệch vị trí (Team.RefreshPositionMultipliers tính qua PositionFit). 1 = đúng vị trí.
        public float PositionMultiplier = 1f;

        // Hệ số IQ dùng cho các chỉ số: 1 + (IQ - 50) / 500
        public float GetIQMultiplier() => 1f + (IQ - 50f) / 500f;

        // Thống kê trong trận (dùng cho bảng điểm)
        public int Goals, Assists, Shots, Saves, Tackles, Passes, Dribbles, Fouls;

        // Base rate cho việc hao hụt thể lực (GDD: 0.5 - 1.0/phút)
        public float BaseStaminaDropRate = 0.75f; 

        public void ResetForMatch()
        {
            Stamina = 100f;
            Goals = Assists = Shots = Saves = Tackles = Passes = Dribbles = Fouls = 0;
        }

        public float GetFatigueMultiplier()
        {
            if (Stamina >= 80f) return 1.0f;
            if (Stamina >= 60f) return 0.95f;
            if (Stamina >= 40f) return 0.9f;
            if (Stamina >= 20f) return 0.8f;
            return 0.65f; // Dưới 20
        }

        public abstract void UpdateStamina(float minutesPassed);
        public abstract float CalculateOVR();
    }
}
