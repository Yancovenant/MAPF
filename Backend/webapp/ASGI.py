from starlette.applications import Starlette
from starlette.staticfiles import StaticFiles
from starlette.requests import Request

from .AUGV.controller import AGENT_FRAMES
from .AUGV.obstacle import AGENT_PROCS, AGENT_QUEUES, AGENT_OUT_QUEUES, AGENT_STATE, GLOBAL_AGENT
import os
from .tools.decorator import endroute, ROUTES, render_layout
import threading, psutil, time
import queue

STATIC_DIR = os.path.join(os.path.dirname(__file__), "static")

def log_resource_usage():
    import shutil
    try:
        import GPUtil
    except ImportError:
        GPUtil = None
    while True:
        cpu = psutil.cpu_percent(interval=1)
        mem = psutil.virtual_memory()
        disk = psutil.disk_usage('/')
        net = psutil.net_io_counters()
        log = f"[Resource] CPU: {cpu}% | Mem: {mem.percent}% ({mem.used//(1024**2)}MB/{mem.total//(1024**2)}MB) | Disk: {disk.percent}% | Net: sent {net.bytes_sent//(1024**2)}MB recv {net.bytes_recv//(1024**2)}MB"
        if GPUtil:
            gpus = GPUtil.getGPUs()
            for gpu in gpus:
                log += f" | GPU {gpu.id}: {gpu.load*100:.1f}% {gpu.memoryUsed}MB/{gpu.memoryTotal}MB"
        print(log)
        time.sleep(10)

threading.Thread(target=log_resource_usage, daemon=True).start()

@endroute("/monitor", type="http", methods=["GET"])
async def monitor_frontend(req: Request):
    EXPECTED_AGENTS = [f"AUGV_{i}" for i in range(1, 6)]
    agents = list(set(AGENT_FRAMES.keys()) | set(EXPECTED_AGENTS))
    with open("webapp/static/xml/page_monitor.xml", "r", encoding="utf-8") as f:
        base_template = f.read()
    agents_monitor = ""
    for agent in agents:
        agents_monitor += f'''
        <div class="col-6 col-md-4 col-lg-3">
            <div class="agent" id="agent_{agent}">
                <div class="agent-name">{agent}</div>
                <canvas id="canvas_{agent}" width="640" height="480"></canvas>
            </div>
        </div>
        '''
    content = base_template.replace("<t t-agents/>", agents_monitor)
    return render_layout("Yolo Monitor", content)

@endroute("/", type="http", methods=["GET"])
async def home(req: Request):
    with open("webapp/static/xml/page_home.xml", "r", encoding="utf-8") as f:
        content = f.read()
    return render_layout("Yolo Home", content)

@endroute("/map", type="http", methods=["GET"])
async def map(req: Request):
    with open("webapp/static/xml/page_map.xml", "r", encoding="utf-8") as f:
        content = f.read()
    return render_layout("Yolo Map", content)

@endroute("/client", type="http", methods=["GET"])
async def client_frontend(req: Request):
    with open("webapp/static/xml/page_client.xml", "r", encoding="utf-8") as f:
        content = f.read()
    return render_layout("Client Route Editor", content)

async def not_found(req: Request, exc):
    with open("webapp/static/xml/page_404.xml", "r", encoding="utf-8") as f:
        content = f.read()
    return render_layout("Yolo 404", content)

async def on_shutdown():
    """ This function is called when the server is shutting down, to make sure all processes and queues are closed. """
    print("Shutting down all processes and queues...")
    for agent_id, proc in list(AGENT_PROCS.items()):
        if proc.is_alive():
            try:
                proc.stop() if hasattr(proc, 'stop') else proc.terminate()
                proc.join(timeout=5)
                if proc.is_alive():
                    proc.kill()
            except Exception as e:
                print(f"Error shutting down agent {agent_id}: {e}")

        for agent_id in list(GLOBAL_AGENT.keys()):
            _cleanup_model(agent_id)
        
        _cleanup_all_queues()

        AGENT_FRAMES.clear()
        AGENT_OUT_QUEUES.clear()
        AGENT_QUEUES.clear()
        AGENT_STATE.clear()
        GLOBAL_AGENT.clear()
        AGENT_PROCS.clear()

        print("All processes and queues cleaned up completely")

app = Starlette(routes=ROUTES, debug=True)
app.add_exception_handler(404, not_found)
app.add_event_handler("shutdown", on_shutdown)
app.mount("/static", StaticFiles(directory=STATIC_DIR, html=True), name="static")

application = app

def _cleanup_model(agent_id):
    """ Cleanup model resources for an agent """
    try:
        if agent_id in GLOBAL_AGENT:
            agent = GLOBAL_AGENT[agent_id]
            if hasattr(agent, 'stop'):
                agent.stop()
            if hasattr(agent, 'model'):
                del agent.model
            if hasattr(agent, 'ort_sess'):
                agent.ort_sess = None
            
            GLOBAL_AGENT.pop(agent_id, None)
        
        print(f"[ASGI] Model resources cleaned up for agent {agent_id}")
    except Exception as e:
        print(f"[ASGI] Error cleaning up model resources for agent {agent_id}: {e}")
    
def _cleanup_all_queues():
    for agent_id in list(AGENT_QUEUES.keys()):
        try:
            q = AGENT_QUEUES.get(agent_id)
            while not q.empty():
                try:
                    q.get_nowait()
                except queue.Empty:
                    break
            AGENT_QUEUES.pop(agent_id, None)
            
            if agent_id in AGENT_OUT_QUEUES:
                q = AGENT_OUT_QUEUES.get(agent_id)
                while q and not q.empty():
                    try:
                        q.get_nowait()
                    except queue.Empty:
                        break
                AGENT_OUT_QUEUES.pop(agent_id, None)

            print(f"[ASGI] Queues cleaned up for agent {agent_id}")
        
        except Exception as e:
            print(f"[ASGI] Error cleaning up queues for agent {agent_id}: {e}")
    