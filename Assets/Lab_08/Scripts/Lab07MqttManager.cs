using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Lab 07 - MQTT Client & Network Manager
/// Hỗ trợ đầy đủ:
/// 1. Joining app: Tự động gửi "Hello, I’am here!" khi kết nối.
/// 2. Handling player exits:
///    - Không mong muốn (unintentional): Broker kích hoạt Last Will Message (LWT).
///    - Chủ động thoát (intentional): Gửi thông điệp tạm biệt trước khi DISCONNECT.
/// 3. User events:
///    - Bấm ảnh: Gửi thời gian và số lần click lên server.
///    - Bấm nút Submit: Cập nhật text số lần click và gửi lên cloud (server).
/// Hỗ trợ cả Public Broker (broker.emqx.io, broker.hivemq.com) và Adafruit IO.
/// </summary>
public class Lab07MqttManager : MonoBehaviour
{
    public static Lab07MqttManager Instance { get; private set; }

    public enum BrokerMode
    {
        PublicBroker,   // broker.emqx.io hoặc broker.hivemq.com (không cần auth)
        AdafruitIO,     // io.adafruit.com (cần userName và aioKey)
        Custom          // Tự cấu hình topic và broker
    }

    [Header("Cấu hình Broker")]
    [Tooltip("Chế độ broker: PublicBroker dùng test ngay không cần key; AdafruitIO dùng feed cá nhân")]
    public BrokerMode brokerMode = BrokerMode.PublicBroker;

    [Tooltip("Địa chỉ MQTT Broker (VD: broker.emqx.io hoặc io.adafruit.com)")]
    public string brokerAddress = "broker.emqx.io";

    [Tooltip("Cổng kết nối TCP thường (1883)")]
    public int brokerPort = 1883;

    [Tooltip("Tên tài khoản (nếu dùng Adafruit IO)")]
    public string userName = "hauuto";

    [Tooltip("AIO Key bí mật của Adafruit IO (bỏ trống nếu dùng Public Broker)")]
    public string aioKey = "";

    [Header("Cấu hình Topic")]
    [Tooltip("Topic/Feed nhận số lần click khi bấm Submit")]
    public string countTopic = "unity/lab07/hauuto/count";

    [Tooltip("Topic/Feed nhận sự kiện trạng thái (Join, Last Will, Image Click)")]
    public string statusTopic = "unity/lab07/hauuto/status";

    [Tooltip("Tên feed số đếm trên Adafruit IO")]
    public string adafruitFeedCount = "count";

    [Tooltip("Tên feed trạng thái trên Adafruit IO")]
    public string adafruitFeedStatus = "status";

    [Header("Thông điệp (Theo đề bài Lab 07)")]
    [Tooltip("Thông điệp khi tham gia (Yêu cầu 1: Joining app)")]
    public string joinMessage = "Hello, I’am here!";

    [Tooltip("Thông điệp Last Will khi thoát (Yêu cầu 2: Handling player exits)")]
    public string lastWillMessage = "Someone has left the application";

    [Header("Tùy chọn kết nối")]
    [Tooltip("Tự động kết nối khi vào Play Mode")]
    public bool autoConnectOnStart = true;

    [Tooltip("Thời gian Keep Alive (giây) theo chuẩn MQTT")]
    [Range(10, 120)]
    public ushort keepAliveSeconds = 60;

    [Tooltip("Khoảng thời gian gửi PINGREQ định kỳ để duy trì kết nối")]
    [Range(5, 60)]
    public float pingIntervalSeconds = 20f;

    [Tooltip("Thời gian chờ kết nối tối đa (ms)")]
    public int timeoutMs = 8000;

    // Events cho UI / logic đăng ký
    public event Action<bool> OnConnectionChanged;
    public event Action<string, string> OnMessageReceived;

    // Trạng thái nội bộ
    public bool IsConnected { get; private set; }
    public string ClientId { get; private set; }

    private TcpClient tcpClient;
    private NetworkStream networkStream;
    private CancellationTokenSource cts;
    private readonly SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);
    private ushort nextPacketId = 1;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(gameObject); return; }

        ClientId = "unity-lab07-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        ApplyBrokerDefaults();
    }

    void Start()
    {
        if (autoConnectOnStart)
        {
            _ = ConnectAsync();
        }
    }

    void OnValidate()
    {
        ApplyBrokerDefaults();
    }

    public void ApplyBrokerDefaults()
    {
        if (brokerMode == BrokerMode.PublicBroker)
        {
            if (string.IsNullOrEmpty(brokerAddress) || brokerAddress.Contains("adafruit"))
                brokerAddress = "broker.emqx.io";
            countTopic = $"unity/lab07/{userName}/count";
            statusTopic = $"unity/lab07/{userName}/status";
        }
        else if (brokerMode == BrokerMode.AdafruitIO)
        {
            brokerAddress = "io.adafruit.com";
            countTopic = $"{userName}/feeds/{adafruitFeedCount}";
            statusTopic = $"{userName}/feeds/{adafruitFeedStatus}";
        }
    }

    public string GetCountTopic()
    {
        if (brokerMode == BrokerMode.AdafruitIO || brokerAddress.Contains("adafruit"))
            return $"{userName}/feeds/{adafruitFeedCount}";
        return countTopic;
    }

    public string GetStatusTopic()
    {
        if (brokerMode == BrokerMode.AdafruitIO || brokerAddress.Contains("adafruit"))
            return $"{userName}/feeds/{adafruitFeedStatus}";
        return statusTopic;
    }

    /// <summary>
    /// Kết nối tới MQTT Broker và cấu hình Last Will Message (Yêu cầu 1 & 2)
    /// </summary>
    public async Task<bool> ConnectAsync()
    {
        if (IsConnected)
        {
            Debug.Log("[MQTT] Đã kết nối trước đó.");
            return true;
        }

        cts?.Cancel();
        cts = new CancellationTokenSource();
        var token = cts.Token;

        string targetStatusTopic = GetStatusTopic();
        string targetCountTopic = GetCountTopic();

        Debug.Log($"[MQTT] Bắt đầu kết nối {brokerAddress}:{brokerPort} với ClientId={ClientId}...");
        Debug.Log($"[MQTT] Last Will Message được cấu hình: topic='{targetStatusTopic}', message='{lastWillMessage}'");

        try
        {
            tcpClient = new TcpClient();
            var connectTask = tcpClient.ConnectAsync(brokerAddress, brokerPort);
            if (await Task.WhenAny(connectTask, Task.Delay(timeoutMs, token)) != connectTask)
                throw new TimeoutException($"Không thể mở cổng TCP tới {brokerAddress}:{brokerPort} sau {timeoutMs}ms");

            await connectTask;
            networkStream = tcpClient.GetStream();

            // 1. Gửi gói tin CONNECT (kèm Last Will Flag và cấu hình auth)
            bool isAdafruit = (brokerMode == BrokerMode.AdafruitIO) || brokerAddress.Contains("adafruit");
            string authUser = isAdafruit ? userName : (string.IsNullOrEmpty(userName) ? null : userName);
            string authPass = isAdafruit ? aioKey : null;

            byte[] connectPacket = BuildConnectPacket(
                ClientId,
                authUser,
                authPass,
                targetStatusTopic,
                lastWillMessage,
                keepAliveSeconds
            );

            await networkStream.WriteAsync(connectPacket, 0, connectPacket.Length, token);

            // 2. Chờ nhận phản hồi CONNACK (4 bytes: 0x20, 0x02, sessionFlags, returnCode)
            byte[] connack = await ReadExactAsync(networkStream, 4, token);
            if (connack[0] != 0x20 || connack[3] != 0)
            {
                byte code = connack[3];
                string reason = code switch
                {
                    1 => "Phiên bản giao thức không được chấp nhận",
                    2 => "Mã Client Identifier bị từ chối",
                    3 => "Máy chủ MQTT không khả dụng",
                    4 => "Sai username hoặc password (AIO Key)",
                    5 => "Không có quyền kết nối",
                    _ => $"Mã lỗi không xác định ({code})"
                };
                throw new Exception($"Broker từ chối kết nối MQTT: {reason}");
            }

            IsConnected = true;
            Debug.Log($"<color=green>[MQTT] Kết nối thành công!</color> Broker: {brokerAddress}:{brokerPort}");
            OnConnectionChanged?.Invoke(true);

            // 3. Khởi chạy luồng đọc dữ liệu đến và luồng Ping Keep-Alive
            _ = Task.Run(() => ReadLoopAsync(networkStream, token), token);
            _ = Task.Run(() => PingLoopAsync(networkStream, token), token);

            // 4. Đăng ký Subscribe lắng nghe topic trạng thái và topic đếm
            await SubscribeAsync(targetStatusTopic);
            await SubscribeAsync(targetCountTopic);

            // 5. YÊU CẦU 1: Gửi thông điệp tham gia "Hello, I’am here!"
            Debug.Log($"[MQTT - Requirement 1] Gửi thông điệp tham gia: '{joinMessage}' tới {targetStatusTopic}");
            await PublishAsync(targetStatusTopic, joinMessage);

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MQTT] Lỗi kết nối: {ex.Message}");
            CleanUpConnection();
            OnConnectionChanged?.Invoke(false);
            return false;
        }
    }

    /// <summary>
    /// Gửi thông điệp PUBLISH (QoS 0)
    /// </summary>
    public async Task<bool> PublishAsync(string topic, string payload)
    {
        if (!IsConnected || networkStream == null)
        {
            Debug.LogWarning($"[MQTT] Chưa kết nối, không thể gửi tới {topic}: {payload}");
            return false;
        }

        try
        {
            byte[] packet = BuildPublishPacket(topic, payload);
            await sendLock.WaitAsync();
            try
            {
                await networkStream.WriteAsync(packet, 0, packet.Length);
                await networkStream.FlushAsync();
            }
            finally
            {
                sendLock.Release();
            }

            Debug.Log($"<color=cyan>[MQTT Publish]</color> Topic: <b>{topic}</b> | Nội dung: <i>{payload}</i>");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MQTT] Lỗi khi gửi tới {topic}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// YÊU CẦU 3: Khi click vào ảnh, gửi thời gian (time) và số lần clicks lên server.
    /// </summary>
    public void SendImageClick(int clicks, DateTime timestamp)
    {
        string topic = GetStatusTopic();
        string timeStr = timestamp.ToString("yyyy-MM-dd HH:mm:ss");
        string payload = $"Clicks: {clicks}, Time: {timeStr}";

        Debug.Log($"[MQTT - Requirement 3] Click vào ảnh -> Gửi time & clicks: {payload}");
        _ = PublishAsync(topic, payload);
    }

    /// <summary>
    /// YÊU CẦU 3: Khi bấm nút Submit, gửi số clicks lên cloud (server).
    /// </summary>
    public void SendSubmitClick(int clicks)
    {
        string topic = GetCountTopic();
        string payload = clicks.ToString();

        Debug.Log($"[MQTT - Requirement 3] Bấm nút Submit -> Gửi số clicks: {payload} tới {topic}");
        _ = PublishAsync(topic, payload);
    }

    /// <summary>
    /// Đăng ký lắng nghe topic (SUBSCRIBE QoS 0)
    /// </summary>
    public async Task<bool> SubscribeAsync(string topic)
    {
        if (!IsConnected || networkStream == null) return false;

        try
        {
            ushort packetId = nextPacketId++;
            byte[] packet = BuildSubscribePacket(packetId, topic, 0);

            await sendLock.WaitAsync();
            try
            {
                await networkStream.WriteAsync(packet, 0, packet.Length);
                await networkStream.FlushAsync();
            }
            finally
            {
                sendLock.Release();
            }

            Debug.Log($"[MQTT Subscribe] Đã gửi yêu cầu lắng nghe topic: {topic}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MQTT Subscribe] Lỗi: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Luồng đọc gói tin phản hồi từ Broker
    /// </summary>
    private async Task ReadLoopAsync(NetworkStream stream, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested && stream != null)
            {
                byte[] header = new byte[1];
                int n = await stream.ReadAsync(header, 0, 1, token);
                if (n == 0) break; // Broker đóng kết nối

                byte packetType = header[0];
                int remainingLength = await ReadVariableLengthAsync(stream, token);
                byte[] payload = remainingLength > 0 ? await ReadExactAsync(stream, remainingLength, token) : Array.Empty<byte>();

                ProcessIncomingPacket(packetType, payload);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (IsConnected)
                Debug.LogWarning($"[MQTT ReadLoop] Kết thúc luồng đọc: {ex.Message}");
        }
        finally
        {
            CleanUpConnection();
            OnConnectionChanged?.Invoke(false);
        }
    }

    /// <summary>
    /// Xử lý các loại gói tin MQTT nhận về
    /// </summary>
    private void ProcessIncomingPacket(byte packetType, byte[] data)
    {
        byte typeNibble = (byte)(packetType & 0xF0);

        // PUBLISH (0x30)
        if (typeNibble == 0x30)
        {
            if (data.Length < 2) return;
            int topicLen = (data[0] << 8) | data[1];
            if (data.Length < 2 + topicLen) return;

            string topic = Encoding.UTF8.GetString(data, 2, topicLen);
            int payloadOffset = 2 + topicLen;
            int payloadLen = data.Length - payloadOffset;
            string payload = payloadLen > 0 ? Encoding.UTF8.GetString(data, payloadOffset, payloadLen) : "";

            Debug.Log($"<color=yellow>[MQTT Received]</color> Topic: <b>{topic}</b> | Payload: <i>{payload}</i>");
            OnMessageReceived?.Invoke(topic, payload);
        }
        // SUBACK (0x90)
        else if (typeNibble == 0x90)
        {
            Debug.Log("[MQTT] Broker đã chấp nhận đăng ký (SUBACK).");
        }
        // PINGRESP (0xD0)
        else if (typeNibble == 0xD0)
        {
            // Keep-alive phản hồi từ broker
        }
    }

    /// <summary>
    /// Luồng gửi PINGREQ định kỳ để duy trì kết nối
    /// </summary>
    private async Task PingLoopAsync(NetworkStream stream, CancellationToken token)
    {
        byte[] pingPacket = new byte[] { 0xC0, 0x00 };
        while (!token.IsCancellationRequested && IsConnected)
        {
            try
            {
                await Task.Delay((int)(pingIntervalSeconds * 1000), token);
                if (!IsConnected || stream == null) break;

                await sendLock.WaitAsync(token);
                try
                {
                    await stream.WriteAsync(pingPacket, 0, 2, token);
                    await stream.FlushAsync(token);
                }
                finally
                {
                    sendLock.Release();
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MQTT Ping] Lỗi ping keep-alive: {ex.Message}");
                break;
            }
        }
    }

    /// <summary>
    /// Thoát kết nối. Nếu chủ động (intentional), gửi thông điệp tạm biệt rồi mới gửi DISCONNECT.
    /// (Yêu cầu 2: Handling player exits)
    /// </summary>
    public async Task DisconnectAsync(bool sendGoodbye = true)
    {
        if (!IsConnected) return;

        Debug.Log("[MQTT - Requirement 2] Thoát ứng dụng chủ động (intentional exit)...");
        try
        {
            if (sendGoodbye && networkStream != null)
            {
                // Gửi thông báo tạm biệt tới mọi người trước khi ngắt
                string topic = GetStatusTopic();
                Debug.Log($"[MQTT - Requirement 2] Gửi ghi chú tạm biệt: '{lastWillMessage}' tới {topic}");
                await PublishAsync(topic, lastWillMessage);
            }

            if (networkStream != null)
            {
                // Gói DISCONNECT (0xE0 0x00)
                byte[] disconnect = new byte[] { 0xE0, 0x00 };
                await sendLock.WaitAsync();
                try
                {
                    await networkStream.WriteAsync(disconnect, 0, 2);
                    await networkStream.FlushAsync();
                }
                finally
                {
                    sendLock.Release();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[MQTT Disconnect] {ex.Message}");
        }
        finally
        {
            CleanUpConnection();
            OnConnectionChanged?.Invoke(false);
            Debug.Log("[MQTT] Đã ngắt kết nối an toàn.");
        }
    }

    private void CleanUpConnection()
    {
        IsConnected = false;
        cts?.Cancel();
        try { networkStream?.Close(); } catch { }
        try { tcpClient?.Close(); } catch { }
        networkStream = null;
        tcpClient = null;
    }

    void OnApplicationQuit()
    {
        // Khi người chơi đóng ứng dụng (chủ động thoát):
        // Gửi ngay thông điệp tạm biệt chủ động và gói DISCONNECT
        if (IsConnected && networkStream != null)
        {
            try
            {
                string topic = GetStatusTopic();
                Debug.Log($"[MQTT - Requirement 2] Chủ động thoát: Gửi '{lastWillMessage}' tới {topic}");
                byte[] pub = BuildPublishPacket(topic, lastWillMessage);
                networkStream.Write(pub, 0, pub.Length);

                byte[] disc = new byte[] { 0xE0, 0x00 };
                networkStream.Write(disc, 0, 2);
                networkStream.Flush();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MQTT OnApplicationQuit] {ex.Message}");
            }
            finally
            {
                CleanUpConnection();
            }
        }
        else
        {
            CleanUpConnection();
        }
    }

    void OnDestroy()
    {
        CleanUpConnection();
    }

    // ==========================================
    // Đóng gói giao thức MQTT 3.1.1 (Pure C#)
    // ==========================================

    private static byte[] BuildConnectPacket(
        string clientId,
        string userName,
        string password,
        string willTopic,
        string willPayload,
        ushort keepAlive)
    {
        var body = new List<byte>();

        // 1. Variable Header
        body.AddRange(EncodeMqttString("MQTT")); // Protocol Name
        body.Add(0x04);                          // Protocol Level: MQTT 3.1.1

        // Connect Flags:
        // bit 7: Username, bit 6: Password, bit 5: Will Retain(0), bit 4-3: Will QoS(00), bit 2: Will Flag, bit 1: Clean Session(1), bit 0: Reserved(0)
        byte flags = 0x02; // Clean Session = 1
        bool hasWill = !string.IsNullOrEmpty(willTopic);
        if (hasWill) flags |= 0x04; // Will Flag = 1

        bool hasUser = !string.IsNullOrEmpty(userName);
        if (hasUser) flags |= 0x80; // Username Flag = 1

        bool hasPass = !string.IsNullOrEmpty(password);
        if (hasPass) flags |= 0x40; // Password Flag = 1

        body.Add(flags);

        // Keep Alive (2 bytes big-endian)
        body.Add((byte)(keepAlive >> 8));
        body.Add((byte)(keepAlive & 0xFF));

        // 2. Payload theo đúng thứ tự chuẩn MQTT 3.1.1:
        // [Client Identifier] -> [Will Topic] -> [Will Message] -> [Username] -> [Password]
        body.AddRange(EncodeMqttString(clientId));

        if (hasWill)
        {
            body.AddRange(EncodeMqttString(willTopic));
            body.AddRange(EncodeMqttString(willPayload ?? ""));
        }

        if (hasUser)
        {
            body.AddRange(EncodeMqttString(userName));
        }

        if (hasPass)
        {
            body.AddRange(EncodeMqttString(password));
        }

        return WrapPacket(0x10, body);
    }

    private static byte[] BuildPublishPacket(string topic, string payload)
    {
        var body = new List<byte>();
        body.AddRange(EncodeMqttString(topic));
        if (!string.IsNullOrEmpty(payload))
        {
            body.AddRange(Encoding.UTF8.GetBytes(payload));
        }
        return WrapPacket(0x30, body); // 0x30 = PUBLISH QoS 0
    }

    private static byte[] BuildSubscribePacket(ushort packetId, string topic, byte qos = 0)
    {
        var body = new List<byte>();
        body.Add((byte)(packetId >> 8));
        body.Add((byte)(packetId & 0xFF));
        body.AddRange(EncodeMqttString(topic));
        body.Add(qos);
        return WrapPacket(0x82, body); // 0x82 = SUBSCRIBE
    }

    private static byte[] EncodeMqttString(string s)
    {
        byte[] b = Encoding.UTF8.GetBytes(s ?? "");
        byte[] result = new byte[b.Length + 2];
        result[0] = (byte)(b.Length >> 8);
        result[1] = (byte)(b.Length & 0xFF);
        Buffer.BlockCopy(b, 0, result, 2, b.Length);
        return result;
    }

    private static byte[] WrapPacket(byte packetType, List<byte> body)
    {
        var packet = new List<byte> { packetType };
        int len = body.Count;
        do
        {
            byte d = (byte)(len % 128);
            len /= 128;
            if (len > 0) d |= 0x80;
            packet.Add(d);
        } while (len > 0);

        packet.AddRange(body);
        return packet.ToArray();
    }

    private static async Task<byte[]> ReadExactAsync(Stream stream, int count, CancellationToken token)
    {
        byte[] buffer = new byte[count];
        int total = 0;
        while (total < count)
        {
            int read = await stream.ReadAsync(buffer, total, count - total, token);
            if (read == 0) throw new EndOfStreamException("Kết nối mạng bị đóng bất ngờ.");
            total += read;
        }
        return buffer;
    }

    private static async Task<int> ReadVariableLengthAsync(Stream stream, CancellationToken token)
    {
        int multiplier = 1;
        int value = 0;
        byte[] b = new byte[1];
        int digit;
        do
        {
            int read = await stream.ReadAsync(b, 0, 1, token);
            if (read == 0) throw new EndOfStreamException("Kết nối mạng bị đóng.");
            digit = b[0];
            value += (digit & 0x7F) * multiplier;
            multiplier *= 128;
            if (multiplier > 128 * 128 * 128)
                throw new Exception("Lỗi định dạng độ dài còn lại (Remaining Length) trong MQTT.");
        } while ((digit & 0x80) != 0);

        return value;
    }

    // ==========================================
    // Context Menu hỗ trợ test nhanh trong Editor
    // ==========================================

    [ContextMenu("Test: Kết nối")]
    public void TestConnect()
    {
        _ = ConnectAsync();
    }

    [ContextMenu("Test: Gửi Join Message ('Hello, I’am here!')")]
    public void TestSendJoin()
    {
        _ = PublishAsync(GetStatusTopic(), joinMessage);
    }

    [ContextMenu("Test: Gửi Image Click (Time + Clicks)")]
    public void TestSendImageClick()
    {
        SendImageClick(10, DateTime.Now);
    }

    [ContextMenu("Test: Gửi Submit Clicks")]
    public void TestSendSubmitClick()
    {
        SendSubmitClick(10);
    }

    [ContextMenu("Test: Ngắt kết nối")]
    public void TestDisconnect()
    {
        _ = DisconnectAsync(true);
    }
}
