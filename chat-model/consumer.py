import pika
import json
import time
import requests

def connect_with_retry():
    max_retries = 10
    retry_delay = 5
    for attempt in range(max_retries):
        try:
            connection = pika.BlockingConnection(pika.ConnectionParameters('rabbitmq', credentials=pika.PlainCredentials('guest', 'guest')))
            return connection
        except pika.exceptions.AMQPConnectionError as e:
            print(f"Failed to connect to RabbitMQ (attempt {attempt + 1}/{max_retries}): {e}")
            if attempt < max_retries - 1:
                time.sleep(retry_delay)
            else:
                raise

def process_message(ch, method, properties, body):
    chat_request = json.loads(body.decode())
    
    try:
        response = requests.post("http://localhost:8001/chat", json=chat_request)
        ai_response = response.json().get("response", "Error processing message") if response.ok else "Error processing message"
    except requests.RequestException as e:
        print(f"Error calling FastAPI: {e}")
        ai_response = "Sorry, something went wrong!"

    reply_props = pika.BasicProperties(correlation_id=properties.correlation_id)
    ch.basic_publish(
        exchange="",
        routing_key=properties.reply_to,
        properties=reply_props,
        body=ai_response.encode()
    )
    ch.basic_ack(delivery_tag=method.delivery_tag)
    print(f"Sent response: {ai_response}")

def start_consumer():
    connection = connect_with_retry()
    channel = connection.channel()
    channel.queue_declare(queue='llm_queue', durable=True)
    channel.basic_qos(prefetch_count=1)
    channel.basic_consume(queue='llm_queue', on_message_callback=process_message)
    print("Starting RabbitMQ consumer...")
    channel.start_consuming()

if __name__ == "__main__":
    start_consumer()