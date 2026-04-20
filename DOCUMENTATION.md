# GraspIt — AI-Powered Academic Assistant

> **Version:** MVP 1.0  
> **Author:** Alan Benny  
> **Architecture:** ASP.NET Core MVC (.NET 10) + PostgreSQL + pgvector  
> **Last Updated:** April 9, 2026

---

## Table of Contents

1. [System Overview](#1-system-overview)
2. [Architecture & Project Structure](#2-architecture--project-structure)
3. [Core Features](#3-core-features)
   - [3.1 RAG-Powered Chat](#31-rag-powered-chat)
   - [3.2 Document Management & Embedding](#32-document-management--embedding)
   - [3.3 AI Question Paper Generator](#33-ai-question-paper-generator)
   - [3.4 Multi-Format Document Export](#34-multi-format-document-export)
   - [3.5 LLM Provider Management](#35-llm-provider-management)
4. [Database Schema](#4-database-schema)
5. [RAG Pipeline (Detailed)](#5-rag-pipeline-detailed)
6. [Exam Generation Pipeline (Detailed)](#6-exam-generation-pipeline-detailed)
7. [Export Pipeline (Detailed)](#7-export-pipeline-detailed)
8. [Chat System (Detailed)](#8-chat-system-detailed)
9. [UI/UX Design System](#9-uiux-design-system)
10. [Technology Stack](#10-technology-stack)
11. [Setup & Running](#11-setup--running)
12. [Configuration](#12-configuration)
13. [API Endpoints](#13-api-endpoints)
14. [Error Handling & Resilience](#14-error-handling--resilience)

---

## 1. System Overview

GraspIt is an AI-powered academic assistant that enables students to:

- **Upload study materials** (PDFs) that are permanently stored, chunked, and embedded into a pgvector database for semantic retrieval
- **Ask questions** with context retrieved from their uploaded documents using Retrieval-Augmented Generation (RAG), with per-document filtering and a real-time RAG toggle
- **Generate mock exam papers** by uploading a reference question paper, which the system structurally analyzes to clone the format, retrieves syllabus context via RAG, uses a local LLM to generate new questions, and renders them as downloadable documents in four formats (DOCX, PDF, Markdown, LaTeX)
- **Organize chats** into folders and sessions, with drag-and-drop reorganization and full message history persistence
- **Export any AI response** directly from the chat workspace into Word, PDF, or Markdown files

The system is designed to work with **local LLMs** (LM Studio, Ollama, vLLM) so students can use it without cloud API costs, while also supporting cloud providers (OpenAI, DeepSeek, Google Gemini) for higher quality results.

---

## 2. Architecture & Project Structure

GraspIt follows **Clean Architecture** with four distinct layers:

```
GraspIt.slnx
├── src/
│   ├── GraspIt.Core/              # Domain entities and interfaces (zero external dependencies)
│   │   ├── Entities/
│   │   │   ├── User.cs
│   │   │   ├── Document.cs
│   │   │   ├── DocumentChunk.cs   # Stores embedded text chunks with pgvector embeddings
│   │   │   ├── ChatSession.cs     # Persistent chat conversations
│   │   │   ├── ChatMessage.cs     # Individual messages within a session
│   │   │   ├── ChatFolder.cs      # User-created folders to organize chats
│   │   │   ├── MockExam.cs
│   │   │   ├── Question.cs
│   │   │   └── AiProvider.cs      # LLM provider configuration (endpoint, model, API key)
│   │   └── Interfaces/
│   │       ├── IAiService.cs      # LLM interaction contract
│   │       ├── IVectorDbService.cs # Embedding storage and similarity search contract
│   │       ├── IPdfParserService.cs
│   │       ├── IExportService.cs  # Multi-format document export contract
│   │       └── IRepository.cs     # Generic repository pattern
│   │
│   ├── GraspIt.Application/       # Business logic, CQRS handlers, DTOs
│   │   ├── DTOs/
│   │   │   └── AiProviderDto.cs
│   │   ├── Features/
│   │   │   ├── Chat/Queries/
│   │   │   │   ├── AskQuestionQuery.cs        # CQRS query with RAG toggle & doc filter
│   │   │   │   └── AskQuestionQueryHandler.cs  # Orchestrates RAG → LLM pipeline
│   │   │   ├── Documents/Commands/
│   │   │   │   ├── ProcessDocumentCommand.cs
│   │   │   │   └── ProcessDocumentCommandHandler.cs  # PDF parse → embed → store
│   │   │   └── Exams/Queries/
│   │   │       ├── GenerateMockExamQuery.cs
│   │   │       └── GenerateMockExamQueryHandler.cs
│   │   └── Mappings/
│   │       └── AiProviderMappingProfile.cs    # AutoMapper profile
│   │
│   ├── GraspIt.Infrastructure/    # External service implementations
│   │   ├── Data/
│   │   │   ├── GraspItDbContext.cs  # EF Core context with pgvector & HNSW indexing
│   │   │   └── Repository.cs       # Generic repository implementation
│   │   └── Services/
│   │       ├── AiService.cs                   # Semantic Kernel LLM interaction
│   │       ├── VectorDbService.cs             # Embedding generation & cosine similarity search
│   │       ├── RagSearchService.cs            # Advanced RAG with metadata pre-filtering
│   │       ├── RagRetrievalService.cs         # Simplified RAG for exam pipeline
│   │       ├── PdfParserService.cs            # UglyToad.PdfPig text extraction
│   │       ├── DocumentExtractionService.cs   # Structural PDF→ExamPaperStructure parser
│   │       ├── LocalOpenAIProvider.cs         # OpenAI SDK for local LLM chat completion
│   │       ├── ExamRendererService.cs         # OpenXML DOCX rendering with table formatting
│   │       └── ExportService.cs               # Multi-format (DOCX/PDF/MD/LaTeX) export
│   │
│   └── GraspIt.Web/              # ASP.NET Core MVC presentation layer
│       ├── Controllers/
│       │   ├── HomeController.cs
│       │   ├── ChatController.cs      # Chat sessions, folders, document filter
│       │   ├── DocumentsController.cs  # Upload, list, delete documents
│       │   ├── MockExamController.cs   # Structural exam generation pipeline
│       │   ├── ExportController.cs     # File download endpoints (DOCX/PDF/MD/LaTeX)
│       │   └── ProvidersController.cs  # CRUD for LLM providers
│       ├── Views/
│       │   ├── Shared/_Layout.cshtml   # Dark-themed master layout with marked.js CDN
│       │   ├── Home/Index.cshtml       # Landing page with feature cards
│       │   ├── Chat/Index.cshtml       # Full chat workspace with drag-and-drop sidebar
│       │   ├── Documents/Index.cshtml  # Document library with upload & management
│       │   ├── MockExam/Index.cshtml   # Exam generator with AJAX preview & export
│       │   └── Providers/*.cshtml      # Provider CRUD views
│       └── wwwroot/
│           ├── css/site.css            # Complete dark theme design system
│           └── js/site.js
│
├── docker-compose.yml            # PostgreSQL 16 + pgvector container
└── .env                          # Database credentials & resource limits
```

### Design Patterns

| Pattern | Implementation |
|---|---|
| **Clean Architecture** | Core → Application → Infrastructure → Web (dependency inversion) |
| **CQRS** | MediatR separates commands (`ProcessDocumentCommand`) from queries (`AskQuestionQuery`) |
| **Repository Pattern** | Generic `IRepository<T>` abstracts EF Core data access |
| **Strategy Pattern** | `ILLMProvider` and `IAiService` allow swapping LLM backends at runtime |
| **Retry with Exponential Backoff** | Polly wraps all LLM API calls with 3 retries |
| **Structural Parsing** | `DocumentExtractionService` parses PDFs into a rich `ExamPaperStructure` model |
| **Format-Preserving Export** | `ExportService` reconstructs the parsed structure into DOCX/PDF/LaTeX with identical table layout |

---

## 3. Core Features

### 3.1 RAG-Powered Chat

The chat workspace is the primary feature of GraspIt. It provides a full-featured conversational interface where every answer can be grounded in the student's own study materials.

#### Chat Sessions & History
- Each conversation is persisted as a `ChatSession` entity with a title auto-generated from the first message (truncated to 60 characters)
- Individual messages (`ChatMessage`) store the role (`user`/`assistant`), content, timestamp, and whether RAG was used for that turn
- Sessions load instantly from the database when clicked in the sidebar
- Sessions can be deleted individually with a confirmation dialog

#### Folder Organization with Drag-and-Drop
- Users can create named folders (e.g., "Computer Networks", "Data Structures") via an inline input at the top of the sidebar
- Chat sessions can be moved into folders via two mechanisms:
  1. **Dropdown selector**: Each session has a compact `<select>` dropdown with all available folders listed. Selecting a folder instantly moves the session (via the `MoveSession` API endpoint) and refreshes the sidebar
  2. **Native HTML5 drag-and-drop**: Sessions have `draggable="true"` and emit `dragstart`/`dragend` events. Folder headers act as drop targets with `dragover`/`drop` listeners. When a session is dropped onto a folder header, the `moveSession()` function is called. Dropping onto the "All Chats" header moves a session out of any folder (sets `folderId` to `null`). Active drop targets receive a `.drop-target` CSS class that highlights them with the accent glow color
- Folders display their session count as a badge and can be expanded/collapsed with a rotating arrow indicator
- Deleting a folder moves its sessions back to "All Chats" (the database uses `ON DELETE SET NULL` on the `FolderId` foreign key)

#### RAG Toggle
- A toggle switch in the chat header enables/disables RAG context retrieval on a per-message basis
- When **ON**: The user's query is embedded via the local embedding model, the most similar document chunks are retrieved from pgvector using cosine distance, and these chunks are prepended to the LLM prompt as context
- When **OFF**: The query is sent directly to the LLM without any document context (general-purpose chat mode)
- The document filter card visually dims when RAG is disabled (opacity 0.4)

#### Document-Scoped RAG
- The right sidebar displays all uploaded documents with their chunk counts as clickable "pills"
- Users can click document pills to select/deselect specific documents for RAG filtering
- When documents are selected, the vector search is constrained to only those documents' chunks using a `WHERE DocumentId IN (...)` clause in `VectorDbService.SearchAsync()`
- This allows targeted queries like "search only in my CN textbook" vs. searching the entire knowledge base

#### LLM Provider Selection
- A dropdown in the right sidebar lets users switch between configured LLM providers mid-conversation
- The selected provider ID is sent with each `AskQuestion` request
- If no provider is selected, the system uses the default provider (marked with `IsDefault = true`)

#### Think-Tag Rendering
- LLM responses from reasoning models (like DeepSeek R1) often contain `<think>...</think>` blocks with chain-of-thought reasoning
- The `parseReasoningTags()` JavaScript function detects these blocks and converts them into expandable `<details>` elements
- Each reasoning block displays a collapsed summary ("▶ Reasoning Process") that can be clicked to expand
- The chevron (`▶`) rotates 90° when expanded, using CSS `transform: rotate(90deg)` on the `details[open]` state
- The reasoning content renders in a monospace font (`Cascadia Code`/`Fira Code`) with muted text color for visual distinction from the main response

#### Message Rendering
- Bot responses are parsed through `marked.js` for full Markdown rendering (headers, code blocks, tables, lists, inline code)
- Markdown tables inside messages are styled with the dark theme's border and background colors via `.message-content table` CSS rules
- Code blocks use a dark background with syntax-appropriate coloring
- Each bot message includes export buttons (DOCX, PDF, MD) that appear subtly at the bottom and become fully visible on hover

#### Quick Upload
- The right sidebar includes a compact upload form that allows users to quickly add a PDF without leaving the chat workspace
- The form posts to `DocumentsController.Upload` and supports all material types

### 3.2 Document Management & Embedding

#### Upload Pipeline
1. User selects a PDF file and material type (Syllabus, Textbook, Slides, Notes, Previous Paper, Other)
2. The file is saved to `wwwroot/uploads/` with a timestamp-prefixed filename to prevent collisions
3. A `ProcessDocumentCommand` is dispatched via **Hangfire** as a background job (fire-and-forget)
4. The `ProcessDocumentCommandHandler` executes:
   - **PDF Text Extraction**: `PdfParserService` uses **UglyToad.PdfPig** to extract text from every page. If the PDF is corrupted or has an invalid header, a `try-catch` block gracefully returns an empty string instead of crashing the background worker
   - **Document Record**: A `Document` entity is persisted with title, file path, type, and user ID
   - **Metadata Prepending**: The document type and title are prepended to the text for RAG contextualization: `[Source Document Type: Textbook]\n[Source Title: CN Textbook.pdf]\n\n{extracted text}`
   - **Chunking**: The text is split at double-newline boundaries. Segments over 2000 characters are sub-chunked into 1500-character blocks
   - **Embedding**: Each chunk is sent to the local embedding model via the **OpenAI SDK** to generate a 768-dimensional vector
   - **Storage**: Each chunk is stored as a `DocumentChunk` entity with `ChunkText` and `Embedding` (pgvector `vector(768)` column)

#### Permanent Storage
- Documents are stored permanently in PostgreSQL (not in-memory or temporary)
- The pgvector extension enables native vector storage and similarity search
- An **HNSW index** (`vector_cosine_ops`) is configured on the `Embedding` column for fast approximate nearest-neighbor searches
- Deleting a document cascades to all its `DocumentChunk` records (`ON DELETE CASCADE`) and also deletes the physical file from disk

#### Document Library UI
- Displays a table of all uploaded documents with:
  - Document title and material type badge
  - Chunk count (shows "Processing..." badge if no chunks yet, indicating Hangfire is still working)
  - Upload timestamp
  - Delete button with confirmation dialog

### 3.3 AI Question Paper Generator

The exam generator implements a **5-phase orchestration pipeline** that structurally analyzes a reference paper and clones its layout for the generated output.

#### Phase 1: Structural Document Extraction
- User uploads a reference question paper (PDF or image)
- `DocumentExtractionService.ExtractStructuredAsync()` opens the PDF with PdfPig and performs a multi-pass structural parse:
  1. **Header Detection**: Lines before the first question or section heading are captured as header metadata (institution name, exam title, date, semester, etc.)
  2. **Section Detection**: Regex pattern `(PART|SECTION|MODULE|UNIT)\s*[-–]?\s*[A-Z0-9]+` identifies section boundaries. Sub-headings (lines starting with '(') are captured as section annotations (e.g., "Answer All Questions")
  3. **Question Boundary Detection**: Regex `Q\.?\s*\d+[\.\):]?|[0-9]+[\.\):]` identifies question numbers. Sub-parts (`a. b. c.`) are detected and nested under their parent question
  4. **Metadata Extraction**: For each question line, marks (`(10 Marks)`, `[15]`), Bloom's Taxonomy level (`L1`–`L6`, `BTL 3`, `APPLY`), and Course Outcome (`CO1`–`CO9`) are extracted using dedicated regex patterns
  5. **Table Detection**: If the source PDF contains pipe-delimited table rows (`|`), the service detects column headers and maps them for faithful reproduction
- The result is an `ExamPaperStructure` object containing: `HeaderLines`, `Sections[]` (each with `Heading`, `SubHeading`, `Questions[]`), `RawText`, `HasTableStructure`, and `TableColumns`
- If PdfPig throws a `PdfDocumentFormatException` (invalid/corrupted file), a fallback structure with standard question format is returned instead of crashing

#### Phase 2: RAG Context Retrieval (pgvector)
- The user's generation instructions are embedded using the local embedding model
- `RagRetrievalService` performs cosine similarity search against all `DocumentChunk` embeddings in PostgreSQL
- Top-K (default: 5) most relevant chunks are returned as syllabus context
- This ensures generated questions are grounded in actual syllabus content

#### Phase 3: LLM Question Generation
- `LocalOpenAIProvider` constructs a system prompt instructing the LLM to generate structured JSON
- The prompt includes: OCR reference formatting as a Markdown table, RAG syllabus context, and user instructions
- The LLM is instructed to output a `List<QuestionRow>` JSON array with keys: `QNo`, `Question`, `Marks`, `BT`, `CO`
- Response sanitization:
  - Strips `<think>` tags and conversational text by finding the first `[` and last `]` characters to isolate the JSON array
  - Falls back to an empty list if deserialization fails after all retries
- Polly retry policy handles transient failures with 3 attempts and exponential backoff (2s, 4s, 8s)

#### Phase 4: Structural Merging
- `MockExamController.MergeQuestionsIntoStructure()` distributes the flat `QuestionRow` list back into the original paper's section layout
- Questions are allocated proportionally: if the original had 3 questions in Part A and 5 in Part B, the generated questions are split in the same 3:5 ratio
- Any excess questions are appended to the last section

#### Phase 5: Interactive Preview & Export
- The controller returns a JSON response containing the structured markdown preview
- The frontend renders this markdown with `marked.parse()` in a styled preview card
- Four export buttons (DOCX, PDF, MD, LaTeX) are dynamically injected, each submitting a hidden form to the `ExportController`

### 3.4 Multi-Format Document Export

All export formats faithfully reconstruct the structural layout of the response, including table formatting, section headings, and sub-question indentation.

#### DOCX Export (via OpenXML SDK)
- The markdown is reverse-parsed into an `ExamPaperStructure` by `ExportService.ParseMarkdownToStructure()`
- `ExamRendererService.RenderExamToDocxAsync()` generates a Word document with:
  - A4 page margins (1080 DXA on all sides)
  - Centered header paragraphs (institution, title, date) in bold
  - A full-width question table with:
    - **Dark header row** (background `#1F2937`, white text) that repeats across page breaks (`TableHeader` property)
    - **Section separator rows** that span all columns with a dark gray background (`#374151`) and centered section name
    - **Question data rows** with computed column widths: the "Question" column dynamically fills remaining space after fixed-width numeric columns (Q No: 600, Marks: 700, BT: 700, CO: 700 DXA)
    - **Sub-part indentation**: Sub-questions (a, b, c) are rendered with leading spaces and slightly smaller font size (18pt vs 20pt)
    - All cells have vertical center alignment
  - The `SectionProperties` element at the bottom sets the page size to A4

#### PDF Export (via QuestPDF)
- Uses the `QuestPDF.Settings.License = LicenseType.Community` configuration
- Generates an A4 PDF with:
  - Centered page header with institution name and metadata
  - Per-section question tables with:
    - Dark header row with white bold text
    - Alternating row shading (white / light gray) for readability
    - Sub-part rows in italic with slightly smaller font
    - Section heading blocks with dark background spanning full width
  - Page footer with "GraspIt AI · Page X of Y"
  - Table cell borders in medium gray with 4px padding

#### Markdown Export
- Returns the raw markdown bytes (UTF-8 encoded) that the frontend already rendered

#### LaTeX Export
- Generates a complete compilable LaTeX document with:
  - Document class: `article`, A4 paper, 12pt
  - Packages: `geometry`, `longtable`, `booktabs`, `array`, `xcolor`, `colortbl`
  - Custom colors: `headercolor` (RGB 17, 24, 39) and `sectioncolor` (RGB 55, 65, 81)
  - `\begin{center}` header block with institution name
  - Per-section `longtable` environments with:
    - `\rowcolor{headercolor}` header rows with `\textcolor{white}` text
    - `\hline` separators between every row
    - `\endhead` directive for multi-page header repetition
    - Sub-parts rendered with `\quad` indentation and `\textit{}` italics
  - All special characters (`&`, `%`, `$`, `#`, `_`, `{`, `}`, `~`, `^`, `\`) are properly escaped to prevent LaTeX compilation errors

### 3.5 LLM Provider Management

- Full CRUD interface for managing LLM providers via the Providers controller
- Each provider stores: Name, Type (`OpenAICompatible`/`AzureOpenAI`), Endpoint URL, Model ID, API Key, IsDefault flag
- Setting a provider as "Default" automatically un-defaults all other providers
- Pre-seeded providers include: LM Studio, Ollama, Google Gemini, DeepSeek, OpenAI
- The `AiService` dynamically builds a **Semantic Kernel** with the selected provider's configuration
- All providers use the OpenAI-compatible API format, making them interchangeable
- Provider selection is available per-message in the chat workspace via a dropdown

---

## 4. Database Schema

The database runs on **PostgreSQL 16 with pgvector extension** via Docker.

### Entity Relationship Diagram

```
User (1) ──→ (N) Document ──→ (N) DocumentChunk [with vector(768) embedding, HNSW indexed]
User (1) ──→ (N) MockExam ──→ (N) Question
User (1) ──→ (N) ChatSession ──→ (N) ChatMessage
User (1) ──→ (N) ChatFolder ──→ (N) ChatSession [nullable FK, ON DELETE SET NULL]
AiProvider [standalone configuration table, auto-seeded]
```

### Key Columns

| Entity | Key Columns | Notes |
|---|---|---|
| `User` | Id, Username, Email, CreatedAt | Default user auto-seeded on startup |
| `Document` | Id, Title, FilePath, DocumentType, Subject, UserId | Permanent storage; file at `wwwroot/uploads/` |
| `DocumentChunk` | Id, DocumentId, ChunkText, Embedding `vector(768)`, UnitNumber, HeadingLevel | HNSW indexed for cosine similarity |
| `ChatSession` | Id, Title, UserId, FolderId?, CreatedAt, UpdatedAt | Nullable FK to folder; UpdatedAt tracks last activity |
| `ChatMessage` | Id, SessionId, Role, Content, UsedRag, CreatedAt | Cascade delete on session removal |
| `ChatFolder` | Id, Name, UserId, CreatedAt | SetNull on delete preserves sessions |
| `AiProvider` | Id, Name, ProviderType, Endpoint, ModelId, ApiKey, IsDefault | Multi-provider support |
| `MockExam` | Id, Title, UserId, CreatedAt | Exam metadata |
| `Question` | Id, Text, Type, OptionsJson, CorrectAnswer, Explanation, MockExamId | MCQ/ShortAnswer |

### pgvector Configuration

- Extension: `vector` (enabled via `modelBuilder.HasPostgresExtension("vector")`)
- Column type: `vector(768)` (matches `nomic-embed-text-v1.5` dimensionality)
- Index type: **HNSW** (Hierarchical Navigable Small World) for fast approximate nearest-neighbor search
- Index operator: `vector_cosine_ops` (optimized for cosine distance queries)
- Cosine distance function: `CosineDistance()` via Pgvector.EntityFrameworkCore LINQ translation

---

## 5. RAG Pipeline (Detailed)

### Ingestion Phase (Upload Time)
```
PDF File → PdfPig Text Extraction → Double-newline Chunking (max 1500 chars)
         → OpenAI Embedding API (local model) → pgvector Storage with HNSW Index
```

### Query Phase (Chat Time)
```
User Query → [If RAG enabled] → Embed query via OpenAI API (local)
           → pgvector Cosine Distance Search (optionally filtered by doc IDs)
           → Top-K chunks retrieved → Prepended to LLM prompt as context
           → Semantic Kernel invokes LLM → Response returned to user
```

### Implementation Details

1. **Embedding Model**: `text-embedding-nomic-embed-text-v1.5` running locally on LM Studio at `http://localhost:1234/v1`
2. **Embedding Dimensions**: 768 (stored as pgvector `vector(768)`)
3. **Search Algorithm**: Cosine distance via pgvector's `<=>` operator, accelerated by HNSW index
4. **Document Filtering**: Optional `WHERE DocumentId IN (...)` clause constrains search to user-selected documents
5. **Chunk Size**: Max 1500 characters per chunk, split at double-newline boundaries
6. **Metadata Enrichment**: Each chunk is prepended with `[Source Document Type: X]\n[Source Title: Y]` to give the LLM provenance information
7. **Cache**: Responses are cached in `IMemoryCache` for 10 minutes (sliding expiration) keyed by `{query}_{providerId}_{useRag}`

---

## 6. Exam Generation Pipeline (Detailed)

```
Reference Paper (PDF) → PdfPig → ExamPaperStructure (headers, sections, questions)
                                          ↓
User Instructions → Embedding → pgvector Search → Syllabus Context (RAG)
                                          ↓
          [Structural Format + RAG Context + Instructions] → Local LLM
                                          ↓
               JSON QuestionRow[] → Merged into ExamPaperStructure
                                          ↓
                        Markdown Preview → Export (DOCX / PDF / MD / LaTeX)
```

### Structural Models

```csharp
ExamPaperStructure
├── HeaderLines: List<string>       // Institution, title, date
├── Sections: List<ExamSection>
│   ├── Heading: string             // "PART A", "SECTION 1"
│   ├── SubHeading: string          // "(Answer All Questions)"
│   └── Questions: List<ExamQuestion>
│       ├── QNo: string             // "Q1.", "1."
│       ├── Text: string            // Question text
│       ├── Marks: string           // "10"
│       ├── BT: string              // "L3", "APPLY"
│       ├── CO: string              // "CO2"
│       └── SubParts: List<ExamQuestion>  // a., b., c.
├── RawText: string                 // Full extracted text
├── HasTableStructure: bool         // True if original had pipe tables
└── TableColumns: List<string>      // Detected column names
```

### Error Handling
- Invalid/corrupted PDF: `PdfDocumentFormatException` is caught, fallback structure returned
- Empty extraction: LLM receives a template structure with standard academic columns
- LLM failure: Polly retries 3x with exponential backoff; empty list returned on final failure
- JSON parsing failure: `<think>` tags and conversational text are stripped before deserialization

---

## 7. Export Pipeline (Detailed)

### Markdown → Structure Parsing (`ExportService.ParseMarkdownToStructure`)

When a user clicks an export button (DOCX, PDF, LaTeX), the markdown content from the chat message or exam preview is reverse-parsed back into an `ExamPaperStructure`:

1. Lines starting with `#` are captured as header lines
2. Lines starting with `**` (bold) are captured as sub-headers / instructions
3. Pipe-delimited lines (`|`) are parsed as table rows:
   - The first pipe row is treated as the header row (skipped)
   - Separator rows (`|---|---|`) are detected and skipped
   - Rows where the first cell is bold or contains "PART"/"SECTION" are treated as section headings
   - All other pipe rows are parsed into `ExamQuestion` objects with cells mapped to QNo, Question, Marks, BT, CO by position

### Per-Format Rendering

| Format | Engine | Key Features |
|---|---|---|
| **DOCX** | `ExamRendererService` (OpenXML SDK) | Dark header row, section shading, column width computation, sub-part indentation, page-break header repetition |
| **PDF** | `ExportService` (QuestPDF) | A4 layout, alternating row colors, page header/footer, section highlight blocks |
| **Markdown** | Direct passthrough | UTF-8 bytes of the original markdown |
| **LaTeX** | `ExportService` (string builder) | `longtable`, `booktabs`, `xcolor` for colored headers, proper character escaping, `\endhead` for multi-page tables |

---

## 8. Chat System (Detailed)

### Frontend Architecture

The chat workspace (`Chat/Index.cshtml`) is a single-page-like experience within the MVC layout. All interactions (sending messages, loading sessions, managing folders) happen via `fetch()` API calls without full page reloads.

### Session Lifecycle

```
1. User types message → chat-form submit handler fires
2. If currentSessionId is null → server auto-creates ChatSession
3. User message saved to ChatMessages table
4. Query dispatched via MediatR → AskQuestionQueryHandler:
   a. If RAG enabled → VectorDbService.SearchAsync() retrieves context
   b. AiService.GenerateResponseAsync() calls Semantic Kernel
5. Assistant response saved to ChatMessages table
6. Session.UpdatedAt refreshed
7. JSON response returned: { answer, sessionId }
8. Frontend calls addMessage() → markdown parsed → DOM updated
```

### Drag-and-Drop Implementation

The drag-and-drop system uses the **HTML5 Drag and Drop API** (no external libraries):

1. **Draggable sessions**: Each `createSessionEl()` sets `div.draggable = true` and attaches `dragstart`/`dragend` listeners. The `dragstart` handler stores the session ID in a module-level `draggedSessionId` variable and adds a `.dragging` CSS class (opacity 0.4)
2. **Drop targets (folders)**: Each folder header in `loadFolders()` has `dragover` (prevents default, adds `.drop-target`), `dragleave` (removes highlight), and `drop` (calls `moveSession(draggedSessionId, folderId)`) listeners
3. **Drop target ("All Chats")**: The static "📋 All Chats" header at the bottom of the sidebar also acts as a drop target, moving sessions out of any folder by setting `folderId` to `null`
4. **Visual feedback**: The `.drop-target` CSS class applies an accent-colored border and glow background to indicate valid drop zones

### Message Export

Each bot message includes three compact export buttons (DOCX, PDF, MD) that:
- Are wrapped in hidden `<form>` elements pointing to `/Export/Download{Format}`
- Include the raw message text in a hidden `<input name="content">` field
- Open in a new tab (`target="_blank"`) to avoid navigating away from the chat
- Use the `.export-tools` CSS class with `opacity: 0.6` that becomes `1` on message hover

---

## 9. UI/UX Design System

GraspIt uses a custom dark theme built with CSS custom properties. Key design decisions:

### Color Palette
| Variable | Value | Usage |
|---|---|---|
| `--bg-primary` | `#0a0e1a` | Page background (deep navy) |
| `--bg-card` | `rgba(17, 24, 39, 0.8)` | Card backgrounds with transparency |
| `--accent` | `#6366f1` | Indigo primary accent for interactive elements |
| `--success` | `#10b981` | Emerald green for RAG toggle active state |
| `--text-primary` | `#ffffff` | All headings and primary content |
| `--text-secondary` | `#c3c8d0` | Descriptions, labels, nav links |
| `--text-muted` | `#9ca3af` | Placeholders, timestamps, secondary metadata |

### Typography
- Font stack: `Inter` (Google Fonts) → system fallbacks
- Base size: 15px
- Antialiasing: `-webkit-font-smoothing: antialiased`

### Glass Morphism
- Cards use `backdrop-filter: blur(12px)` with semi-transparent backgrounds
- The navbar uses `blur(20px) saturate(180%)` for a frosted glass effect
- A fixed `body::before` pseudo-element creates subtle radial gradient orbs for ambient depth

### Animations
- `fadeInUp`: 0.4s ease-out entrance animation for cards and messages
- `typing`: Bouncing dots indicator (3 spans with staggered animation delays)
- `pulse`: Infinite opacity pulse for loading states
- All interactive elements use `transition: all 0.25s cubic-bezier(0.4, 0, 0.2, 1)` for smooth state changes

### Accessibility
- All form controls have explicit `!important` color overrides to prevent OS-level dark-on-dark rendering
- `<select>` `<option>` elements have forced dark background (`#111827`) and white text to ensure readability across all browsers
- The `.form-text` helper class uses `--text-muted` for sufficient contrast against card backgrounds

---

## 10. Technology Stack

| Component | Technology | Version | Purpose |
|---|---|---|---|
| **Framework** | ASP.NET Core MVC | .NET 10 | Web application framework |
| **Database** | PostgreSQL + pgvector | 16 | Relational DB + vector similarity search |
| **ORM** | Entity Framework Core | 10.0.3 | Database access and migrations |
| **Vector Extension** | Pgvector.EntityFrameworkCore | 0.3.0 | EF Core bindings for pgvector |
| **CQRS** | MediatR | 14.1.0 | Command/Query separation |
| **Mapping** | AutoMapper | 16.1.1 | Entity ↔ DTO mapping |
| **AI Orchestration** | Microsoft Semantic Kernel | 1.73.0 | LLM interaction abstraction |
| **Embedding/Chat** | OpenAI SDK | 2.9.1 | Embedding generation & chat completion |
| **PDF Parsing** | UglyToad.PdfPig | 1.7.0-custom-5 | Text extraction from PDFs |
| **DOCX Generation** | DocumentFormat.OpenXml | 3.5.1 | Programmatic Word document creation |
| **PDF Generation** | QuestPDF | 2026.2.4 | High-quality PDF rendering with tables |
| **Background Jobs** | Hangfire + PostgreSQL | 1.8.23 | Fire-and-forget document processing |
| **Resilience** | Polly | 8.6.6 | Retry policies with exponential backoff |
| **Markdown Rendering** | marked.js | CDN (latest) | Client-side Markdown → HTML |
| **CSS Framework** | Bootstrap | 5.x | Responsive layout utilities |
| **Container** | Docker Compose | 3.8 | PostgreSQL pgvector container |

---

## 11. Setup & Running

### Prerequisites
- **.NET 10 SDK** installed
- **Docker Desktop** running (for PostgreSQL + pgvector)
- **LM Studio** (or Ollama/vLLM) running with:
  - A **chat model** loaded (e.g., `llama3`, `mistral`, `deepseek-r1`)
  - An **embedding model** loaded (e.g., `nomic-embed-text-v1.5`)

### Steps

1. **Start the database:**
   ```bash
   docker-compose up -d
   ```
   This starts PostgreSQL 16 with pgvector on port 5432.

2. **Build and run:**
   ```bash
   dotnet build GraspIt.slnx
   dotnet run --project src/GraspIt.Web
   ```

3. **Open in browser:**  
   Navigate to `https://localhost:5001` (or the port shown in console output).

4. **First-time setup:**
   - The database tables are auto-created via `EnsureCreated()`
   - A default user and LLM providers are auto-seeded
   - Go to **⚙️ LLM Config** to edit provider endpoints/API keys
   - Upload documents in the **📄 Documents** section
   - Start chatting in the **💬 Workspace**

---

## 12. Configuration

### .env (Docker)
```env
DB_USER=postgres
DB_PASSWORD=mypassword
DB_NAME=GraspItDb
DB_RAM_LIMIT=512m    # Increase for large document sets
DB_CPU_LIMIT=1.0
```

### appsettings.json
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=GraspItDb;Username=postgres;Password=mypassword;"
  }
}
```

### Embedding Model Configuration
The embedding endpoint is configured in `VectorDbService.cs` and `RagRetrievalService.cs`:
```csharp
Endpoint = new Uri("http://localhost:1234/v1")
Model = "text-embedding-nomic-embed-text-v1.5"
```
To use a different embedding model, update the model name and ensure the vector dimension in `DocumentChunk.cs` matches:
```csharp
[Column(TypeName = "vector(768)")]  // Change 768 to match your model's dimensions
public Vector? Embedding { get; set; }
```

### Upload Limits
Both Kestrel and ASP.NET form options are configured for 100MB max uploads in `Program.cs`:
```csharp
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = 100 * 1024 * 1024);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 100 * 1024 * 1024);
```

---

## 13. API Endpoints

### Chat Controller (`/Chat/`)

| Method | Endpoint | Description |
|---|---|---|
| GET | `/Chat/Index` | Chat workspace UI |
| POST | `/Chat/AskQuestion` | Send a message (JSON: `query`, `providerId`, `sessionId`, `useRag`, `documentIds`, `folderDocumentIds`) |
| GET | `/Chat/GetSessions` | List all chat sessions with folder assignments |
| GET | `/Chat/GetMessages?sessionId=` | Get messages for a session |
| POST | `/Chat/RenameSession` | Rename a session (JSON: `sessionId`, `title`) |
| POST | `/Chat/DeleteSession` | Delete a session (JSON: `id`) |
| POST | `/Chat/MoveSession` | Move session to folder (JSON: `sessionId`, `folderId`) |
| GET | `/Chat/GetFolders` | List all chat folders with session counts |
| POST | `/Chat/CreateFolder` | Create folder (JSON: `name`) |
| POST | `/Chat/DeleteFolder` | Delete folder (JSON: `id`) |
| GET | `/Chat/GetDocuments` | List documents for RAG filter UI |

### Documents Controller (`/Documents/`)

| Method | Endpoint | Description |
|---|---|---|
| GET | `/Documents/Index` | Document library UI |
| POST | `/Documents/Upload` | Upload & embed PDF (form: `file`, `documentType`) |
| POST | `/Documents/Delete/{id}` | Delete document and its embeddings |

### MockExam Controller (`/MockExam/`)

| Method | Endpoint | Description |
|---|---|---|
| GET | `/MockExam/Index` | Exam generator UI |
| POST | `/MockExam/GenerateExam` | Generate exam (form: `referenceImage`, `instructions`) → JSON with markdown preview |

### Export Controller (`/Export/`)

| Method | Endpoint | Description |
|---|---|---|
| POST | `/Export/DownloadDocx` | Export content as Word document (form: `content`) |
| POST | `/Export/DownloadPdf` | Export content as PDF (form: `content`) |
| POST | `/Export/DownloadMarkdown` | Export content as Markdown file (form: `content`) |
| POST | `/Export/DownloadLatex` | Export content as LaTeX source (form: `content`) |

### Providers Controller (`/Providers/`)

| Method | Endpoint | Description |
|---|---|---|
| GET | `/Providers/Index` | Provider list UI |
| GET | `/Providers/Create` | Add provider form |
| POST | `/Providers/Create` | Save new provider |
| GET | `/Providers/Edit/{id}` | Edit provider form |
| POST | `/Providers/Edit/{id}` | Update provider |
| GET | `/Providers/Delete/{id}` | Delete provider |

### Hangfire Dashboard

| Endpoint | Description |
|---|---|
| `/hangfire` | Background job monitoring dashboard |

---

## 14. Error Handling & Resilience

### PDF Parsing
- `PdfParserService.ExtractTextAsync()`: Wraps `PdfDocument.Open()` in a `try-catch`. If the file is corrupted or not a valid PDF (`PdfDocumentFormatException`, or any other exception), the method logs the error and returns an empty string instead of crashing the Hangfire background worker
- `DocumentExtractionService.ExtractStructuredAsync()`: Similarly catches all exceptions during `PdfDocument.Open()` and falls back to a template structure

### LLM Communication
- **Polly Retry Policy**: All LLM API calls (both `AiService` and `LocalOpenAIProvider`) are wrapped in a Polly `AsyncRetryPolicy` with 3 retries and exponential backoff (2s, 4s, 8s). This handles network timeouts, rate limits, and transient errors from local LLMs
- **JSON Extraction**: `LocalOpenAIProvider` handles chatty LLM responses by isolating the JSON array between the first `[` and last `]` characters, effectively ignoring `<think>` reasoning tags, markdown fences, and conversational filler

### Background Jobs
- Document embedding runs as a Hangfire fire-and-forget job. If the embedding model is unavailable, individual chunk failures are caught and logged without terminating the entire job, allowing partial embeddings to complete
- The Hangfire dashboard at `/hangfire` provides visibility into job status, retries, and failures

### Upload Safety
- File uploads are validated for presence and size before processing
- Files are saved with timestamp prefixes to prevent filename collisions
- Temporary files created during exam extraction are cleaned up in `finally` blocks

### Database
- `EnsureCreated()` on startup handles first-run schema creation
- Default user and provider seeding uses `if (!context.X.Any())` guards to prevent duplicate seeding
