using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

/// MQTT 3.1.1 tối giản (QoS 0, chỉ PUBLISH), không cần thư viện ngoài. Broker mặc định: Adafruit IO.
/// Mỗi lần gửi: TCP → CONNECT → CONNACK → PUBLISH từng feed → DISCONNECT.
/// AIO Key KHÔNG lưu trong scene: đọc từ UserSettings/adafruit_key.txt (thư mục bị .gitignore của Unity).
public class Lab08MqttPublisher : MonoBehaviour
{
    [Header("Broker")]
    public string brokerAddress = "io.adafruit.com";
    public int brokerPort = 1883;               // 1883 = MQTT không mã hoá
    public string userName = "";                // Adafruit username
    [SerializeField] string keyFile = "UserSettings/adafruit_key.txt";

    [Header("Feed keys (tạo sẵn trên Adafruit IO)")]
    public string labelFeed = "lab08-label";    // chuỗi "tiger cat 87.5%"
    public string boxFeed = "lab08-box";        // JSON toạ độ box
    public string imageFeed = "lab08-image";    // ảnh JPEG base64

    [Tooltip("Adafruit free: 30 tin/phút. Mỗi lần gửi 3 tin → 3 × 60/30 = 6 giây/lần.")]
    public float minSendInterval = 6f;
    [SerializeField] int timeoutMs = 5000;

    string key;
    float lastSend = -999f;
    volatile bool sending;
    public bool Ready => !string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(userName);
    public string LastStatus { get; private set; } = "MQTT: idle";

    void Awake()
    {
        string p = Path.Combine(Directory.GetCurrentDirectory(), keyFile);
        if (File.Exists(p)) key = File.ReadAllText(p).Trim();
        if (!Ready) { LastStatus = "MQTT: thiếu username/key"; Debug.LogWarning($"[MQTT] Thiếu userName hoặc key ({p})."); }
    }

    /// Gửi nhiều cặp (feed, payload) trong một kết nối. Trả false nếu bị chặn vì gửi quá nhanh.
    public bool Publish(params (string feed, string payload)[] msgs)
    {
        if (!Ready) return false;
        if (sending || Time.time - lastSend < minSendInterval) { LastStatus = $"MQTT: chờ {minSendInterval}s giữa 2 lần gửi"; return false; }
        lastSend = Time.time;
        LastStatus = "MQTT: đang gửi...";
        _ = Task.Run(() => SendAsync(msgs));
        return true;
    }

    async Task SendAsync((string feed, string payload)[] msgs)
    {
        sending = true;
        try
        {
            using var tcp = new TcpClient();
            var conn = tcp.ConnectAsync(brokerAddress, brokerPort);
            if (await Task.WhenAny(conn, Task.Delay(timeoutMs)) != conn) throw new TimeoutException("TCP timeout");
            await conn;
            var s = tcp.GetStream();

            byte[] c = Packet(0x10, Connect("unity-lab08-" + Guid.NewGuid().ToString("N").Substring(0, 6)));
            await s.WriteAsync(c, 0, c.Length);

            byte[] ack = new byte[4]; int read = 0;
            var rt = Task.Run(async () => { while (read < 4) { int n = await s.ReadAsync(ack, read, 4 - read); if (n == 0) break; read += n; } });
            if (await Task.WhenAny(rt, Task.Delay(timeoutMs)) != rt || read < 4) throw new TimeoutException("Không nhận CONNACK");
            if (ack[0] != 0x20 || ack[3] != 0) throw new Exception($"Broker từ chối, mã {ack[3]} (4/5 = sai user/key)");

            foreach (var m in msgs)
            {
                var body = new List<byte>();
                body.AddRange(Str($"{userName}/feeds/{m.feed}"));     // QoS 0 → không có packet id
                body.AddRange(Encoding.UTF8.GetBytes(m.payload));
                byte[] p = Packet(0x30, body);
                await s.WriteAsync(p, 0, p.Length);
            }
            await s.WriteAsync(new byte[] { 0xE0, 0x00 }, 0, 2); // DISCONNECT
            await s.FlushAsync();
            LastStatus = $"MQTT: đã gửi {msgs.Length} feed lúc {DateTime.Now:HH:mm:ss}";
            Debug.Log("[MQTT] " + LastStatus);
        }
        catch (Exception e) { LastStatus = "MQTT lỗi: " + e.Message; Debug.LogError("[MQTT] " + e.Message); }
        finally { sending = false; }
    }

    List<byte> Connect(string clientId)
    {
        var b = new List<byte>();
        b.AddRange(Str("MQTT")); b.Add(0x04);   // MQTT 3.1.1
        b.Add(0xC2);                            // username + password + clean session
        b.Add(0x00); b.Add(0x3C);               // keep alive 60 s
        b.AddRange(Str(clientId)); b.AddRange(Str(userName)); b.AddRange(Str(key));
        return b;
    }

    static byte[] Str(string s)
    {
        byte[] d = Encoding.UTF8.GetBytes(s);
        var r = new byte[d.Length + 2];
        r[0] = (byte)(d.Length >> 8); r[1] = (byte)(d.Length & 0xFF);
        Buffer.BlockCopy(d, 0, r, 2, d.Length);
        return r;
    }

    static byte[] Packet(byte type, List<byte> body)
    {
        var p = new List<byte> { type };
        int len = body.Count;
        do { byte d = (byte)(len % 128); len /= 128; if (len > 0) d |= 0x80; p.Add(d); } while (len > 0); // remaining length 7 bit/byte
        p.AddRange(body);
        return p.ToArray();
    }
}
