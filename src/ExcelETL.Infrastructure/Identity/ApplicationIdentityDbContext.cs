using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ExcelETL.Infrastructure.Identity;

public class ApplicationIdentityDbContext(DbContextOptions<ApplicationIdentityDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
}
