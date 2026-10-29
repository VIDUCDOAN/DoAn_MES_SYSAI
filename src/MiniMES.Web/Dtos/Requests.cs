using System.ComponentModel.DataAnnotations;

namespace MiniMES.Web.Dtos;

public sealed class LoginDto
{
    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập.")]
    [StringLength(50)]
    public string Username { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [StringLength(128)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";
}

public class ProductCreateDto
{
    [Required(ErrorMessage = "Mã sản phẩm là bắt buộc.")]
    [StringLength(50, MinimumLength = 2)]
    [RegularExpression(@"^[A-Z0-9][A-Z0-9_-]+$", ErrorMessage = "Mã chỉ gồm chữ hoa, số, dấu gạch ngang hoặc gạch dưới.")]
    public string ModelCode { get; set; } = "";

    [Required(ErrorMessage = "Tên sản phẩm là bắt buộc.")]
    [StringLength(200, MinimumLength = 2)]
    public string ModelName { get; set; } = "";

    [StringLength(1000)]
    public string? Description { get; set; }
}

public sealed class ProductUpdateDto : ProductCreateDto
{
    [Range(1, int.MaxValue)]
    public int Version { get; set; }
}

public sealed class ProductDeleteDto
{
    [Range(1, int.MaxValue)]
    public int Version { get; set; }
}
