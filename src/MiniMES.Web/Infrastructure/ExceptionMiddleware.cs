using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using MiniMES.Web.Services;

namespace MiniMES.Web.Infrastructure;

public sealed class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            var (status, message) = ex switch
            {
                BusinessException business => (business.StatusCode, business.Message),
                SqlException { Number: 2601 or 2627 } => (409, "Mã đã tồn tại. Vui lòng dùng mã khác."),
                SqlException { Number: 51001 } => (409, "Dữ liệu đã thay đổi. Vui lòng tải lại."),
                _ => (500, "Không thể xử lý yêu cầu. Vui lòng thử lại hoặc liên hệ quản trị viên.")
            };
            if (status == 500) logger.LogError(ex, "Request failed. TraceId: {TraceId}", context.TraceIdentifier);
            context.Response.Clear();
            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = status, Title = message, Extensions = { ["traceId"] = context.TraceIdentifier }
            });
        }
    }
}
