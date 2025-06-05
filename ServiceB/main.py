import asyncio
import logging
import httpx
import grpc
from concurrent import futures

# Import the generated classes
import proxy_streamer_pb2
import proxy_streamer_pb2_grpc

logger = logging.getLogger(__name__)
logging.basicConfig(level=logging.INFO)

SSE_URL = "http://service-c:9000/sse" # Or your actual internal/external URL

class ProxyStreamerServicer(proxy_streamer_pb2_grpc.ProxyStreamerServicer):
    async def GetStream(self, request, context):
        """
        Implements the server-streaming RPC method.
        Connects to an SSE stream and forwards data to the gRPC client.
        """
        logger.info("gRPC GetStream called")
        try:
            async with httpx.AsyncClient(timeout=None) as client:
                async with client.stream("GET", SSE_URL) as sse:
                    headers_str = ", ".join(f"{k}={v}" for k, v in sse.headers.items())
                    logger.info(f"SSE response headers: {headers_str}")

                    async for line in sse.aiter_lines():
                        logger.info(f"SSE Received line: {line}")
                        if line.startswith("data: "):
                            data_to_send = line[6:]
                            logger.info(f"gRPC Sending data: {data_to_send}")
                            yield proxy_streamer_pb2.StreamResponse(data=data_to_send)
                            # Small sleep to allow event loop to process, similar to original.
                            # May not be strictly necessary in all gRPC contexts but retained
                            # for closer parity with the original asyncio.sleep(0).
                            await asyncio.sleep(0.001) # Adjusted for gRPC context
        except httpx.RequestError as e:
            logger.error(f"Error connecting to SSE stream: {e}")
            context.set_code(grpc.StatusCode.UNAVAILABLE)
            context.set_details(f"Could not connect to SSE stream: {e}")
            # Optionally yield an error message or raise an exception recognized by gRPC
            # For a streaming RPC, simply ending the stream might be enough,
            # or you could yield a specific error message if your proto supports it.
        except Exception as e:
            logger.error(f"An unexpected error occurred in GetStream: {e}")
            context.set_code(grpc.StatusCode.INTERNAL)
            context.set_details(f"An internal error occurred: {e}")
        finally:
            logger.info("gRPC GetStream finished")


async def serve():
    server = grpc.aio.server(futures.ThreadPoolExecutor(max_workers=10))
    proxy_streamer_pb2_grpc.add_ProxyStreamerServicer_to_server(
        ProxyStreamerServicer(), server
    )
    listen_addr = '[::]:50051' # Standard gRPC port
    server.add_insecure_port(listen_addr)
    logger.info(f"Starting gRPC server on {listen_addr}")
    await server.start()
    try:
        await server.wait_for_termination()
    except KeyboardInterrupt:
        logger.info("gRPC server shutting down...")
        await server.stop(0)

if __name__ == '__main__':
    asyncio.run(serve())