from starlette.applications import Starlette
from starlette.staticfiles import StaticFiles
from starlette.requests import Request

from starlette.responses import JSONResponse

from .AUGV.controller import AGENT_FRAMES
from .AUGV.obstacle import AGENT_PROCS, AGENT_QUEUES
import os
from .tools.decorator import endroute, ROUTES, render_layout
import threading, psutil, time 
import socket, json

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

@endroute("/monitor", methods=["GET"])
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

@endroute("/", methods=["GET"])
async def home(req: Request):
    with open("webapp/static/xml/page_home.xml", "r", encoding="utf-8") as f:
        content = f.read()
    return render_layout("Yolo Home", content)

@endroute("/map", methods=["GET"])
async def map(req: Request):
    with open("webapp/static/xml/page_map.xml", "r", encoding="utf-8") as f:
        content = f.read()
    return render_layout("Yolo Map", content)

@endroute("/client", methods=["GET"])
async def client_frontend(req: Request):
    with open("webapp/static/xml/page_client.xml", "r", encoding="utf-8") as f:
        content = f.read()
    return render_layout("Client Route Editor", content)

@endroute("/send-routes", methods=["POST"])
async def send_routes(req: Request):
    body = await req.json()
    try:
        s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        s.connect(("localhost", 8051))
        s.send(json.dumps(body).encode())
        s.close()
        return JSONResponse({"status": "ok"})
    except Exception as e:
        return JSONResponse({"status": "error", "error": str(e)}, status_code=500)

async def not_found(req: Request, exc):
    with open("webapp/static/xml/page_404.xml", "r", encoding="utf-8") as f:
        content = f.read()
    return render_layout("Yolo 404", content)

async def on_shutdown():
    """ This function is called when the server is shutting down, to make sure all processes and queues are closed. """
    print("Shutting down all processes and queues...")
    for agent_id, proc in list(AGENT_PROCS.items()):
        q = AGENT_QUEUES.get(agent_id)
        if proc and q:
            try:
                q.put(None)
                proc.join(timeout=5)
            except Exception as e:
                print(f"Error shutting down agent {agent_id}: {e}")

app = Starlette(routes=ROUTES, debug=False)
app.add_exception_handler(404, not_found)
app.mount("/static", StaticFiles(directory=STATIC_DIR, html=True), name="static")

application = app