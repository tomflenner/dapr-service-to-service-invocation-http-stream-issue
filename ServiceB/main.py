from fastapi import FastAPI
from fastapi.responses import StreamingResponse
import httpx
import asyncio
import logging

app = FastAPI()
logger = logging.getLogger("uvicorn.error")
SSE_URL = "http://service-c:9000/sse"

@app.get("/proxy-stream")
async def proxy_stream():
    async def event_stream():
        async with httpx.AsyncClient(timeout=None) as client:
            async with client.stream("GET", SSE_URL) as sse:
                headers_str = ", ".join(f"{k}={v}" for k, v in sse.headers.items())
                logger.info(f"SSE response headers: {headers_str}")

                async for chunk in sse.aiter_bytes():
                    if not chunk:
                        break
                    logger.info(f"Proxying chunk: {chunk!r}")
                    yield chunk
                    await asyncio.sleep(0)

    return StreamingResponse(event_stream(), media_type="text/event-stream")

