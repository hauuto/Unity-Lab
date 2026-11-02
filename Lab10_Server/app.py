"""REST API nhan dien vat the. Chay production bang Gunicorn, xem Dockerfile."""
import os
import time

from flask import Flask, jsonify, request
from PIL import UnidentifiedImageError
from prometheus_client import Counter, Histogram
from prometheus_flask_exporter import PrometheusMetrics

from detector import Detector

MODEL_PATH = os.environ.get("MODEL_PATH", "model/yolo26n.onnx")
MAX_UPLOAD_MB = 10  # khop client_max_body_size trong nginx.conf

app = Flask(__name__)
app.config["MAX_CONTENT_LENGTH"] = MAX_UPLOAD_MB * 1024 * 1024
metrics = PrometheusMetrics(app)  # tao /metrics va so lieu request theo route

INFERENCE_SECONDS = Histogram("model_inference_seconds", "Thoi gian chay model")
DETECTIONS = Counter("model_detections_total", "So vat the nhan dien duoc", ["label"])

detector = Detector(MODEL_PATH)


@app.get("/health")
@metrics.do_not_track()
def health():
    return jsonify(status="ok", model=os.path.basename(MODEL_PATH))


@app.post("/predict")
def predict():
    file = request.files.get("image")
    if file is None:
        return jsonify(error="Thieu truong 'image' trong multipart/form-data"), 400
    try:
        min_confidence = float(request.form.get("min_confidence", 0.25))
    except ValueError:
        return jsonify(error="min_confidence phai la so"), 400
    if not 0 <= min_confidence <= 1:
        return jsonify(error="min_confidence phai nam trong 0..1"), 400

    started = time.perf_counter()
    try:
        width, height, detections = detector.detect(file.read(), min_confidence)
    except UnidentifiedImageError:
        return jsonify(error="File gui len khong phai anh hop le"), 400
    elapsed = time.perf_counter() - started

    INFERENCE_SECONDS.observe(elapsed)
    for item in detections:
        DETECTIONS.labels(item["label"]).inc()
    return jsonify(width=width, height=height,
                   inference_ms=round(elapsed * 1000, 1), detections=detections)


@app.errorhandler(413)
def too_large(_):
    return jsonify(error=f"Anh vuot qua {MAX_UPLOAD_MB} MB"), 413
