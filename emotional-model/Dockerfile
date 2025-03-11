FROM python:3.9
WORKDIR /app
COPY requirements.txt .
RUN pip install -r requirements.txt
COPY consumer.py .
COPY app.py .
COPY models/airoboros-mistral2.2-7b.Q4_K_S.gguf ./models/
COPY emotion_model/ ./emotion_model/
CMD ["sh", "-c", "python consumer.py & uvicorn app:app --host 0.0.0.0 --port 8001"]