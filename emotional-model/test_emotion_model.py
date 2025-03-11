import torch
from transformers import BertTokenizer, BertForSequenceClassification

tokenizer = BertTokenizer.from_pretrained("./emotion_model")
model = BertForSequenceClassification.from_pretrained("./emotion_model")
model.eval()
emotion_labels = ["happy", "sad", "angry", "neutral", "surprise"]

def detect_emotion(text: str) -> str:
    inputs = tokenizer(text, return_tensors="pt", padding=True, truncation=True, max_length=128)
    
    with torch.no_grad():
        outputs = model(**inputs)
        prediction = torch.argmax(outputs.logits, dim=1).item()
    
    return emotion_labels[prediction]

test_responses = [
    "I’m so thrilled to assist you today!",
    "This chat is making me so happy!",
    "I’m loving every moment of this!",
    "I feel so cheerful right now!",
    "I’m delighted to be your bot!",

    "I feel a bit down about this.",
    "I’m sad I couldn’t help more.",
    "I’m feeling pretty gloomy today.",
    "I’m sorry I’m not at my best.",
    "I feel so low right now.",

    "This glitch is driving me nuts!",
    "I’m so mad this isn’t working!",
    "I hate when things go wrong!",
    "I’m furious at this error!",
    "This is making me so angry!",

    "I’m just here to chat with you.",
    "It’s a normal day for me.",
    "I feel pretty even right now.",
    "I’m doing my usual thing.",
    "This is a standard response.",

    "Wow, I didn’t expect that at all!",
    "I’m shocked by this turn!",
    "This is totally wild to me!",
    "I can’t believe what just happened!",
    "I’m so surprised right now!"
]

print("Testing the bot’s emotional model...")
for response in test_responses:
    emotion = detect_emotion(response)
    print(f"Bot Response: '{response}' -> Bot Emotion: {emotion}")