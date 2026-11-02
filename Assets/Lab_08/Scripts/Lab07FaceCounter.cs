using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Lab 07 - FaceCounter & UI Controller
/// Đảm nhiệm:
/// 1. Quản lý biến đếm số lần click.
/// 2. Hiển thị số lần click lên txtCount.
/// 3. Khi click vào ảnh: tăng biến đếm, cập nhật text, gửi time + clicks lên server (Yêu cầu 3).
/// 4. Khi bấm nút Submit: cập nhật text, gửi số clicks lên cloud (Yêu cầu 3).
/// </summary>
public class Lab07FaceCounter : MonoBehaviour
{
    [Header("Tham chiếu UI")]
    [Tooltip("Ảnh khuôn mặt để người dùng click")]
    public RawImage imgFace;

    [Tooltip("Text TMP hiển thị số lần click")]
    public TMP_Text txtCount;

    [Tooltip("Nút Submit")]
    public Button btnSubmit;

    [Tooltip("Tham chiếu tới Lab07MqttManager (nếu để trống sẽ tự lấy từ cùng GameObject hoặc Instance)")]
    public Lab07MqttManager mqttManager;

    [Header("Cấu hình bộ đếm")]
    [SerializeField] private int startValue = 0;
    [SerializeField] private int step = 1;

    public int Count { get; private set; }

    void Awake()
    {
        if (mqttManager == null) mqttManager = GetComponent<Lab07MqttManager>();
    }

    void Start()
    {
        if (mqttManager == null) mqttManager = Lab07MqttManager.Instance;
        Count = startValue;
        UpdateUI();
    }

    /// <summary>
    /// Gán vào OnClick() của Button nằm trên imgFace.
    /// Yêu cầu 3: "If user click on picture then application also send time and clicks to server too."
    /// </summary>
    public void OnImageClick()
    {
        Count += step;
        UpdateUI();

        var mgr = mqttManager != null ? mqttManager : Lab07MqttManager.Instance;
        if (mgr != null)
        {
            mgr.SendImageClick(Count, DateTime.Now);
        }
        else
        {
            Debug.LogWarning("[Lab07FaceCounter] Chưa tìm thấy Lab07MqttManager instance!");
        }
    }

    /// <summary>
    /// Gán vào OnClick() của nút btnSubmit.
    /// Yêu cầu 3: "User presses submit button, text will show number of clicks and at the same time send it to cloud (server)."
    /// </summary>
    public void OnSubmitClick()
    {
        UpdateUI();

        var mgr = mqttManager != null ? mqttManager : Lab07MqttManager.Instance;
        if (mgr != null)
        {
            mgr.SendSubmitClick(Count);
        }
        else
        {
            Debug.LogWarning("[Lab07FaceCounter] Chưa tìm thấy Lab07MqttManager instance!");
        }
    }

    /// <summary>
    /// Cập nhật hiển thị số lần click lên màn hình
    /// </summary>
    public void UpdateUI()
    {
        if (txtCount != null)
        {
            txtCount.text = Count.ToString();
        }
    }

    public void SetCount(int newCount)
    {
        Count = newCount;
        UpdateUI();
    }

    // ==========================================
    // Context Menu hỗ trợ test trong Editor
    // ==========================================

    [ContextMenu("Test: Click Image (+1)")]
    void TestImageClick()
    {
        OnImageClick();
    }

    [ContextMenu("Test: Click Submit")]
    void TestSubmitClick()
    {
        OnSubmitClick();
    }

    [ContextMenu("Test: Reset Count")]
    void TestReset()
    {
        Count = startValue;
        UpdateUI();
    }
}
