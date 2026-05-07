using GraspIt.Core.Entities;
using GraspIt.Core.Interfaces;
using GraspIt.Infrastructure.Data;
using GraspIt.Infrastructure.Services;
using GraspIt.Application.Mappings;
using Microsoft.EntityFrameworkCore;
using Hangfire;
using Hangfire.PostgreSql;
using MediatR;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

// Set QuestPDF license
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

builder.Services.AddControllersWithViews();

// Upload size limits (100MB)
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 100 * 1024 * 1024;
});
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 100 * 1024 * 1024;
});

// PostgreSQL + pgvector
builder.Services.AddDbContext<GraspItDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Host=localhost;Database=GraspItDb;Username=postgres;Password=mypassword",
        npgsqlOptions => npgsqlOptions.UseVector()
    ));

// Repository pattern
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

// MediatR (CQRS)
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(AiProviderMappingProfile).Assembly));

// AutoMapper
builder.Services.AddSingleton<AutoMapper.IMapper>(sp =>
{
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    var config = new AutoMapper.MapperConfiguration(cfg =>
    {
        cfg.AddProfile<AiProviderMappingProfile>();
    }, loggerFactory);
    config.AssertConfigurationIsValid();
    return config.CreateMapper();
});

builder.Services.AddMemoryCache();

// Hangfire background processing
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(
        cfg => cfg.UseNpgsqlConnection(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Host=localhost;Database=GraspItDb;Username=postgres;Password=mypassword")
    ));
builder.Services.AddHangfireServer();

// Infrastructure services
builder.Services.AddScoped<IAiService, AiService>();
builder.Services.AddScoped<IVectorDbService, VectorDbService>();
builder.Services.AddScoped<IPdfParserService, PdfParserService>();
builder.Services.AddScoped<RagSearchService>();

// Mock exam pipeline services
builder.Services.AddScoped<IDocumentExtractionService, DocumentExtractionService>();
builder.Services.AddScoped<IRagRetrievalService, RagRetrievalService>();
builder.Services.AddScoped<ILLMProvider, LocalOpenAIProvider>();
builder.Services.AddScoped<IExamRendererService, ExamRendererService>();
builder.Services.AddScoped<IExportService, ExportService>();

// High-fidelity question paper generation (Node.js generator + multi-format export)
builder.Services.AddSingleton<IWebHostEnvironmentAccessor, WebHostEnvironmentAccessor>();
builder.Services.AddScoped<IQuestionPaperGeneratorService, QuestionPaperGeneratorService>();

var app = builder.Build();

// Database initialization
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<GraspItDbContext>();
    context.Database.Migrate();

    if (!context.Users.Any())
    {
        context.Users.Add(new User { Username = "student", Email = "student@graspit.local" });
        context.SaveChanges();
    }

    if (!context.AiProviders.Any())
    {
        context.AiProviders.AddRange(
            new AiProvider { Name = "LM Studio", ProviderType = "OpenAICompatible", Endpoint = "http://localhost:1234/v1", ModelId = "local-model", IsDefault = true },
            new AiProvider { Name = "Ollama", ProviderType = "OpenAICompatible", Endpoint = "http://localhost:11434/v1", ModelId = "llama3", IsDefault = false },
            new AiProvider { Name = "Google Gemini", ProviderType = "OpenAICompatible", Endpoint = "https://generativelanguage.googleapis.com/v1beta/openai/", ModelId = "gemini-2.5-pro", ApiKey = "AI...", IsDefault = false },
            new AiProvider { Name = "DeepSeek", ProviderType = "OpenAICompatible", Endpoint = "https://api.deepseek.com/v1", ModelId = "deepseek-reasoner", ApiKey = "sk-...", IsDefault = false },
            new AiProvider { Name = "OpenAI", ProviderType = "OpenAICompatible", Endpoint = "https://api.openai.com/v1", ModelId = "gpt-4o", IsDefault = false, ApiKey = "sk-" }
        );
        context.SaveChanges();
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.UseHangfireDashboard();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
public partial class Program { }