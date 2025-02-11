import speech_recognition as sr
import pyttsx3
import requests
import json

recognizer = sr.Recognizer()


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

print(listen_for_command())