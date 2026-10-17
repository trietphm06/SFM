using UnityEngine;

namespace SFM.Core
{
    public class Goalkeeper : Player 
    {
        [Header("Bộ 6 chỉ số gốc thủ môn")]
        public float BaseDiving;
        public float BaseHandling;
        public float BaseKicking;
        public float BaseReflexing;
        public float BaseSpeed;
        public float BasePositioning;

        public float BasePhysical = 50f; 

        public override void UpdateStamina(float minutesPassed)
        {
            float divisor = Mathf.Max(1f, MaxStamina > 0 ? MaxStamina : BasePhysical);
            float drop = minutesPassed * (BaseStaminaDropRate * 0.35f) * (100f / divisor); 
            Stamina = Mathf.Clamp(Stamina - drop, 0f, 100f);
        }

        public override float CalculateOVR()
        {
            float baseStats = (BaseDiving + BaseHandling + BaseKicking + BaseReflexing + BaseSpeed + BasePositioning) / 6f;
            return Mathf.Round(baseStats * 0.85f + IQ * 0.08f + MaxStamina * 0.07f);
        }

        private float Common => GetIQMultiplier() * PositionMultiplier;

        public float GetActualDiving() => BaseDiving * Common;
        public float GetActualHandling() => BaseHandling * Common;
        public float GetActualKicking() => BaseKicking * Common;
        public float GetActualReflexing() => BaseReflexing * Common;
        public float GetActualPositioning() => BasePositioning * Common;
        
        public float GetActualSpeed() => BaseSpeed * GetFatigueMultiplier() * Common;
    }
}
