import requests
import subprocess
import time
import os

# RabbitMQ Management API settings (use localhost since running on host)
RABBITMQ_URL = "http://localhost:15672/api/queues/%2F/llm_queue"
RABBITMQ_USER = "guest"
RABBITMQ_PASS = "guest"

# Scaling thresholds
LLM_MIN_INSTANCES = 1
LLM_MAX_INSTANCES = 5
LLM_SCALE_UP_THRESHOLD = 100
LLM_SCALE_DOWN_THRESHOLD = 10

USERAPI_MIN_INSTANCES = 1
USERAPI_MAX_INSTANCES = 5
USERAPI_SCALE_UP_THRESHOLD = 100
USERAPI_SCALE_DOWN_THRESHOLD = 10

# Project directory on host
PROJECT_DIR = r"C:\Users\mccul\Documents\GitHub\Capstone-Project-Brain"

def get_queue_length(queue_name="llm_queue"):
    try:
        response = requests.get(
            f"http://localhost:15672/api/queues/%2F/{queue_name}",
            auth=(RABBITMQ_USER, RABBITMQ_PASS)
        )
        response.raise_for_status()
        return response.json().get("messages", 0)
    except requests.RequestException as e:
        print(f"Error fetching queue length: {e}")
        return 0

def get_current_instances(service):
    try:
        result = subprocess.run(
            ["docker-compose", "-f", "docker-compose.yaml", "ps", "-q", service],
            cwd=PROJECT_DIR,
            capture_output=True,
            text=True,
            check=True
        )
        return len(result.stdout.strip().splitlines())
    except subprocess.CalledProcessError as e:
        print(f"Error checking instances: {e}")
        return 1

def scale_service(service, target_instances):
    print(f"Scaling {service} to {target_instances} instances...")
    subprocess.run(
        ["docker-compose", "-f", "docker-compose.yaml", "up", "-d", "--scale", f"{service}={target_instances}", "--no-recreate"],
        cwd=PROJECT_DIR,
        check=True
    )

def manage_scaling():
    while True:
        llm_queue_length = get_queue_length("llm_queue")
        current_llm_instances = get_current_instances("llm")
        print(f"LLM Queue: {llm_queue_length}, Instances: {current_llm_instances}")

        if llm_queue_length > LLM_SCALE_UP_THRESHOLD and current_llm_instances < LLM_MAX_INSTANCES:
            scale_service("llm", current_llm_instances + 1)
        elif llm_queue_length < LLM_SCALE_DOWN_THRESHOLD and current_llm_instances > LLM_MIN_INSTANCES:
            scale_service("llm", current_llm_instances - 1)

        userapi_queue_length = get_queue_length("userapi_queue")
        current_userapi1_instances = get_current_instances("userapi1")
        current_userapi2_instances = get_current_instances("userapi2")
        total_userapi_instances = current_userapi1_instances + current_userapi2_instances
        print(f"UserAPI Queue: {userapi_queue_length}, Instances: {total_userapi_instances}")

        if userapi_queue_length > USERAPI_SCALE_UP_THRESHOLD and total_userapi_instances < USERAPI_MAX_INSTANCES:
            scale_service("userapi1", current_userapi1_instances + 1)
        elif userapi_queue_length < USERAPI_SCALE_DOWN_THRESHOLD and total_userapi_instances > USERAPI_MIN_INSTANCES:
            scale_service("userapi1", current_userapi1_instances - 1)

        time.sleep(30)

if __name__ == "__main__":
    os.chdir(PROJECT_DIR)
    subprocess.run(["docker-compose", "-f", "docker-compose.yaml", "up", "-d"], check=True)
    manage_scaling()