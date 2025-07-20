/**
* == CameraCapture.cs ==
* Used for capture and sending
*/

using UnityEngine;
using NativeWebSocket;
using System.Threading.Tasks;
using System;
using UnityEngine.Rendering;
using System.Collections.Generic;

public class CameraCapture : MonoBehaviour {
    private string serverUrl;
    private int serverPort;
    private WebSocket ws;
    private string wsUrl;

    public string agentId;
    public Camera cam;
    private RenderTexture rt;
    private Texture2D tex;

    private int resolutionWidth;
    private int resolutionHeight;
    private int jpegQuality;
    private int targetedFps;
    private float lastSendTime = 0f;

    private bool running = false;

    void Start() {
        agentId = gameObject.name;
        var config = GlobalConfig.Instance;
        serverUrl = config.serverUrl;
        serverPort = config.serverPort;
        resolutionWidth = config.resolutionWidth;
        resolutionHeight = config.resolutionHeight;
        jpegQuality = config.jpegQuality;
        targetedFps = config.targetedFps;

        var isDeployed = serverUrl.StartsWith("https://");
        var wsProtocol = isDeployed ? "wss" : "ws";
        serverUrl = isDeployed ? serverUrl.Replace("https://", "") : serverUrl + ":" + serverPort;
        wsUrl = $"{wsProtocol}://{serverUrl}/ws/augv/{agentId}";
        Debug.Log($"{agentId} wsUrl: {wsUrl}");
        Application.runInBackground = true;

        cam = GetComponent<Camera>();
        if (cam == null) cam = GetComponentInChildren<Camera>();
        cam.forceIntoRenderTexture = true;

        if (rt == null) rt = new RenderTexture(resolutionWidth, resolutionHeight, 16);
        if (tex == null) tex = new Texture2D(resolutionWidth, resolutionHeight, TextureFormat.RGB24, false);
        cam.targetTexture = rt;

        running = true;
        _connectBackend();
    }

    void OnDestroy() {
        running = false;
        _cleanWs();
    }

    void OnApplicationQuit() {
        running = false;
        _cleanWs();
    }

    void Update() => ws?.DispatchMessageQueue();

    public async void TrySendImageAndResponse() {
        if (!running || ws == null || ws.State != WebSocketState.Open) return;
        if (targetedFps > 0 && Time.time - lastSendTime < 1f / targetedFps) return;
        lastSendTime = Time.time;

        bool yoloTrue = GlobalConfig.Instance.GetAgentYolo(agentId);
        var param = new Dictionary<string, object> {
            {"useYolo", yoloTrue}
        };

        string headerJson = MiniJSON.Json.Serialize(param);
        byte[] headerBytes = System.Text.Encoding.UTF8.GetBytes(headerJson);
        byte[] delimiter = System.Text.Encoding.UTF8.GetBytes("\n");
        
        var req = AsyncGPUReadback.Request(rt, 0, TextureFormat.RGB24);
        while (!req.done) await Task.Delay(1);
        if (!req.hasError) {
            var raw = req.GetData<byte>();
            RenderTexture.active = rt;
            tex.LoadRawTextureData(raw);
            tex.Apply(false, false);
            byte[] frame = tex.EncodeToJPG(jpegQuality);
            byte[] payload = new byte[headerBytes.Length + delimiter.Length + frame.Length];
            System.Buffer.BlockCopy(headerBytes, 0, payload, 0, headerBytes.Length);
            System.Buffer.BlockCopy(delimiter, 0, payload, headerBytes.Length, delimiter.Length);
            System.Buffer.BlockCopy(frame, 0, payload, headerBytes.Length + delimiter.Length, frame.Length);
            _ = ws.Send(payload).ContinueWith(task => {
                if (task.IsFaulted) {
                    Debug.LogError($"{agentId} failed to send image: {task.Exception}");
                }
            });
        }
    }

    private async void _cleanWs() {
        if (ws != null && ws.State == WebSocketState.Open) {
            await ws.Close();
        }
        ws = null;

        // TODO: clean up the render texture and texture2d.
        if (rt != null) {
            rt.Release();
            rt = null;
        }
        if (tex != null) {
            DestroyImmediate(tex);
            tex = null;
        }
    }

    private async void _connectBackend() {
        ws = new WebSocket(wsUrl);
        ws.OnOpen += () => Debug.Log($"{agentId} connected to backend");
        ws.OnError += (e) => Debug.LogError($"{agentId} error: {e}");
        ws.OnClose += (e) => Debug.Log($"{agentId} closed connection: {e}");
        ws.OnMessage += (bytes) => {
            var message = System.Text.Encoding.UTF8.GetString(bytes);
            var parsed = MiniJSON.Json.Deserialize(message) as System.Collections.Generic.Dictionary<string, object>;
            if (parsed != null && parsed.TryGetValue("action", out var action)) {
                if (action.ToString() == "obstacle") {
                    if (parsed.TryGetValue("data", out var data) && PathSupervisor.Instance != null) {
                        PathSupervisor.Instance.AssignObstacleFromJSON(data as System.Collections.Generic.Dictionary<string, object>);
                    }
                } else {
                    Debug.LogError($"{agentId} invalid action: {action}");
                }
            } else {
                Debug.LogError($"{agentId} no action in message: {message}");
            }
        };
        try {
            await ws.Connect();
        } catch (Exception e) {
            Debug.LogWarning($"{agentId} failed to connect to backend: {e}");
            await Task.Delay(1000);
            if (running) _connectBackend();
        }
    }
} 