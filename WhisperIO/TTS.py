import speech_recognition as sr
import pyttsx3
import requests
import json

tts_engine = pyttsx3.init()

def speak_response(response_text):
    print(f"Response: {response_text}")
    tts_engine.say(response_text)
    tts_engine.runAndWait()



speak_response("Howdy! Partner!")