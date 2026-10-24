using UnityEngine;

namespace SFM.Core
{
    [System.Serializable]
    public class OutfieldPlayer : Player 
    {
        [Header("Bộ 6 chỉ số gốc")]
        public float BasePace;
        public float BaseShooting;
        public float BasePassing;
        public float BaseDribbling;
        public float BaseDefending;
        public float BasePhysical;

        public override void UpdateStamina(float minutesPassed)
        {
            // Tốc độ giảm tỉ lệ nghịch với Stamina/Physical gốc
            float physicalDivisor = Mathf.Max(1f, MaxStamina > 0 ? MaxStamina : BasePhysical);
            float drop = minutesPassed * BaseStaminaDropRate * (100f / physicalDivisor);
            Stamina = Mathf.Clamp(Stamina - drop, 0f, 100f);
        }

        public override float CalculateOVR()
        {
            float baseStats = (BasePace + BaseShooting + BasePassing + BaseDribbling + BaseDefending + BasePhysical) / 6f;
            return Mathf.Round(baseStats * 0.85f + IQ * 0.08f + MaxStamina * 0.07f);
        }

        // CHỈ SỐ THỰC TẾ TRÊN SÂN (khớp getActualStat bên JS):
        // Gốc * IQ * Hệ số vị trí; riêng Pace/Physical nhân thêm hệ số thể lực.
        private float Common => GetIQMultiplier() * PositionMultiplier;

        public float GetActualPace(bool hasBall = false) 
        {
            float val = BasePace * GetFatigueMultiplier() * Common;
            if (hasBall) val *= 0.9f; // Hình phạt khống chế bóng: giảm 10% tốc độ
            return val;
        }

        public float GetActualPhysical() => BasePhysical * GetFatigueMultiplier() * Common;

        public float GetActualShooting() => BaseShooting * Common;
        public float GetActualPassing() => BasePassing * Common;
        public float GetActualDribbling() => BaseDribbling * Common;
        public float GetActualDefending() => BaseDefending * Common;
    }
}
