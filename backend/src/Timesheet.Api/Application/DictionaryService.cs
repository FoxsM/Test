using MongoDB.Driver;
using Timesheet.Api.Contracts;
using Timesheet.Api.Domain;
using Timesheet.Api.Infrastructure;

namespace Timesheet.Api.Application;

/// <summary>Справочники для выпадающих списков + правка истории ставок.</summary>
public class DictionaryService
{
    private readonly MongoContext _context;

    public DictionaryService(MongoContext context)
    {
        _context = context;
    }

    public async Task<List<EmployeeDto>> GetEmployeesAsync(CancellationToken cancellationToken)
    {
        var employees = await _context.Employees
            .Find(Builders<Employee>.Filter.Empty)
            .SortBy(x => x.FullName)
            .ToListAsync(cancellationToken);

        return employees.Select(ToDto).ToList();
    }

    public async Task<List<ProjectDto>> GetProjectsAsync(CancellationToken cancellationToken)
    {
        var projects = await _context.Projects
            .Find(Builders<Project>.Filter.Empty)
            .SortBy(x => x.Code)
            .ToListAsync(cancellationToken);

        return projects.Select(project => new ProjectDto
        {
            Id = project.Id,
            Code = project.Code,
            Name = project.Name,
            Budget = project.Budget,
            StartDate = Dates.ToDateOnly(project.StartDate),
            EndDate = project.EndDate is null ? null : Dates.ToDateOnly(project.EndDate.Value)
        }).ToList();
    }

    /// <summary>
    /// Заменяет историю ставок целиком. Нужна для приёмочного сценария 8:
    /// поменять ставку задним числом и увидеть, что отчёт пересчитался.
    /// Стоимость нигде не денормализована, поэтому пересчитывать записи не требуется.
    /// </summary>
    public async Task<EmployeeDto> ReplaceRatesAsync(
        string employeeId,
        SaveRatesRequest request,
        CancellationToken cancellationToken)
    {
        var id = Ids.Parse(employeeId, "id");
        ValidateRates(request);

        var employee = await _context.Employees
            .Find(Builders<Employee>.Filter.Eq(x => x.Id, id.ToString()))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new EntityNotFoundException($"Сотрудник {employeeId} не найден");

        var rates = request.Rates
            .Select(rate => new EmployeeRate
            {
                From = Dates.FromDateOnly(rate.From),
                Value = rate.Value
            })
            .OrderBy(rate => rate.From)
            .ToList();

        await _context.Employees.UpdateOneAsync(
            Builders<Employee>.Filter.Eq(x => x.Id, employee.Id),
            Builders<Employee>.Update.Set(x => x.Rates, rates),
            cancellationToken: cancellationToken);

        employee.Rates = rates;
        return ToDto(employee);
    }

    private static void ValidateRates(SaveRatesRequest request)
    {
        var errors = new List<ValidationError>();

        if (request.Rates.Count == 0)
        {
            errors.Add(new ValidationError("rates", "Нужна хотя бы одна ставка"));
        }

        for (var i = 0; i < request.Rates.Count; i++)
        {
            if (request.Rates[i].Value <= 0)
            {
                errors.Add(new ValidationError($"rates[{i}].value", "Ставка должна быть положительной"));
            }
        }

        var duplicates = request.Rates
            .GroupBy(rate => rate.From)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        foreach (var duplicate in duplicates)
        {
            errors.Add(new ValidationError(
                "rates",
                $"Две ставки с одной датой начала: {duplicate:dd.MM.yyyy}"));
        }

        if (errors.Count > 0)
        {
            throw new RequestValidationException(errors);
        }
    }

    private static EmployeeDto ToDto(Employee employee) => new()
    {
        Id = employee.Id,
        FullName = employee.FullName,
        Department = employee.Department,
        Rates = employee.Rates
            .OrderBy(rate => rate.From)
            .Select(rate => new EmployeeRateDto
            {
                From = Dates.ToDateOnly(rate.From),
                Value = rate.Value
            })
            .ToList()
    };
}
