using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using DiscordEventBot.Models.db;
using DiscordEventBot.Services;

namespace DiscordEventBot.Controllers
{
    public class GuildConfigsController : Controller
    {
        private readonly EventContext _context;

        public GuildConfigsController(EventContext context)
        {
            _context = context;
        }

        // GET: GuildConfigs
        public async Task<IActionResult> Index()
        {
            return View(await _context.Guilds.Include(g => g.DiscordBot).ToListAsync());
        }

        // GET: GuildConfigs/Details/5
        public async Task<IActionResult> Details(ulong? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var guildConfig = await _context.Guilds
                .FirstOrDefaultAsync(m => m.Id == id);
            if (guildConfig == null)
            {
                return NotFound();
            }

            return View(guildConfig);
        }

        // GET: GuildConfigs/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: GuildConfigs/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,EventChannelName,CalendarUrl,Template")] GuildConfig guildConfig)
        {
            if (ModelState.IsValid)
            {
                _context.Add(guildConfig);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(guildConfig);
        }

        // GET: GuildConfigs/Edit/5
        public async Task<IActionResult> Edit(ulong? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var guildConfig = await _context.Guilds.FindAsync(id);
            if (guildConfig == null)
            {
                return NotFound();
            }
            return View(guildConfig);
        }

        // POST: GuildConfigs/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ulong id, [Bind("Id,Name,EventChannelName,CalendarUrl,Template")] GuildConfig guildConfig)
        {
            if (id != guildConfig.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(guildConfig);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!GuildConfigExists(guildConfig.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(guildConfig);
        }

        // GET: GuildConfigs/Delete/5
        public async Task<IActionResult> Delete(ulong? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var guildConfig = await _context.Guilds
                .FirstOrDefaultAsync(m => m.Id == id);
            if (guildConfig == null)
            {
                return NotFound();
            }

            return View(guildConfig);
        }

        // POST: GuildConfigs/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(ulong id)
        {
            var guildConfig = await _context.Guilds.FindAsync(id);
            _context.Guilds.Remove(guildConfig);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool GuildConfigExists(ulong id)
        {
            return _context.Guilds.Any(e => e.Id == id);
        }
    }
}
