using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// Đếm số lần bấm vào ảnh và hiển thị lên txtCount.
/// Chỉ lo phần đếm. Việc gửi MQTT để script khác (Checkpoint 4) đọc giá trị Count.
public class FaceCounter : MonoBehaviour
{
    [Header("UI")]
    public RawImage imgFace;     // ảnh để bấm
    public TMP_Text txtCount;    // chỗ hiển thị số

    [Header("Cấu hình")]
    [SerializeField] private int startValue = 0;  // giá trị ban đầu khi Play
    [SerializeField] private int step = 1;        // mỗi lần bấm cộng thêm bao nhiêu

    public int Count { get; private set; }

    void Start()
    {
        Count = startValue;
        UpdateUI();
    }

    // Gán vào On Click() của Button nằm trên imgFace
    public void OnImageClick()
    {
        Count += step;
        UpdateUI();
    }

    void UpdateUI()
    {
        if (txtCount != null) txtCount.text = Count.ToString();
    }

    // Chuột phải vào tên component trong Inspector để thử, không cần Play Mode
    [ContextMenu("Test: +1")]
    void TestIncrease() { OnImageClick(); }

    [ContextMenu("Test: Reset")]
    void TestReset() { Count = startValue; UpdateUI(); }
}