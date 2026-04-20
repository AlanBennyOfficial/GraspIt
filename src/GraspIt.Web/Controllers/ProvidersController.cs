using Microsoft.AspNetCore.Mvc;
using GraspIt.Core.Entities;
using GraspIt.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using AutoMapper;
using GraspIt.Application.DTOs;

namespace GraspIt.Web.Controllers;

public class ProvidersController : Controller
{
    private readonly GraspItDbContext _context;
    private readonly IMapper _mapper;

    public ProvidersController(GraspItDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<IActionResult> Index()
    {
        var providers = await _context.AiProviders.ToListAsync();
        var dtos = _mapper.Map<List<AiProviderDto>>(providers);
        return View(dtos);
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Create([Bind("Name,ProviderType,Endpoint,ModelId,ApiKey,IsDefault")] AiProviderDto dto)
    {
        if (ModelState.IsValid)
        {
            var aiProvider = _mapper.Map<AiProvider>(dto);

            if (aiProvider.IsDefault)
            {
                var defaults = await _context.AiProviders.Where(p => p.IsDefault).ToListAsync();
                defaults.ForEach(p => p.IsDefault = false);
            }

            _context.Add(aiProvider);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(dto);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var aiProvider = await _context.AiProviders.FindAsync(id);
        if (aiProvider == null) return NotFound();
        
        var dto = _mapper.Map<AiProviderDto>(aiProvider);
        return View(dto);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name,ProviderType,Endpoint,ModelId,ApiKey,IsDefault")] AiProviderDto dto)
    {
        if (id != dto.Id) return NotFound();

        if (ModelState.IsValid)
        {
            var aiProvider = await _context.AiProviders.FindAsync(id);
            if (aiProvider == null) return NotFound();

            // Check default transition
            if (dto.IsDefault && !aiProvider.IsDefault)
            {
                var defaults = await _context.AiProviders.Where(p => p.IsDefault && p.Id != id).ToListAsync();
                defaults.ForEach(p => p.IsDefault = false);
            }

            _mapper.Map(dto, aiProvider); // update existing entity

            _context.Update(aiProvider);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(dto);
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var aiProvider = await _context.AiProviders.FindAsync(id);
        if (aiProvider != null)
        {
            _context.AiProviders.Remove(aiProvider);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }
}
