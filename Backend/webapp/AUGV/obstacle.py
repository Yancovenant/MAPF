###
### webapp/AUGV/obstacle.py
###

"""
This is obstacle detection module for our webapp AUGV
It is using YOLO Obstacle Detection Model
It will detect and send the blocked offsets to Unity
It will also bypassing the obstacle detection if the agent is not using YOLO
This is important for sending camera from unity to the backend and display it back again to frontend webapp.

...

Dragons:
>>> Controller from /webapp/AUGV/controller.py
    - Will handle the AGENT_QUEUES and responsible to @create_agent()
    - This is to make sure that we are using the right config for the server runnning.
    - Avoiding any bottleneck and always making sure that the server run for performance.
>>> Mixin from /webapp/AUGV/obstacle.py
    - The base class for all agents.
    - it will handle the obstacle detection and send the blocked offsets to Unity
    - it will also bypassing the obstacle detection if the agent is not using YOLO
    - This is important for sending camera from unity to the backend and display it back again to frontend webapp.
>>> [deprecated] _get_offset() from /webapp/AUGV/obstacle.py
    - This is the function to calculate the offset of the obstacle.
    - It is using the camera config from /webapp/tools/config.py
    - It is using the distance-based bias for dy.
    - It is using the numba to speed up the calculation.
    - In simple it will convert camera 3d viewpoint into 2d grid offset from agent/camera position,
        :dy: forward/backward
        :dx: left/right
>>> [New] _send_to_unity_feet() from /webapp/AUGV/obstacle.py
    - Immediately send the feet list to Unity.
    - Yolo (0, 0) is top left.
    - feet_x => center_x
    - feet_y => center_y + half_det_height
    -> Will result in the middle and very bottom of bbox detections.
    - And we let unity to decide the offset from the feet list using RayCast.
"""

from webapp.tools.config import CONFIG
from ultralytics import YOLO
import threading, queue, numpy as np, math, asyncio
from collections import defaultdict
from numba import njit
import multiprocessing
from webapp.tools.config import CONFIG, get_onnx_session
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
        
        self._running = True
    
    def stop(self):
        """ Graceful stop method """
        self._running = False
        if hasattr(self, 'q') and self.q:
            try:
                self.q.put(None, timeout=1)
            except:
                pass

    def _process_frame(self, frame):
        detections = []
        blocked_offsets = set()
        feet_list = []

        if not self.use_yolo:
            return detections, blocked_offsets, feet_list
        if self.onnx:
            if not hasattr(self, 'ort_sess') or not hasattr(self, 'input_name'):
                raise ValueError("ORT session and input name must be set for onnx")
            orig_h, orig_w = frame.shape[:2]
            image, ratio, (dw, dh) = self._preprocess_onnx_image(frame)
            outputs = self.ort_sess.run(None, {self.input_name: image}) # type: ignore[attr-defined]
            detections, blocked_offsets, feet_list = self._postprocess_onnx(outputs, 640, 640, ratio, dw, dh, orig_w, orig_h)
        else:
            if not hasattr(self, 'model'):
                raise ValueError("YOLO model must be set for pt inference")
            conf_thres = CONFIG.get('CONF_THRES', 0.6)
            image = np.ascontiguousarray(frame)
            img_h, img_w = image.shape[:2]
            res = list(self.model.predict(image, conf=conf_thres, verbose=False, stream=True))[0] # type: ignore[attr-defined]

            detections, blocked_offsets, feet_list = self._postprocess_pt(res, img_h, img_w)
        
        blocked_offsets = set([b for b in blocked_offsets if b is not None])

        return detections, blocked_offsets, feet_list

    def _postprocess_pt(self, res, img_h, img_w):
        detections = []
        blocked_offsets = set()
        feet_list = []
        for box in getattr(res, "boxes", []):
            if self.model.names[int(box.cls[0])] != "person":
                continue
            xywh = box.xywh[0].tolist()
            x, y, w, h = xywh
            feet_x = x
            feet_y = y + h/2
            feet_list.append((feet_x, feet_y) if feet_x is not None and feet_y is not None else None)
            # dx, dy = _get_offset(feet_x, feet_y, img_w, img_h, h)
            # blocked_offsets.add((dx, dy) if dx is not None and dy is not None and dy > 0 and dy <= 5 else None)
            detections.append({
                "label": "person",
                "confidence": round(float(box.conf[0]), 3),
                "bbox": [round(v, 2) for v in xywh],
                "feet": [feet_x, feet_y],
                # "offset": [dx, dy]
            })
        return detections, blocked_offsets, feet_list
    
    def _postprocess_onnx(self, outputs, img_w, img_h, ratio, dw, dh, orig_w, orig_h):
        detections = []
        blocked_offsets = set()
        feet_list = []
        conf_thres = CONFIG.get('CONF_THRES', 0.6)
        output = np.squeeze(outputs[0]).T
        for det in output:
            cls_id = det[4:].argmax()
            conf_score = det[4:].max()
            if cls_id != 0 or conf_score < conf_thres:
                continue
            x, y, w, h = det[:4]

            # Scale the bounding box to the original image size
            x_mapped = (x - dw) / ratio
            y_mapped = (y - dh) / ratio
            w_mapped = w / ratio
            h_mapped = h / ratio
            x_mapped = np.clip(x_mapped, 0, orig_w)
            y_mapped = np.clip(y_mapped, 0, orig_h)
            w_mapped = np.clip(w_mapped, 0, orig_w)
            h_mapped = np.clip(h_mapped, 0, orig_h)

            feet_x = x_mapped
            feet_y = y_mapped + h_mapped/2
            feet_list.append((feet_x, feet_y) if feet_x is not None and feet_y is not None else None)
            # dx, dy = _get_offset(feet_x, feet_y, orig_w, orig_h, h_mapped)
            # blocked_offsets.add((dx, dy) if dx is not None and dy is not None and dy > 0 and dy <= 5 else None)
            detections.append({
                "label": "person",
                "confidence": round(float(conf_score), 3),
                "bbox": [round(float(v), 2) for v in [x_mapped, y_mapped, w_mapped, h_mapped]],
                "feet": [float(feet_x), float(feet_y)],
                # "offset": [int(dx), int(dy)]
            })
        return detections, blocked_offsets, feet_list

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
        ## DEBUG
        # cv2.imwrite("letterbox.jpg", img)
        return img, ratio, (dw, dh)
    
    def _send_to_unity(self, agent_id, blocked):
        """ Deprecated: Use _send_to_unity_feet instead """
        if self.last_detection == blocked:
            return
        _send_to_unity(agent_id, blocked)
        self.last_detection = blocked.copy()
    
    def _send_to_unity_feet(self, agent_id, feet_list):
        # TODO: test if this is needed or not.
        if self.last_detection == feet_list:
            return
        
        try:
            AGENT_OUT_QUEUES[agent_id].put_nowait({
                "action": "obstacle",
                "data": {
                    "agent_id": agent_id,
                    "feet": feet_list
                }
            })
            self.last_detection = feet_list.copy()
        except Exception as e:
            print(f"Error sending to Unity for agent {agent_id}: {e}")


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
        
        while self._running:
            try:
                frame = self.q.get()
                if frame is None:
                    continue
                detections, blocked_offsets, feet_list = self._process_frame(frame)
                
                if blocked_offsets:
                    """ Deprecated: use _send_to_unity_feet instead """
                    self._send_to_unity(self.agent_id, blocked_offsets)
                
                if feet_list:
                    self._send_to_unity_feet(self.agent_id, feet_list)
                
                AGENT_STATE[self.agent_id] = {
                    "status": "blocked" if blocked_offsets else "safe",
                    "detections": detections,
                    "blocked_offsets": [offset for offset in blocked_offsets if offset is not None]
                }
            except queue.Empty:
                continue
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
        
        while self._running:
            try:
                frame = self.q.get()
                if frame is None:
                    break

                detections, blocked_offsets, feet_list = self._process_frame(frame)

                if blocked_offsets:
                    """ Deprecated: Use _send_to_unity_feet instead """
                    self._send_to_unity(self.agent_id, blocked_offsets)
                
                if feet_list:
                    self._send_to_unity_feet(self.agent_id, feet_list)
                
                AGENT_STATE[self.agent_id] = {
                    "status": "blocked" if blocked_offsets else "safe",
                    "detections": detections,
                    "blocked_offsets": [offset for offset in blocked_offsets if offset is not None]
                }
            except queue.Empty:
                continue
            except Exception as e:
                print(f"Error in AgentYoloMP for agent {self.agent_id}: {e}")
                AGENT_STATE[self.agent_id]['status'] = 'error'
                break
        

#### ONNX
class AUGVOnnx(threading.Thread, AUGVMixin):
    def __init__(self, agent_id):
        super().__init__(daemon=True)
        self._populate_data(agent_id, onnx=True, mp=False)
        self.ort_sess = get_onnx_session(CONFIG['MODEL_NAME'], CONFIG.get('BACKEND_DEVICE', 'cpu'))
        self.input_name = self.ort_sess.get_inputs()[0].name

    def run(self):
        while self._running:
            try:
                frame = self.q.get()
                if frame is None:
                    continue

                detections, blocked_offsets, feet_list = self._process_frame(frame)

                if blocked_offsets:
                    """ Deprecated: Use _send_to_unity_feet instead """
                    self._send_to_unity(self.agent_id, blocked_offsets)
                
                if feet_list:
                    self._send_to_unity_feet(self.agent_id, feet_list)

                AGENT_STATE[self.agent_id] = {
                    "status": "blocked" if blocked_offsets else "safe",
                    "detections": detections,
                    "blocked_offsets": list(blocked_offsets)
                }
            except queue.Empty:
                continue
            except Exception as e:
                print(f"Error in AgentOnnx for agent {self.agent_id}: {e}")
                AGENT_STATE[self.agent_id]['status'] = 'error'
                break
        

class AUGVOnnxMP(multiprocessing.Process, AUGVMixin):
    def __init__(self, agent_id):
        super().__init__(daemon=True)
        self._populate_data(agent_id, onnx=True, mp=True)
        self.ort_sess = None
        self.input_name = None
    
    def run(self):
        self.ort_sess = get_onnx_session(CONFIG['MODEL_NAME'], CONFIG.get('BACKEND_DEVICE', 'cpu'))
        self.input_name = self.ort_sess.get_inputs()[0].name

        while self._running:        
            try:
                frame = self.q.get()
                if frame is None:
                    break
                
                detections, blocked_offsets, feet_list = self._process_frame(frame)
                
                if blocked_offsets:
                    """ Deprecated: Use _send_to_unity_feet instead """
                    self._send_to_unity(self.agent_id, blocked_offsets)
                
                if feet_list:
                    self._send_to_unity_feet(self.agent_id, feet_list)

                AGENT_STATE[self.agent_id] = {
                    "status": "blocked" if blocked_offsets else "safe",
                    "detections": detections,
                    "blocked_offsets": list(blocked_offsets)
                }
            except queue.Empty:
                continue
            except Exception as e:
                print(f"Error in AgentOnnxMP for agent {self.agent_id}: {e}")
                AGENT_STATE[self.agent_id]['status'] = 'error'
                break

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

#@debounce(1)
def _send_to_unity(agent_id, blocked):
    # valid = [offset for offset in blocked if offset is not None and offset[0] == 0 and offset[1] != 0]
    valid = [offset for offset in blocked if offset is not None and offset[1] != 0]
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
    except Exception as e:
        print(f"Error sending to Unity for agent {agent_id}: {e}")


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
    dx = int(round(world_x + bias) / _grid_size) # Left/Right
    return dx, dy

