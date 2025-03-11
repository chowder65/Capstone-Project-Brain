import pika
connection = pika.BlockingConnection(pika.ConnectionParameters('localhost'))
channel = connection.channel()
channel.queue_declare(queue='llm_queue', durable=True)
for i in range(200):
    channel.basic_publish(exchange='', routing_key='llm_queue', body=f"Message {i}".encode())
connection.close()