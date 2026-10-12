using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;

namespace AcxiomCRM.Controllers;

[Authorize]
public class CustomersController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;

    public CustomersController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IAuditService auditService)
    {
        _context = context;
        _userManager = userManager;
        _auditService = auditService;
    }

    // GET: Customers
    public async Task<IActionResult> Index(string? search, string? status, int page = 1, int pageSize = 10)
    {
        var user = await _userManager.GetUserAsync(User);
        var userEmail = user?.Email ?? User.Identity?.Name ?? string.Empty;
        var isAdmin = User.IsInRole(DbInitializer.RoleAdmin);
        var isManager = User.IsInRole(DbInitializer.RoleManager);

        var query = _context.Customers.AsNoTracking();

        // Role-based scope (Section 7.1)
        if (!isAdmin && !isManager)
        {
            query = query.Where(c => c.OwnerId == userEmail);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(c => c.CustomerName.Contains(term) ||
                                     c.Email.Contains(term) ||
                                     c.Phone.Contains(term) ||
                                     (c.CompanyName != null && c.CompanyName.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(c => c.Status == status);
        }

        var totalItems = await query.CountAsync();
        var customers = await query
            .OrderByDescending(c => c.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewData["CurrentSearch"] = search;
        ViewData["CurrentStatus"] = status;
        ViewData["CurrentPage"] = page;
        ViewData["TotalPages"] = (int)Math.Ceiling(totalItems / (double)pageSize);
        ViewData["TotalItems"] = totalItems;

        return View(customers);
    }

    // GET: Customers/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var customer = await _context.Customers
            .Include(c => c.Opportunities)
            .Include(c => c.FollowUps)
            .Include(c => c.Activities)
            .FirstOrDefaultAsync(m => m.CustomerId == id);

        if (customer == null) return NotFound();

        // Check ownership if SalesExecutive
        var userEmail = User.Identity?.Name ?? string.Empty;
        if (!User.IsInRole(DbInitializer.RoleAdmin) && !User.IsInRole(DbInitializer.RoleManager))
        {
            if (customer.OwnerId != userEmail)
            {
                return Forbid();
            }
        }

        return View(customer);
    }

    // GET: Customers/Create
    public async Task<IActionResult> Create()
    {
        await PopulateUsersDropdown();
        var model = new Customer
        {
            CustomerCode = "CUST-" + DateTime.UtcNow.Ticks.ToString()[^6..],
            Status = "Active",
            OwnerId = User.Identity?.Name
        };
        return View(model);
    }

    // POST: Customers/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Customer customer)
    {
        // Uniqueness validation (Sections 17.5 & 17.7)
        if (await _context.Customers.AnyAsync(c => c.CustomerCode == customer.CustomerCode))
        {
            ModelState.AddModelError(nameof(customer.CustomerCode), "A customer with this code already exists.");
        }

        if (await _context.Customers.AnyAsync(c => c.Email == customer.Email))
        {
            ModelState.AddModelError(nameof(customer.Email), "A customer with this email address already exists.");
        }

        if (await _context.Customers.AnyAsync(c => c.Phone == customer.Phone))
        {
            ModelState.AddModelError(nameof(customer.Phone), "A customer with this phone number already exists.");
        }

        if (ModelState.IsValid)
        {
            customer.CreatedDate = DateTime.UtcNow;
            customer.CreatedBy = User.Identity?.Name;
            if (string.IsNullOrEmpty(customer.OwnerId))
            {
                customer.OwnerId = User.Identity?.Name;
            }

            _context.Add(customer);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                User.Identity?.Name,
                "Create",
                "Customer",
                customer.CustomerId.ToString(),
                newValue: $"{customer.CustomerName} ({customer.CustomerCode})",
                details: "Created customer master record.");

            TempData["SuccessMessage"] = $"Customer '{customer.CustomerName}' created successfully.";
            return RedirectToAction(nameof(Index));
        }

        await PopulateUsersDropdown();
        return View(customer);
    }

    // GET: Customers/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var customer = await _context.Customers.FindAsync(id);
        if (customer == null) return NotFound();

        // Check ownership if SalesExecutive
        var userEmail = User.Identity?.Name ?? string.Empty;
        if (!User.IsInRole(DbInitializer.RoleAdmin) && !User.IsInRole(DbInitializer.RoleManager))
        {
            if (customer.OwnerId != userEmail)
            {
                return Forbid();
            }
        }

        await PopulateUsersDropdown();
        return View(customer);
    }

    // POST: Customers/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Customer customer)
    {
        if (id != customer.CustomerId) return NotFound();

        var existing = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.CustomerId == id);
        if (existing == null) return NotFound();

        // Uniqueness validation excluding current record
        if (await _context.Customers.AnyAsync(c => c.Email == customer.Email && c.CustomerId != id))
        {
            ModelState.AddModelError(nameof(customer.Email), "Another customer with this email already exists.");
        }

        if (await _context.Customers.AnyAsync(c => c.Phone == customer.Phone && c.CustomerId != id))
        {
            ModelState.AddModelError(nameof(customer.Phone), "Another customer with this phone number already exists.");
        }

        if (ModelState.IsValid)
        {
            try
            {
                customer.ModifiedDate = DateTime.UtcNow;
                _context.Update(customer);
                await _context.SaveChangesAsync();

                await _auditService.LogAsync(
                    User.Identity?.Name,
                    "Update",
                    "Customer",
                    customer.CustomerId.ToString(),
                    oldValue: $"{existing.CustomerName} ({existing.Status})",
                    newValue: $"{customer.CustomerName} ({customer.Status})",
                    details: "Updated customer details.");

                TempData["SuccessMessage"] = $"Customer '{customer.CustomerName}' updated successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Customers.AnyAsync(e => e.CustomerId == id))
                {
                    return NotFound();
                }
                throw;
            }
        }

        await PopulateUsersDropdown();
        return View(customer);
    }

    // GET: Customers/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var customer = await _context.Customers.FirstOrDefaultAsync(m => m.CustomerId == id);
        if (customer == null) return NotFound();

        // SalesExecutive cannot delete customer
        if (!User.IsInRole(DbInitializer.RoleAdmin) && !User.IsInRole(DbInitializer.RoleManager))
        {
            return Forbid();
        }

        return View(customer);
    }

    // POST: Customers/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var customer = await _context.Customers.FindAsync(id);
        if (customer != null)
        {
            customer.Status = "Inactive";
            customer.ModifiedDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                User.Identity?.Name,
                "Delete",
                "Customer",
                id.ToString(),
                oldValue: "Active",
                newValue: "Inactive",
                details: $"Deactivated customer: {customer.CustomerCode}");

            TempData["SuccessMessage"] = $"Customer '{customer.CustomerName}' deactivated.";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateUsersDropdown()
    {
        var users = await _userManager.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();
        ViewBag.UsersList = new SelectList(users, "Email", "FullName");
    }
}
