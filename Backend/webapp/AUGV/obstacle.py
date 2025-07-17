###
### webapp/AUGV/obstacle.py
###

from webapp.tools.config import CONFIG
from ultralytics import YOLO
import threading, queue, numpy as np, math, socket, json, asyncio
from collections import defaultdict
from numba import njit
import multiprocessing
from webapp.tools.config import CONFIG, get_onnx_session, preprocess_onnx
import onnxruntime as ort
import cv2

AGENT_QUEUES, AGENT_STATE = {}, {}
UNITY_HOST, UNITY_PORT = "localhost", 8051
AGENT_OUT_QUEUES = defaultdict(asyncio.Queue)

# TRACK ALL RUNGING MULTIPROCESSING AGENTS
AGENT_PROCS = {}
GLOBAL_AGENT = {}

# ======== 
# YOLO
# ========
class AUGVMixin:
    def _populate_data(self, agent_id, onnx=False, mp=False):
        self.agent_id = agent_id
        if mp:
            self.q = multiprocessing.Queue(maxsize=1)
        else:
            self.q = queue.Queue(maxsize=1)
        AGENT_QUEUES[agent_id] = self.q
        AGENT_STATE[agent_id] = {
            'status': 'waiting',
            'detections': []
        }
        self.AGENT_STATE = AGENT_STATE
        self.AGENT_QUEUES = AGENT_QUEUES
        self.last_detection = set()
        self.use_yolo = False
        GLOBAL_AGENT[agent_id] = self
        if onnx:
            self.class_names = ["person"]
            self.onnx = True
        else:
            self.onnx = False
    
    def _process_frame(self, frame):
        detections = []
        blocked_offsets = set()

        if not self.use_yolo:
            return detections, blocked_offsets
        if self.onnx:
            if not hasattr(self, 'ort_sess') or not hasattr(self, 'input_name'):
                raise ValueError("ORT session and input name must be set for onnx")
            orig_h, orig_w = frame.shape[:2]
            image, ratio, (dw, dh) = self._preprocess_onnx_image(frame)
            outputs = self.ort_sess.run(None, {self.input_name: image}) # type: ignore[attr-defined]
            detections, blocked_offsets = self._postprocess_onnx(outputs, 640, 640, ratio, dw, dh, orig_w, orig_h)
        else:
            if not hasattr(self, 'model'):
                raise ValueError("YOLO model must be set for pt inference")
            conf_thres = CONFIG.get('CONF_THRES', 0.6)
            image = np.ascontiguousarray(frame)
            img_h, img_w = image.shape[:2]
            res = list(self.model.predict(image, conf=conf_thres, verbose=False, stream=True))[0] # type: ignore[attr-defined]

            detections, blocked_offsets = self._postprocess_pt(res, img_h, img_w)
        
        blocked_offsets = set([b for b in blocked_offsets if b is not None])

        return detections, blocked_offsets

    def _postprocess_pt(self, res, img_h, img_w):
        detections = []
        blocked_offsets = set()
        print(f"[PT] img_w: {img_w}, img_h: {img_h}")
        for box in getattr(res, "boxes", []):
            if self.model.names[int(box.cls[0])] != "person":
                continue
            xywh = box.xywh[0].tolist()
            x, y, w, h = xywh
            print(f"[PT] raw bbox: x={x}, y={y}, w={w}, h={h}")
            feet_x = x
            feet_y = y + h/2
            dx, dy = _get_offset(feet_x, feet_y, img_w, img_h, h)
            print(f"[PT] mapped bbox: x={x}, y={y}, w={w}, h={h}, feet=({feet_x},{feet_y}), offset=({dx},{dy})")
            blocked_offsets.add((dx, dy) if dx is not None and dy is not None and dy > 0 and dy <= 5 else None)
            detections.append({
                "label": "person",
                "confidence": round(float(box.conf[0]), 3),
                "bbox": [round(v, 2) for v in xywh],
                "feet": [feet_x, feet_y],
                "offset": [dx, dy]
            })
        return detections, blocked_offsets
    
    def _postprocess_onnx(self, outputs, img_w, img_h, ratio, dw, dh, orig_w, orig_h):
        detections = []
        blocked_offsets = set()
        conf_thres = CONFIG.get('CONF_THRES', 0.6)
        output = np.squeeze(outputs[0]).T
        print(f"[ONNX] orig_w: {orig_w}, orig_h: {orig_h}, img_w: {img_w}, img_h: {img_h}, ratio: {ratio}, dw: {dw}, dh: {dh}")
        for det in output:
            cls_id = det[4:].argmax()
            conf_score = det[4:].max()
            if cls_id != 0 or conf_score < conf_thres:
                continue
            x, y, w, h = det[:4]
            print(f"[ONNX] raw bbox: x={x}, y={y}, w={w}, h={h}")
            x_mapped = (x - dw) / ratio
            y_mapped = (y - dh) / ratio
            w_mapped = w / ratio
            h_mapped = h / ratio
            print(f"[ONNX] mapped bbox: x={x_mapped}, y={y_mapped}, w={w_mapped}, h={h_mapped}")
            x_mapped = np.clip(x_mapped, 0, orig_w)
            y_mapped = np.clip(y_mapped, 0, orig_h)
            w_mapped = np.clip(w_mapped, 0, orig_w)
            h_mapped = np.clip(h_mapped, 0, orig_h)
            feet_x = x_mapped
            feet_y = y_mapped + h_mapped/2
            dx, dy = _get_offset(feet_x, feet_y, orig_w, orig_h, h_mapped)
            print(f"[ONNX] feet=({feet_x},{feet_y}), offset=({dx},{dy})")
            blocked_offsets.add((dx, dy) if dx is not None and dy is not None and dy > 0 and dy <= 5 else None)
            detections.append({
                "label": "person",
                "confidence": round(float(conf_score), 3),
                "bbox": [round(float(v), 2) for v in [x_mapped, y_mapped, w_mapped, h_mapped]],
                "feet": [float(feet_x), float(feet_y)],
                "offset": [int(dx), int(dy)]
            })
        return detections, blocked_offsets

    def _preprocess_onnx_image(self, frame):
        img, ratio, (dw, dh) = self._letterbox(frame, (640, 640))
        img = img.astype(np.float32) / 255.0
        img = np.transpose(img, (2, 0, 1)) # HWC -> CHW
        img = np.expand_dims(img, 0) # Add batch dimension
        return img, ratio, (dw, dh)
    
    def _letterbox(self, img, new_shape=(640, 640), color=(114, 114, 114)):

        # current shape [height, width]
        shape = img.shape[:2]
        # print(f"height: {shape[0]}, width: {shape[1]}")

        # min(640 / height, 640 / width) = min(640/ 320, 640/ 640) = min(2, 1) = 1
        ratio = min(new_shape[0] / shape[0], new_shape[1] / shape[1])

        # (width * 1), (height * 1) = (640, 320)
        new_unpad = (int(round(shape[1] * ratio)), int(round(shape[0] * ratio)))

        # 640 - 640 = 0 [dw/padding left or right]
        # 640 - 320 = 320 [dh/padding top or bottom]
        dw = new_shape[1] - new_unpad[0]
        dh = new_shape[0] - new_unpad[1]

        # [dw] 0 / 2 = 0
        # [dh] 320 / 2 = 160 (left right padding)
        dw /= 2  # divide padding into 2 sides
        dh /= 2
        # print(f"dw: {dw}, dh: {dh}")

        # resize to match the new shape (640, 320)
        img = cv2.resize(img, new_unpad, interpolation=cv2.INTER_LINEAR)

        # top = round(160 - 0.1) = 159.9 = 160
        # bottom = round(160 + 0.1) = 160.1 = 160
        top, bottom = int(round(dh - 0.1)), int(round(dh + 0.1))

        # left = round(0 - 0.1) = -0.1 = 0
        # right = round(0 + 0.1) = 0.1 = 0
        left, right = int(round(dw - 0.1)), int(round(dw + 0.1))

        # copy make border to add padding to the image
        img = cv2.copyMakeBorder(img, top, bottom, left, right, cv2.BORDER_CONSTANT, value=color)
        cv2.imwrite("letterbox.jpg", img)
        return img, ratio, (dw, dh)


class AUGVYolo(threading.Thread, AUGVMixin):
    def __init__(self, agent_id):
        super().__init__(daemon=True)
        self._populate_data(agent_id, onnx=False, mp=False)
    
    def run(self):
        try:
            model = YOLO(CONFIG['MODEL_NAME'])
            device = CONFIG.get("DEVICE", "cpu")
            model.to(device)
            self.model = model
        except Exception as e:
            print(f"Error loading YOLO model for agent {self.agent_id}: {e}")
            AGENT_STATE[self.agent_id]['status'] = 'error'
            return
        
        while True:
            try:
                frame = self.q.get()
                if frame is None:
                    continue

                # image = np.ascontiguousarray(frame)
                # img_h, img_w = image.shape[:2]

                detections, blocked_offsets = self._process_frame(frame)

                # res = list(model.predict(image, conf=0.6, verbose=False, stream=True))[0]

                # detections = []
                # blocked_offsets = set()

                # for box in getattr(res, "boxes", []):
                #     if model.names[int(box.cls[0])] != "person":
                #         continue

                #     xywh = box.xywh[0].tolist()
                #     x, y, w, h = xywh

                #     feet_x = x
                #     feet_y = y + h/2
                #     dx, dy = _get_offset(feet_x, feet_y, img_w, img_h, h)

                #     offset = (dx, dy) if dx is not None and dy is not None and dy > 0 and dy <= 5 else None
                #     blocked_offsets.add(offset)

                #     detections.append({
                #         "label": "person",
                #         "confidence": round(float(box.conf[0]), 3),
                #         "bbox": [round(v, 2) for v in xywh],
                #         "feet": [feet_x, feet_y],
                #         "offset": [dx, dy]
                #     })
                
                if blocked_offsets:
                    _send_to_unity(self.agent_id, blocked_offsets)
                
                AGENT_STATE[self.agent_id] = {
                    "status": "blocked" if blocked_offsets else "safe",
                    "detections": detections,
                    "blocked_offsets": [offset for offset in blocked_offsets if offset is not None]
                }
            except Exception as e:
                print(f"Error in AgentYoloThread for agent {self.agent_id}: {e}")
                AGENT_STATE[self.agent_id]['status'] = 'error'
                break

class AUGVYoloMP(multiprocessing.Process, AUGVMixin):
    def __init__(self, agent_id):
        super().__init__(daemon=True)
        self._populate_data(agent_id, onnx=False, mp=True)
        AGENT_PROCS[agent_id] = self
    
    def run(self):
        try:
            model = YOLO(CONFIG['MODEL_NAME'])
            device = CONFIG.get("DEVICE", "cpu")
            model.to(device)
            self.model = model
        except Exception as e:
            print(f"Error loading YOLO model for agent {self.agent_id}: {e}")
            AGENT_STATE[self.agent_id]['status'] = 'error'
            return
        
        while True:
            try:
                frame = self.q.get()
                if frame is None:
                    break

                detections, blocked_offsets = self._process_frame(frame)

                # image = np.ascontiguousarray(frame)
                # img_h, img_w = image.shape[:2]

                # res = list(model.predict(image, conf=0.6, verbose=False, stream=True))[0]

                # detections = []
                # blocked_offsets = set()

                # for box in getattr(res, "boxes", []):
                #     if model.names[int(box.cls[0])] != "person":
                #         continue
                    
                #     xywh = box.xywh[0].tolist()
                #     x, y, w, h = xywh
                #     feet_x = x
                #     feet_y = y + h/2
                #     dx, dy = _get_offset(feet_x, feet_y, img_w, img_h, h)

                #     offset = (dx, dy) if dx is not None and dy is not None and dy > 0 and dy <= 5 else None
                #     blocked_offsets.add(offset)

                #     detections.append({
                #         "label": "person",
                #         "confidence": round(float(box.conf[0]), 3),
                #         "bbox": [round(v, 2) for v in xywh],
                #         "feet": [feet_x, feet_y],
                #         "offset": [dx, dy]
                #     })
                
                if blocked_offsets:
                    _send_to_unity(self.agent_id, blocked_offsets)
                
                AGENT_STATE[self.agent_id] = {
                    "status": "blocked" if blocked_offsets else "safe",
                    "detections": detections,
                    "blocked_offsets": [offset for offset in blocked_offsets if offset is not None]
                }
            except Exception as e:
                print(f"Error in AgentYoloMP for agent {self.agent_id}: {e}")

#### ONNX
class AUGVOnnx(threading.Thread, AUGVMixin):
    def __init__(self, agent_id):
        super().__init__(daemon=True)
        self._populate_data(agent_id, onnx=True, mp=False)
        self.ort_sess = get_onnx_session(CONFIG['MODEL_NAME'], CONFIG.get('BACKEND_DEVICE', 'cpu'))
        self.input_name = self.ort_sess.get_inputs()[0].name

    def run(self):
        while True:
            try:
                frame = self.q.get()
                if frame is None:
                    continue

                detections, blocked_offsets = self._process_frame(frame)

                # image = np.ascontiguousarray(frame)
                # img_h, img_w = image.shape[:2]
                # x = preprocess_onnx(image)
                # outputs = self.ort_sess.run(None, {self.input_name: x})
                # detections, blocked_offsets = postprocess_onnx(outputs, img_h, img_w)
                
                if blocked_offsets and self.last_detection != blocked_offsets:
                    _send_to_unity(self.agent_id, blocked_offsets)
                    self.last_detection = blocked_offsets.copy()

                AGENT_STATE[self.agent_id] = {
                    "status": "blocked" if blocked_offsets else "safe",
                    "detections": detections,
                    "blocked_offsets": list(blocked_offsets)
                }
            except Exception as e:
                print(f"Error in AgentOnnx for agent {self.agent_id}: {e}")
                AGENT_STATE[self.agent_id]['status'] = 'error'

class AUGVOnnxMP(multiprocessing.Process, AUGVMixin):
    def __init__(self, agent_id):
        super().__init__(daemon=True)
        self._populate_data(agent_id, onnx=True, mp=True)
        self.ort_sess = None
        self.input_name = None
    
    def run(self):
        self.ort_sess = get_onnx_session(CONFIG['MODEL_NAME'], CONFIG.get('BACKEND_DEVICE', 'cpu'))
        self.input_name = self.ort_sess.get_inputs()[0].name

        while True:        
            try:
                frame = self.q.get()
                if frame is None:
                    break
                # image = np.ascontiguousarray(frame)
                # img_h, img_w = image.shape[:2]
                # x = preprocess_onnx(image)
                # outputs = self.ort_sess.run(None, {self.input_name: x})
                # detections, blocked_offsets = postprocess_onnx(outputs, img_h, img_w)
                
                detections, blocked_offsets = self._process_frame(frame)

                if blocked_offsets and self.last_detection != blocked_offsets:
                    _send_to_unity(self.agent_id, blocked_offsets)
                    self.last_detection = blocked_offsets.copy()
                
                AGENT_STATE[self.agent_id] = {
                    "status": "blocked" if blocked_offsets else "safe",
                    "detections": detections,
                    "blocked_offsets": list(blocked_offsets)
                }
            except Exception as e:
                print(f"Error in AgentOnnxMP for agent {self.agent_id}: {e}")
                AGENT_STATE[self.agent_id]['status'] = 'error'

def postprocess_onnx(outputs, img_h, img_w, conf_thres=None):
    conf_thres = conf_thres or 0.6
    detections = []
    blocked_offsets = set()
    output = outputs[0]
    if output.ndim == 3:
        output = output[0]
    for det in output:
        x, y, w, h, obj_conf = det[:5]
        class_score = det[5:]
        cls_id = int(np.argmax(class_score))
        conf = obj_conf * class_score[cls_id]
        if conf < conf_thres:
            continue
        if cls_id != 0:
            continue
        feet_x = x
        feet_y = y + h/2
        dx, dy = _get_offset(feet_x, feet_y, img_w, img_h, h)
        blocked_offsets.add((dx, dy) if dx is not None and dy is not None and dy > 0 and dy <= 5 else None)
        detections.append({
            "label": "person",
            "confidence": round(float(conf), 3),
            "bbox": [round(float(v), 2) for v in [x, y, w, h]],
            "feet": [float(feet_x), float(feet_y)],
            "offset": [int(dx), int(dy)]
        })
    blocked_offsets = set([b for b in blocked_offsets if b is not None])
    return detections, blocked_offsets

def create_agent(agent_id):
    backend = CONFIG.get('BACKEND', 'pt')
    method = CONFIG.get('INFERENCE_METHOD', 'threading')
    if backend == 'pt':
        if method == 'threading':
            return AUGVYolo(agent_id)
        elif method == 'multiprocessing':
            return AUGVYoloMP(agent_id)
    elif backend == 'onnx':
        if method == 'threading':
            return AUGVOnnx(agent_id)
        elif method == 'multiprocessing':
            return AUGVOnnxMP(agent_id)
    else:
        raise ValueError(f"Invalid backend: {backend}")

## important for performance
"""
==============================================
CONFIG CAMERA FROM UNITY SETUP
==============================================
"""
CAMERA_CONFIG = CONFIG['CAMERA_CONFIG']
_cam_height = CAMERA_CONFIG['height']
_cam_forward = CAMERA_CONFIG['forward']
_cam_rot_x = CAMERA_CONFIG['rot_x']
_cam_fov = CAMERA_CONFIG['fov']
_grid_size = CAMERA_CONFIG['grid_size']
_node_center = CAMERA_CONFIG['grid_center']
"""
==============================================
END CONFIG
==============================================
"""
@njit
def _get_offset(feet_x, feet_y, img_w, img_h, h):
    """ 
    Project image pixel (x_img, y_img) to (dx, dy) grid offset
    Relative to agent/camera position.
    Use distance-based bias for dy.
    """
    # Normalized device coordinates
    x_ndc = (feet_x / img_w - 0.5) * 2
    y_ndc = (feet_y / img_h - 0.5) * 2

    # Convert to camera coordinates
    fov_rad = math.radians(_cam_fov)
    aspect_ratio = img_w / img_h
    tan_fov = math.tan(fov_rad / 2)

    # Ray in camera space
    x_cam = x_ndc * tan_fov * aspect_ratio
    y_cam = -y_ndc * tan_fov
    z_cam = 1

    # Rotate around x-axis (downward)
    rot_x = math.radians(_cam_rot_x)
    y_rot = y_cam * math.cos(rot_x) - z_cam * math.sin(rot_x)
    z_rot = y_cam * math.sin(rot_x) + z_cam * math.cos(rot_x)

    # Calculate distance to world plane
    t = -_cam_height / y_rot if y_rot != 0 else 0
    world_x = x_cam * t
    world_z = (_node_center + _cam_forward) + z_rot * t

    # Calculate distance to world plane
    distance = math.sqrt(world_x**2 + world_z**2)

    #########################################################
    ############## distance-based bias for dy ################
    #########################################################
    if distance <= 2.0:
        bias = 0.2 # Close Object
    elif distance <= 4.0:
        bias = 0.5 # Medium Object
    elif distance <= 6.0:
        bias = 0.8 # Far Object
    else:
        bias = 1.2 # Very Far Object
    
    dy = int(round(world_z + bias) / _grid_size) # Forward/Backward
    dx = int(round(world_x / _grid_size)) # Left/Right
    return dx, dy

#@debounce(1)
def _send_to_unity(agent_id, blocked):
    valid = [offset for offset in blocked if offset is not None and offset[0] == 0 and offset[1] != 0]
    if not valid:
        return
    
    try:
        print(f"Sending to Unity for agent {agent_id}: {valid}")
        AGENT_OUT_QUEUES[agent_id].put_nowait({
            "action": "obstacle",
            "data": {
                "agent_id": agent_id,
                "blocked": valid
            }
        })
        # with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as s:
        #     print(f"Sending to Unity for agent {agent_id}: {valid}")
        #     s.sendto(json.dumps({
        #         "action": "obstacle",
        #         "data": {
        #             "agent_id": agent_id,
        #             "blocked": valid
        #         }
        #     }).encode(), (UNITY_HOST, UNITY_PORT))
    except Exception as e:
        print(f"Error sending to Unity for agent {agent_id}: {e}")
