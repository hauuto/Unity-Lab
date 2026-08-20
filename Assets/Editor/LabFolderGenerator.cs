using UnityEngine;
using UnityEditor;
using System.IO;

public class LabFolderGenerator : EditorWindow
{
    private string labName = "Lab_0X";

    // Tạo menu item trên thanh công cụ của Unity
    [MenuItem("Tools/Tự động tạo thư mục Lab")]
    public static void ShowWindow()
    {
        GetWindow<LabFolderGenerator>("Tạo Lab Mới");
    }

    // Vẽ giao diện cửa sổ nhập liệu
    private void OnGUI()
    {
        GUILayout.Label("Nhập tên bài Lab:", EditorStyles.boldLabel);
        labName = GUILayout.TextField(labName);

        GUILayout.Space(10);

        if (GUILayout.Button("Tạo cấu trúc thư mục", GUILayout.Height(30)))
        {
            GenerateFolders();
        }
    }

    private void GenerateFolders()
    {
        // Đường dẫn tuyệt đối đến thư mục Assets
        string basePath = Path.Combine(Application.dataPath, labName);

        if (!Directory.Exists(basePath))
        {
            Directory.CreateDirectory(Path.Combine(basePath, "Scenes"));
            Directory.CreateDirectory(Path.Combine(basePath, "Scripts"));
            Directory.CreateDirectory(Path.Combine(basePath, "Prefabs"));
            Directory.CreateDirectory(Path.Combine(basePath, "Materials"));
            Directory.CreateDirectory(Path.Combine(basePath, "UI"));

            // Yêu cầu Unity load lại các file mới tạo để hiện lên Editor
            AssetDatabase.Refresh();
            Debug.Log($"[Thành công] Đã tạo cấu trúc thư mục cho: {labName}");
        }
        else
        {
            Debug.LogWarning($"[Lỗi] Thư mục {labName} đã tồn tại trong Assets!");
        }
    }
}