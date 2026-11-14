using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace QuanLyCuDan.Auth;

// Việt hóa toàn bộ thông báo kiểm tra dữ liệu mặc định (tiếng Anh) của ASP.NET: tên trường + nội dung lỗi.
public sealed class VietnameseMetadataProvider : IDisplayMetadataProvider, IValidationMetadataProvider
{
    private static readonly Dictionary<string, string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["UserName"] = "Tên đăng nhập", ["Password"] = "Mật khẩu", ["ConfirmPassword"] = "Nhập lại mật khẩu",
        ["CurrentPassword"] = "Mật khẩu hiện tại", ["NewPassword"] = "Mật khẩu mới", ["FullName"] = "Họ và tên",
        ["PhoneNumber"] = "Số điện thoại", ["Email"] = "Email", ["DateOfBirth"] = "Ngày sinh", ["Gender"] = "Giới tính",
        ["CitizenId"] = "Số CCCD", ["Occupation"] = "Nghề nghiệp", ["Address"] = "Địa chỉ", ["GroupId"] = "Thôn",
        ["ResidentialGroupId"] = "Thôn", ["ResidenceStatus"] = "Hình thức cư trú", ["HeadName"] = "Họ tên chủ hộ",
        ["Name"] = "Tên", ["DisplayName"] = "Tên hiển thị", ["Description"] = "Mô tả", ["Code"] = "Mã", ["Module"] = "Phân hệ",
        ["Scopes"] = "Phạm vi", ["Subject"] = "Tiêu đề", ["Title"] = "Tiêu đề", ["Body"] = "Nội dung", ["Type"] = "Loại",
        ["Kind"] = "Loại", ["Audience"] = "Đối tượng", ["StartDate"] = "Ngày bắt đầu", ["EndDate"] = "Ngày kết thúc",
        ["Reason"] = "Lý do", ["RoleId"] = "Vai trò", ["ScopeType"] = "Phạm vi", ["TargetHouseholdCode"] = "Mã hộ tiếp nhận",
        ["RowVersion"] = "Phiên bản dữ liệu", ["ResidentVersion"] = "Phiên bản dữ liệu", ["ConcurrencyStamp"] = "Phiên bản dữ liệu",
        ["ClientKey"] = "Mã gửi", ["Version"] = "Phiên bản dữ liệu"
    };

    public void CreateDisplayMetadata(DisplayMetadataProviderContext context)
    {
        var key = context.Key.Name;
        if (key != null && context.DisplayMetadata.DisplayName == null && Names.TryGetValue(key, out var vi))
            context.DisplayMetadata.DisplayName = () => vi;
    }

    public void CreateValidationMetadata(ValidationMetadataProviderContext context)
    {
        foreach (var attribute in context.ValidationMetadata.ValidatorMetadata.OfType<ValidationAttribute>())
        {
            if (attribute.ErrorMessage != null || attribute.ErrorMessageResourceName != null)
                continue;
            attribute.ErrorMessage = attribute switch
            {
                RequiredAttribute => "Vui lòng nhập {0}.",
                StringLengthAttribute s when s.MinimumLength > 0 => "{0} phải dài từ {2} đến {1} ký tự.",
                StringLengthAttribute => "{0} tối đa {1} ký tự.",
                MinLengthAttribute => "{0} phải có ít nhất {1} ký tự.",
                MaxLengthAttribute => "{0} tối đa {1} ký tự.",
                RegularExpressionAttribute => "{0} không đúng định dạng.",
                EmailAddressAttribute => "{0} không đúng định dạng email.",
                PhoneAttribute => "{0} không hợp lệ.",
                RangeAttribute => "{0} phải nằm trong khoảng từ {1} đến {2}.",
                CompareAttribute => "{0} không khớp với {1}.",
                _ => null
            };
        }
    }

    // Thông báo lỗi khi dữ liệu gửi lên không đọc được (sai kiểu số, ngày...).
    public static void Apply(DefaultModelBindingMessageProvider p)
    {
        p.SetValueMustNotBeNullAccessor(_ => "Vui lòng nhập giá trị.");
        p.SetMissingBindRequiredValueAccessor(name => $"Thiếu giá trị cho {name}.");
        p.SetMissingKeyOrValueAccessor(() => "Thiếu giá trị.");
        p.SetMissingRequestBodyRequiredValueAccessor(() => "Thiếu nội dung gửi lên.");
        p.SetAttemptedValueIsInvalidAccessor((value, name) => $"Giá trị '{value}' không hợp lệ cho {name}.");
        p.SetNonPropertyAttemptedValueIsInvalidAccessor(value => $"Giá trị '{value}' không hợp lệ.");
        p.SetUnknownValueIsInvalidAccessor(name => $"Giá trị không hợp lệ cho {name}.");
        p.SetNonPropertyUnknownValueIsInvalidAccessor(() => "Giá trị không hợp lệ.");
        p.SetValueIsInvalidAccessor(value => $"Giá trị '{value}' không hợp lệ.");
        p.SetValueMustBeANumberAccessor(name => $"{name} phải là một số.");
        p.SetNonPropertyValueMustBeANumberAccessor(() => "Giá trị phải là một số.");
    }
}
