"""
YOLOv8 ONNX Export Script
------------------------
Edit the CONFIG section below to control export options.
Each option is documented with its purpose and a recommended value.
Just edit and run this script. It will overwrite yolov8n.onnx.
"""

from ultralytics import YOLO
import torch
import os

# =====================
# CONFIGURATION SECTION
# =====================
CONFIG = {
    # Path to the YOLOv8 PyTorch model (.pt)
    'model_path': 'yolov8n.pt',  # Path to your .pt file (default: yolov8n.pt)

    # Path to save the exported ONNX file
    'output_path': 'yolov8n.onnx',  # Output ONNX file (will overwrite if exists)

    # Image size for export (int or tuple)
    'imgsz': 640,  # Size of input images (default: 640). Must match your training/export size.

    # Use letterbox (aspect-ratio padding) or plain resize
    'letterbox': True,  # True = keep aspect ratio with padding (recommended), False = plain resize (may distort)

    # ONNX opset version
    'opset': 12,  # ONNX opset version (default: 12). Use 12+ for best compatibility.

    # Export with dynamic axes (variable image size)
    'dynamic': False,  # True = allow variable image sizes (not always supported), False = fixed size (recommended)

    # Device to use for export
    'device': 'cpu',  # 'cpu' or 'cuda'. Use 'cpu' for best compatibility.

    # Export with half precision (FP16)
    'half': False,  # True = export in FP16 (smaller, faster, but less compatible), False = FP32 (recommended)

    # Include NMS in ONNX graph
    'simplify': False,  # True = simplify ONNX graph (may break some models), False = standard export
    
    # Verbose logging
    'verbose': True,  # True = print more info during export
}

# =====================
#      EXPORT LOGIC
# =====================

def main():
    print("\n[INFO] Exporting YOLOv8 model to ONNX with config:")
    for k, v in CONFIG.items():
        print(f"  {k}: {v}")
    model = YOLO(CONFIG['model_path'])
    model.export(
        format='onnx',
        imgsz=CONFIG['imgsz'],
        opset=CONFIG['opset'],
        dynamic=CONFIG['dynamic'],
        device=CONFIG['device'],
        half=CONFIG['half'],
        simplify=CONFIG['simplify'],
        verbose=CONFIG['verbose'],
        # Letterbox is handled automatically by Ultralytics for ONNX export
        # If you want to force plain resize, set 'rect' to False (not exposed in export, only in predict)
        # For most use cases, leave as is
        # See: https://docs.ultralytics.com/modes/export/#onnx
    )
    # Move/rename output if needed
    if os.path.exists('yolov8n.onnx') and CONFIG['output_path'] != 'yolov8n.onnx':
        os.replace('yolov8n.onnx', CONFIG['output_path'])
    print(f"\n[INFO] Export complete. ONNX model saved to: {CONFIG['output_path']}")

if __name__ == "__main__":
    main() 