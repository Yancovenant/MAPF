###
### webapp/AUGV/controller.py
###

"""
This is the main controller for our webapp AUGV
it will handle all the websocket and post requests
"""

from webapp.tools.decorator import endroute
from webapp.AUGV.obstacle import AGENT_QUEUES, AGENT_STATE, AGENT_OUT_QUEUES, AGENT_PROCS, create_agent, GLOBAL_AGENT
from webapp.tools.config import CONFIG

import os, cv2, numpy as np, asyncio, json, socket, shutil
from pathlib import Path

from starlette.websockets import WebSocketDisconnect, WebSocket
from starlette.responses import JSONResponse
from starlette.requests import Request

MONITOR_CLIENTS = set()
AGENT_FRAMES = {}

@endroute("/ws/augv/{agent_id}", type="ws")
async def augv_ws(ws: WebSocket):
    agent_id = ws.path_params["agent_id"]
    await ws.accept()
    
    if agent_id not in AGENT_QUEUES:
        agent = create_agent(agent_id)
        agent.start()

    async def _dispatch():
        while True:
            try:
                msg = await AGENT_OUT_QUEUES[agent_id].get()
                await ws.send_json(msg)
            except asyncio.CancelledError:
                print(f"[Controller] Agent {agent_id} asyncio cancelled")
                break
            except WebSocketDisconnect:
                print(f"[Controller] Agent {agent_id} disconnected")
                break
            except Exception as e:
                print(f"[Controller] Error sending message to agent {agent_id}: {e}")
                break 

    send_msg = asyncio.create_task(_dispatch())

    try:
        while True:
            raw = await ws.receive_bytes()
            header, data = raw.split(b"\n", 1)
            params = json.loads(header.decode('utf-8'))
            useYolo = params.get("useYolo", False)
            if useYolo:
                xx = GLOBAL_AGENT[agent_id]
                xx.use_yolo = True
            else:
                xx = GLOBAL_AGENT[agent_id]
                xx.use_yolo = False

            AGENT_FRAMES[agent_id] = data

            try:
                frame = cv2.imdecode(np.frombuffer(data, dtype=np.uint8), cv2.IMREAD_COLOR)
                
                if frame is not None and not AGENT_QUEUES[agent_id].full():
                    AGENT_QUEUES[agent_id].put_nowait(frame)
                
            except Exception as e:
                print(f"Error processing frame for agent {agent_id}: {e}")
        
            if MONITOR_CLIENTS:
                header = json.dumps({
                    "agent_id": agent_id,
                    "detections": AGENT_STATE.get(agent_id, {}).get("detections", [])
                }).encode() + b"\n"
                payload = header + data

                send_task = [client.send_bytes(payload) for client in list(MONITOR_CLIENTS)]
                results = await asyncio.gather(*send_task, return_exceptions=True)

                for client, result in zip(list(MONITOR_CLIENTS), results):
                    if isinstance(result, Exception):
                        print(f"Error sending to monitor client: {result}")
                        MONITOR_CLIENTS.discard(client)

    except WebSocketDisconnect:
        print(f"[Controller] Agent {agent_id} disconnected")
    except Exception as e:
        print(f"[Controller] Error in agent {agent_id} websocket: {e}")
    finally:
        print(f"[Controller] Agent {agent_id} websocket closed")
        await _cleanup(agent_id)
        send_msg.cancel()

@endroute("/ws/monitor", type="ws") 
async def monitor_ws(ws: WebSocket):
    await ws.accept()
    MONITOR_CLIENTS.add(ws)

    try:
        for agent_id, frame in AGENT_FRAMES.items():
            try:
                header = json.dumps({
                    "agent_id": agent_id
                }).encode() + b"\n"
                await ws.send_bytes(header + frame)
            except WebSocketDisconnect:
                print(f"Monitor client disconnected")
                break
            except Exception as e:
                print(f"Error sending frame to monitor client: {e}")
            
            while True:
                await asyncio.sleep(10)
            
    except WebSocketDisconnect:
        print("Monitor client disconnected")
    except Exception as e:
        print(f"Error in monitor websocket: {e}")
    finally:
        print("Monitor client websocket closed")
        MONITOR_CLIENTS.discard(ws)

# Controller json
MAPS_DIR = os.path.join(os.path.dirname(__file__), "maps_json")
os.makedirs(MAPS_DIR, exist_ok=True)

@endroute("/maps", type="http", methods=["GET"])
async def get_maps(req: Request):
    map_list = [f[:-5] for f in os.listdir(MAPS_DIR) if f.endswith(".json")]
    return JSONResponse(map_list)

@endroute("/maps/{map_name}", type="http", methods=["GET"])
async def get_map(req: Request):
    map_name = req.path_params["map_name"]
    file_path = os.path.join(MAPS_DIR, f"{map_name}.json")
    with open(file_path, "r") as f:
        return JSONResponse(json.load(f))

@endroute("/maps/delete", type="http", methods=["POST"])
async def delete_map(req: Request):
    body = await req.json()
    map_name = body.get("name")
    file_path = os.path.join(MAPS_DIR, f"{map_name}.json")
    print(map_name)
    if map_name == "default":
        return JSONResponse({"status": "error", "error": "Default map cannot be deleted"}, status_code=400)
    if os.path.exists(file_path):
        os.remove(file_path)
        return JSONResponse({"status": "ok"})
    else:
        return JSONResponse({"status": "error", "error": "Map not found"}, status_code=404)

@endroute("/maps/save", type="http", methods=["POST"])
async def save_map(req: Request):
    data = await req.json()
    map_name = data.get("name")
    map_data = data.get("layout")
    file_path = os.path.join(MAPS_DIR, f"{map_name}.json")
    
    if not map_data or not isinstance(map_data, list):
        return JSONResponse({"status": "error", "error": "Invalid map data"}, status_code=400)
    if map_name == "default":
        return JSONResponse({"status": "error", "error": "Default map cannot be saved"}, status_code=400)

    with open(file_path, "w") as f:
        json.dump({"name": map_name, "layout": map_data}, f)

    copy_map_json_to_unity()
    return JSONResponse({"status": "ok"})



@endroute("/send-routes", type="http", methods=["POST"])
async def send_routes(req: Request):
    body = await req.json()
    try:
        unity_port = CONFIG['UNITY_PORT']
        s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        s.connect(("localhost", unity_port))
        s.send(json.dumps(body).encode())
        s.close()
        return JSONResponse({"status": "ok"})
    except Exception as e:
        return JSONResponse({"status": "error", "error": str(e)}, status_code=500)

def copy_map_json_to_unity():
    src_dir = Path(__file__).parent / 'maps_json'
    unity_maps_dir = Path(__file__).parent.parent.parent.parent / 'Assets' / 'Maps'
    if not src_dir.exists():
        print(f"[MapCopy] Source directory does not exist: {src_dir}")
        return
    unity_maps_dir.mkdir(parents=True, exist_ok=True)
    for json_file in src_dir.glob('*.json'):
        dest = unity_maps_dir / json_file.name
        shutil.copy2(json_file, dest)
        print(f"[MapCopy] Copied {json_file} -> {dest}")
    
async def _cleanup(agent_id):
    """ Cleanup for agent disconnection """
    try:
        AGENT_FRAMES.pop(agent_id, None)
        AGENT_OUT_QUEUES.pop(agent_id, None)
        AGENT_QUEUES.pop(agent_id, None)
        AGENT_STATE.pop(agent_id, None)
        GLOBAL_AGENT.pop(agent_id, None)

        if CONFIG['INFERENCE_METHOD'] == 'multiprocessing':
            proc = AGENT_PROCS.pop(agent_id, None)
            if proc and proc.is_alive():
                try:
                    proc.terminate()
                    proc.join(timeout=5)
                    if proc.is_alive():
                        proc.kill()
                except Exception as e:
                    print(f"[Controller] Error shutting down process {agent_id}: {e}")

        dead_clients = []
        for client in MONITOR_CLIENTS:
            try:
                if client.state.value == 3:
                    dead_clients.append(client)
            except:
                dead_clients.append(client)
        
        for client in dead_clients:
            MONITOR_CLIENTS.discard(client)
        
        print(f"[Controller] {len(dead_clients)} monitor clients disconnected")
        
    except Exception as e:
        print(f"[Controller] Error cleaning up agent {agent_id}: {e}")