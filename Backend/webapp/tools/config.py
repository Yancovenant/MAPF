# webapp/tools/config.py

"""
This is the config module for our webapp AUGV
It will handle the config before the server runnning.
It will find the best recommendation setting for both server and unity for maximum performance.
It will also handle the camera config for the obstacle detection.

...

If .recommend_cache.json is not found, it will ask for user input to test the environment setup.
else it will use the cached setting and ask for user input to retest the environment setup.
"""

import torch
import os
from ultralytics import YOLO
import numpy as np, socket
import onnxruntime as ort
import json as _json
CACHE_PATH = os.path.join(os.path.dirname(__file__), '../../.recommend_cache.json')

CONFIG = {
    # Model selection
    'MODEL_NAME': 'yolov8n.pt',  # or 'yolo11n-seg.pt'
    # Inference method: 'threading' or 'multiprocessing'
    'INFERENCE_METHOD': 'threading',
    # Number of agents/processes/threads
    'NUM_AGENTS': 5,
    # Target FPS per agent
    'TARGET_FPS': 10,
    # YOLO confidence threshold
    'YOLO_CONF': 0.5,
    # Image size (width, height)
    'IMAGE_SIZE': (640, 480),
    # Device: 'cuda' or 'cpu' (auto-detect if None)
    'DEVICE': None,
    # Camera config for grid offset
    'CAMERA_CONFIG': {
        'height': 0.6,
        'forward': 0.31,
        'rot_x': 20,
        'fov': 90,
        'res_w': 640,
        'res_h': 480,
        'grid_size': 1,
        'grid_center': 0.5
    },
    # Number of images to process in benchmark
    'BENCHMARK_IMAGES': 60,

    # Server Port
    'SERVER_PORT': 8080,
    # Unity Port
    'UNITY_PORT': 8051
}

def get_onnx_session(model_path, backend_device='cpu'):
    if backend_device == 'cuda':
        providers = ['CUDAExecutionProvider', 'CPUExecutionProvider']
    else:
        providers = ['CPUExecutionProvider']
    return ort.InferenceSession(model_path, providers=providers)

def preprocess_onnx(img):
    img = img.astype(np.float32) / 255.0
    img = np.transpose(img, (2, 0, 1))  # HWC to CHW
    img = np.expand_dims(img, 0)  # Add batch dim
    return img

ONNX_IMG_SHAPE = (640, 640, 3)

# --- AGENT FUNCTIONS MUST BE TOP LEVEL FOR MULTIPROCESSING ---
def yolo_agent_thread(idx, stats_list, model_name, device, num_images, image_size, yolo_conf):
    import numpy as np, time, psutil
    from ultralytics import YOLO
    import torch
    model = YOLO(model_name)
    model.to(device)
    img = np.random.randint(0, 255, (image_size[1], image_size[0], 3), dtype=np.uint8)
    process = psutil.Process()
    cpu_samples = []
    start = time.time()
    for _ in range(num_images):
        list(model.predict(img, conf=yolo_conf, verbose=False, stream=True))[0]
        cpu_samples.append(process.cpu_percent(interval=None))
    elapsed = time.time() - start
    fps = num_images / elapsed if elapsed > 0 else 0
    avg_cpu = sum(cpu_samples) / len(cpu_samples) if cpu_samples else 0
    stats_list.append((fps, avg_cpu, num_images))

def yolo_agent_proc(idx, stats_list, model_name, device, num_images, image_size, yolo_conf):
    import numpy as np, time, psutil
    from ultralytics import YOLO
    import torch
    model = YOLO(model_name)
    model.to(device)
    img = np.random.randint(0, 255, (image_size[1], image_size[0], 3), dtype=np.uint8)
    process = psutil.Process()
    cpu_samples = []
    start = time.time()
    for _ in range(num_images):
        list(model.predict(img, conf=yolo_conf, verbose=False, stream=True))[0]
        cpu_samples.append(process.cpu_percent(interval=None))
    elapsed = time.time() - start
    fps = num_images / elapsed if elapsed > 0 else 0
    avg_cpu = sum(cpu_samples) / len(cpu_samples) if cpu_samples else 0
    stats_list.append((fps, avg_cpu, num_images))

def onnx_agent_thread(idx, stats_list, model_path, backend_device, num_images, image_size):
    import numpy as np, time, psutil
    ort_sess = get_onnx_session(model_path, backend_device)
    # Always use ONNX_IMG_SHAPE for ONNX
    img = np.random.randint(0, 255, ONNX_IMG_SHAPE, dtype=np.uint8)
    x = preprocess_onnx(img)
    input_name = ort_sess.get_inputs()[0].name
    process = psutil.Process()
    cpu_samples = []
    start = time.time()
    for _ in range(num_images):
        ort_sess.run(None, {input_name: x})
        cpu_samples.append(process.cpu_percent(interval=None))
    elapsed = time.time() - start
    fps = num_images / elapsed if elapsed > 0 else 0
    avg_cpu = sum(cpu_samples) / len(cpu_samples) if cpu_samples else 0
    stats_list.append((fps, avg_cpu, num_images))

def onnx_agent_proc(idx, stats_list, model_path, backend_device, num_images, image_size):
    import numpy as np, time, psutil
    ort_sess = get_onnx_session(model_path, backend_device)
    # Always use ONNX_IMG_SHAPE for ONNX
    img = np.random.randint(0, 255, ONNX_IMG_SHAPE, dtype=np.uint8)
    x = preprocess_onnx(img)
    input_name = ort_sess.get_inputs()[0].name
    process = psutil.Process()
    cpu_samples = []
    start = time.time()
    for _ in range(num_images):
        ort_sess.run(None, {input_name: x})
        cpu_samples.append(process.cpu_percent(interval=None))
    elapsed = time.time() - start
    fps = num_images / elapsed if elapsed > 0 else 0
    avg_cpu = sum(cpu_samples) / len(cpu_samples) if cpu_samples else 0
    stats_list.append((fps, avg_cpu, num_images))

# --- BENCHMARKING FUNCTIONS ---
def benchmark_yolo(model_name, device, num_agents, num_images, image_size, method, yolo_conf):
    import threading, multiprocessing
    stats = []
    if method == 'threading':
        threads = [threading.Thread(target=yolo_agent_thread, args=(i, stats, model_name, device, num_images, image_size, yolo_conf)) for i in range(num_agents)]
        for t in threads: t.start()
        for t in threads: t.join()
    elif method == 'multiprocessing':
        with multiprocessing.Manager() as manager:
            stats_list = manager.list()
            procs = [multiprocessing.Process(target=yolo_agent_proc, args=(i, stats_list, model_name, device, num_images, image_size, yolo_conf)) for i in range(num_agents)]
            for p in procs: p.start()
            for p in procs: p.join()
            stats.extend(stats_list)
    total_processed = sum(stat[2] for stat in stats)
    avg_fps = sum(stat[0] for stat in stats) / num_agents if stats else 0
    avg_cpu = sum(stat[1] for stat in stats) / num_agents if stats else 0
    return avg_fps, avg_cpu, total_processed

def benchmark_onnx(model_path, num_agents, num_images, image_size, method, backend_device):
    import threading, multiprocessing
    if image_size != (640, 640):
        print("[WARN] ONNX expects input shape (640, 640, 3). Overriding image_size for ONNX.")
    stats = []
    if method == 'threading':
        threads = [threading.Thread(target=onnx_agent_thread, args=(i, stats, model_path, backend_device, num_images, ONNX_IMG_SHAPE)) for i in range(num_agents)]
        for t in threads: t.start()
        for t in threads: t.join()
    elif method == 'multiprocessing':
        with multiprocessing.Manager() as manager:
            stats_list = manager.list()
            procs = [multiprocessing.Process(target=onnx_agent_proc, args=(i, stats_list, model_path, backend_device, num_images, ONNX_IMG_SHAPE)) for i in range(num_agents)]
            for p in procs: p.start()
            for p in procs: p.join()
            stats.extend(stats_list)
    total_processed = sum(stat[2] for stat in stats)
    avg_fps = sum(stat[0] for stat in stats) / num_agents if stats else 0
    avg_cpu = sum(stat[1] for stat in stats) / num_agents if stats else 0
    return avg_fps, avg_cpu, total_processed

# --- RECOMMENDATION LOGIC ---
def recommend_settings():
    import threading, multiprocessing
    # Try to load cache
    cache = None
    if os.path.exists(CACHE_PATH):
        try:
            with open(CACHE_PATH, 'r') as f:
                cache = _json.load(f)
        except Exception:
            cache = None
    if cache:
        print("\n[Cache] Found previous recommended settings:")
        for k, v in cache.items():
            print(f"{k}: {v}")
        ans = input("Do you want to retest env setup? (yes/no): ").strip().lower()
        if ans == 'no' or ans != 'yes':
            CONFIG.update(cache)
            print("\n>>> Using cached recommended settings:")
            for k, v in cache.items():
                print(f"{k}: {v}")
            print("===============================================\n")
            return
    device = 'cuda' if torch.cuda.is_available() else 'cpu'
    CONFIG['DEVICE'] = device
    model_name = CONFIG['MODEL_NAME']
    onnx_model_path = os.path.abspath(os.path.join(os.path.dirname(__file__), '../../yolov8n.onnx'))
    num_images = CONFIG.get('BENCHMARK_IMAGES', 30)
    image_size = CONFIG['IMAGE_SIZE']
    agent_range = [1, 2, 3, 4, 5]
    methods = ['threading', 'multiprocessing']
    backends = [('pt', None), ('onnx', 'cpu')]
    try:
        providers = ort.get_available_providers()
        if 'CUDAExecutionProvider' in providers:
            backends.append(('onnx', 'cuda'))
    except Exception:
        pass
    best = {'fps': 0}
    print("\n===== Backend Recommendation (Multi-Agent, Extended) =====")
    print(f"Device: {device}, Models: {model_name} & {onnx_model_path}, Images per agent: {num_images}")
    print(f"Testing agent counts: {agent_range}, methods: {methods}, backends: {backends}")
    print("\nSummary Table:")
    print(f"{'Backend':<6} {'Device':<6} {'Method':<13} {'Agents':<6} {'FPS/agent':<10} {'CPU/agent':<10}")
    for backend, backend_device in backends:
        for method in methods:
            for agents in agent_range:
                if backend == 'pt':
                    avg_fps, avg_cpu, total = benchmark_yolo(model_name, device, agents, num_images, image_size, method, CONFIG['YOLO_CONF'])
                else:
                    avg_fps, avg_cpu, total = benchmark_onnx(onnx_model_path, agents, num_images, image_size, method, backend_device)
                print(f"{backend:<6} {str(backend_device):<6} {method:<13} {agents:<6} {avg_fps:<10.2f} {avg_cpu:<10.1f}")
                if avg_fps > best.get('fps', 0):
                    best = {
                        'fps': avg_fps,
                        'backend': backend,
                        'backend_device': backend_device,
                        'method': method,
                        'agents': agents,
                        'model': model_name if backend == 'pt' else onnx_model_path
                    }
    # Write best config
    CONFIG['INFERENCE_METHOD'] = best['method']
    CONFIG['TARGET_FPS'] = max(1, int(best['fps'] * 0.8))
    CONFIG['BACKEND'] = best['backend']
    CONFIG['BACKEND_DEVICE'] = best['backend_device']
    CONFIG['NUM_AGENTS'] = best['agents']
    CONFIG['MODEL_NAME'] = CONFIG['MODEL_NAME'] if best['backend'] == 'pt' else best['model'] 
    # Save to cache
    cache_out = {k: CONFIG[k] for k in ['INFERENCE_METHOD','TARGET_FPS','BACKEND','BACKEND_DEVICE','NUM_AGENTS','MODEL_NAME']}
    with open(CACHE_PATH, 'w') as f:
        _json.dump(cache_out, f, indent=2)
    print("\n>>> Recommended settings:")
    for k, v in cache_out.items():
        print(f"{k}: {v}")
    print("===============================================\n") 

def find_free_port(start, max_tries=100):
    port = start
    for _ in range(max_tries):
        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
            try:
                s.bind(('', port))
                return port
            except OSError:
                port += 1
    raise RuntimeError(f"No free ports found in range {start}-{start+max_tries}")

def configure_ports():
    CONFIG['SERVER_PORT'] = find_free_port(8080)
    CONFIG['UNITY_PORT'] = find_free_port(8051)
    return CONFIG['SERVER_PORT'], CONFIG['UNITY_PORT']