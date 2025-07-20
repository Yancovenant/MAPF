/**
* === Scripts/Mapgenerator.cs ===
* This Generate Maps env.
*/

using UnityEngine;
using System.Collections.Generic;
using System.IO;
using MiniJSON;

public class MapGenerator : MonoBehaviour {
    public GameObject personPrefab, roadPrefab, buildingPrefab, warehousePrefab, spawnMarkerPrefab, garageDoorPrefab, loadingSpotPrefab;

    string[] mapLayout;

    public Vector2Int mapSize;
    public List<Vector3> spawnPoints = new List<Vector3>();
    public List<Transform> warehouseTargets = new List<Transform>();

    public void GenerateMap() {
        spawnPoints.Clear();
        warehouseTargets.Clear();
        mapLayout = LoadMapLayout();
        if (mapLayout == null) {
            Debug.LogError("MapGenerator: Failed to load map layout. Using fallback empty map.");
            mapLayout = new string[] { "............................................", "............................................" };
        }
        _GenerateMap();
        mapSize = new Vector2Int(mapLayout[0].Length, mapLayout.Length);
    }

    string[] LoadMapLayout() {
        string fileName = GlobalConfig.Instance.GetSelectedMapFileName();
        string path = Path.Combine(Application.dataPath, "Maps", fileName);
        if (!File.Exists(path)) {
            Debug.LogError($"MapGenerator: Map file not found: {path}");
            return null;
        }
        try {
            string json = File.ReadAllText(path);
            var parsed = Json.Deserialize(json) as System.Collections.Generic.Dictionary<string, object>;
            if (parsed != null && parsed.TryGetValue("layout", out var layoutObj) && layoutObj is System.Collections.IList layoutList) {
                var lines = new List<string>();
                foreach (var line in layoutList) lines.Add(line.ToString());
                return lines.ToArray();
            } else {
                Debug.LogError($"MapGenerator: Invalid map JSON structure in {fileName}");
                return null;
            }
        } catch (System.Exception ex) {
            Debug.LogError($"MapGenerator: Error reading map file {fileName}: {ex.Message}");
            return null;
        }
    }

    void _GenerateMap() {
        for (int y = 0; y < mapLayout.Length; y++) {
            string row = mapLayout[y];
            for (int x = 0; x < row.Length; x++) {
                char cell = row[x];
                Vector3 position = new Vector3(x, 0, mapLayout.Length - y - 1);
                switch(cell) {
                    case 'R':
                        Instantiate(roadPrefab, new Vector3(position.x, 0.05f, position.z), Quaternion.identity, transform);
                        break;
                    case 'B':
                        var building = Instantiate(buildingPrefab, new Vector3(position.x, 0.5f, position.z), Quaternion.identity, transform);
                        building.layer = LayerMask.NameToLayer("Unwalkable");
                        break;
                    case 'W':
                        var warehouse = Instantiate(warehousePrefab, position, Quaternion.identity, transform);
                        warehouse.name = $"Warehouse_{warehouseTargets.Count + 1}";

                        Instantiate(roadPrefab, new Vector3(position.x, 0.05f, position.z), Quaternion.identity, transform);

                        if (_hasRoad(x, y)) _spawnDoor(warehouse, x, y);
                        warehouseTargets.Add(warehouse.transform.Find("TargetPoint"));
                        break;
                    case 'M':
                        var mat = Instantiate(loadingSpotPrefab, new Vector3(position.x, 0.025f, position.z), Quaternion.identity, transform);
                        mat.layer = LayerMask.NameToLayer("Unwalkable");
                        break;
                    case 'S':
                        Instantiate(spawnMarkerPrefab, new Vector3(position.x, 0.05f, position.z), Quaternion.identity, transform);
                        spawnPoints.Add(position + Vector3.up * .4f);
                        break;
                    case '.':
                        GameObject blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        blocker.transform.position = position + Vector3.up * .5f;
                        blocker.transform.localScale = new Vector3(.9f, 1, .9f);
                        blocker.layer = LayerMask.NameToLayer("Unwalkable");
                        DestroyImmediate(blocker.GetComponent<Renderer>());
                        blocker.transform.parent = transform;
                        break;

                    case 'P':
                        var person = Instantiate(personPrefab, position, Quaternion.identity, transform);
                        break;
                }
            }
        }
    }

    bool _hasRoad(int x, int y) {
        int[,] dirs = { {0,1}, {1,0}, {0,-1}, {-1,0} };
        for (int i = 0; i < dirs.GetLength(0); i++) {
            int newX = x + dirs[i, 0];
            int newY = y + dirs[i, 1];
            if (newY < 0 || 
                newY >= mapLayout.Length ||
                newX < 0 || 
                newX >= mapLayout[newY].Length) continue;
            if (mapLayout[newY][newX] == 'R' ||
                mapLayout[newY][newX] == 'W') return true;
        }
        return false;
    }

    void _spawnDoor(GameObject warehouse, int x, int y) {
        Dictionary<Vector2Int, (string wallName, float angle)> directions = new Dictionary<Vector2Int, (string,float)> {
            { new Vector2Int(0, 1), ("Wall_N", 0f) },
            { new Vector2Int(0, -1), ("Wall_S", 180f) },
            { new Vector2Int(1, 0), ("Wall_E", 90f) },
            { new Vector2Int(-1, 0), ("Wall_W", -90f) }
        };
        foreach (var dir in directions) {
            int nx = x + dir.Key.x;
            int ny = y + dir.Key.y;

            if (ny < 0 || ny >= mapLayout.Length) continue;
            string line = mapLayout[ny];
            if (nx < 0 || nx >= line.Length) continue;

            char tile = line[nx];
            if (tile == 'R' || tile == 'S') {
                Transform wall = warehouse.transform.Find(dir.Value.wallName);
                if (wall != null) Destroy(wall.gameObject);
                Vector3 offset = new Vector3(dir.Key.x * 1.5f, 0.6f, dir.Key.y * 1.5f);
                Quaternion rotation = Quaternion.Euler(0, dir.Value.angle, 0);
                Instantiate(garageDoorPrefab, warehouse.transform.position + offset, rotation, warehouse.transform);
            }
        }
    }
}
