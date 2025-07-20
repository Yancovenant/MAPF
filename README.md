# MAPF: Multi-Agent Pathfinding System

> **Unity + Python Backend**  
> Obstacle detection, pathfinding, map editing, and agent monitoring.

---

## 📁 Project Structure

```
MAPF/ <-- Unity Project Parent Path
├── Assets/         # Unity project assets (scenes, scripts, prefabs, etc)
├── Backend/        # Python backend (YOLO, WebSocket, API, map management)
├── ProjectSettings/
├── Packages/
└── README.md
```

---

## 🚀 Quick Start

### Option 1: Import Unity Package (Recommended)

1. **Download** `MAPF.unitypackage` from this repository
2. **Create** a new Unity project named `MAPF`
3. **Import** the package: `Assets > Import Package > Custom Package`
4. **Install NativeWebSocket** (see [Dependencies](#-dependencies) below)
5. **Configure** the backend settings in `Scene/MainScene > EnvStart/GlobalProperties`

### Option 2: Clone Repository

1. **Git Clone**
```sh
# Navigate to your Unity projects folder
cd /path/to/your/unity/projects

# Clone the repository
git clone https://github.com/Yancovenant/MAPF.git
```
2. **Copy and Paste** `/MAPF/Backend` and `/MAPF/Assets/` back to your unity projects folder.

**Important:** Name your Unity project `MAPF` for seamless integration using git clone.

---

## 🔧 Setup

### 1. Install Dependencies

#### Unity Dependencies
- **NativeWebSocket** (required for AUGV-backend communication)
  - Open `Window > Package Manager`
  - Click the **+** button > "Add package from git URL"
  - Enter: `https://github.com/endel/NativeWebSocket.git#upm`
 
#### Python Backend Dependencies
```sh
cd Backend

# Option 1:
pip install --upgrade pip
pip install -r requirements.txt
# Option 2:
pip install -e .
```

### 2. Start the Backend Server

```sh
cd Backend

# Option 1:
uvicorn webapp.ASGI:app --host 0.0.0.0 --port 8080 --workers 2
# Option 2:
python -m webapp
```

The server will start at [http://localhost:8080](http://localhost/8080)

### 3. Configure Backend Settings

On first run, the backend automatically tests your system and recommends optimal settings:
- **FPS** for your device
- **YOLO model** (ONNX or PT)
- **Inference method** (threading or multiprocessing)
- **Maximum agents** for YOLO detection

Copy these settings to Unity: `Scene/MainScene > EnvStart/GlobalProperties`

### 4. Run Unity

- Press **Play** in Unity Editor
- Configure YOLO agents in `EnvStart/GlobalProperties > Agent Yolo Config`
- **Note:** More YOLO agents = higher resource usage

---

## 🌐 Web Dashboard

Access these tools at [http://localhost:8080](http://localhost/8080):

- **`/monitor`** - Live agent monitoring and video feeds
  <img width="960" height="498.5" alt="image" src="https://github.com/user-attachments/assets/18a23d17-e5ef-417a-b2c5-dc565052b536" />
  <img width="250.5" height="492" alt="image" src="https://github.com/user-attachments/assets/79c6eb53-ad7b-47cb-b016-b13dccf52d44" />

- **`/map`** - Visual map editor
  <img width="960" height="498.5" alt="image" src="https://github.com/user-attachments/assets/18f12278-224d-4fc4-b242-12a9babd5a8e" />
  <img width="250.5" height="492" alt="image" src="https://github.com/user-attachments/assets/f621d67c-7ab8-491a-97a3-52b5fcb1db2c" />
  
- **`/client`** - Route planning interface
  <img width="960" height="498.5" alt="image" src="https://github.com/user-attachments/assets/78092d2e-591e-4bac-b984-e517b278945e" />
  <img width="250.5" height="492" alt="image" src="https://github.com/user-attachments/assets/19f1ff89-fafc-4d67-af13-e7d8f3e4f4ec" />

---

## 🧩 Dependencies

### Unity
- [NativeWebSocket](https://github.com/endel/NativeWebSocket) package (for agent-backend communication)
- No extra Unity packages required (all scripts included)

### Python Backend
- Python 3.10+
- See `Backend/requirements.txt` for all packages:
  - `ultralytics` (YOLO)
  - `opencv-python`
  - `starlette`, `uvicorn`
  - `websockets`, `wsproto`
  - `numba`, `psutil`
  - and more...

---

## 🗺️ Features

- **Smart Multi-agent AStar pathfinding** - A* algorithm with conflict resolution
- **Real-Time obstacle detection** - YOLO-powered object recognition (Python backend)
- **Visual Map editor** - Drag-and-drop interface (web frontend, Konva.js)
- **Live agent monitoring** - Real-time agent tracking (web dashboard)
- **Dynamic map loading** - Hot-swappable map configurations (Unity + backend)
- **WebSocket communication** - Seamless (Unity <> Python) Integration
- **Warehouse/road placement rules** - Intelligent building constraints (map editor)
- **Local storage for map drafts** - Auto-save map drafts
- **Route Planning input AUGV's routes** - Path assignment for agents

---

## ⚡ Performance Optimizations & Code Design

- **Async & Coroutines:**
  - Unity uses coroutines for agent movement and camera capture to avoid blocking the main thread.
  - Backend uses async WebSocket and Starlette for high concurrency.
- **Object Pooling:**
  - Reduces memory allocations and garbage collection in Unity.
- **Lockstep Simulation:**
  - Ensures all agents move in sync, preventing deadlocks and race conditions.
- **Efficient Pathfinding:**
  - Custom A* implementation, optimized for grid and node reuse.
- **Minimized Memory Leaks:**
  - Careful cleanup of textures, threads, and sockets in Unity and backend.
- **Batch Processing:**
  - Backend processes frames and detections in batches for better throughput.
- **Numba JIT:**
  - Backend uses Numba to speed up critical math functions.
- **Onstart Config:**
  - Backend for the very first time, will run the test itself to recommend:
    - The best FPS for your current device;
    - The best YOLO model (onnx, pt);
    - The best inference method (threading, multi-processing);
    - The best maximum number of AUGV that will use YOLO.

---

## 🚀 Technical Highlights

### Multi-Agent Pathfinding Engine
- **Centralized Coordination** - Supervisor system manages all agent movements
- **Conflict Resolution** - Advance algorithms prevent collision and deadlocks
- **Lockstep Movement** - All agents move in perfect synchronization
- **Scalable Architecture** - Handles multiple agents efficiently

### Obstacle Detection System
- **Universal Compatibility** - Works on CPU/GPU with ONNX/PyTorch models
- **20x Performance Boost** - Numba JIT compilation
- **Real-Time Integration** - Live feeds to both Unity and web dashboard
- **Flexible Configuration** - Adapts to your hardware capabilities

---

## 🛠️ Useful Commands

```sh
# Start backend server
cd Backend && python -m webapp

# Install backend dependencies
pip install -r requirements.txt
# Or
pip install -e .

# Activate Unity project
# (Open MAPF folder in Unity Hub)
```

---

## 📝 Important Notes

- **Backend is NOT included in Unity builds.**  
  Deploy/copy the `Backend/` folder separately if needed.
- **Resource Usage** - YOLO detection is resource-intensive. Configure based on your hardware.
- **Development Mode** - Always run both Unity and backend for full functionality.

---

## 💬 Need Help?

- Check the code comments for explanations.
- For issues, open a GitHub issue or ask in the project discussions.

---

**Ready to build the future of autonomous navigation! 🚗🤖** 
**Happy coding! 🚗🤖**
