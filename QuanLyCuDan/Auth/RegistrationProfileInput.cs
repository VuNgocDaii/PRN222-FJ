using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace QuanLyCuDan.Auth;

// Submitted information stays on the pending account until an administrator verifies it.
public class RegistrationProfileInput : IValidatableObject
{
    [Required(ErrorMessage = "Chọn ngày sinh."), DataType(DataType.Date)]
    public DateOnly? DateOfBirth { get; set; }
    [Required, RegularExpression("^(MALE|FEMALE|OTHER)$", ErrorMessage = "Chọn giới tính hợp lệ.")]
    public string Gender { get; set; } = "";
    [RegularExpression("^[0-9]{12}$", ErrorMessage = "CCCD gồm đúng 12 chữ số.")]
    public string? CitizenId { get; set; }
    [StringLength(150)] public string? Occupation { get; set; }
    [Required(ErrorMessage = "Nhập địa chỉ cư trú."), StringLength(300)]
    public string Address { get; set; } = "";
    [Required(ErrorMessage = "Chọn tổ dân phố."), Range(1, long.MaxValue)]
    public long? GroupId { get; set; }
    [Required, RegularExpression("^(RESIDING|TEMPORARY)$")]
    public string ResidenceStatus { get; set; } = "RESIDING";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DateOfBirth is { } birth && (birth < new DateOnly(1900, 1, 1) || birth > DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7))))
            yield return new ValidationResult("Ngày sinh phải từ năm 1900 đến hôm nay.", [nameof(DateOfBirth)]);
    }

    public static RegistrationProfileInput? Read(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonSerializer.Deserialize<RegistrationProfileInput>(json); }
        catch (JsonException) { return null; }
    }
}
