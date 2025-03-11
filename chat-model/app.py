import torch
from transformers import BertTokenizer, BertForSequenceClassification
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
from llama_cpp import Llama
import logging
from typing import List, Optional
import pika
import json
import threading
import time

logging.basicConfig(level=logging.DEBUG)
logger = logging.getLogger(__name__)

app = FastAPI()

logger.info("Loading emotion detection model...")
tokenizer = BertTokenizer.from_pretrained("./emotion_model")
model = BertForSequenceClassification.from_pretrained("./emotion_model")
model.eval()
emotion_labels = ["happy", "sad", "angry", "neutral", "surprise"]
logger.info("Emotion model loaded successfully")

# Load the Llama model
logger.info("Loading GGUF model...")
llm = Llama(
    model_path="./models/airoboros-mistral2.2-7b.Q4_K_S.gguf",
    n_ctx=2048,
    n_threads=8,
)
logger.info("Model loaded successfully")

class PastMessage(BaseModel):
    user: str
    assistant: str

class ChatRequest(BaseModel):
    prompt: Optional[str] = None
    past_messages: Optional[List[PastMessage]] = None 
    new_message: str

def generate_response(request: ChatRequest) -> dict:
    try:
        detected_emotion = detect_emotion(request.new_message)
        logger.info(f"Detected emotion from '{request.new_message}': {detected_emotion}")

        input_lines = []
        if request.prompt:
            input_lines.append(f"### Prompt: {request.prompt}")
        if request.past_messages:
            input_lines.append("### Past Messages:")
            for msg in request.past_messages:
                input_lines.append(f"User: {msg.user}")
                input_lines.append(f"Assistant: {msg.assistant}")
        input_lines.append(f"### New Message: {request.new_message}")
        input_lines.append(f"### Detected Emotion: {detected_emotion}")
        input_text = "\n".join(input_lines) + "\n### Response:"

        logger.info(f"Input to model: {input_text}")

        output = llm(
            input_text,
            max_tokens=200,
            stop=["</s>", "###"],
            echo=False,
            temperature=0.7,
            top_k=40,
            top_p=0.9,
        )
        response = output["choices"][0]["text"].strip()

        if response.startswith("### Response:"):
            response = response[len("### Response:"):].strip()

        logger.info(f"Raw response: '{response}'")
        if not response or len(response) < 3:
            logger.warning("Response too short, using fallback")
            response = f"I noticed you're feeling {detected_emotion}. How can I assist?"

        logger.info(f"Final response: '{response}'")
        return {"response": response, "detected_emotion": detected_emotion}
    except Exception as e:
        logger.error(f"Error generating response: {str(e)}")
        return {"response": "Sorry, something went wrong!", "detected_emotion": "neutral"}


def detect_emotion(text: str) -> str:
    inputs = tokenizer(text, return_tensors="pt", padding=True, truncation=True, max_length=128)
    with torch.no_grad():
        outputs = model(**inputs)
        prediction = torch.argmax(outputs.logits, dim=1).item()
    return emotion_labels[prediction]

@app.post("/chat")
async def chat_endpoint(request: ChatRequest):
    result = generate_response(request)
    return result

@app.get("/health")
async def health_check():
    return {"status": "healthy"}

def connect_with_retry():
    max_retries = 10
    retry_delay = 5
    for attempt in range(max_retries):
        try:
            connection = pika.BlockingConnection(pika.ConnectionParameters('rabbitmq', credentials=pika.PlainCredentials('guest', 'guest')))
            return connection
        except pika.exceptions.AMQPConnectionError as e:
            logger.error(f"Failed to connect to RabbitMQ (attempt {attempt + 1}/{max_retries}): {e}")
            if attempt < max_retries - 1:
                time.sleep(retry_delay)
            else:
                raise

def process_message(ch, method, properties, body):
    try:
        chat_request_dict = json.loads(body.decode())
        chat_request = ChatRequest(**chat_request_dict) 
        result = generate_response(chat_request)
        ai_response = result["response"]
        detected_emotion = result["detected_emotion"]

        response_payload = {
            "response": ai_response,
            "detected_emotion": detected_emotion
        }

        reply_props = pika.BasicProperties(correlation_id=properties.correlation_id)
        ch.basic_publish(
            exchange="",
            routing_key=properties.reply_to,
            properties=reply_props,
            body=json.dumps(response_payload).encode()
        )
        ch.basic_ack(delivery_tag=method.delivery_tag)
        logger.info(f"Sent response via RabbitMQ: {ai_response} with emotion: {detected_emotion}")
    except Exception as e:
        logger.error(f"Error processing RabbitMQ message: {e}")
        ch.basic_nack(delivery_tag=method.delivery_tag, requeue=False)


def start_consumer():
    connection = connect_with_retry()
    channel = connection.channel()
    channel.queue_declare(queue='llm_queue', durable=True)
    channel.basic_qos(prefetch_count=1)
    channel.basic_consume(queue='llm_queue', on_message_callback=process_message)
    logger.info("Starting RabbitMQ consumer...")
    channel.start_consuming()

def run_consumer_in_background():
    consumer_thread = threading.Thread(target=start_consumer, daemon=True)
    consumer_thread.start()
    logger.info("RabbitMQ consumer thread started")

if __name__ == "__main__":
    test_responses = [
        "I’m so thrilled to assist you today!",
        "I feel a bit down about this.",
        "This glitch is driving me nuts!",
        "I’m just here to chat with you.",
        "Wow, I didn’t expect that at all!"
    ]
    print("Testing the bot’s emotional model at startup...")
    for response in test_responses:
        emotion = detect_emotion(response)
        print(f"Test Input: '{response}' -> Detected Emotion: {emotion}")

    run_consumer_in_background()

    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8000)