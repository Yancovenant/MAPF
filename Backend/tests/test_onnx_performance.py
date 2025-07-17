import os, time, threading, multiprocessing, psutil
import numpy as np
import pytest
import onnxruntime as ort

ONNX_MODEL_PATH = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', 'yolov8n.onnx'))
ONNX_IMG_SHAPE = (640, 640, 3)  # Match ONNX model input
NUM_IMAGES = 30
TARGET_FPS = 10

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