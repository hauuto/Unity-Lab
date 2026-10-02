using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

/// MQTT client tối giản (MQTT 3.1.1, QoS 0) — chỉ PUBLISH, không cần thư viện ngoài.
/// Mỗi lần gửi: mở TCP → CONNECT → chờ CONNACK → PUBLISH → DISCONNECT → đóng.
public class MqttImage : MonoBehaviour
{
    [Header("Adafruit IO (giống cấu hình trong MQTTX)")]
    public string brokerAddress = "io.adafruit.com";
    public int brokerPort = 1883;              // 1883 = không mã hoá
    public string userName = "";               // Adafruit username
    public string aioKey = "";                 // aio_... (KHÔNG commit lên GitHub)
    public string feedKey = "count";

    [Header("Nguồn dữ liệu")]
    public FaceCounter counter;

    [Tooltip("Adafruit free: 30 lần/phút → 60/30 = 2 giây/lần")]
    [SerializeField] private float minSendInterval = 2f;
    [Tooltip("Chờ kết nối / CONNACK tối đa bao lâu (ms)")]
    [SerializeField] private int timeoutMs = 5000;

    private float lastSendTime = -999f;
    private volatile bool sending;

    public string Topic => $"{userName}/feeds/{feedKey}";

    // Gán vào On Click() của btnSubmit
    public void OnSubmitClick()
    {
        if (sending) { Debug.LogWarning("[MQTT] Đang gửi lần trước, chờ chút."); return; }
        if (Time.time - lastSendTime < minSendInterval)
        {
            Debug.LogWarning($"[MQTT] Bấm quá nhanh, chờ {minSendInterval}s giữa 2 lần gửi.");
            return;
        }
        lastSendTime = Time.time;
        string value = counter.Count.ToString();          // đọc trên main thread
        _ = Task.Run(() => SendAsync(value));              // gửi trên thread riêng
    }

    // Bấm ⋮ trên tiêu đề component MqttImage → Test: gửi giá trị 99 (không cần Play)
    [ContextMenu("Test: gửi giá trị 99")]
    void TestSend()
    {
        Debug.Log("[MQTT] Test bấm");
        _ = Task.Run(() => SendAsync("99"));
    }

    async Task SendAsync(string value)
    {
        sending = true;
        Debug.Log($"[MQTT] Bắt đầu gửi {value} → {brokerAddress}:{brokerPort}, topic {Topic}");
        try
        {
            using var tcp = new TcpClient();
            var connectTask = tcp.ConnectAsync(brokerAddress, brokerPort);
            if (await Task.WhenAny(connectTask, Task.Delay(timeoutMs)) != connectTask)
                throw new TimeoutException("Không kết nối được TCP tới broker.");
            await connectTask;

            var stream = tcp.GetStream();

            // 1) CONNECT
            byte[] connect = BuildConnect("unity-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            await stream.WriteAsync(connect, 0, connect.Length);

            // 2) CONNACK: 4 byte = 0x20 0x02 <flags> <returnCode>
            byte[] ack = new byte[4];
            int read = 0;
            var readTask = Task.Run(async () =>
            {
                while (read < 4)
                {
                    int n = await stream.ReadAsync(ack, read, 4 - read);
                    if (n == 0) break;
                    read += n;
                }
            });
            if (await Task.WhenAny(readTask, Task.Delay(timeoutMs)) != readTask || read < 4)
                throw new TimeoutException("Không nhận được CONNACK.");
            if (ack[0] != 0x20 || ack[3] != 0)
                throw new Exception($"Broker từ chối, return code = {ack[3]} (4/5 = sai user/key).");

            // 3) PUBLISH (QoS 0, không retain)
            byte[] publish = BuildPublish(Topic, value);
            await stream.WriteAsync(publish, 0, publish.Length);

            // 4) DISCONNECT
            await stream.WriteAsync(new byte[] { 0xE0, 0x00 }, 0, 2);
            await stream.FlushAsync();

            Debug.Log($"[MQTT] Đã gửi {value} → {Topic}");
        }
        catch (Exception e)
        {
            Debug.LogError("[MQTT] Gửi thất bại: " + e.Message);
        }
        finally { sending = false; }
    }

    // ---------- Đóng gói MQTT 3.1.1 ----------

    byte[] BuildConnect(string clientId)
    {
        var body = new List<byte>();
        body.AddRange(Str("MQTT"));      // tên protocol
        body.Add(0x04);                  // level 4 = MQTT 3.1.1
        body.Add(0xC2);                  // username(0x80) + password(0x40) + clean session(0x02)
        body.Add(0x00); body.Add(0x3C);  // keep alive = 60 giây
        body.AddRange(Str(clientId));
        body.AddRange(Str(userName));
        body.AddRange(Str(aioKey));
        return Packet(0x10, body);
    }

    byte[] BuildPublish(string topic, string payload)
    {
        var body = new List<byte>();
        body.AddRange(Str(topic));                       // QoS 0 → không có packet id
        body.AddRange(Encoding.UTF8.GetBytes(payload));   // payload không có tiền tố độ dài
        return Packet(0x30, body);
    }

    // Chuỗi MQTT = 2 byte độ dài (big-endian) + nội dung UTF-8
    static byte[] Str(string s)
    {
        byte[] b = Encoding.UTF8.GetBytes(s);
        var r = new byte[b.Length + 2];
        r[0] = (byte)(b.Length >> 8);
        r[1] = (byte)(b.Length & 0xFF);
        Buffer.BlockCopy(b, 0, r, 2, b.Length);
        return r;
    }

    // Header cố định + "remaining length" mã hoá 7 bit/byte
    static byte[] Packet(byte type, List<byte> body)
    {
        var p = new List<byte> { type };
        int len = body.Count;
        do
        {
            byte d = (byte)(len % 128);
            len /= 128;
            if (len > 0) d |= 0x80;
            p.Add(d);
        } while (len > 0);
        p.AddRange(body);
        return p.ToArray();
    }
}