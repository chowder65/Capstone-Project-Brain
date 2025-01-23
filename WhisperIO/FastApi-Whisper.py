from fastapi import FastAPI, File, UploadFile
from fastapi.responses import JSONResponse
from pydantic import BaseModel
import uvicorn
from tts_service import text_to_speech
from stt_service import transcribe_audio
import os

app = FastAPI()

UPLOAD_DIR = "uploaded_audio"
os.makedirs(UPLOAD_DIR, exist_ok=True)

class TTSRequest(BaseModel):
    text: str

@app.post("/stt/")
async def speech_to_text(file: UploadFile = File(...)):
    try:
        file_path = os.path.join(UPLOAD_DIR, file.filename)
        with open(file_path, "wb") as f:
            f.write(await file.read())
        
        transcription = transcribe_audio(file_path)
        return JSONResponse(content={"transcription": transcription})
    except Exception as e:
        return JSONResponse(content={"error": str(e)}, status_code=500)

@app.post("/tts/")
async def text_to_speech_endpoint(request: TTSRequest):
    try:
        audio_path = text_to_speech(request.text)
        return JSONResponse(content={"audio_file": audio_path})
    except Exception as e:
        return JSONResponse(content={"error": str(e)}, status_code=500)

if __name__ == "__main__":
    uvicorn.run(app, host="0.0.0.0", port=8000)