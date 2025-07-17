import os, time, threading, multiprocessing, psutil
import numpy as np
import pytest
from ultralytics import YOLO

MODEL_NAME = "yolov8n.pt"
IMG_SHAPE = (480, 640, 3)  # Match your Unity input
NUM_AGENTS = 5
NUM_IMAGES = 60
TARGET_FPS_LIST = [5, 10, 20, 30, 45, 60]

# Helper: Resource monitor
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

def get_yolo_model():
    import torch
    device = 'cuda' if torch.cuda.is_available() else 'cpu'
    model = YOLO(MODEL_NAME)
    model.to(device)
    return model

@pytest.mark.parametrize('use_stream', [False, True])
@pytest.mark.parametrize('target_fps', TARGET_FPS_LIST)
def test_yolo_multi_agent_threading_stream(use_stream, target_fps):
    model = get_yolo_model()
    img = np.random.randint(0, 255, IMG_SHAPE, dtype=np.uint8)
    agent_stats = []
    results_list = []
    monitor = ResourceMonitor()
    
    def agent_thread():
        processed = 0
        cpu_samples = []
        local_results = []
        start = time.time()
        next_frame = start
        for _ in range(NUM_IMAGES):
            now = time.time()
            if now < next_frame:
                time.sleep(max(0, next_frame - now))
            
            # Run inference with or without stream
            if use_stream:
                results = list(model.predict(img, conf=0.5, verbose=False, stream=True))
            else:
                results = model.predict(img, conf=0.5, verbose=False, stream=False)
            
            # Store results for comparison
            if results:
                local_results.append(results[0] if isinstance(results, list) else results)
            
            processed += 1
            cpu_samples.append(psutil.Process().cpu_percent(interval=None))
            next_frame += 1.0 / target_fps
        
        elapsed = time.time() - start
        fps = processed / elapsed
        avg_cpu = sum(cpu_samples) / len(cpu_samples) if cpu_samples else 0
        agent_stats.append((fps, avg_cpu, processed))
        results_list.append(local_results)
    
    threads = [threading.Thread(target=agent_thread) for _ in range(NUM_AGENTS)]
    monitor.start()
    for t in threads: t.start()
    for t in threads: t.join()
    monitor.stop()
    
    total_processed = sum(stat[2] for stat in agent_stats)
    summary = monitor.summary()
    stream_label = "Stream=True" if use_stream else "Stream=False"
    print(f"[YOLO Threading] {stream_label} Target FPS: {target_fps}, Per Agent: {[stat[0] for stat in agent_stats]}, Total Processed: {total_processed}, Max Mem: {summary['max_mem']/1e6:.2f}MB, Avg CPU: {summary['avg_cpu']:.1f}%")
    assert total_processed == NUM_AGENTS * NUM_IMAGES

def agent_process_mp_stream(use_stream, target_fps, return_dict):
    model = get_yolo_model()
    img = np.random.randint(0, 255, IMG_SHAPE, dtype=np.uint8)
    processed = 0
    cpu_samples = []
    local_results = []
    start = time.time()
    next_frame = start
    for _ in range(NUM_IMAGES):
        now = time.time()
        if now < next_frame:
            time.sleep(max(0, next_frame - now))
        
        # Run inference with or without stream
        if use_stream:
            results = list(model.predict(img, conf=0.5, verbose=False, stream=True))
        else:
            results = model.predict(img, conf=0.5, verbose=False, stream=False)
        
        # Store results for comparison
        if results:
            local_results.append(results[0] if isinstance(results, list) else results)
        
        processed += 1
        cpu_samples.append(psutil.Process().cpu_percent(interval=None))
        next_frame += 1.0 / target_fps
    
    elapsed = time.time() - start
    fps = processed / elapsed
    avg_cpu = sum(cpu_samples) / len(cpu_samples) if cpu_samples else 0
    return_dict[multiprocessing.current_process().name] = (fps, avg_cpu, processed, local_results)

@pytest.mark.parametrize('use_stream', [False, True])
@pytest.mark.parametrize('target_fps', TARGET_FPS_LIST)
def test_yolo_multi_agent_multiprocessing_stream(use_stream, target_fps):
    manager = multiprocessing.Manager()
    return_dict = manager.dict()
    processes = [multiprocessing.Process(target=agent_process_mp_stream, args=(use_stream, target_fps, return_dict), name=f'Agent_{i}') for i in range(NUM_AGENTS)]
    monitor = ResourceMonitor()
    monitor.start()
    for p in processes: p.start()
    for p in processes: p.join()
    monitor.stop()
    
    mp_stats = [return_dict[f'Agent_{i}'] for i in range(NUM_AGENTS)]
    total_processed = sum(stat[2] for stat in mp_stats)
    summary = monitor.summary()
    stream_label = "Stream=True" if use_stream else "Stream=False"
    print(f"[YOLO Multiprocessing] {stream_label} Target FPS: {target_fps}, Per Agent: {[stat[0] for stat in mp_stats]}, Total Processed: {total_processed}, Max Mem: {summary['max_mem']/1e6:.2f}MB, Avg CPU: {summary['avg_cpu']:.1f}%")
    assert total_processed == NUM_AGENTS * NUM_IMAGES

def test_stream_vs_no_stream_results_identical():
    """Test that stream=True and stream=False produce identical results"""
    model = get_yolo_model()
    img = np.random.randint(0, 255, IMG_SHAPE, dtype=np.uint8)
    
    # Run inference with stream=False
    results_no_stream = model.predict(img, conf=0.5, verbose=False, stream=False)
    
    # Run inference with stream=True
    results_stream = list(model.predict(img, conf=0.5, verbose=False, stream=True))
    
    # Compare results
    assert len(results_stream) == 1, "Stream should return exactly one result for single image"
    result_stream = results_stream[0]
    result_no_stream = results_no_stream[0] if isinstance(results_no_stream, list) else results_no_stream
    
    # Compare boxes
    if hasattr(result_stream, 'boxes') and result_stream.boxes is not None:
        assert hasattr(result_no_stream, 'boxes') and result_no_stream.boxes is not None
        assert len(result_stream.boxes) == len(result_no_stream.boxes), "Number of detections should be identical"
        
        # Compare box coordinates (with small tolerance for floating point)
        for i in range(len(result_stream.boxes)):
            stream_box = result_stream.boxes.xyxy[i].cpu().numpy()
            no_stream_box = result_no_stream.boxes.xyxy[i].cpu().numpy()
            np.testing.assert_array_almost_equal(stream_box, no_stream_box, decimal=5)
            
            # Compare confidence scores
            stream_conf = result_stream.boxes.conf[i].cpu().numpy()
            no_stream_conf = result_no_stream.boxes.conf[i].cpu().numpy()
            np.testing.assert_almost_equal(stream_conf, no_stream_conf, decimal=5)
            
            # Compare class IDs
            stream_cls = result_stream.boxes.cls[i].cpu().numpy()
            no_stream_cls = result_no_stream.boxes.cls[i].cpu().numpy()
            np.testing.assert_almost_equal(stream_cls, no_stream_cls, decimal=5)
    else:
        # Both should have no detections
        assert not (hasattr(result_no_stream, 'boxes') and result_no_stream.boxes is not None)
    
    print("✅ Stream=True and Stream=False produce identical results")

@pytest.mark.parametrize('use_stream', [False, True])
def test_single_inference_performance(use_stream):
    """Test single inference performance with and without stream"""
    model = get_yolo_model()
    img = np.random.randint(0, 255, IMG_SHAPE, dtype=np.uint8)
    monitor = ResourceMonitor()
    
    monitor.start()
    start_time = time.time()
    
    # Run inference
    if use_stream:
        results = list(model.predict(img, conf=0.5, verbose=False, stream=True))
    else:
        results = model.predict(img, conf=0.5, verbose=False, stream=False)
    
    end_time = time.time()
    monitor.stop()
    
    inference_time = end_time - start_time
    summary = monitor.summary()
    stream_label = "Stream=True" if use_stream else "Stream=False"
    print(f"[Single Inference] {stream_label} Time: {inference_time:.4f}s, Max Mem: {summary['max_mem']/1e6:.2f}MB, Avg CPU: {summary['avg_cpu']:.1f}%")
    
    assert len(results) > 0, "Should return at least one result"
    assert inference_time > 0, "Inference time should be positive" 