using System.ComponentModel.DataAnnotations;

namespace QuanLyCuDan.Auth;

public class RegisterInput
{
    [Required(ErrorMessage = "Nhập tên đăng nhập."), StringLength(256)]
    [RegularExpression(@"[a-zA-Z0-9_.-]{3,50}", ErrorMessage = "Tên đăng nhập gồm 3–50 chữ cái không dấu, số, dấu chấm, gạch ngang hoặc gạch dưới.")]
    public string UserName { get; set; } = "";

    [Required(ErrorMessage = "Nhập họ và tên."), StringLength(150)]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Nhập số điện thoại."), RegularExpression(@"[0-9+ ()-]{9,20}", ErrorMessage = "Số điện thoại không hợp lệ.")]
    public string PhoneNumber { get; set; } = "";

    [Required(ErrorMessage = "Nhập mật khẩu."), StringLength(128, MinimumLength = 8, ErrorMessage = "Mật khẩu phải có từ 8 đến 128 ký tự.")]
    public string Password { get; set; } = "";

    [Required, Compare(nameof(Password), ErrorMessage = "Mật khẩu nhập lại không khớp.")]
    public string ConfirmPassword { get; set; } = "";
}

public class RegistrationReviewInput
{
    [Required]
    public string UserId { get; set; } = "";
    [Required]
    public string RowVersion { get; set; } = "";
    public bool Approve { get; set; }
    public long? ResidentId { get; set; }
    [StringLength(1000)]
    public string? Reason { get; set; }
}
