###
### webapp/AUGV/controller.py
###

from webapp.tools.decorator import endroute
from webapp.AUGV.obstacle import AUGVYolo, AUGVYoloMP, AGENT_QUEUES, AGENT_STATE, AGENT_OUT_QUEUES

from webapp.tools.config import CONFIG
from webapp.AUGV.obstacle import AGENT_PROCS, create_agent, GLOBAL_AGENT

import cv2, numpy as np, asyncio, json

from starlette.websockets import WebSocketDisconnect, WebSocket

MONITOR_CLIENTS = set()
AGENT_FRAMES = {}

@endroute("/ws/augv/{agent_id}", con_type="ws")
async def augv_ws(ws: WebSocket):
    agent_id = ws.path_params["agent_id"]
    await ws.accept()
    
    if agent_id not in AGENT_QUEUES:
        agent = create_agent(agent_id)
        agent.start()

    async def _dispatch():
        while True:
            msg = await AGENT_OUT_QUEUES[agent_id].get()
            await ws.send_json(msg)

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
        print(f"Agent {agent_id} disconnected")
    except Exception as e:
        print(f"Error in agent {agent_id} websocket: {e}")
    finally:
        print(f"Agent {agent_id} websocket closed")
        AGENT_FRAMES.pop(agent_id, None)
        if CONFIG['INFERENCE_METHOD'] == 'multiprocessing':
            proc =AGENT_PROCS.pop(agent_id, None)
            q = AGENT_QUEUES.get(agent_id)
            if proc and q:
                try:
                    q.put(None)
                    proc.join(timeout=5)
                except Exception as e:
                    print(f"Error shutting down process {agent_id}: {e}")
        send_msg.cancel()

@endroute("/ws/monitor", con_type="ws") 
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
            