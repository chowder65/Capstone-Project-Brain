FROM python:3.9-slim

WORKDIR /app

RUN apt-get update && apt-get install -y \
    gcc \
    g++ \
    git \
    && rm -rf /apt/lists/*

COPY training.py .
COPY app.py .

RUN pip install --no-cache-dir \
    torch \
    transformers \
    fastapi \
    uvicorn \
    pydantic \
    llama-cpp-python \
    pika

CMD ["python", "app.py"]