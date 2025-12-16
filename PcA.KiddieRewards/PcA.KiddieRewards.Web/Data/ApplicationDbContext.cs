using Microsoft.EntityFrameworkCore;

namespace PcA.KiddieRewards.Web.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : AppDbContext(options)
{
}
