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
    
    void Start() {
        cts = new CancellationTokenSource();
        listener = new TcpListener(IPAddress.Any, GlobalConfig.Instance.unityPort);
        listener.Start();
        serverThread = new (() => _handleConnections(cts.Token));
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
                using (TcpClient client = listener.AcceptTcpClient()) 
                using (NetworkStream stream = client.GetStream()) {
                    byte[] buffer = new byte[client.ReceiveBufferSize];
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    string json = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    incomingMessages.Enqueue(json);
                }
            }
        } catch (Exception e) {
            Debug.LogError($"SocketServer: Error handling connections: {e.Message}");
        }
    }

    void OnDestroy() {
        if (cts != null) {
            cts.Cancel();
            cts.Dispose();
        }
    }
    
    void OnApplicationQuit() {
        listener?.Stop();
        if (serverThread != null &&
            serverThread.IsAlive) {
                cts.Cancel();
                cts.Dispose();
                serverThread.Join();
            }
    }
}
