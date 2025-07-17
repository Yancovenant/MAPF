/**
* === Scripts/GlobalConfig.cs ===
* Global configuration for the application.
*/

using UnityEngine;
using System.Collections.Generic;

public class GlobalConfig : MonoBehaviour {
    public static GlobalConfig Instance { get; private set; }

    [Header("Backend Server Configuration")]
    [Tooltip("URL of the backend server to connect to")]
    public string serverUrl = "localhost";
    public int serverPort = 8080;
    [Tooltip("Targeted FPS for camera sending")] 
    public int targetedFps = 36;

    [Header("Unity Port")]
    [Tooltip("Port for the Unity client to connect to")]
    public int unityPort = 8051;

    [Header("YOLO Obstacle Detection")]
    [Tooltip("Time in seconds to consider an obstacle as expired")]
    public float YOLO_OBSTACLE_TIMEOUT = 0.5f;

    [System.Serializable]
    public class AgentYoloConfig {
        public string agentId;
        public bool useYolo;
    }

    [Header("Agent YOLO Usage")]
    public List<AgentYoloConfig> agentYoloConfigs = new List<AgentYoloConfig>();

    [Header("Camera Resolution")]
    [Tooltip("Resolution of the camera")]
    public int resolutionWidth = 640;
    public int resolutionHeight = 320;
    public int jpegQuality = 30;

    void Awake() {
        if (Instance != null &&
            Instance != this) {
                Destroy(gameObject);
                return;
            }
        Instance = this;
    }

    public bool GetAgentYolo(string agentId) {
        var cfg = agentYoloConfigs.Find(a => a.agentId == agentId);
        return cfg != null && cfg.useYolo;
    }
}
