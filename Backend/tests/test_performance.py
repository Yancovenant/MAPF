import sys, os
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))

import pytest, asyncio, psutil, os, time
from starlette.testclient import TestClient
from webapp.ASGI import app
import threading
import numpy as np
from webapp.AUGV.obstacle import img_to_grid_offset
from numba import njit
import math
import multiprocessing
from multiprocessing import Process, Manager

CAMERA_CONFIG = {
    'height': 0.6,
    'forward': 0.31,
    'rot_x': 20,
    'fov': 90,
    'res_w': 640,
    'res_h': 480,
    'grid_size': 1,
    'grid_center': 0.5
}

# Helper to measure memory and CPU usage
class ResourceMonitor:
    def __init__(self, interval=0.1):
        self.process = psutil.Process(os.getpid())
        self.interval = interval
        self.mem_usage = []
        self.cpu_usage = []
        self.running = False
        self.thread = None
    def _monitor(self):
        while self.running:
            self.mem_usage.append(self.process.memory_info().rss)
            self.cpu_usage.append(self.process.cpu_percent(interval=None))
            time.sleep(self.interval)
    def start(self):
        self.running = True
        self.thread = threading.Thread(target=self._monitor)
        self.thread.start()
    def stop(self):
        self.running = False
        if self.thread:
            self.thread.join()
    def summary(self):
        return {
            'max_mem': max(self.mem_usage) if self.mem_usage else 0,
            'avg_cpu': sum(self.cpu_usage)/len(self.cpu_usage) if self.cpu_usage else 0
        }

@pytest.fixture(scope="module")
def client():
    with TestClient(app) as c:
        yield c

def test_http_endpoints(client):
    for url in ['/', '/monitor', '/map']:
        r = client.get(url)
        assert r.status_code == 200
        assert 'Yolo' in r.text

def test_yolo_inference():
    from webapp.AUGV.obstacle import AUGVYolo
    img = np.random.randint(0, 255, (480, 640, 3), dtype=np.uint8)
    yolo = AUGVYolo('test_agent')
    yolo.start()
    yolo.q.put(img)
    time.sleep(2)
    assert 'status' in yolo.AGENT_STATE['test_agent']
    yolo.q.queue.clear()

def test_resource_usage(client):
    monitor = ResourceMonitor()
    monitor.start()
    from webapp.AUGV.obstacle import AUGVYolo
    agents = [AUGVYolo(f'AUGV_{i}') for i in range(5)]
    for a in agents: a.start()
    img = np.random.randint(0, 255, (480, 640, 3), dtype=np.uint8)
    for _ in range(10):
        for a in agents:
            a.q.put(img)
        time.sleep(0.2)
    monitor.stop()
    summary = monitor.summary()
    print('Resource usage:', summary)
    assert summary['max_mem'] < 2*1024*1024*1024
    assert summary['avg_cpu'] < 900  # Allow up to 900% for 8+ core CPUs

# --- Performance comparison for img_to_grid_offset ---
@njit
def img_to_grid_offset_numba(x_img, y_img, img_w, img_h, h):
    # Inline all constants for Numba compatibility
    height = 0.6
    forward = 0.31
    rot_x = 20
    fov = 90
    grid_size = 1
    grid_center = 0.5

    x_ndc = (x_img / img_w - 0.5) * 2
    y_ndc = (y_img / img_h - 0.5) * 2
    fov_rad = math.radians(fov)
    aspect_ratio = img_w / img_h
    tan_fov = math.tan(fov_rad / 2)
    x_cam = x_ndc * tan_fov * aspect_ratio
    y_cam = -y_ndc * tan_fov
    z_cam = 1

    rot_x_rad = math.radians(rot_x)
    y_rot = y_cam * math.cos(rot_x_rad) - z_cam * math.sin(rot_x_rad)
    z_rot = y_cam * math.sin(rot_x_rad) + z_cam * math.cos(rot_x_rad)
    cam_y = height
    cam_z = grid_center + forward
    t = -cam_y / z_rot if y_rot != 0 else 0
    world_x = x_cam * t
    world_z = cam_z + z_rot * t
    distance = math.sqrt(world_x**2 + world_z**2)
    if distance <= 2.0:
        bias = 0.2
    elif distance <= 4.0:
        bias = 0.5
    elif distance <= 6.0:
        bias = 0.8
    else:
        bias = 1.2
    dy = int(round((world_z + bias) / grid_size))
    dx = int(round(world_x / grid_size))
    return dx, dy

def test_img_to_grid_offset_perf():
    # Generate random test data
    xs = np.random.uniform(0, 640, 10000)
    ys = np.random.uniform(0, 480, 10000)
    ws = np.full(10000, 640)
    hs = np.full(10000, 480)
    hs2 = np.random.uniform(10, 100, 10000)
    # Warmup Numba
    img_to_grid_offset_numba(xs[0], ys[0], ws[0], hs[0], hs2[0])
    # Warmup original
    #img_to_grid_offset(xs[0], ys[0], ws[0], hs[0], hs2[0])
    # Time original
    t0 = time.time()
    for i in range(10000):
        img_to_grid_offset(xs[i], ys[i], ws[i], hs[i], hs2[i])
    t1 = time.time()
    # Time Numba
    t2 = time.time()
    for i in range(10000):
        img_to_grid_offset_numba(xs[i], ys[i], ws[i], hs[i], hs2[i])
    t3 = time.time()
    print(f"Original: {t1-t0:.4f}s, Numba: {t3-t2:.4f}s")
    assert (t3-t2) < (t1-t0)  # Numba should be faster 

def test_img_to_grid_offset_output_equivalence():
    xs = np.random.uniform(0, 640, 1000)
    ys = np.random.uniform(0, 480, 1000)
    ws = np.full(1000, 640)
    hs = np.full(1000, 480)
    hs2 = np.random.uniform(10, 100, 1000)
    for i in range(1000):
        orig = img_to_grid_offset(xs[i], ys[i], ws[i], hs[i], hs2[i])
        numba = img_to_grid_offset_numba(xs[i], ys[i], ws[i], hs[i], hs2[i])
        assert orig == numba, f"Mismatch at {i}: {orig} != {numba}"

def test_img_to_grid_offset_original_benchmark(benchmark):
    xs = np.random.uniform(0, 640, 10000)
    ys = np.random.uniform(0, 480, 10000)
    ws = np.full(10000, 640)
    hs = np.full(10000, 480)
    hs2 = np.random.uniform(10, 100, 10000)
    def original():
        for i in range(10000):
            img_to_grid_offset(xs[i], ys[i], ws[i], hs[i], hs2[i])
    benchmark(original)

def test_img_to_grid_offset_numba_benchmark(benchmark):
    xs = np.random.uniform(0, 640, 10000)
    ys = np.random.uniform(0, 480, 10000)
    ws = np.full(10000, 640)
    hs = np.full(10000, 480)
    hs2 = np.random.uniform(10, 100, 10000)
    # Warmup Numba
    img_to_grid_offset_numba(xs[0], ys[0], ws[0], hs[0], hs2[0])
    def numba():
        for i in range(10000):
            img_to_grid_offset_numba(xs[i], ys[i], ws[i], hs[i], hs2[i])
    benchmark(numba) 

def test_yolo_inference_benchmark(benchmark):
    from webapp.AUGV.obstacle import YOLO
    import numpy as np
    import psutil, os, time
    # model = YOLO("yolo11n-seg.pt")
    model = YOLO("yolov8n.pt")
    img = np.random.randint(0, 255, (480, 640, 3), dtype=np.uint8)
    process = psutil.Process(os.getpid())
    cpu_before = process.cpu_percent(interval=None)
    mem_before = process.memory_info().rss
    def run_inference():
        model.predict(img, conf=0.5, verbose=False, stream=True)
    t0 = time.time()
    benchmark(run_inference)
    t1 = time.time()
    cpu_after = process.cpu_percent(interval=None)
    mem_after = process.memory_info().rss
    print(f"YOLO inference time: {t1-t0:.4f}s, CPU before: {cpu_before}%, CPU after: {cpu_after}%, Mem before: {mem_before/1e6:.2f}MB, Mem after: {mem_after/1e6:.2f}MB") 

def get_yolo_model(model_name):
    from ultralytics import YOLO
    import torch
    device = 'cuda' if torch.cuda.is_available() else 'cpu'
    model = YOLO(model_name)
    model.to(device)
    return model

def test_yolo_v8_inference_benchmark(benchmark):
    import numpy as np
    model = get_yolo_model("yolov8n.pt")
    img = np.random.randint(0, 255, (480, 640, 3), dtype=np.uint8)
    def run_inference():
        results = list(model.predict(img, conf=0.5, verbose=False, stream=True))[0]
        assert hasattr(results, 'boxes') and results.boxes is not None
    benchmark(run_inference)


def test_yolo_v11_inference_benchmark(benchmark):
    import numpy as np
    model = get_yolo_model("yolo11n-seg.pt")
    img = np.random.randint(0, 255, (480, 640, 3), dtype=np.uint8)
    def run_inference():
        results = list(model.predict(img, conf=0.5, verbose=False, stream=True))[0]
        assert hasattr(results, 'boxes') and results.boxes is not None
    benchmark(run_inference)

def test_yolo_inference_fps_load_v8_v11():
    import numpy as np, time
    import psutil
    for model_name in ["yolov8n.pt", "yolo11n-seg.pt"]:
        model = get_yolo_model(model_name)
        img = np.random.randint(0, 255, (480, 640, 3), dtype=np.uint8)
        process = psutil.Process()
        for target_fps in [5, 10, 20, 30, 45, 60]:
            processed = 0
            start = time.time()
            cpu_samples = []
            duration = 3  # seconds
            next_frame = start
            while time.time() - start < duration:
                now = time.time()
                if now >= next_frame:
                    model.predict(img, conf=0.5, verbose=False, stream=True)
                    processed += 1
                    cpu_samples.append(process.cpu_percent(interval=None))
                    next_frame += 1.0 / target_fps
                else:
                    time.sleep(max(0, next_frame - now))
            elapsed = time.time() - start
            actual_fps = processed / elapsed
            avg_cpu = sum(cpu_samples) / len(cpu_samples) if cpu_samples else 0
            print(f"Model: {model_name}, Target FPS: {target_fps}, Actual FPS: {actual_fps:.2f}, Avg CPU: {avg_cpu:.1f}%")
            assert actual_fps > 0

def test_yolo_multi_agent_threads():
    import numpy as np, time, threading
    import psutil
    model_names = ["yolov8n.pt", "yolo11n-seg.pt"]
    for model_name in model_names:
        results = []
        agent_stats = []
        def agent_thread(idx):
            from ultralytics import YOLO
            import torch
            model = YOLO(model_name)
            device = 'cuda' if torch.cuda.is_available() else 'cpu'
            model.to(device)
            processed = 0
            cpu_samples = []
            start = time.time()
            duration = 3
            next_frame = start
            target_fps = 10
            while time.time() - start < duration:
                now = time.time()
                if now >= next_frame:
                    img = np.random.randint(0, 255, (480, 640, 3), dtype=np.uint8)  # New random image each time
                    model.predict(img, conf=0.5, verbose=False, stream=True)
                    processed += 1
                    cpu_samples.append(psutil.Process().cpu_percent(interval=None))
                    next_frame += 1.0 / target_fps
                else:
                    time.sleep(max(0, next_frame - now))
            elapsed = time.time() - start
            fps = processed / elapsed
            avg_cpu = sum(cpu_samples) / len(cpu_samples) if cpu_samples else 0
            agent_stats.append((fps, avg_cpu, processed))
            results.append(processed)
        threads = [threading.Thread(target=agent_thread, args=(i,)) for i in range(5)]
        for t in threads: t.start()
        for t in threads: t.join()
        total_processed = sum(results)
        print(f"\n=== 🧵 Multi-Agent Threads Test for {model_name} ===")
        for i, (fps, avg_cpu, processed) in enumerate(agent_stats):
            print(f"Agent {i+1}: FPS: {fps:.2f}, Avg CPU: {avg_cpu:.1f}%, Images: {processed}")
        print(f"✅ Model: {model_name}, 5 agents, total processed: {total_processed}, per agent: {[r for r in results]}")
        assert total_processed > 0 


### AS THE ULTRALYTICS YOLO / PYTORCH, EVEN WITH USING GPU,
### IT IS NOT THREAD SAFE, SO IT WILL ALWAYS WAIT FOR THEIR TURNS.
### THUS MAKING THE FPS IS MUCH LOWER THAN THE SINGLE TEST UNIT.

def agent_process_mp(agent_id, model_name, duration, return_dict):
    from ultralytics import YOLO
    import numpy as np, time, psutil
    import torch
    model = YOLO(model_name)
    device = 'cuda' if torch.cuda.is_available() else 'cpu'
    model.to(device)
    process = psutil.Process()
    processed = 0
    cpu_samples = []
    start = time.time()
    next_frame = start
    target_fps = 10
    while time.time() - start < duration:
        now = time.time()
        if now >= next_frame:
            img = np.random.randint(0, 255, (480, 640, 3), dtype=np.uint8)  # New random image each time
            model.predict(img, conf=0.5, verbose=False, stream=True)
            processed += 1
            cpu_samples.append(process.cpu_percent(interval=None))
            next_frame += 1.0 / target_fps
        else:
            time.sleep(max(0, next_frame - now))
    elapsed = time.time() - start
    fps = processed / elapsed
    avg_cpu = sum(cpu_samples) / len(cpu_samples) if cpu_samples else 0
    return_dict[agent_id] = (fps, avg_cpu, processed)

def test_yolo_multi_agent_threads_vs_processes():
    import numpy as np, time, threading, psutil
    model_names = ["yolov8n.pt", "yolo11n-seg.pt"]
    duration = 3
    for model_name in model_names:
        # Threading test (PRODUCTION-SAFE: Each thread creates its own model)
        results = []
        agent_stats = []
        def agent_thread(idx):
            from ultralytics import YOLO
            import torch
            model = YOLO(model_name)
            device = 'cuda' if torch.cuda.is_available() else 'cpu'
            model.to(device)
            processed = 0
            cpu_samples = []
            start = time.time()
            next_frame = start
            target_fps = 10
            while time.time() - start < duration:
                now = time.time()
                if now >= next_frame:
                    img = np.random.randint(0, 255, (480, 640, 3), dtype=np.uint8)  # New random image each time
                    model.predict(img, conf=0.5, verbose=False, stream=True)
                    processed += 1
                    cpu_samples.append(psutil.Process().cpu_percent(interval=None))
                    next_frame += 1.0 / target_fps
                else:
                    time.sleep(max(0, next_frame - now))
            elapsed = time.time() - start
            fps = processed / elapsed
            avg_cpu = sum(cpu_samples) / len(cpu_samples) if cpu_samples else 0
            agent_stats.append((fps, avg_cpu, processed))
            results.append(processed)
        threads = [threading.Thread(target=agent_thread, args=(i,)) for i in range(5)]
        for t in threads: t.start()
        for t in threads: t.join()
        total_processed = sum(results)
        print(f"\n=== 🧵 Multi-Agent Threads Test for {model_name} ===")
        for i, (fps, avg_cpu, processed) in enumerate(agent_stats):
            print(f"Agent {i+1}: FPS: {fps:.2f}, Avg CPU: {avg_cpu:.1f}%, Images: {processed}")
        print(f"✅ Model: {model_name}, 5 agents, total processed: {total_processed}, per agent: {[r for r in results]}")
        assert total_processed > 0
        # Multiprocessing test (PRODUCTION-SAFE: Each process creates its own model)
        with Manager() as manager:
            return_dict = manager.dict()
            processes = [Process(target=agent_process_mp, args=(i, model_name, duration, return_dict)) for i in range(5)]
            for p in processes: p.start()
            for p in processes: p.join()
            mp_stats = [return_dict[i] for i in range(5)]
            total_mp_processed = sum(stat[2] for stat in mp_stats)
            print(f"\n=== 🧩 Multi-Agent Multiprocessing Test for {model_name} ===")
            for i, (fps, avg_cpu, processed) in enumerate(mp_stats):
                print(f"Agent {i+1}: FPS: {fps:.2f}, Avg CPU: {avg_cpu:.1f}%, Images: {processed}")
            print(f"✅ Model: {model_name}, 5 agents, total processed: {total_mp_processed}, per agent: {[stat[2] for stat in mp_stats]}")
            assert total_mp_processed > 0 

