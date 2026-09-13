using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using HeThongVanBangSo.Data;
using HeThongVanBangSo.Services;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Giới hạn kích thước request body: 15 MB (bao gồm cả metadata của multipart form)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 15 * 1024 * 1024;
});

// 1. Cấu hình DbContext kết nối SQL Server
builder.Services.AddDbContext<HeThongVanBangDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure()
    )
);

// 2. Đăng ký Dependency Injection cho Service Ký số & Băm SHA-256
builder.Services.AddSingleton<ISignatureService, SignatureService>();

// 3. Cấu hình Controllers (API & MVC) và xử lý JSON vòng lặp
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.WriteIndented = true;
    });

// 4. Cấu hình Swagger API Documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Hệ Thống Cấp Phát & Kiểm Tra Văn Bằng Số API",
        Version = "v1",
        Description = "API cấp phát văn bằng có ký số RSA, băm mã SHA-256 bảo đảm toàn vẹn và tra cứu kiểm tra tính hợp lệ."
    });
});

// 5. Cấu hình CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// 6. Cấu hình Middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Hệ Thống Văn Bằng Số v1");
        c.RoutePrefix = "swagger"; // Đổi Swagger từ trang chủ sang /swagger để nhường trang chủ cho MVC Views
    });
}

app.UseCors("AllowAll");
app.UseHttpsRedirection();
app.UseStaticFiles(); // Cho phép truy cập file tĩnh (wwwroot, css, js)

app.UseRouting();

app.UseAuthorization();

// Map API Controllers
app.MapControllers();

// Map MVC Default Route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
