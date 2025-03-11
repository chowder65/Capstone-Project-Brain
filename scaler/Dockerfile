FROM python:3.9-slim

WORKDIR /app

RUN apt-get update && apt-get install -y \
    docker.io \
    && rm -rf /var/lib/apt/lists/*

RUN pip install requests docker-compose

COPY scaler/scale.py scale.py
COPY docker-compose.yaml docker-compose.yaml

CMD ["python", "scale.py"]