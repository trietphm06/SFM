using UnityEngine;
using System.Collections.Generic;

namespace SFM.Core
{
    public enum PositionRole 
    { 
        GK, 
        CB, LB, RB,    // Hậu vệ
        CDM, CM, CAM,  // Tiền vệ trung tâm
        LM, RM,        // Tiền vệ cánh
        LW, RW,        // Tiền đạo cánh
        ST,            // Tiền đạo cắm

        // ---- Thêm mới: để ở CUỐI enum để không làm lệch giá trị đã serialize trong các asset sơ đồ cũ ----
        LCB, RCB,      // Trung vệ trái/phải (sơ đồ 4 hậu vệ)
        LWB, RWB,      // Hậu vệ cánh (3-5-2)
        LDM, RDM,      // Tiền vệ phòng ngự đôi (4-2-3-1)
        LCM, RCM,      // Tiền vệ trung tâm trái/phải
        LAM, RAM,      // Tiền vệ tấn công cánh (4-2-3-1)
        LST, RST,      // Cặp tiền đạo (4-4-2, 3-5-2)
        CF             // Tiền đạo lùi
    }

    /// <summary>
    /// Lưu trữ vị trí trên sân của một cầu thủ trong sơ đồ chiến thuật.
    /// Giúp game top-down 2D biết vẽ cầu thủ ở đâu và tính khoảng cách.
    /// </summary>
    [System.Serializable]
    public class FormationSlot
    {
        public PositionRole Role;
        
        // Tọa độ tương đối trên sân (ví dụ: X từ -1 biên trái đến 1 biên phải, Y từ -1 sân nhà đến 1 sân khách)
        public Vector2 BasePosition; 
        
        public OutfieldPlayer Player;
    }

    /// <summary>
    /// Dữ liệu sơ đồ chiến thuật (ví dụ: 4-4-2, 4-3-3).
    /// Có thể dùng ScriptableObject để lưu thành các file asset sẵn trong Unity.
    /// </summary>
    [CreateAssetMenu(fileName = "New Formation", menuName = "SFM/Formation")]
    public class FormationData : ScriptableObject
    {
        public string FormationName = "4-4-2";
        public List<FormationSlot> Slots = new List<FormationSlot>();
    }
}
