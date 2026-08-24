namespace Timesheet.Api.Application;

/// <summary>
/// Аутентификации в задании нет, но служебные поля «кто создал/изменил» есть.
/// Берём имя из заголовка X-User, чтобы поля были не пустыми и было видно,
/// куда встроится настоящая аутентификация.
/// </summary>
public class CurrentUser
{
    public const string HeaderName = "X-User";
    public const string Anonymous = "anonymous";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string Name
    {
        get
        {
            var header = _httpContextAccessor.HttpContext?.Request.Headers[HeaderName].ToString();
            return string.IsNullOrWhiteSpace(header) ? Anonymous : header.Trim();
        }
    }
}
