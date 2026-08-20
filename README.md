# 🎮 Unity Labs Repository

Repository này lưu trữ toàn bộ các bài thực hành (Lab) môn học CNM (Học kỳ 1, Năm học 2026 - 2027) tại trường Đại học Công nghiệp TP.HCM (IUH). 

Project được thiết lập theo kiến trúc **Monorepo** (1 Project - Nhiều Labs) để tối ưu hóa không gian lưu trữ và dễ dàng quản lý version control.

## 🛠 Tech Stack
* **Engine:** Unity 6.5
* **Ngôn ngữ:** C#
* **Version Control:** Git & Git LFS (Large File Storage)

## 📁 Cấu trúc thư mục (Project Structure)

Toàn bộ mã nguồn và tài nguyên được đặt trong thư mục `Assets/`. Các file tạm (như `Library`, `Temp`, `Logs`) đã được loại bỏ qua `.gitignore`.

```text
Assets/
├── _Shared/           # Chứa các scripts, UI, materials dùng chung cho nhiều bài lab
├── Editor/            # Chứa các tool C# tự viết để hỗ trợ Editor (VD: Tool tạo folder)
├── Lab_01/            # Tài nguyên độc lập của bài Lab 1
│   ├── Scenes/        # Scene chính của bài thực hành
│   ├── Scripts/       
│   └── Prefabs/       
├── Lab_02/            # Tài nguyên độc lập của bài Lab 2
└── ...
