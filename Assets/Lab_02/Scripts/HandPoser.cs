using UnityEngine;

/// <summary>
/// Chuyen tu the ban tay robot bang mot lua chon trong Inspector.
/// Gan script nay vao object RobotArm.
///
/// Cach hoat dong:
/// - Lan dau gan, script chup lai goc xoay hien tai cua 15 khop lam "rest pose"
///   (gom ca goc xoe cua cac _J1 va huong dat cua ngon cai).
/// - Moi tu the la bo goc CONG THEM vao rest pose, nen khong lam mat cac goc goc.
///
/// Quy tac goc gap cua ban tay that:
///   khop ban-ngon (J1) gap 90 do  -> dot 1 nam ngang, chia ra truoc
///   khop giua     (J2) gap 90 do  -> dot 2 chuc thang xuong
///   khop dau      (J3) gap 25 do  -> dot 3 quap nhe
/// Gap deu ba khop se ra hinh cai vuot, khong phai nam dam.
/// </summary>
[ExecuteAlways]
public class HandPoser : MonoBehaviour
{
    // Tang so nay moi khi doi du lieu tu the mac dinh.
    // Khi script nap lai va thay so cu, no tu ghi de lai 5 tu the.
    private const int POSE_DATA_VERSION = 4;

    public enum PoseId { Open = 0, Fist = 1, OK = 2, Point = 3, VSign = 4 }

    [System.Serializable]
    public class FingerAngles
    {
        public Vector3 j1;
        public Vector3 j2;
        public Vector3 j3;
    }

    [System.Serializable]
    public class HandPose
    {
        public string name = "Pose";
        public FingerAngles index = new FingerAngles();
        public FingerAngles middle = new FingerAngles();
        public FingerAngles ring = new FingerAngles();
        public FingerAngles pinky = new FingerAngles();
        public FingerAngles thumb = new FingerAngles();

        [Header("Goc TUYET DOI cho goc ngon cai (Thumb_J1)")]
        [Tooltip("Bat de nhap thang goc xoay cua Thumb_J1, khong cong them vao rest pose. " +
                 "Ngon cai co rest pose xoay nhieu nen tinh theo goc cong them rat kho doan.")]
        public bool thumbBaseAbsolute;
        public Vector3 thumbBaseEuler;
    }

    [Header("Chon tu the")]
    public PoseId pose = PoseId.Open;

    [Header("Chuyen dong muot (chi co tac dung khi Play)")]
    public bool smooth = false;
    [Range(1f, 30f)] public float speed = 8f;

    [Header("Du lieu 5 tu the (co the chinh tay)")]
    public HandPose[] poses;

    [SerializeField, HideInInspector] private Quaternion[] restRotations;
    [SerializeField, HideInInspector] private bool restCaptured;
    [SerializeField, HideInInspector] private int poseVersion = -1;

    /// <summary>
    /// Goc goc ngon cai khi gap sat vao nam tay.
    /// Lay tu thao tac xoay tay tren mo hinh that, dung cho ca ba tu the
    /// Nam tay - Chi tro - Chu V.
    /// </summary>
    private static readonly Vector3 THUMB_TUCKED = new Vector3(-35.771f, -229.513f, 19.368f);

    private static readonly string[] FingerNames = { "Index", "Middle", "Ring", "Pinky", "Thumb" };
    private readonly Transform[] joints = new Transform[15];

    // ---------------------------------------------------------------- lifecycle

    private void Reset()
    {
        BuildDefaultPoses();
        CacheJoints();
        CaptureRest();
    }

    private void OnEnable()
    {
        EnsurePoseData();
        CacheJoints();
        if (!restCaptured) CaptureRest();
    }

    private void OnValidate()
    {
        EnsurePoseData();
        CacheJoints();
        if (!restCaptured) CaptureRest();
        if (!Application.isPlaying || !smooth) ApplyImmediate();
    }

    private void Update()
    {
        if (Application.isPlaying && smooth) ApplySmooth();
    }

    private void EnsurePoseData()
    {
        if (poses == null || poses.Length != 5 || poseVersion != POSE_DATA_VERSION)
            BuildDefaultPoses();
    }

    // ---------------------------------------------------------------- nut bam

    [ContextMenu("Ap dung tu the ngay")]
    public void ApplyImmediate()
    {
        if (!restCaptured) return;
        HandPose p = poses[(int)pose];

        for (int f = 0; f < 5; f++)
        {
            FingerAngles a = GetFinger(p, f);

            bool absoluteThumbBase = (f == 4) && p.thumbBaseAbsolute;
            if (absoluteThumbBase && joints[12] != null)
                joints[12].localRotation = Quaternion.Euler(p.thumbBaseEuler);
            else
                SetJoint(f * 3 + 0, a.j1);

            SetJoint(f * 3 + 1, a.j2);
            SetJoint(f * 3 + 2, a.j3);
        }
    }

    /// <summary>Chi bam khi ban tay dang o tu the Open.</summary>
    [ContextMenu("Chup lai rest pose")]
    public void CaptureRest()
    {
        restRotations = new Quaternion[15];
        for (int i = 0; i < 15; i++)
            restRotations[i] = joints[i] != null ? joints[i].localRotation : Quaternion.identity;
        restCaptured = true;
    }

    /// <summary>
    /// Xoay Thumb_J1 bang tay trong Scene view cho vua y, roi bam nut nay
    /// de luu goc do vao tu the dang chon.
    /// </summary>
    [ContextMenu("Ghi goc ngon cai hien tai vao tu the dang chon")]
    public void CaptureThumbBase()
    {
        CacheJoints();
        if (joints[12] == null || poses == null) return;
        HandPose p = poses[(int)pose];
        p.thumbBaseEuler = joints[12].localRotation.eulerAngles;
        p.thumbBaseAbsolute = true;
    }

    /// <summary>Ghi de 5 tu the ve gia tri mac dinh, khong dung toi rest pose.</summary>
    [ContextMenu("Nap lai 5 tu the mac dinh")]
    public void ReloadDefaultPoses()
    {
        BuildDefaultPoses();
        ApplyImmediate();
    }

    // ---------------------------------------------------------------- noi bo

    private void ApplySmooth()
    {
        if (!restCaptured) return;
        HandPose p = poses[(int)pose];
        float t = 1f - Mathf.Exp(-speed * Time.deltaTime);

        for (int f = 0; f < 5; f++)
        {
            FingerAngles a = GetFinger(p, f);

            if ((f == 4) && p.thumbBaseAbsolute && joints[12] != null)
                joints[12].localRotation = Quaternion.Slerp(
                    joints[12].localRotation, Quaternion.Euler(p.thumbBaseEuler), t);
            else
                LerpJoint(f * 3 + 0, a.j1, t);

            LerpJoint(f * 3 + 1, a.j2, t);
            LerpJoint(f * 3 + 2, a.j3, t);
        }
    }

    private void SetJoint(int i, Vector3 delta)
    {
        if (joints[i] == null) return;
        joints[i].localRotation = restRotations[i] * Quaternion.Euler(delta);
    }

    private void LerpJoint(int i, Vector3 delta, float t)
    {
        if (joints[i] == null) return;
        Quaternion target = restRotations[i] * Quaternion.Euler(delta);
        joints[i].localRotation = Quaternion.Slerp(joints[i].localRotation, target, t);
    }

    private static FingerAngles GetFinger(HandPose p, int index)
    {
        switch (index)
        {
            case 0: return p.index;
            case 1: return p.middle;
            case 2: return p.ring;
            case 3: return p.pinky;
            default: return p.thumb;
        }
    }

    private void CacheJoints()
    {
        for (int f = 0; f < 5; f++)
            for (int j = 0; j < 3; j++)
                joints[f * 3 + j] = FindDeep(transform, FingerNames[f] + "_J" + (j + 1));
    }

    private static Transform FindDeep(Transform root, string targetName)
    {
        if (root.name == targetName) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeep(root.GetChild(i), targetName);
            if (found != null) return found;
        }
        return null;
    }

    // ---------------------------------------------------------------- du lieu mac dinh

    private void BuildDefaultPoses()
    {
        poses = new HandPose[5];

        // ---- 1 - Xoe phang: tat ca bang 0
        poses[0] = new HandPose { name = "1 - Xoe phang" };

        // ---- 2 - Nam tay
        poses[1] = new HandPose { name = "2 - Nam tay" };
        Curl(poses[1].index, 90f, 95f, 35f);
        Curl(poses[1].middle, 90f, 95f, 35f);
        Curl(poses[1].ring, 90f, 95f, 35f);
        Curl(poses[1].pinky, 90f, 90f, 35f);
        // goc nay chup tu thao tac xoay tay tren mo hinh that
        poses[1].thumbBaseAbsolute = true;
        poses[1].thumbBaseEuler = THUMB_TUCKED;
        poses[1].thumb.j2 = new Vector3(35f, 0f, 0f);
        poses[1].thumb.j3 = new Vector3(20f, 0f, 0f);

        // ---- 3 - OK
        poses[2] = new HandPose { name = "3 - OK" };
        Curl(poses[2].index, 65f, 75f, 30f);
        // ngon cai gan nhu duoi thang moi voi toi dau ngon tro
        poses[2].thumbBaseAbsolute = true;
        poses[2].thumbBaseEuler = new Vector3(38f, 0f, -9f);
        poses[2].thumb.j2 = new Vector3(20f, 0f, 0f);
        poses[2].thumb.j3 = new Vector3(15f, 0f, 0f);

        // ---- 4 - Chi tro
        poses[3] = new HandPose { name = "4 - Chi tro" };
        Curl(poses[3].middle, 90f, 95f, 35f);
        Curl(poses[3].ring, 90f, 95f, 35f);
        Curl(poses[3].pinky, 90f, 90f, 35f);
        poses[3].thumbBaseAbsolute = true;
        poses[3].thumbBaseEuler = THUMB_TUCKED;
        poses[3].thumb.j2 = new Vector3(30f, 0f, 0f);
        poses[3].thumb.j3 = new Vector3(20f, 0f, 0f);

        // ---- 5 - Chu V
        poses[4] = new HandPose { name = "5 - Chu V" };
        // xoe ngang vua phai: qua lon se lo qua bi o khop ban-ngon
        poses[4].index.j1 = new Vector3(0f, 0f, 6f);
        poses[4].middle.j1 = new Vector3(0f, 0f, -6f);
        Curl(poses[4].ring, 90f, 95f, 35f);
        Curl(poses[4].pinky, 90f, 90f, 35f);
        poses[4].thumbBaseAbsolute = true;
        poses[4].thumbBaseEuler = THUMB_TUCKED;
        poses[4].thumb.j2 = new Vector3(30f, 0f, 0f);
        poses[4].thumb.j3 = new Vector3(20f, 0f, 0f);

        poseVersion = POSE_DATA_VERSION;
    }

    private static void Curl(FingerAngles a, float x1, float x2, float x3)
    {
        a.j1 = new Vector3(x1, 0f, 0f);
        a.j2 = new Vector3(x2, 0f, 0f);
        a.j3 = new Vector3(x3, 0f, 0f);
    }
}