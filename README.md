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

## 🚀 First-Time Installation

### 1. Clone the Repository

```sh
# Open terminal and cd to the folder where you want the project
cd /path/to/your/projects # Make sure that you cd to your unity project parent first.
```

**!Important!**
rename your project unity into 'MAPF'.
otherwise you need to copy&paste the /Backend and /Assets folder to your project.

or simply create a new unity project and name it to 'MAPF'

**Make sure that before cloning this repository to 'cd' to your path project first, to make sure that unity & backend is integrated completly without any future error.**

```sh
git clone https://github.com/Yancovenant/MAPF.git
```

---

### 2. Unity Setup

- Open the `MAPF` folder in Unity Hub.
- Let Unity import and compile all assets.

---

### 3. Python Backend Setup

> **Backend is inside the `Backend/` folder.**

#### a. Install backend dependencies

```sh
cd Backend

# Install backend
pip install --upgrade pip
pip install -r requirements.txt

# or simpy do

pip install -e .
```

---

### 4. Running the Backend Server

```sh
# From the Backend folder (venv activated)
python -m webapp
# or
uvicorn webapp.ASGI:app --host 0.0.0.0 --port 8080 --workers 2
```
- The backend will start on [http://localhost:8080](http://localhost:8080)
- Unity will connect to the backend automatically.

---

### 5. Unity Build/Run

- Press **Play** in the Unity Editor to start the simulation.

---

## 🧩 Dependencies

### Unity
- [NativeWebSocket](https://github.com/endel/NativeWebSocket) (for agent-backend communication)
- No extra Unity packages required (all scripts included)

#### How to Install ?
- Window -> Assets/Package Management
- [Top Left Corner] (+) plus icon, add from git url
```sh
https://github.com/endel/NativeWebSocket.git#upm
```

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

- **Multi-agent A* pathfinding** with conflict resolution
- **YOLO obstacle detection** (Python backend)
- **Map editor** (web frontend, Konva.js)
- **Live agent monitoring** (web dashboard)
- **Dynamic map loading** (Unity + backend)
- **WebSocket communication** (Unity <-> Python)
- **Warehouse/road placement rules** (map editor)
- **Local storage for map drafts**
- **Client input AUGV's routes**

---

## ⚡ Performance & Code Design

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

## 🛠️ Useful Commands

```sh
# Start backend server
cd Backend
python -m webapp

# Install backend dependencies
pip install -r requirements.txt
# Or
pip install -e .

# Activate Unity project
# (Open MAPF folder in Unity Hub)
```

---

## 📝 Notes

- **Backend is NOT included in Unity builds.**  
  Deploy/copy the `Backend/` folder separately if needed.
- **For development:**  
  Always run both Unity and the backend server.
- **For map editing:**  
  Use the web editor at [http://localhost:8080](http://localhost:8080).

---

## 💬 Need Help?

- Check the code comments for explanations.
- For issues, open a GitHub issue or ask in the project discussions.

---

**Happy coding! 🚗🤖**
