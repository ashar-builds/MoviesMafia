using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MoviesMafia.Configurations;
using MoviesMafia.Models;
using MoviesMafia.Models.GenericRepo;
using MoviesMafia.Models.Repo;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.SetupAppSettings();

var connectionString = AppSettings.ConnectionStrings.DefaultConnection;
var adminDetails = AppSettings.AdminDetails; 

// Add services to the container.

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.Configure<IdentityOptions>(options => options.SignIn.RequireConfirmedEmail = true);
builder.Services.AddIdentity<AppUser, IdentityRole>().AddEntityFrameworkStores<AppDbContext>().AddDefaultTokenProviders();
builder.Services.AddScoped<IUserRepo, UserRepo>();
builder.Services.AddScoped<IAPICalls, APICalls>();
builder.Services.AddScoped<IRecordsRepo, RecordsRepo>();
builder.Services.AddScoped(typeof(IGenericRepo<>), typeof(GenericRepo<>));
builder.Services.AddHttpContextAccessor();
// Add additional services, etc.
builder.Services.AddControllersWithViews();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;
});


var app = builder.Build();

using var scope = app.Services.CreateScope();

using var appDbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
await appDbContext.Database.MigrateAsync();


using var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
if (!await roleManager.RoleExistsAsync("Admin"))
{
    var adminRole = new IdentityRole("Admin");
    await roleManager.CreateAsync(adminRole);
}

if (!await roleManager.RoleExistsAsync("User"))
{
    var userRole = new IdentityRole("User");
    await roleManager.CreateAsync(userRole);
}


var adminPassword = adminDetails.Password;
var adminUsername = adminDetails.UserName;
var adminEmail = adminDetails.Email;
var adminProfilePicturePath = adminDetails.ProfilePicturePath;
using var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
var adminUser = await userManager.FindByNameAsync(adminUsername);
if (adminUser == null)
{
    adminUser = new AppUser
    {
        UserName = adminUsername,
        Email = adminEmail,
        EmailConfirmed = true,
        LockoutEnabled = false,
        ProfilePicturePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "ProfilePictures", adminProfilePicturePath)
    };
    var result = await userManager.CreateAsync(adminUser, adminPassword);
    if (result.Succeeded)
    {
        await userManager.AddToRoleAsync(adminUser, "Admin");
    }

}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=LandingPage}/{action=LandingPage}/{id?}");
app.Run();



