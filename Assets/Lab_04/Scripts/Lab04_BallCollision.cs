using UnityEngine;
using TMPro;

// Lab 04 - Bai 1: bong lan tren san, va vao tuong thi tuong doi mau + hien "Ouch!".
// Khi bong lan tiep (khong cham tuong) thi tuong tro lai mau goc + hien "Keep Rolling...".
// San va tuong la MOT mesh ProBuilder (Floor) co 2 submesh:
//   submesh 0 = mat san, submesh 1 = tuong  -> doi material o vi tri wallMaterialIndex.
[RequireComponent(typeof(Rigidbody))]
public class Lab04_BallCollision : MonoBehaviour
{
    [Header("Dieu khien bong (WASD / mui ten)")]
    public float pushForce = 6f;

    [Header("Tuong")]
    public Renderer wallRenderer;          // Renderer cua Floor
    public int wallMaterialIndex = 1;      // slot material cua tuong
    public Material wallColor;             // mau goc
    public Material afterCollision;        // mau khi va cham

    [Header("UI")]
    public TMP_Text statusText;
    public string hitMessage = "Ouch!";
    public string rollMessage = "Keep Rolling...";

    [Tooltip("Sau bao lau khong cham tuong thi coi nhu bong da lan tiep (giay)")]
    public float releaseDelay = 0.3f;

    // Phap tuyen co |y| nho hon nguong nay -> mat dung (tuong), lon hon -> mat san
    const float WallNormalMaxY = 0.5f;

    Rigidbody rb;
    float lastWallHitTime = -999f;
    bool showingHit;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        SetState(false);
    }

    void FixedUpdate()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        rb.AddForce(new Vector3(h, 0f, v) * pushForce);
    }

    void Update()
    {
        bool touchingWall = Time.time - lastWallHitTime < releaseDelay;
        if (touchingWall != showingHit) SetState(touchingWall);
    }

    void OnCollisionEnter(Collision c) { CheckWall(c); }
    void OnCollisionStay(Collision c)  { CheckWall(c); }

    void CheckWall(Collision c)
    {
        if (wallRenderer == null || c.collider.gameObject != wallRenderer.gameObject) return;
        for (int i = 0; i < c.contactCount; i++)
        {
            if (Mathf.Abs(c.GetContact(i).normal.y) < WallNormalMaxY)
            {
                lastWallHitTime = Time.time;
                return;
            }
        }
    }

    void SetState(bool hit)
    {
        showingHit = hit;
        if (wallRenderer != null)
        {
            var mats = wallRenderer.sharedMaterials;
            if (wallMaterialIndex < mats.Length)
            {
                mats[wallMaterialIndex] = hit ? afterCollision : wallColor;
                wallRenderer.sharedMaterials = mats;
            }
        }
        if (statusText != null) statusText.text = hit ? hitMessage : rollMessage;
    }
}
