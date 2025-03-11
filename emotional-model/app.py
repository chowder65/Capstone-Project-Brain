from fastapi import FastAPI, HTTPException
import torch
from transformers import BertTokenizer, BertForSequenceClassification

app = FastAPI()

tokenizer = BertTokenizer.from_pretrained("./stateful_emotion_response_model")
model = BertForSequenceClassification.from_pretrained("./stateful_emotion_response_model")
emotion_labels = ["happy", "sad", "shy", "pumped", "excited", "anxious", "grateful", "angry"]
previous_emotion = "neutral"

@app.post("/emotion")
async def get_emotion(message: str):
    global previous_emotion
    try:
        inputs = tokenizer(message, return_tensors="pt", truncation=True, padding="max_length", max_length=128)
        prev_emotion_idx = torch.tensor([emotion_labels.index(previous_emotion)])
        inputs["previous_emotion"] = prev_emotion_idx.unsqueeze(-1).float()

        model.eval()
        with torch.no_grad():
            outputs = model(**inputs)
            predicted_idx = torch.argmax(outputs.logits, dim=-1).item()
        response = emotion_labels[predicted_idx]
        previous_emotion = response
        return {"emotion": response}
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

@app.get("/health")
async def health_check():
    return {"status": "healthy"}