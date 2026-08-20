using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerControllerAuto : MonoBehaviour
{
    [Header("Di chuyen")]
    public float moveSpeed = 5f;

    [Header("Nhay")]
    public float jumpHeight = 1.5f;
    public float gravity = -20f;

    [Header("Camera - chinh bang so, khong can keo tay")]
    [Tooltip("Khoang cach tu camera den nhan vat")]
    public float cameraDistance = 7f;
    [Tooltip("Camera ngam vao diem cao bao nhieu tren nhan vat")]
    public float cameraAimHeight = 1.2f;
    [Tooltip("Goc nghieng ban dau: 0 = ngang tam mat, 90 = nhin thang tu tren xuong")]
    public float startPitch = 40f;
    public float minPitch = 10f;
    public float maxPitch = 75f;

    [Header("Chuot")]
    public float mouseSensitivity = 3f;

    private CharacterController controller;
    private Transform cameraPivot;   // truc xoay, tu tao khi chay
    private Transform cam;
    private Vector3 velocity;
    private float pitch;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        pitch = startPitch;

        SetupCamera();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // ---------- Tu dung camera rig ----------
    void SetupCamera()
    {
        // Tim camera chinh trong scene
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogError("Khong tim thay Main Camera trong scene. " +
                           "Kiem tra camera da co tag 'MainCamera' chua.");
            return;
        }
        cam = mainCam.transform;

        // Tao mot object rong lam truc xoay, gan vao nhan vat
        GameObject pivot = new GameObject("CameraPivot");
        cameraPivot = pivot.transform;
        cameraPivot.SetParent(transform);
        cameraPivot.localPosition = new Vector3(0f, cameraAimHeight, 0f);
        cameraPivot.localRotation = Quaternion.identity;

        // Gan camera vao truc xoay, day lui ra sau dung khoang cach
        cam.SetParent(cameraPivot);
        cam.localPosition = new Vector3(0f, 0f, -cameraDistance);
        cam.localRotation = Quaternion.identity;

        ApplyPitch();
    }

    void ApplyPitch()
    {
        if (cameraPivot == null) return;

        // Xoay truc, camera tu dong di theo va luon huong vao nhan vat
        cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        cam.localPosition = new Vector3(0f, 0f, -cameraDistance);
    }

    void Update()
    {
        HandleMouseLook();
        HandleMovement();
    }

    // ---------- Goc nhin ----------
    void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Chuot ngang: xoay ca nhan vat
        transform.Rotate(Vector3.up, mouseX);

        // Chuot doc: chi xoay truc camera len xuong
        pitch = Mathf.Clamp(pitch - mouseY, minPitch, maxPitch);
        ApplyPitch();

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    // ---------- Di chuyen + nhay ----------
    void HandleMovement()
    {
        if (controller.isGrounded && velocity.y < 0f)
            velocity.y = -2f;

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 move = (transform.right * h + transform.forward * v).normalized;
        controller.Move(move * moveSpeed * Time.deltaTime);

        if (Input.GetKeyDown(KeyCode.Space) && controller.isGrounded)
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}