/**
* === Scripts/SocketServer.cs ===
* Socket server for communication with the Unity client.
*/

using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Concurrent;
using System;
using System.Collections.Generic;

public class SocketServer : MonoBehaviour {
    
    private TcpListener listener;
    private Thread serverThread;
    private CancellationTokenSource cts;
    private ConcurrentQueue<string> incomingMessages = new();
    
    private bool isShuttingDown = false;
    void Start() {
        cts = new CancellationTokenSource();
        listener = new TcpListener(IPAddress.Any, GlobalConfig.Instance.unityPort);
        listener.Start();
        serverThread = new Thread(() => _handleConnections(cts.Token));
        serverThread.IsBackground = true;
        serverThread.Start();
        Debug.Log($"SocketServer: Started on port {GlobalConfig.Instance.unityPort}, {listener}");
    }

    void Update() {
        while (incomingMessages.TryDequeue(out string json)) {
            if (!(MiniJSON.Json.Deserialize(json) is Dictionary<string, object> parsed)) {
                Debug.LogError($"SocketServer: Invalid JSON: {json}");
                continue;
            }
            if (parsed.TryGetValue("action", out var action)) {
                if (action.ToString() == "route") {
                    PathSupervisor.Instance.AssignRouteFromJSON(parsed["data"] as Dictionary<string, object>);
                } else if (action.ToString() == "obstacle") {
                    PathSupervisor.Instance.AssignObstacleFromJSON(parsed["data"] as Dictionary<string, object>);
                } else {
                    Debug.LogError($"SocketServer: Invalid action: {action}");
                }
            } else {
                Debug.LogError($"SocketServer: No action in message: {json}");
                continue;
            }
        }
    }

    private void _handleConnections(CancellationToken token) {
        try {
            while (true && !token.IsCancellationRequested) {
                TcpClient client = null;
                NetworkStream stream = null;
                try {
                    client = listener.AcceptTcpClient();
                    stream = client.GetStream();
                    byte[] buffer = new byte[client.ReceiveBufferSize];
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    string json = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    incomingMessages.Enqueue(json);
                } catch (SocketException e) {
                    if (isShuttingDown || token.IsCancellationRequested) break;
                    Debug.LogError($"SocketServer: SocketException: {e.Message}");
                } catch (Exception e) {
                    if (isShuttingDown || token.IsCancellationRequested) break;
                    Debug.LogError($"SocketServer: Exception: {e.Message}");
                } finally {
                    client?.Close();
                    stream?.Close();
                }
            }
        } catch (ObjectDisposedException e) {
            Debug.LogWarning($"SocketServer: ObjectDisposedException: {e.Message}");
        } catch (Exception e) {
            Debug.LogError($"SocketServer: Error handling connections: {e.Message}");
        }
    }

    void _cleanUp() {
        if (isShuttingDown) return;
        isShuttingDown = true;
        try {
            cts?.Cancel();
        } catch (Exception e) {
            Debug.LogWarning($"SocketServer: Exception during cts.Cancel(): {e.Message}");
        }

        try {
            listener?.Stop();
        } catch (Exception e) {
            Debug.LogWarning($"SocketServer: Exception during listener.Stop(): {e.Message}");
        }

        try {
            if (serverThread != null && serverThread.IsAlive) {
                if (!serverThread.Join(1000)) {
                    Debug.LogWarning($"SocketServer: Thread did not exit in time");
                }
            }
        } catch (Exception e) {
            Debug.LogWarning($"SocketServer: Exception during serverThread.Join(): {e.Message}");
        }

        try {
            cts?.Dispose();
        } catch (Exception e) {
            Debug.LogWarning($"SocketServer: Exception during cts.Dispose(): {e.Message}");
        }
    }
    void OnDestroy() {
        _cleanUp();
    }
    
    void OnApplicationQuit() {
        _cleanUp();
    }
}
