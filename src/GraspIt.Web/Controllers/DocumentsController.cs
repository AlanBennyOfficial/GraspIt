using Microsoft.AspNetCore.Mvc;
using GraspIt.Application.Features.Documents.Commands;
using GraspIt.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Hangfire;

namespace GraspIt.Web.Controllers;

public class DocumentsController : Controller
{
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly GraspItDbContext _context;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(IBackgroundJobClient backgroundJobClient, GraspItDbContext context, ILogger<DocumentsController> logger)
    {
        _backgroundJobClient = backgroundJobClient;
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var documents = await _context.Documents
            .Include(d => d.DocumentChunks)
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync();
        return View(documents);
    }

    [HttpPost]
    public async Task<IActionResult> Upload(IFormFile file, string documentType = "Other", string subject = "")
    {
        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "Please select a file to upload.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            Directory.CreateDirectory(uploadsFolder);

            var safeFileName = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{file.FileName}";
            var filePath = Path.Combine(uploadsFolder, safeFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            int currentUserId = 1;

            _backgroundJobClient.Enqueue<ProcessDocumentCommandHandler>(
                handler => handler.Handle(
                    new ProcessDocumentCommand
                    {
                        FilePath = filePath,
                        UserId = currentUserId,
                        DocumentType = documentType
                    },
                    CancellationToken.None
                )
            );

            TempData["Success"] = $"'{file.FileName}' has been uploaded and scheduled for AI embedding.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file upload.");
            TempData["Error"] = "An error occurred while processing the file.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var document = await _context.Documents
            .Include(d => d.DocumentChunks)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (document != null)
        {
            // Delete the physical file if it exists
            if (System.IO.File.Exists(document.FilePath))
            {
                try { System.IO.File.Delete(document.FilePath); }
                catch { /* non-critical */ }
            }

            _context.Documents.Remove(document);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"'{document.Title}' has been deleted.";
        }

        return RedirectToAction(nameof(Index));
    }
}
