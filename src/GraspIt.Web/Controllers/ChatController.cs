using Microsoft.AspNetCore.Mvc;
using GraspIt.Application.Features.Chat.Queries;
using GraspIt.Application.DTOs;
using GraspIt.Core.Entities;
using GraspIt.Infrastructure.Data;
using MediatR;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace GraspIt.Web.Controllers;

public class ChatController : Controller
{
    private readonly IMediator _mediator;
    private readonly GraspItDbContext _context;
    private readonly IMapper _mapper;

    public ChatController(IMediator mediator, GraspItDbContext context, IMapper mapper)
    {
        _mediator = mediator;
        _context = context;
        _mapper = mapper;
    }

    [HttpGet]
    public IActionResult Index()
    {
        var providers = _context.AiProviders.ToList();
        ViewBag.Providers = _mapper.Map<List<AiProviderDto>>(providers);
        return View();
    }

    // Chat message endpoint with RAG toggle and document filter
    [HttpPost]
    public async Task<IActionResult> AskQuestion([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return BadRequest("Query cannot be empty.");

        int userId = 1; // default user

        // Auto-create session if none provided
        int sessionId = request.SessionId ?? 0;
        if (sessionId == 0)
        {
            var session = new ChatSession
            {
                Title = request.Query.Length > 60 ? request.Query[..60] + "..." : request.Query,
                UserId = userId
            };
            _context.ChatSessions.Add(session);
            await _context.SaveChangesAsync();
            sessionId = session.Id;
        }

        // Save user message
        _context.ChatMessages.Add(new ChatMessage
        {
            SessionId = sessionId,
            Role = "user",
            Content = request.Query,
            UsedRag = request.UseRag
        });
        await _context.SaveChangesAsync();

        // Resolve document IDs from folder filter if specified
        var docIds = request.DocumentIds ?? new List<int>();
        if (request.FolderDocumentIds != null && request.FolderDocumentIds.Count > 0)
        {
            docIds.AddRange(request.FolderDocumentIds);
            docIds = docIds.Distinct().ToList();
        }

        var history = await _context.ChatMessages
            .Where(m => m.SessionId == sessionId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(10)
            .ToListAsync();
        history.Reverse();

        var query = new AskQuestionQuery
        {
            Query = request.Query,
            ProviderId = request.ProviderId,
            UseRag = request.UseRag,
            UseWebSearch = request.UseWebSearch,
            DocumentIds = docIds.Count > 0 ? docIds : null,
            History = history
        };

        var response = await _mediator.Send(query);

        // Save assistant response
        _context.ChatMessages.Add(new ChatMessage
        {
            SessionId = sessionId,
            Role = "assistant",
            Content = response,
            UsedRag = request.UseRag
        });

        // Update session timestamp
        var sess = await _context.ChatSessions.FindAsync(sessionId);
        if (sess != null) sess.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Json(new { answer = response, sessionId });
    }

    // --- Session Management ---

    [HttpGet]
    public async Task<IActionResult> GetSessions()
    {
        var sessions = await _context.ChatSessions
            .Where(s => s.UserId == 1)
            .OrderByDescending(s => s.UpdatedAt)
            .Select(s => new { s.Id, s.Title, s.FolderId, s.UpdatedAt, MessageCount = s.Messages.Count })
            .ToListAsync();
        return Json(sessions);
    }

    [HttpGet]
    public async Task<IActionResult> GetMessages(int sessionId)
    {
        var messages = await _context.ChatMessages
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new { m.Id, m.Role, m.Content, m.UsedRag, m.CreatedAt })
            .ToListAsync();
        return Json(messages);
    }

    [HttpPost]
    public async Task<IActionResult> RenameSession([FromBody] RenameRequest req)
    {
        var session = await _context.ChatSessions.FindAsync(req.SessionId);
        if (session == null) return NotFound();
        session.Title = req.Title;
        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> DeleteSession([FromBody] IdRequest req)
    {
        var session = await _context.ChatSessions.FindAsync(req.Id);
        if (session == null) return NotFound();
        _context.ChatSessions.Remove(session);
        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> MoveSession([FromBody] MoveSessionRequest req)
    {
        var session = await _context.ChatSessions.FindAsync(req.SessionId);
        if (session == null) return NotFound();
        session.FolderId = req.FolderId;
        await _context.SaveChangesAsync();
        return Ok();
    }

    // --- Folder Management ---

    [HttpGet]
    public async Task<IActionResult> GetFolders()
    {
        var folders = await _context.ChatFolders
            .Where(f => f.UserId == 1)
            .OrderBy(f => f.Name)
            .Select(f => new { f.Id, f.Name, SessionCount = f.Sessions.Count })
            .ToListAsync();
        return Json(folders);
    }

    [HttpPost]
    public async Task<IActionResult> CreateFolder([FromBody] NameRequest req)
    {
        var folder = new ChatFolder { Name = req.Name, UserId = 1 };
        _context.ChatFolders.Add(folder);
        await _context.SaveChangesAsync();
        return Json(new { folder.Id, folder.Name });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteFolder([FromBody] IdRequest req)
    {
        var folder = await _context.ChatFolders.FindAsync(req.Id);
        if (folder == null) return NotFound();
        _context.ChatFolders.Remove(folder);
        await _context.SaveChangesAsync();
        return Ok();
    }

    // --- Document list for RAG filter UI ---

    [HttpGet]
    public async Task<IActionResult> GetDocuments()
    {
        var docs = await _context.Documents
            .OrderByDescending(d => d.UploadedAt)
            .Select(d => new { d.Id, d.Title, d.DocumentType, d.Subject, d.UploadedAt, ChunkCount = d.DocumentChunks.Count })
            .ToListAsync();
        return Json(docs);
    }
}

// Request DTOs
public class ChatRequest
{
    public string Query { get; set; } = string.Empty;
    public int? ProviderId { get; set; }
    public int? SessionId { get; set; }
    public bool UseRag { get; set; } = true;
    public bool UseWebSearch { get; set; } = false;
    public List<int>? DocumentIds { get; set; }
    public List<int>? FolderDocumentIds { get; set; }
}

public class RenameRequest
{
    public int SessionId { get; set; }
    public string Title { get; set; } = string.Empty;
}

public class MoveSessionRequest
{
    public int SessionId { get; set; }
    public int? FolderId { get; set; }
}

public class IdRequest
{
    public int Id { get; set; }
}

public class NameRequest
{
    public string Name { get; set; } = string.Empty;
}
