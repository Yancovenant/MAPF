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

    [Header("Unity Port")]
    [Tooltip("Port for the Unity client to connect to")]
    public int unityPort = 8051;

    [Header("YOLO Obstacle Detection")]
    [Tooltip("Time in seconds to consider an obstacle as expired")]
    public float YOLO_OBSTACLE_TIMEOUT = 0.5f;

    public enum YoloModelType { pt, onnx }
    [Header("YOLO Model Settings")]
    public YoloModelType yoloModelType = YoloModelType.pt;

    [Tooltip("Image size for YOLO model. Only editable if using pt.")]
    public Vector2Int imageSize = new Vector2Int(640, 640);

    [Tooltip("Targeted FPS for camera sending")] 
    public int targetedFps = 36;

    [System.Serializable]
    public class AgentYoloConfig {
        public string agentId;
        public bool useYolo;
    }

    [Header("Agent YOLO Usage (runtime editable)")]
    public List<AgentYoloConfig> agentYoloConfigs = new List<AgentYoloConfig>();

    public event System.Action<string, bool> OnAgentYoloToggleChanged;

    public void SetAgentYolo(string agentId, bool useYolo) {
        var cfg = agentYoloConfigs.Find(a => a.agentId == agentId);
        if (cfg != null && cfg.useYolo != useYolo) {
            cfg.useYolo = useYolo;
            OnAgentYoloToggleChanged?.Invoke(agentId, useYolo);
        }
    }

    public bool GetAgentYolo(string agentId) {
        var cfg = agentYoloConfigs.Find(a => a.agentId == agentId);
        return cfg != null && cfg.useYolo;
    }

    public void UpdateAgentList(List<string> agentIds) {
        foreach (var id in agentIds) {
            if (!agentYoloConfigs.Exists(a => a.agentId == id))
                agentYoloConfigs.Add(new AgentYoloConfig { agentId = id, useYolo = false });
        }
        agentYoloConfigs.RemoveAll(a => !agentIds.Contains(a.agentId));
    }

    void OnValidate() {
        if (yoloModelType == YoloModelType.onnx) {
            imageSize = new Vector2Int(640, 640);
        }
    }

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
}
