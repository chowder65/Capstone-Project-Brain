import speech_recognition as sr
import pyttsx3
import requests
import json

url = "http://localhost:1234/v1/chat/completions"

recognizer = sr.Recognizer()
tts_engine = pyttsx3.init()

trigger_keyword = "assistant"

def listen_for_command():
    with sr.Microphone() as source:
        print("Listening for your command...")
        audio = recognizer.listen(source)

        try:
            text = recognizer.recognize_google(audio).lower()
            print(f"Recognized text: {text}")
            return text
        except sr.UnknownValueError:
            print("Could not understand audio")
            return None
        except sr.RequestError:
            print("Speech Recognition service unavailable")
            return None

def ask_openai(question):
    payload = {
    "model": "llama-3.2-1b-instruct",
    "messages": [{"role": "user", "content": question}],
    "temperature": 0.7
}
    x = requests.post(url, json=payload)

    response = json.loads(x.content.decode('utf-8'))

    message_content = response['choices'][0]['message']['content']
    return message_content

def speak_response(response_text):
    print(f"Response: {response_text}")
    tts_engine.say(response_text)
    tts_engine.runAndWait()

while True:
    command = listen_for_command()

    if command and trigger_keyword in command:
        question = command.replace(trigger_keyword, "").strip()

        response = ask_openai(question)

        speak_response(response)