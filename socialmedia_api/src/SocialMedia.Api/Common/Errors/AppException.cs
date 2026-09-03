namespace SocialMedia.Api.Common.Errors;

/// <summary>
/// Lỗi nghiệp vụ có mã HTTP xác định. Service ném exception loại này, middleware
/// dịch sang ProblemDetails — không nơi nào tự dựng response lỗi (api.md mục 6).
/// </summary>
public abstract class AppException : Exception
{
    protected AppException(string message, string? title = null)
        : base(message)
    {
        Title = title;
    }

    /// <summary>Mã HTTP tương ứng.</summary>
    public abstract int StatusCode { get; }

    /// <summary>Tiêu đề ngắn cho ProblemDetails; null thì dùng tiêu đề mặc định của mã HTTP.</summary>
    public string? Title { get; }
}

/// <summary>400 — dữ liệu gửi lên không hợp lệ về mặt nghiệp vụ.</summary>
public sealed class BadRequestException(string message) : AppException(message)
{
    public override int StatusCode => StatusCodes.Status400BadRequest;
}

/// <summary>401 — chưa xác thực hoặc token không dùng được.</summary>
public sealed class UnauthorizedException(string message) : AppException(message)
{
    public override int StatusCode => StatusCodes.Status401Unauthorized;
}

/// <summary>403 — đã xác thực nhưng không đủ quyền (gồm cả kiểm ownership/membership).</summary>
public sealed class ForbiddenException(string message) : AppException(message)
{
    public override int StatusCode => StatusCodes.Status403Forbidden;
}

/// <summary>404 — không tìm thấy tài nguyên.</summary>
public sealed class NotFoundException(string message) : AppException(message)
{
    public override int StatusCode => StatusCodes.Status404NotFound;
}

/// <summary>409 — trùng hoặc đã được xử lý rồi.</summary>
public sealed class ConflictException(string message) : AppException(message)
{
    public override int StatusCode => StatusCodes.Status409Conflict;
}

/// <summary>423 — tài khoản đang bị khóa tạm thời (SPEC US-002/AC-03).</summary>
public sealed class LockedException(string message) : AppException(message)
{
    public override int StatusCode => StatusCodes.Status423Locked;
}

/// <summary>400 kèm chi tiết lỗi theo từng field — kết quả của FluentValidation.</summary>
public sealed class ValidationFailedException(IDictionary<string, string[]> errors)
    : AppException("Dữ liệu gửi lên không hợp lệ.")
{
    public override int StatusCode => StatusCodes.Status400BadRequest;

    public IDictionary<string, string[]> Errors { get; } = errors;
}
