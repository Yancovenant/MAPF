/**
* === Scripts/Mapgenerator.cs ===
* This Generate Maps env.
*/

using UnityEngine;
using System.Collections.Generic;

public class MapGenerator : MonoBehaviour {
    public GameObject personPrefab, roadPrefab, buildingPrefab, warehousePrefab, spawnMarkerPrefab, garageDoorPrefab, loadingSpotPrefab;

    string[] mapLayout = new string[] {
        "..BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB",
        "...........................................B",
        "RRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRR.B",
        "R...R........................R...........R.B",
        "R...R.BBBBBBBBBBBBBBBB.......W..BBBBBBBB.R.B",
        "R...R.BBBBBBBBBBBBBBBB.......R..BBBBBBBB.R.B",
        "R...R.BBBBBBBBBBBBBBBB.RRRRRRR..BBBBBBBB.R.B",
        "RRWRR.BBBBBBBBBBBBBBBB.R........BBBBBBBB.R.B",
        "R...R.BBBBBBBBBBBBBBBB.R.BBBBBBBBBBBBBBB.R.B",
        "R.B.R.BBBBBBBBBBBBBBBB.R.BBBBBBBBBBBBBBB.R.B",
        "R...R..................R.BBBBBBBBBBBBBBB.R.B",
        "RRRRRRRRRRRRRRRRR......R...........BBBBB.R.B",
        "R...............RRRRRRRRRRRRRRRRRR.BBBBB.R.B",
        "R.BBBBBBBBBBBBB.R..R......R......R.BBBBB.R.B",
        "R.BBBBBBBBBBBBB.R..W..BB..W..BB..W.BBBBB.R.B",
        "R.BBBBBBBBBBBBB.R..R......R......R....BB.R.B",
        "R.BBBBBBBBBBBBB.RRRRRRRRRRRRRRRRRRRRRR.B.R.B",
        "RRRRRRRRRRRRRRRRR............R.BBBBB.R.B.R.B",
        "R.BBBBBBRBBBBBB.RTBBBBBBBBBB.R.BBBBB.R.B.R.B",
        "R.BBBBBBRBBBBBB.R.BBBBBBBBBB.R.BBBBB.R.B.R.B",
        "R.BBBBBBRBBBBBB.R.BBBBBBBBBB.R.BBBBB.R.B.R.B",
        "R.......R.BBBBB.R.BBBBBBBBBB.R.B.....R.B.R.B",
        "RRRRRRRRR.BBBBB.R.BBBBBBBBBB.RPB.....R...R.B",
        "....R.B.R.BBBBB.R............R.B.WRRRRRWRR.B",
        ".WRRR.B.R.BBBBB.RRRRRRRRR....R.B.R...R.....B",
        "....R.B.R.BBBBB.R.......RRRWRR.B.RRRRR......",
        ".BB.R.B.R.......R.BBBBB.R....R...R..........",
        ".BB.R.B.RRRRRRRRR.BBBBB.R.BB.RRRRRRRRRBBBBBB",
        ".BB.R...R.......R.BBBBB.R.BB.R...R.BBRBBBBBB",
        ".BB.RRWRR.BBBBB.W.BBBBB.R.BB.RRWRR.BBRBBBBBB",
        ".BB.R...R.......R.......R....R...R.BBRBBBBBB",
        ".BB.RRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRBBBBBB",
        ".BB.......R...R...R...R...R........BBBBBBBBB",
        ".BBBBBB...R...R...R...R...R..BBBBBBBBBBBBBBB",
        ".BBBBBB..MR..MR..MR..MR..MR..BBBBBBBBBBBBBBB",
        ".BBBBBB..MS..MS..MS..MS..MS..BBBBBBBBBBBBBBB",
        ".........MM..MM..MM..MM..MM..BBBBBBBBBBBBBBB",
    };

    public Vector2Int mapSize;
    public List<Vector3> spawnPoints = new List<Vector3>();
    public List<Transform> warehouseTargets = new List<Transform>();

    public void GenerateMap() {
        spawnPoints.Clear();
        warehouseTargets.Clear();
        _GenerateMap();
        mapSize = new Vector2Int(mapLayout[0].Length, mapLayout.Length);
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
