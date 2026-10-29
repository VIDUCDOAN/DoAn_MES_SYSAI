using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniMES.Web.Dtos;
using MiniMES.Web.Security;
using MiniMES.Web.Services;

namespace MiniMES.Web.Controllers.Api;

[ApiController]
[Authorize]
[Route("api/products")]
public sealed class ProductsController(ProductService products) : ControllerBase
{
    [HttpGet]
    [PermissionAuthorize(Permissions.ProductView)]
    public async Task<IActionResult> List() => Ok(await products.ListAsync());

    [HttpPost]
    [PermissionAuthorize(Permissions.ProductAdd)]
    public async Task<IActionResult> Create(ProductCreateDto dto)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var product = await products.CreateAsync(dto, AuditActor.From(HttpContext));
        return StatusCode(StatusCodes.Status201Created, product);
    }

    [HttpPut("{id:int:min(1)}")]
    [PermissionAuthorize(Permissions.ProductEdit)]
    public async Task<IActionResult> Update(int id, ProductUpdateDto dto)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        return Ok(await products.UpdateAsync(id, dto, AuditActor.From(HttpContext)));
    }

    [HttpDelete("{id:int:min(1)}")]
    [PermissionAuthorize(Permissions.ProductDelete)]
    public async Task<IActionResult> Delete(int id, ProductDeleteDto dto)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        await products.DeleteAsync(id, dto.Version, AuditActor.From(HttpContext));
        return NoContent();
    }
}
