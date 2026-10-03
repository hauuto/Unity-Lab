using UnityEngine;
using UnityEngine.AI;

// Lab 04 - Bai 2: robot duoi theo nhan vat bang NavMeshAgent.
// Can NavMesh da bake tren Maze (NavMeshSurface cua package AI Navigation).
[RequireComponent(typeof(NavMeshAgent))]
public class Lab04_RobotChaser : MonoBehaviour
{
    public Transform target;               // nhan vat can duoi
    [Tooltip("Cap nhat duong di moi bao nhieu giay (tranh tinh lai moi frame)")]
    public float repathInterval = 0.2f;
    [Tooltip("Khoang cach coi la da bat duoc nhan vat")]
    public float catchDistance = 0.6f;

    NavMeshAgent agent;
    float timer;

    void Start() { agent = GetComponent<NavMeshAgent>(); }

    void Update()
    {
        if (target == null || !agent.isOnNavMesh) return;
        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            agent.SetDestination(target.position);
            timer = repathInterval;
        }
        if (!agent.pathPending && agent.remainingDistance <= catchDistance)
            Debug.Log("Robot da bat duoc nhan vat!");
    }
}
