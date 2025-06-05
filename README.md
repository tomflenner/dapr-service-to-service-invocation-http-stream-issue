# Dapr Service Invocation Streaming Regression - Proof of Concept

This repository showcases a potential **regression in Dapr's service-to-service invocation API** when handling streaming over HTTP/1.1 using `Transfer-Encoding: chunked`.

## 🧩 Architecture

The proof-of-concept (POC) involves **three services**:

- **Service A**: .NET service that consumes a stream via HTTP/1.1 (`Transfer-Encoding: chunked`).
- **Service B**: Python service that relays an incoming stream (via `text/event-stream`) to its consumer (via `text/plain`).
- **Service C**: Python "fake producer" that emits streaming data using HTTP/1.1 with chunked encoding.

### 🔁 Streaming Pipeline
```
Service C (fake producer)
└──[chunked text/event-stream]──▶
Service B (relay)
└──[chunked transfer text/plain]──▶
Service A (consumer)
```

## 🎯 Purpose
The POC demonstrates that:

- ✅ When **calling services directly** using a standard HTTP client, streaming works as expected — data arrives **chunk-by-chunk**.
- ❌ When the **same communication is routed through the Dapr sidecar**, the `Transfer-Encoding: chunked` is **lost** or **buffered**, and data arrives **only at the end** of the request — breaking streaming behavior.

This suggests that Dapr's invocation API might be buffering or mishandling chunked streams.

## 🚀 Getting Started

### Prerequisites

- [Docker](https://www.docker.com/)
- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/)
- [Dapr Runtime](https://docs.dapr.io/getting-started/install-dapr-selfhost/)

### Build & Run

From the root of the repository:
```bash
docker compose build
docker compose up -d
```

### ✅ Reproduce Working Streaming (Without Dapr)
Test the streaming pipeline directly using:
```bash
curl -v --no-buffer http://localhost:7500/start-non-dapr-stream
```

https://github.com/user-attachments/assets/2ffd6175-652a-4127-8cdd-b147cebc2e16

You should observe data arriving chunk-by-chunk.

![non-dapr-schema.png](images/non-dapr-schema.png)

### ❌ Reproduce Streaming Regression (With Dapr)
Test the same pipeline via Dapr service invocation:

```bash
curl -v --no-buffer http://localhost:7500/start-dapr-stream
```

https://github.com/user-attachments/assets/9b64ece8-e739-468c-a13e-30f04b64cf89

You will notice that:
- No chunks are received progressively.
- Data is buffered and arrives all at once, only after the stream ends.

![dapr-schema.png](images/dapr-schema.png)

This illustrates a potential bug or unintended behavior in Dapr’s service-to-service invocation handling.

### 🧪 Notes
The HTTP headers used in the POC include Transfer-Encoding: chunked and Content-Type: text/event-stream.

⚠️ This POC is for demonstration and debugging purposes only. It is not intended for production use.
