using MongoDB.Driver;
using Timesheet.Api.Contracts;
using Timesheet.Api.Domain;
using Timesheet.Api.Infrastructure;
using Timesheet.Api.Validation;

namespace Timesheet.Api.Application;

/// <summary>Закрытые периоды: их закрывает бухгалтерия, после чего месяц только для чтения.</summary>
public class PeriodService
{
    private readonly MongoContext _context;
    private readonly CurrentUser _currentUser;

    public PeriodService(MongoContext context, CurrentUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<bool> IsClosedAsync(int year, int month, CancellationToken cancellationToken)
    {
        var filter = Builders<ClosedPeriod>.Filter.And(
            Builders<ClosedPeriod>.Filter.Eq(x => x.Year, year),
            Builders<ClosedPeriod>.Filter.Eq(x => x.Month, month));

        return await _context.ClosedPeriods.Find(filter).AnyAsync(cancellationToken);
    }

    /// <summary>Проверка перед любым изменением записи табеля. Само правило — в TimeEntryRules.</summary>
    public async Task EnsureOpenAsync(DateTime date, CancellationToken cancellationToken)
    {
        var isClosed = await IsClosedAsync(date.Year, date.Month, cancellationToken);
        TimeEntryRules.EnsurePeriodOpen(isClosed, date);
    }

    public async Task<List<ClosedPeriodDto>> ListAsync(int? year, CancellationToken cancellationToken)
    {
        var filter = year.HasValue
            ? Builders<ClosedPeriod>.Filter.Eq(x => x.Year, year.Value)
            : Builders<ClosedPeriod>.Filter.Empty;

        var periods = await _context.ClosedPeriods
            .Find(filter)
            .SortBy(x => x.Year)
            .ThenBy(x => x.Month)
            .ToListAsync(cancellationToken);

        return periods
            .Select(x => new ClosedPeriodDto
            {
                Year = x.Year,
                Month = x.Month,
                ClosedAt = x.ClosedAt,
                ClosedBy = x.ClosedBy
            })
            .ToList();
    }

    public async Task<ClosedPeriodDto> CloseAsync(PeriodRequest request, CancellationToken cancellationToken)
    {
        PeriodValidator.ValidateMonth(request.Year, request.Month);

        var closedAt = DateTime.UtcNow;
        var closedBy = _currentUser.Name;

        var filter = Builders<ClosedPeriod>.Filter.And(
            Builders<ClosedPeriod>.Filter.Eq(x => x.Year, request.Year),
            Builders<ClosedPeriod>.Filter.Eq(x => x.Month, request.Month));

        // Повторное закрытие уже закрытого месяца — не ошибка, операция идемпотентна.
        // year и month при upsert подставляются из фильтра равенства, поэтому $set только на служебные поля:
        // ReplaceOne здесь не подходит — он попытался бы поменять неизменяемый _id существующего документа.
        var update = Builders<ClosedPeriod>.Update
            .Set(x => x.ClosedAt, closedAt)
            .Set(x => x.ClosedBy, closedBy);

        await _context.ClosedPeriods.UpdateOneAsync(
            filter,
            update,
            new UpdateOptions { IsUpsert = true },
            cancellationToken);

        return new ClosedPeriodDto
        {
            Year = request.Year,
            Month = request.Month,
            ClosedAt = closedAt,
            ClosedBy = closedBy
        };
    }

    public async Task OpenAsync(PeriodRequest request, CancellationToken cancellationToken)
    {
        PeriodValidator.ValidateMonth(request.Year, request.Month);

        var filter = Builders<ClosedPeriod>.Filter.And(
            Builders<ClosedPeriod>.Filter.Eq(x => x.Year, request.Year),
            Builders<ClosedPeriod>.Filter.Eq(x => x.Month, request.Month));

        await _context.ClosedPeriods.DeleteOneAsync(filter, cancellationToken);
    }
}
