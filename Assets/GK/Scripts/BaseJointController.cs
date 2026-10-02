using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Câu 2b – Điều khiển khớp đế (J1) của cánh tay Kinova.
/// ← : xoay 10° theo chiều từ phải sang trái (nhìn từ camera phía sau robot) → góc Y giảm.
/// → : xoay 10° theo chiều từ trái sang phải → góc Y tăng.
/// Mỗi lần nhấn cộng ±10° vào góc đích; khớp quay mượt tới góc đích.
/// Giữ phím: sau holdDelay giây thì tự lặp thêm 10° mỗi khi khớp đã tới đích.
/// </summary>
public class BaseJointController : MonoBehaviour
{
    [Tooltip("Empty khớp đế J1, xoay quanh trục Y local")]
    public Transform baseJoint;
    [Tooltip("Mỗi lần nhấn xoay bao nhiêu độ (đề yêu cầu 10°)")]
    public float stepAngle = 10f;
    [Tooltip("Tốc độ quay (độ/giây) để chuyển động mượt")]
    public float rotateSpeed = 60f;
    [Tooltip("Giữ phím bao lâu (giây) thì bắt đầu tự lặp")]
    public float holdDelay = 0.25f;

    float targetAngle;   // góc đích (độ), có thể vượt 360
    float currentAngle;  // góc hiện tại
    float holdTimer;

    /// <summary>true khi khớp đang quay – SensorRay dùng để biết robot đang di chuyển.</summary>
    public bool IsMoving => !Mathf.Approximately(currentAngle, targetAngle);

    void Start()
    {
        if (baseJoint == null) baseJoint = transform;
        currentAngle = targetAngle = baseJoint.localEulerAngles.y;
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null)
        {
            int dir = 0;
            if (kb.leftArrowKey.isPressed) dir -= 1;   // phải → trái
            if (kb.rightArrowKey.isPressed) dir += 1;  // trái → phải

            if (kb.leftArrowKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame)
            {
                targetAngle += dir * stepAngle;
                holdTimer = 0f;
            }
            else if (dir != 0)
            {
                holdTimer += Time.deltaTime;
                // đang giữ phím: chỉ thêm bước mới khi khớp gần tới đích → quay liên tục, đều
                if (holdTimer >= holdDelay && Mathf.Abs(targetAngle - currentAngle) < 1f)
                    targetAngle += dir * stepAngle;
            }
        }

        currentAngle = Mathf.MoveTowards(currentAngle, targetAngle, rotateSpeed * Time.deltaTime);
        var e = baseJoint.localEulerAngles;
        baseJoint.localEulerAngles = new Vector3(e.x, currentAngle, e.z);
    }

    [ContextMenu("Xoay trái 10° (Editor)")]
    void StepLeft() { RotateNow(-stepAngle); }

    [ContextMenu("Xoay phải 10° (Editor)")]
    void StepRight() { RotateNow(stepAngle); }

    void RotateNow(float d)
    {
        if (baseJoint == null) baseJoint = transform;
        var e = baseJoint.localEulerAngles;
        baseJoint.localEulerAngles = new Vector3(e.x, e.y + d, e.z);
        currentAngle = targetAngle = baseJoint.localEulerAngles.y;
    }
}
