# GraspIt — AI-Powered Academic Assistant

![Project Status](https://img.shields.io/badge/Status-Active-success?style=for-the-badge)
![Framework](https://img.shields.io/badge/.NET-10.0-512BD4?style=for-the-badge&logo=dotnet)
![Database](https://img.shields.io/badge/PostgreSQL-16-336791?style=for-the-badge&logo=postgresql)
![AI](https://img.shields.io/badge/Semantic_Kernel-1.73-blue?style=for-the-badge)

GraspIt is an advanced academic ecosystem that leverages Retrieval-Augmented Generation (RAG) and structural document analysis to provide a contextual chat experience and high-fidelity automated exam generation.

---

## 🏗️ System Architecture

The diagram below illustrates the end-to-end data flow, service orchestration, and the high-fidelity document generation pipeline.

```mermaid
graph TD
    %% Global Styling and Font
    %%{init: {'theme': 'base', 'themeVariables': { 'fontFamily': 'Segoe UI, Inter, sans-serif', 'lineColor': '#64748b'}}}%%

    %% --- SUBGRAPHS ---

    subgraph Layer_UI ["🎨 Presentation & UI (ASP.NET Core MVC)"]
        direction LR
        View["🖥️ Razor Views & Glassmorphism UI"]
        JS["⚡ Client JS (Fetch API & Drag-and-Drop)"]
        Markdown["🖋️ marked.js (Markdown Engine)"]
    end

    subgraph Layer_APP ["🧠 Application Logic (.NET 10)"]
        direction LR
        Handlers["🛠️ MediatR (CQRS Handlers)"]
        State["🧠 ChatHistory (Persistent Context)"]
        Cache["💾 IMemoryCache (Semantic Caching)"]
        Core["🏗️ Domain Core (Entities/Interfaces)"]
    end

    subgraph Layer_SVC ["🛠️ Infrastructure Services"]
        direction LR
        SK["🤖 Semantic Kernel & AI Service"]
        Vec["🔍 VectorDbService & RagSearchService"]
        Extract["📄 Multi-Format Parser (PdfPig/Tesseract)"]
        Jobs["⚙️ Hangfire (Background Processing)"]
        Resilience["🛡️ Polly (Retry & Resilience)"]
    end

    subgraph Layer_INT ["🤖 Intelligence & APIs"]
        direction LR
        Local["🏠 Local Models (LM Studio/Ollama)"]
        Cloud["☁️ Cloud APIs (OpenAI/DeepSeek/Gemini)"]
        Embed["✨ Embeddings (Nomic-Embed 768d)"]
        Web["🌐 DuckDuckGo API (Web Context)"]
    end

    subgraph Layer_DB ["💾 Persistence & Data"]
        direction LR
        DB_Postgres[(🐘 PostgreSQL 16 + pgvector)]
        DB_EFCore["🗄️ EF Core 10 & Repositories"]
        DB_HNSW["⚡ HNSW Index (vector_cosine_ops)"]
        DB_FS["📁 Local File Storage (wwwroot/uploads)"]
    end

    subgraph Layer_EXP ["📄 Document Export Engine"]
        direction LR
        OpenXML["📦 OpenXML SDK (DOCX/PPTX)"]
        Quest["📑 QuestPDF (Native PDF)"]
        Latex["📐 LaTeX Builder"]
        RawMD["📝 Raw Markdown"]
    end

    %% --- MAIN ARCHITECTURE CONNECTIONS ---
    
    Layer_UI <-->|"API Calls / ViewModels"| Layer_APP
    Layer_APP <-->|"CQRS Commands & Queries"| Layer_SVC
    Layer_SVC <-->|"Prompt & History Assembly"| Layer_INT
    Layer_SVC <-->|"LINQ / Pgvector Extensions"| Layer_DB
    Layer_SVC ==>|"Format & Render Output"| Layer_EXP

    %% --- SPECIFIC PIPELINE CONNECTIONS (Dotted) ---

    DB_FS -. "Fire-and-Forget Job" .-> Jobs
    Jobs -. "Extract Text & Structure" .-> Extract
    Extract -. "Chunking (Max 1500 chars)" .-> Embed
    Embed -. "Store DocumentChunks" .-> DB_Postgres
    
    %% --- CUSTOM STYLING (Modern & Accessible) ---
    
    %% Node Shapes and Colors
    classDef uiNode fill:#eff6ff,stroke:#3b82f6,stroke-width:2px,color:#1e3a8a,rx:6,ry:6
    classDef appNode fill:#f5f3ff,stroke:#8b5cf6,stroke-width:2px,color:#4c1d95,rx:6,ry:6
    classDef svcNode fill:#f0fdf4,stroke:#22c55e,stroke-width:2px,color:#14532d,rx:6,ry:6
    classDef intNode fill:#fffbeb,stroke:#f59e0b,stroke-width:2px,color:#78350f,rx:6,ry:6
    classDef dbNode fill:#f8fafc,stroke:#64748b,stroke-width:2px,color:#0f172a,rx:6,ry:6
    classDef expNode fill:#fff1f2,stroke:#f43f5e,stroke-width:2px,color:#881337,rx:6,ry:6

    %% Subgraph Styling
    classDef layerBox fill:#ffffff,stroke:#cbd5e1,stroke-width:2px,stroke-dasharray: 5 5

    %% Apply node styles
    class View,JS,Markdown uiNode
    class Handlers,State,Cache,Core appNode
    class SK,Vec,Extract,Jobs,Resilience svcNode
    class Local,Cloud,Embed,Web intNode
    class DB_Postgres,DB_EFCore,DB_HNSW,DB_FS dbNode
    class OpenXML,Quest,Latex,RawMD expNode

    %% Apply subgraph styles
    class Layer_UI,Layer_APP,Layer_SVC,Layer_INT,Layer_DB,Layer_EXP layerBox

```

---

## 🚀 Core Workflows & Intelligence

### 1. RAG-Powered Contextual Search
GraspIt doesn't just "chat"—it thinks within the scope of your knowledge:
- **Document-Scoped Queries**: Select specific textbooks or notes to narrow the AI's search space via the sidebar document pills.
- **HNSW Vector Search**: Uses Hierarchical Navigable Small World indexes in `pgvector` for sub-second retrieval across massive document sets.
- **Semantic Caching**: Duplicate queries are served instantly via `IMemoryCache`, reducing LLM API latency and costs.

### 2. Structural Document Analysis
The high-fidelity generation starts with understanding. The `DocumentExtractionService` performs a deep structural scan:
- **Bloom's Taxonomy (BT)**: Automatically extracts and maps cognitive levels (L1-L6) from reference papers.
- **Course Outcome (CO)**: Detects and maps specific course outcomes to questions.
- **Layout Cloning**: Identifies institution headers, section patterns (PART A, SECTION 1), and question table grids.

### 3. The "High-Fidelity" Export Pipeline
GraspIt solves the "LLM formatting" problem through a hybrid generation strategy:
- **JSON-to-DOCX**: The AI outputs structured JSON, which is fed into a **Node.js** engine (`docx` library) to construct a pixel-perfect Microsoft Word document matching the NITTE university format.
- **LibreOffice PDF Path**: For the highest quality PDF output, the system uses **LibreOffice CLI** to convert generated DOCX files, ensuring table borders and university logos are preserved exactly.
- **QuestPDF Fallback**: A native C# rendering engine ensures reliability even if external tools are unavailable.

### 4. Advanced UX Features
- **Web Search Integration**: A dedicated toggle seamlessly injects real-time web context via the DuckDuckGo API into your prompts.
- **Persistent Chat Memory**: The AI naturally remembers the context of your entire conversation via Semantic Kernel's native `ChatHistory` state.
- **Extended Parsing Engine**: Beyond PDFs, the system supports Word (.docx), PowerPoint (.pptx) via OpenXML, and Images (.jpeg, .png) via Tesseract OCR.
- **Reasoning Process**: Automatic detection and expansion of `<think>` tags (from reasoning models like DeepSeek R1) into collapsible UI elements.
- **Smart Folder Organization**: A native HTML5 drag-and-drop system allows you to organize chat sessions into custom folders with live session counts.
- **Interactive UI Elements**: Clean, non-overlapping Copy buttons positioned via Flexbox for quick code and text extraction.
- **Resilience**: Every AI call is wrapped in **Polly** policies for 3x retries with exponential backoff, handling transient network or local LLM failures.

---

## 🛠️ Technology Stack

| Layer | Key Technologies |
| :--- | :--- |
| **Backend Core** | .NET 10, MediatR (CQRS), AutoMapper, Polly |
| **Presentation** | ASP.NET Core MVC, Bootstrap 5, marked.js |
| **Intelligence** | Semantic Kernel, pgvector, HNSW, nomic-embed |
| **Background** | Hangfire, PdfPig |
| **Export Engines** | Node.js, LibreOffice CLI, OpenXML SDK, QuestPDF |
| **Persistence** | PostgreSQL 16 (Docker-optimized) |

---

## 📖 Further Documentation

For deep dives into the database schema, API endpoints, or setup instructions, please refer to the [Detailed Documentation](file:///c:/GitHub/GraspIt/DOCUMENTATION.md).