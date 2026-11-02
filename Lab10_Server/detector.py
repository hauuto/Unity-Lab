"""Chay YOLO26n (ONNX) bang ONNX Runtime. Khong phu thuoc Flask de test rieng duoc."""
import ast
import io

import numpy as np
import onnxruntime as ort
from PIL import Image

INPUT_SIZE = 640  # model nhan anh co dinh [1, 3, 640, 640]
IOU_THRESHOLD = 0.5  # hai box de nhau hon muc nay thi coi la cung mot vat


def _iou(box, boxes):
    x1 = np.maximum(box[0], boxes[:, 0])
    y1 = np.maximum(box[1], boxes[:, 1])
    x2 = np.minimum(box[2], boxes[:, 2])
    y2 = np.minimum(box[3], boxes[:, 3])
    inter = np.clip(x2 - x1, 0, None) * np.clip(y2 - y1, 0, None)
    area = (box[2] - box[0]) * (box[3] - box[1])
    areas = (boxes[:, 2] - boxes[:, 0]) * (boxes[:, 3] - boxes[:, 1])
    return inter / np.maximum(area + areas - inter, 1e-6)


class Detector:
    def __init__(self, model_path):
        self.session = ort.InferenceSession(model_path, providers=["CPUExecutionProvider"])
        self.input_name = self.session.get_inputs()[0].name
        meta = self.session.get_modelmeta().custom_metadata_map
        self.names = ast.literal_eval(meta["names"])  # {0: 'person', ...} do Ultralytics ghi

    def detect(self, image_bytes, min_confidence=0.25, max_detections=20):
        image = Image.open(io.BytesIO(image_bytes)).convert("RGB")
        width, height = image.size
        resized = image.resize((INPUT_SIZE, INPUT_SIZE), Image.BILINEAR)
        tensor = np.asarray(resized, dtype=np.float32).transpose(2, 0, 1)[None] / 255.0

        # Output [1, 84, 8400]: cx, cy, w, h (pixel 640) + 80 diem lop.
        output = self.session.run(None, {self.input_name: tensor})[0][0]
        scores = output[4:]
        class_ids = scores.argmax(axis=0)
        confidences = scores.max(axis=0)
        keep = confidences >= min_confidence
        if not keep.any():
            return width, height, []

        cx, cy, w, h = output[:4, keep]
        # Anh bi keo gian ve 640x640, nen nhan lai theo tung truc.
        sx, sy = width / INPUT_SIZE, height / INPUT_SIZE
        boxes = np.stack([(cx - w / 2) * sx, (cy - h / 2) * sy,
                          (cx + w / 2) * sx, (cy + h / 2) * sy], axis=1)
        boxes[:, [0, 2]] = boxes[:, [0, 2]].clip(0, width)
        boxes[:, [1, 3]] = boxes[:, [1, 3]].clip(0, height)
        confidences, class_ids = confidences[keep], class_ids[keep]

        # Greedy NMS theo tung lop: Lab 08 chi lay 1 box nen khong can, API tra nhieu box nen can.
        detections = []
        order = confidences.argsort()[::-1]
        while order.size and len(detections) < max_detections:
            best = order[0]
            detections.append({
                "label": self.names[int(class_ids[best])],
                "confidence": round(float(confidences[best]), 4),
                "x1": int(round(boxes[best, 0])), "y1": int(round(boxes[best, 1])),
                "x2": int(round(boxes[best, 2])), "y2": int(round(boxes[best, 3])),
            })
            rest = order[1:]
            same_class = class_ids[rest] == class_ids[best]
            overlap = _iou(boxes[best], boxes[rest]) > IOU_THRESHOLD
            order = rest[~(same_class & overlap)]
        return width, height, detections
