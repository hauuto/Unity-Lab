FROM python:3.11-slim
WORKDIR /app
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt
COPY app.py detector.py ./
COPY --from=model yolo26n.onnx model/yolo26n.onnx
RUN useradd --create-home appuser
USER appuser
EXPOSE 5000
HEALTHCHECK --interval=15s --timeout=3s --start-period=20s CMD python -c "import urllib.request as u; u.urlopen('http://localhost:5000/health')"
# 1 worker + 4 thread: ONNX Runtime goi dong thoi an toan, /metrics khong phai gop so lieu nhieu process.
CMD ["gunicorn", "--bind", "0.0.0.0:5000", "--workers", "1", "--threads", "4", "--timeout", "60", "app:app"]
