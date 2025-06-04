import time
import asyncio
from fastapi import FastAPI
from fastapi.responses import StreamingResponse
import logging

app = FastAPI()
logger = logging.getLogger("uvicorn.error")

@app.get("/sse")
async def sse():
    async def event_generator():
        start = time.time()
        count = 0
        while time.time() - start < 10:
            count += 1
            message = f"data: message {count}\n\n"
            logger.info(f"Sending message {count}")
            yield message
            await asyncio.sleep(1)

    return StreamingResponse(event_generator(), media_type="text/event-stream")