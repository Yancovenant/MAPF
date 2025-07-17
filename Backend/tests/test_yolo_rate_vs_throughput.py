import time, threading, multiprocessing, psutil
import numpy as np
import pytest
from ultralytics import YOLO

MODEL_NAME = "yolov8n.pt"
IMG_SHAPE = (480, 640, 3)
NUM_AGENTS = 5
NUM_IMAGES = 30
TARGET_FPS = 10

def agent_thread_rate_limited(results, idx):
    model = YOLO(MODEL_NAME)
    import torch
    device = 'cuda' if torch.cuda.is_available() else 'cpu'
    model.to(device)
    processed = 0
    cpu_samples = []
    start = time.time()
    next_frame = start
    for _ in range(NUM_IMAGES):
        now = time.time()
        if now < next_frame:
            time.sleep(max(0, next_frame - now))
        img = np.random.randint(0, 255, IMG_SHAPE, dtype=np.uint8)
        list(model.predict(img, conf=0.5, verbose=False, stream=True))[0]
        processed += 1
        cpu_samples.append(psutil.Process().cpu_percent(interval=None))
        next_frame += 1.0 / TARGET_FPS
    elapsed = time.time() - start
    fps = processed / elapsed
    avg_cpu = sum(cpu_samples) / len(cpu_samples) if cpu_samples else 0
    results.append((fps, avg_cpu, processed))

def agent_thread_throughput(results, idx):
    model = YOLO(MODEL_NAME)
    import torch
    device = 'cuda' if torch.cuda.is_available() else 'cpu'
    model.to(device)
    processed = 0
    cpu_samples = []
    start = time.time()
    for _ in range(NUM_IMAGES):
        img = np.random.randint(0, 255, IMG_SHAPE, dtype=np.uint8)
        list(model.predict(img, conf=0.5, verbose=False, stream=True))[0]
        processed += 1
        cpu_samples.append(psutil.Process().cpu_percent(interval=None))
    elapsed = time.time() - start
    fps = processed / elapsed
    avg_cpu = sum(cpu_samples) / len(cpu_samples) if cpu_samples else 0
    results.append((fps, avg_cpu, processed))

def agent_thread_throughput_iter(results, idx):
    model = YOLO(MODEL_NAME)
    import torch
    device = 'cuda' if torch.cuda.is_available() else 'cpu'
    model.to(device)
    processed = 0
    cpu_samples = []
    start = time.time()
    for _ in range(NUM_IMAGES):
        img = np.random.randint(0, 255, IMG_SHAPE, dtype=np.uint8)
        for _ in model.predict(img, conf=0.5, verbose=False, stream=True):
            pass  # Just iterate, do not use result
        processed += 1
        cpu_samples.append(psutil.Process().cpu_percent(interval=None))
    elapsed = time.time() - start
    fps = processed / elapsed
    avg_cpu = sum(cpu_samples) / len(cpu_samples) if cpu_samples else 0
    results.append((fps, avg_cpu, processed))

def agent_process_rate_limited(idx, return_dict):
    model = YOLO(MODEL_NAME)
    import torch
    device = 'cuda' if torch.cuda.is_available() else 'cpu'
    model.to(device)
    processed = 0
    cpu_samples = []
    start = time.time()
    next_frame = start
    for _ in range(NUM_IMAGES):
        now = time.time()
        if now < next_frame:
            time.sleep(max(0, next_frame - now))
        img = np.random.randint(0, 255, IMG_SHAPE, dtype=np.uint8)
        list(model.predict(img, conf=0.5, verbose=False, stream=True))[0]
        processed += 1
        cpu_samples.append(psutil.Process().cpu_percent(interval=None))
        next_frame += 1.0 / TARGET_FPS
    elapsed = time.time() - start
    fps = processed / elapsed
    avg_cpu = sum(cpu_samples) / len(cpu_samples) if cpu_samples else 0
    return_dict[idx] = (fps, avg_cpu, processed)

def agent_process_throughput(idx, return_dict):
    model = YOLO(MODEL_NAME)
    import torch
    device = 'cuda' if torch.cuda.is_available() else 'cpu'
    model.to(device)
    processed = 0
    cpu_samples = []
    start = time.time()
    for _ in range(NUM_IMAGES):
        img = np.random.randint(0, 255, IMG_SHAPE, dtype=np.uint8)
        list(model.predict(img, conf=0.5, verbose=False, stream=True))[0]
        processed += 1
        cpu_samples.append(psutil.Process().cpu_percent(interval=None))
    elapsed = time.time() - start
    fps = processed / elapsed
    avg_cpu = sum(cpu_samples) / len(cpu_samples) if cpu_samples else 0
    return_dict[idx] = (fps, avg_cpu, processed)

def agent_process_throughput_iter(idx, return_dict):
    model = YOLO(MODEL_NAME)
    import torch
    device = 'cuda' if torch.cuda.is_available() else 'cpu'
    model.to(device)
    processed = 0
    cpu_samples = []
    start = time.time()
    for _ in range(NUM_IMAGES):
        img = np.random.randint(0, 255, IMG_SHAPE, dtype=np.uint8)
        for _ in model.predict(img, conf=0.5, verbose=False, stream=True):
            pass
        processed += 1
        cpu_samples.append(psutil.Process().cpu_percent(interval=None))
    elapsed = time.time() - start
    fps = processed / elapsed
    avg_cpu = sum(cpu_samples) / len(cpu_samples) if cpu_samples else 0
    return_dict[idx] = (fps, avg_cpu, processed)

@pytest.mark.parametrize('mode', ['rate_limited', 'throughput', 'throughput_iter'])
def test_yolo_multi_agent_threading_rate_vs_throughput(mode):
    results = []
    if mode == 'rate_limited':
        threads = [threading.Thread(target=agent_thread_rate_limited, args=(results, i)) for i in range(NUM_AGENTS)]
    elif mode == 'throughput':
        threads = [threading.Thread(target=agent_thread_throughput, args=(results, i)) for i in range(NUM_AGENTS)]
    elif mode == 'throughput_iter':
        threads = [threading.Thread(target=agent_thread_throughput_iter, args=(results, i)) for i in range(NUM_AGENTS)]
    for t in threads: t.start()
    for t in threads: t.join()
    total_processed = sum(r[2] for r in results)
    print(f"\n=== 🧵 Multi-Agent Threads ({mode}) ===")
    for i, (fps, avg_cpu, processed) in enumerate(results):
        print(f"Agent {i+1}: FPS: {fps:.2f}, Avg CPU: {avg_cpu:.1f}%, Images: {processed}")
    print(f"✅ {mode} | 5 agents, total processed: {total_processed}, per agent: {[r[2] for r in results]}")
    assert total_processed == NUM_AGENTS * NUM_IMAGES

@pytest.mark.parametrize('mode', ['rate_limited', 'throughput', 'throughput_iter'])
def test_yolo_multi_agent_multiprocessing_rate_vs_throughput(mode):
    manager = multiprocessing.Manager()
    return_dict = manager.dict()
    if mode == 'rate_limited':
        processes = [multiprocessing.Process(target=agent_process_rate_limited, args=(i, return_dict)) for i in range(NUM_AGENTS)]
    elif mode == 'throughput':
        processes = [multiprocessing.Process(target=agent_process_throughput, args=(i, return_dict)) for i in range(NUM_AGENTS)]
    elif mode == 'throughput_iter':
        processes = [multiprocessing.Process(target=agent_process_throughput_iter, args=(i, return_dict)) for i in range(NUM_AGENTS)]
    for p in processes: p.start()
    for p in processes: p.join()
    stats = [return_dict[i] for i in range(NUM_AGENTS)]
    total_processed = sum(r[2] for r in stats)
    print(f"\n=== 🧩 Multi-Agent Multiprocessing ({mode}) ===")
    for i, (fps, avg_cpu, processed) in enumerate(stats):
        print(f"Agent {i+1}: FPS: {fps:.2f}, Avg CPU: {avg_cpu:.1f}%, Images: {processed}")
    print(f"✅ {mode} | 5 agents, total processed: {total_processed}, per agent: {[r[2] for r in stats]}")
    assert total_processed == NUM_AGENTS * NUM_IMAGES

import itertools

@pytest.mark.parametrize('num_agents', [1, 2, 3, 4, 5])
@pytest.mark.parametrize('mode', ['rate_limited', 'throughput', 'throughput_iter'])
def test_yolo_multi_agent_threading_rate_vs_throughput_varied_agents(num_agents, mode):
    results = []
    if mode == 'rate_limited':
        threads = [threading.Thread(target=agent_thread_rate_limited, args=(results, i)) for i in range(num_agents)]
    elif mode == 'throughput':
        threads = [threading.Thread(target=agent_thread_throughput, args=(results, i)) for i in range(num_agents)]
    elif mode == 'throughput_iter':
        threads = [threading.Thread(target=agent_thread_throughput_iter, args=(results, i)) for i in range(num_agents)]
    for t in threads: t.start()
    for t in threads: t.join()
    total_processed = sum(r[2] for r in results)
    print(f"\n=== 🧵 Multi-Agent Threads ({mode}) | Agents: {num_agents} ===")
    for i, (fps, avg_cpu, processed) in enumerate(results):
        print(f"Agent {i+1}: FPS: {fps:.2f}, Avg CPU: {avg_cpu:.1f}%, Images: {processed}")
    print(f"✅ {mode} | {num_agents} agents, total processed: {total_processed}, per agent: {[r[2] for r in results]}")
    assert total_processed == num_agents * NUM_IMAGES

@pytest.mark.parametrize('num_agents', [1, 2, 3, 4, 5])
@pytest.mark.parametrize('mode', ['rate_limited', 'throughput', 'throughput_iter'])
def test_yolo_multi_agent_multiprocessing_rate_vs_throughput_varied_agents(num_agents, mode):
    manager = multiprocessing.Manager()
    return_dict = manager.dict()
    if mode == 'rate_limited':
        processes = [multiprocessing.Process(target=agent_process_rate_limited, args=(i, return_dict)) for i in range(num_agents)]
    elif mode == 'throughput':
        processes = [multiprocessing.Process(target=agent_process_throughput, args=(i, return_dict)) for i in range(num_agents)]
    elif mode == 'throughput_iter':
        processes = [multiprocessing.Process(target=agent_process_throughput_iter, args=(i, return_dict)) for i in range(num_agents)]
    for p in processes: p.start()
    for p in processes: p.join()
    stats = [return_dict[i] for i in range(num_agents)]
    total_processed = sum(r[2] for r in stats)
    print(f"\n=== 🧩 Multi-Agent Multiprocessing ({mode}) | Agents: {num_agents} ===")
    for i, (fps, avg_cpu, processed) in enumerate(stats):
        print(f"Agent {i+1}: FPS: {fps:.2f}, Avg CPU: {avg_cpu:.1f}%, Images: {processed}")
    print(f"✅ {mode} | {num_agents} agents, total processed: {total_processed}, per agent: {[r[2] for r in stats]}")
    assert total_processed == num_agents * NUM_IMAGES



def agent_proc(idx, config, num_images, stats_list, model_name):
    from ultralytics import YOLO
    import torch
    device = 'cuda' if torch.cuda.is_available() else 'cpu'
    model = YOLO(model_name)
    model.to(device)
    img = np.random.randint(0, 255, (config['IMAGE_SIZE'][1], config['IMAGE_SIZE'][0], 3), dtype=np.uint8)
    process = psutil.Process()
    cpu_samples = []
    start = time.time()
    for _ in range(num_images):
        list(model.predict(img, conf=config['YOLO_CONF'], verbose=False, stream=True))[0]
        #model.predict(img, conf=config['YOLO_CONF'], verbose=False, stream=True)
        cpu_samples.append(process.cpu_percent(interval=None))
    elapsed = time.time() - start
    fps = num_images / elapsed if elapsed > 0 else 0
    avg_cpu = sum(cpu_samples) / len(cpu_samples) if cpu_samples else 0
    stats_list.append((fps, avg_cpu, num_images))

def agent_proc_iter(idx, config, num_images, stats_list, model_name):
    from ultralytics import YOLO
    import torch
    device = 'cuda' if torch.cuda.is_available() else 'cpu'
    model = YOLO(model_name)
    model.to(device)
    img = np.random.randint(0, 255, (config['IMAGE_SIZE'][1], config['IMAGE_SIZE'][0], 3), dtype=np.uint8)
    process = psutil.Process()
    cpu_samples = []
    start = time.time()
    for _ in range(num_images):
        for _ in model.predict(img, conf=config['YOLO_CONF'], verbose=False, stream=True):
            pass
        cpu_samples.append(process.cpu_percent(interval=None))
    elapsed = time.time() - start
    fps = num_images / elapsed if elapsed > 0 else 0
    avg_cpu = sum(cpu_samples) / len(cpu_samples) if cpu_samples else 0
    stats_list.append((fps, avg_cpu, num_images))

def test_configpy_recommend_settings_benchmark():
    """
    Compare list(model.predict(..., stream=True)) vs. iterating the generator for all agent types.
    """
    import torch
    from ultralytics import YOLO
    import numpy as np, threading, multiprocessing, psutil, time
    CONFIG = {
        'MODEL_NAME': 'yolov8n.pt',
        'INFERENCE_METHOD': 'threading',
        'NUM_AGENTS': 5,
        'TARGET_FPS': 10,
        'YOLO_CONF': 0.5,
        'IMAGE_SIZE': (640, 480),
        'DEVICE': None,
        'BENCHMARK_IMAGES': 30,  # match NUM_IMAGES for fair comparison
    }
    device = 'cuda' if torch.cuda.is_available() else 'cpu'
    CONFIG['DEVICE'] = device
    model_name = CONFIG['MODEL_NAME']
    num_agents = CONFIG.get('NUM_AGENTS', 5)
    num_images = CONFIG.get('BENCHMARK_IMAGES', 30)
    def run_agents(method, use_iter):
        agent_stats = []
        def agent_thread(idx, stats_list):
            from ultralytics import YOLO
            import torch
            device = 'cuda' if torch.cuda.is_available() else 'cpu'
            model = YOLO(model_name)
            model.to(device)
            img = np.random.randint(0, 255, (CONFIG['IMAGE_SIZE'][1], CONFIG['IMAGE_SIZE'][0], 3), dtype=np.uint8)
            process = psutil.Process()
            cpu_samples = []
            start = time.time()
            for _ in range(num_images):
                if use_iter:
                    for _ in model.predict(img, conf=CONFIG['YOLO_CONF'], verbose=False, stream=True):
                        pass
                else:
                    list(model.predict(img, conf=CONFIG['YOLO_CONF'], verbose=False, stream=True))
                cpu_samples.append(process.cpu_percent(interval=None))
            elapsed = time.time() - start
            fps = num_images / elapsed if elapsed > 0 else 0
            avg_cpu = sum(cpu_samples) / len(cpu_samples) if cpu_samples else 0
            stats_list.append((fps, avg_cpu, num_images))
        agent_stats.clear()
        if method == 'threading':
            threads = [threading.Thread(target=agent_thread, args=(i, agent_stats)) for i in range(num_agents)]
            for t in threads: t.start()
            for t in threads: t.join()
        elif method == 'multiprocessing':
            with multiprocessing.Manager() as manager:
                stats_list = manager.list()
                if use_iter:
                    procs = [multiprocessing.Process(target=agent_proc_iter, args=(i, CONFIG, num_images, stats_list, model_name)) for i in range(num_agents)]
                else:
                    procs = [multiprocessing.Process(target=agent_proc, args=(i, CONFIG, num_images, stats_list, model_name)) for i in range(num_agents)]
                for p in procs: p.start()
                for p in procs: p.join()
                agent_stats.extend(stats_list)
        total_processed = sum(stat[2] for stat in agent_stats)
        avg_fps = sum(stat[0] for stat in agent_stats) / num_agents if agent_stats else 0
        avg_cpu = sum(stat[1] for stat in agent_stats) / num_agents if agent_stats else 0
        return agent_stats, avg_fps, avg_cpu, total_processed
    for use_iter in [False, True]:
        label = "list()" if not use_iter else "iter"
        print(f"\n===== [TEST] Config.py Recommend Settings Benchmark ({label}) =====")
        print(f"Device: {device}, Model: {model_name}, Agents: {num_agents}, Images per agent: {num_images}")
        stats_thread, avg_fps_thread, avg_cpu_thread, total_thread = run_agents('threading', use_iter)
        print(f"\n--- Threading ({label}) ---")
        for i, (fps, cpu, processed) in enumerate(stats_thread):
            print(f"Agent {i+1}: FPS: {fps:.2f}, Avg CPU: {cpu:.1f}%, Images: {processed}")
        print(f"Threading: Avg FPS/agent: {avg_fps_thread:.2f}, Avg CPU: {avg_cpu_thread:.1f}%, Total: {total_thread}")
        stats_proc, avg_fps_proc, avg_cpu_proc, total_proc = run_agents('multiprocessing', use_iter)
        print(f"\n--- Multiprocessing ({label}) ---")
        for i, (fps, cpu, processed) in enumerate(stats_proc):
            print(f"Agent {i+1}: FPS: {fps:.2f}, Avg CPU: {cpu:.1f}%, Images: {processed}")
        print(f"Multiprocessing: Avg FPS/agent: {avg_fps_proc:.2f}, Avg CPU: {avg_cpu_proc:.1f}%, Total: {total_proc}")
    # No assert, just print for comparison 

import onnxruntime as ort
import os
ONNX_MODEL_PATH = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', 'yolov8n.onnx'))
ONNX_IMG_SHAPE = (480, 640, 3)

# Helper: Preprocess image for ONNX (float32, NCHW, normalized)
def preprocess_onnx(img):
    img = img.astype(np.float32) / 255.0
    img = np.transpose(img, (2, 0, 1))  # HWC to CHW
    img = np.expand_dims(img, 0)  # Add batch dim
    return img

def get_onnx_session():
    return ort.InferenceSession(ONNX_MODEL_PATH, providers=['CPUExecutionProvider'])

def agent_thread_onnx_rate_limited(results, idx):
    ort_sess = get_onnx_session()
    processed = 0
    cpu_samples = []
    img = np.random.randint(0, 255, ONNX_IMG_SHAPE, dtype=np.uint8)
    x = preprocess_onnx(img)
    input_name = ort_sess.get_inputs()[0].name
    start = time.time()
    next_frame = start
    for _ in range(NUM_IMAGES):
        now = time.time()
        if now < next_frame:
            time.sleep(max(0, next_frame - now))
        ort_sess.run(None, {input_name: x})
        processed += 1
        cpu_samples.append(psutil.Process().cpu_percent(interval=None))
        next_frame += 1.0 / TARGET_FPS
    elapsed = time.time() - start
    fps = processed / elapsed
    avg_cpu = sum(cpu_samples) / len(cpu_samples) if cpu_samples else 0
    results.append((fps, avg_cpu, processed))

def agent_thread_onnx_throughput(results, idx):
    ort_sess = get_onnx_session()
    processed = 0
    cpu_samples = []
    img = np.random.randint(0, 255, ONNX_IMG_SHAPE, dtype=np.uint8)
    x = preprocess_onnx(img)
    input_name = ort_sess.get_inputs()[0].name
    start = time.time()
    for _ in range(NUM_IMAGES):
        ort_sess.run(None, {input_name: x})
        processed += 1
        cpu_samples.append(psutil.Process().cpu_percent(interval=None))
    elapsed = time.time() - start
    fps = processed / elapsed
    avg_cpu = sum(cpu_samples) / len(cpu_samples) if cpu_samples else 0
    results.append((fps, avg_cpu, processed))

def agent_thread_onnx_throughput_iter(results, idx):
    # For ONNX, iter and normal are the same (no generator), so just call run
    agent_thread_onnx_throughput(results, idx)

def agent_process_onnx_rate_limited(idx, return_dict):
    ort_sess = get_onnx_session()
    processed = 0
    cpu_samples = []
    img = np.random.randint(0, 255, ONNX_IMG_SHAPE, dtype=np.uint8)
    x = preprocess_onnx(img)
    input_name = ort_sess.get_inputs()[0].name
    start = time.time()
    next_frame = start
    for _ in range(NUM_IMAGES):
        now = time.time()
        if now < next_frame:
            time.sleep(max(0, next_frame - now))
        ort_sess.run(None, {input_name: x})
        processed += 1
        cpu_samples.append(psutil.Process().cpu_percent(interval=None))
        next_frame += 1.0 / TARGET_FPS
    elapsed = time.time() - start
    fps = processed / elapsed
    avg_cpu = sum(cpu_samples) / len(cpu_samples) if cpu_samples else 0
    return_dict[idx] = (fps, avg_cpu, processed)

def agent_process_onnx_throughput(idx, return_dict):
    ort_sess = get_onnx_session()
    processed = 0
    cpu_samples = []
    img = np.random.randint(0, 255, ONNX_IMG_SHAPE, dtype=np.uint8)
    x = preprocess_onnx(img)
    input_name = ort_sess.get_inputs()[0].name
    start = time.time()
    for _ in range(NUM_IMAGES):
        ort_sess.run(None, {input_name: x})
        processed += 1
        cpu_samples.append(psutil.Process().cpu_percent(interval=None))
    elapsed = time.time() - start
    fps = processed / elapsed
    avg_cpu = sum(cpu_samples) / len(cpu_samples) if cpu_samples else 0
    return_dict[idx] = (fps, avg_cpu, processed)

def agent_process_onnx_throughput_iter(idx, return_dict):
    # For ONNX, iter and normal are the same
    agent_process_onnx_throughput(idx, return_dict)

@pytest.mark.parametrize('num_agents', [1, 2, 3, 4, 5])
@pytest.mark.parametrize('mode', ['rate_limited', 'throughput', 'throughput_iter'])
def test_onnx_multi_agent_threading_varied_agents(num_agents, mode):
    results = []
    if mode == 'rate_limited':
        threads = [threading.Thread(target=agent_thread_onnx_rate_limited, args=(results, i)) for i in range(num_agents)]
    elif mode == 'throughput':
        threads = [threading.Thread(target=agent_thread_onnx_throughput, args=(results, i)) for i in range(num_agents)]
    elif mode == 'throughput_iter':
        threads = [threading.Thread(target=agent_thread_onnx_throughput_iter, args=(results, i)) for i in range(num_agents)]
    for t in threads: t.start()
    for t in threads: t.join()
    total_processed = sum(r[2] for r in results)
    print(f"\n=== 🧵 ONNX Multi-Agent Threads ({mode}) | Agents: {num_agents} ===")
    for i, (fps, avg_cpu, processed) in enumerate(results):
        print(f"Agent {i+1}: FPS: {fps:.2f}, Avg CPU: {avg_cpu:.1f}%, Images: {processed}")
    print(f"✅ {mode} | {num_agents} agents, total processed: {total_processed}, per agent: {[r[2] for r in results]}")
    assert total_processed == num_agents * NUM_IMAGES

@pytest.mark.parametrize('num_agents', [1, 2, 3, 4, 5])
@pytest.mark.parametrize('mode', ['rate_limited', 'throughput', 'throughput_iter'])
def test_onnx_multi_agent_multiprocessing_varied_agents(num_agents, mode):
    manager = multiprocessing.Manager()
    return_dict = manager.dict()
    if mode == 'rate_limited':
        processes = [multiprocessing.Process(target=agent_process_onnx_rate_limited, args=(i, return_dict)) for i in range(num_agents)]
    elif mode == 'throughput':
        processes = [multiprocessing.Process(target=agent_process_onnx_throughput, args=(i, return_dict)) for i in range(num_agents)]
    elif mode == 'throughput_iter':
        processes = [multiprocessing.Process(target=agent_process_onnx_throughput_iter, args=(i, return_dict)) for i in range(num_agents)]
    for p in processes: p.start()
    for p in processes: p.join()
    stats = [return_dict[i] for i in range(num_agents)]
    total_processed = sum(r[2] for r in stats)
    print(f"\n=== 🧩 ONNX Multi-Agent Multiprocessing ({mode}) | Agents: {num_agents} ===")
    for i, (fps, avg_cpu, processed) in enumerate(stats):
        print(f"Agent {i+1}: FPS: {fps:.2f}, Avg CPU: {avg_cpu:.1f}%, Images: {processed}")
    print(f"✅ {mode} | {num_agents} agents, total processed: {total_processed}, per agent: {[r[2] for r in stats]}")
    assert total_processed == num_agents * NUM_IMAGES 