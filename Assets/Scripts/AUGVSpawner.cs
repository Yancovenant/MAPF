/**
* === Scripts/AUGVSpawner.cs ===
* Spawn the Agents.
*/

using UnityEngine;
using System.Collections.Generic;

public class AUGVSpawner : MonoBehaviour {
    public GameObject augvPrefab;
    public MapGenerator mapGen;
    public List<GameObject> agents;

    public void SpawnAgents() {
        if (augvPrefab == null || mapGen == null) {
            Debug.LogError("AUGVSpawner: augvPrefab or mapGen is not set");
            return;
        }
        
        var spawnContainer = new GameObject("LoadingSpots");

        for (int i = 0; i < mapGen.spawnPoints.Count; i++) {
            var spawnPoint = mapGen.spawnPoints[i];
            var augv = Instantiate(augvPrefab, spawnPoint, Quaternion.identity);
            augv.name = $"AUGV_{i + 1}";
            agents.Add(augv);

            // Attach CameraCapture if not present
            if (augv.GetComponentInChildren<Camera>() != null && augv.GetComponentInChildren<CameraCapture>() == null) {
                var cam = augv.GetComponentInChildren<Camera>();
                cam.gameObject.AddComponent<CameraCapture>();
            }

            var spawnPointObj = new GameObject($"AUGV_{i + 1}_Loadingspot");
            spawnPointObj.transform.position = spawnPoint;
            spawnPointObj.transform.parent = spawnContainer.transform;
        }
        // After all agents are spawned, update GlobalConfig agent list
        if (GlobalConfig.Instance != null) {
            var agentIds = new List<string>();
            foreach (var ag in agents) agentIds.Add(ag.name);
            GlobalConfig.Instance.UpdateAgentList(agentIds);
        }
    }
}
