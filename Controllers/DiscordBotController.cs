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
    public class DiscordBotController : Controller
    {
        private readonly EventContext _context;

        public DiscordBotController(EventContext context)
        {
            _context = context;
        }

        // GET: DiscordBots
        public async Task<IActionResult> Index()
        {
            return View(await _context.DiscordBots.ToListAsync());
        }

        // GET: DiscordBots/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var discordBots = await _context.DiscordBots
                .FirstOrDefaultAsync(m => m.Id == id);
            if (discordBots == null)
            {
                return NotFound();
            }

            return View(discordBots);
        }

        // GET: DiscordBots/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: DiscordBots/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,BotToken,Name")] DiscordBot discordBot)
        {
            if (ModelState.IsValid)
            {
                _context.Add(discordBot);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(discordBot);
        }

        // GET: DiscordBots/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var discordBots = await _context.DiscordBots.FindAsync(id);
            if (discordBots == null)
            {
                return NotFound();
            }
            return View(discordBots);
        }

        // POST: DiscordBots/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,BotToken,Name")] DiscordBot discordBot)
        {
            if (id != discordBot.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(discordBot);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!DiscordBotsExists(discordBot.Id))
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
            return View(discordBot);
        }

        // GET: DiscordBots/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var discordBots = await _context.DiscordBots
                .FirstOrDefaultAsync(m => m.Id == id);
            if (discordBots == null)
            {
                return NotFound();
            }

            return View(discordBots);
        }

        // POST: DiscordBots/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var discordBot = await _context.DiscordBots.FindAsync(id);
            _context.DiscordBots.Remove(discordBot);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool DiscordBotsExists(int id)
        {
            return _context.DiscordBots.Any(e => e.Id == id);
        }
    }
}
