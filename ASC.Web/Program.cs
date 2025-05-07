using ASC.Business.Interfaces;
using ASC.Business;
using ASC.Web.Configuration;
using ASC.Web.Data;
using ASC.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AutoMapper;

var builder = WebApplication.CreateBuilder(args);

// 🔐 Force TLS 1.2+
System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12 | System.Net.SecurityProtocolType.Tls13;

// ✅ Add services to the container
builder.Services.AddConfig(builder.Configuration);
builder.Services.AddMyDependencyGroup(builder.Configuration);

builder.Services.AddScoped<IMasterDataOperations, MasterDataOperations>();

builder.Services.AddAutoMapper(typeof(Program));
// Cấu hình Identity Options (nới lỏng chính sách mật khẩu để dễ kiểm tra)
builder.Services.Configure<IdentityOptions>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
});

// Thêm session và bộ nhớ lưu trữ
builder.Services.AddDistributedMemoryCache(); // ✅ Thêm bộ nhớ lưu trữ cho session
builder.Services.AddSession(options =>         // ✅ Thêm cấu hình session
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Thời gian hết hạn session
    options.Cookie.HttpOnly = true; // Cookie chỉ có thể truy cập từ phía máy chủ
    options.Cookie.IsEssential = true; // Cookie cần thiết cho ứng dụng
});

// Thêm logging chi tiết
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
    logging.SetMinimumLevel(LogLevel.Information);
});

var app = builder.Build();

// ✅ Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Kiểm tra xem ứng dụng có cần session không
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

// ✅ Map named route for dashboard
app.MapControllerRoute(
    name: "dashboard",
    pattern: "ServiceRequests/Dashboard/Dashboard",
    defaults: new { area = "ServiceRequests", controller = "Dashboard", action = "Dashboard" }
);

// ✅ Map route for areas
app.MapControllerRoute(
    name: "areaRoute",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}"
);

// ✅ Default route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

// ✅ Razor Pages
app.MapRazorPages();

// ✅ Seed roles & users
try
{
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        var seeder = services.GetRequiredService<IIdentitySeed>();

        await seeder.Seed(
            services.GetRequiredService<UserManager<IdentityUser>>(),
            services.GetRequiredService<RoleManager<IdentityRole>>(),
            services.GetRequiredService<IOptions<ApplicationSettings>>()
        );
        app.Logger.LogInformation("Successfully seeded roles and users.");
    }
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "An error occurred while seeding roles and users.");
    throw; // Ném lỗi để dừng ứng dụng nếu việc seed dữ liệu thất bại
}

// ✅ Initialize navigation cache
try
{
    using (var scope = app.Services.CreateScope())
    {
        var navigationCacheOperations = scope.ServiceProvider.GetRequiredService<INavigationCacheOperations>();
        await navigationCacheOperations.CreateNavigationCacheAsync();
        app.Logger.LogInformation("Successfully initialized navigation cache.");
    }
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "An error occurred while initializing navigation cache.");
    throw; // Ném lỗi để dừng ứng dụng nếu việc khởi tạo cache thất bại
}

app.Run();
