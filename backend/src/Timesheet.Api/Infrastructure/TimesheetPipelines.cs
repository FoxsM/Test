using MongoDB.Bson;

namespace Timesheet.Api.Infrastructure;

/// <summary>
/// Стадии агрегаций собраны в одном месте, потому что список записей и отчёт
/// обязаны считать стоимость абсолютно одинаково.
///
/// Ключевая стадия — <see cref="AddEffectiveRate"/>: ставка выбирается не «первая из массива»
/// и не «текущая», а действовавшая на дату записи. Это $reduce по истории ставок,
/// точный аналог RateSchedule.Effective в C#.
///
/// Стадии собираются как BsonDocument, а не парсятся из строк: так параметры
/// (даты, ObjectId) попадают в конвейер типизированно, без склейки JSON.
/// </summary>
public static class TimesheetPipelines
{
    /// <summary>Отбор записей за месяц с необязательными фильтрами. Ложится на индекс date_employee_project.</summary>
    public static BsonDocument MatchPeriod(DateTime from, DateTime toExclusive, ObjectId? employeeId, ObjectId? projectId)
    {
        var filter = new BsonDocument
        {
            { "date", new BsonDocument { { "$gte", from }, { "$lt", toExclusive } } }
        };

        if (employeeId.HasValue)
        {
            filter.Add("employeeId", employeeId.Value);
        }

        if (projectId.HasValue)
        {
            filter.Add("projectId", projectId.Value);
        }

        return new BsonDocument("$match", filter);
    }

    public static BsonDocument MatchId(ObjectId id) =>
        new BsonDocument("$match", new BsonDocument("_id", id));

    public static BsonDocument Sort(BsonDocument keys) => new BsonDocument("$sort", keys);

    public static BsonDocument SortByDate() =>
        Sort(new BsonDocument { { "date", 1 }, { "_id", 1 } });

    public static BsonDocument Skip(int count) => new BsonDocument("$skip", count);

    public static BsonDocument Limit(int count) => new BsonDocument("$limit", count);

    public static BsonDocument LookupEmployee() =>
        new BsonDocument("$lookup", new BsonDocument
        {
            { "from", MongoContext.EmployeesCollection },
            { "localField", "employeeId" },
            { "foreignField", "_id" },
            { "as", "employee" }
        });

    public static BsonDocument LookupProject() =>
        new BsonDocument("$lookup", new BsonDocument
        {
            { "from", MongoContext.ProjectsCollection },
            { "localField", "projectId" },
            { "foreignField", "_id" },
            { "as", "project" }
        });

    /// <summary>$lookup по _id уже сгруппированных строк отчёта.</summary>
    public static BsonDocument LookupProjectById() =>
        new BsonDocument("$lookup", new BsonDocument
        {
            { "from", MongoContext.ProjectsCollection },
            { "localField", "_id" },
            { "foreignField", "_id" },
            { "as", "project" }
        });

    /// <summary>
    /// Разворачиваем справочник с preserveNullAndEmptyArrays: если сотрудника или проект
    /// удалили, запись не должна молча исчезнуть из отчёта — она попадёт туда с пустыми полями.
    /// </summary>
    public static BsonDocument Unwind(string path) =>
        new BsonDocument("$unwind", new BsonDocument
        {
            { "path", "$" + path },
            { "preserveNullAndEmptyArrays", true }
        });

    /// <summary>
    /// Ставка, действовавшая на дату записи: из истории ставок берём ту,
    /// у которой from ≤ date и from максимальна. Если таких нет — поле rate отсутствует.
    /// </summary>
    public static BsonDocument AddEffectiveRate() =>
        new BsonDocument("$addFields", new BsonDocument("rate", new BsonDocument("$let", new BsonDocument
        {
            {
                "vars", new BsonDocument("effective", new BsonDocument("$reduce", new BsonDocument
                {
                    { "input", new BsonDocument("$ifNull", new BsonArray { "$employee.rates", new BsonArray() }) },
                    { "initialValue", BsonNull.Value },
                    {
                        "in", new BsonDocument("$cond", new BsonArray
                        {
                            new BsonDocument("$and", new BsonArray
                            {
                                new BsonDocument("$lte", new BsonArray { "$$this.from", "$date" }),
                                new BsonDocument("$or", new BsonArray
                                {
                                    new BsonDocument("$eq", new BsonArray { "$$value", BsonNull.Value }),
                                    new BsonDocument("$gt", new BsonArray { "$$this.from", "$$value.from" })
                                })
                            }),
                            "$$this",
                            "$$value"
                        })
                    }
                }))
            },
            { "in", "$$effective.value" }
        })));

    /// <summary>
    /// Стоимость записи: часы × ставка, округление до копеек.
    /// $round в MongoDB округляет «к чётному» — ровно так же, как Money.Round в C#.
    /// </summary>
    public static BsonDocument AddAmount() =>
        new BsonDocument("$addFields", new BsonDocument("amount", new BsonDocument("$round", new BsonArray
        {
            new BsonDocument("$multiply", new BsonArray
            {
                "$hours",
                new BsonDocument("$ifNull", new BsonArray { "$rate", 0 })
            }),
            Domain.Money.Scale
        })));

    /// <summary>Плоская строка списка записей: справочные поля подставлены, лишнее выброшено.</summary>
    public static BsonDocument ProjectEntryRow() =>
        new BsonDocument("$project", new BsonDocument
        {
            { "_id", 1 },
            { "employeeId", 1 },
            { "employeeName", "$employee.fullName" },
            { "projectId", 1 },
            { "projectCode", "$project.code" },
            { "projectName", "$project.name" },
            { "date", 1 },
            { "hours", 1 },
            { "rate", 1 },
            { "amount", 1 },
            { "comment", 1 },
            { "version", 1 }
        });

    /// <summary>Итоги по всему отфильтрованному списку — считает база, не C#.</summary>
    public static BsonDocument GroupListTotals() =>
        new BsonDocument("$group", new BsonDocument
        {
            { "_id", BsonNull.Value },
            { "hours", new BsonDocument("$sum", "$hours") },
            { "amount", new BsonDocument("$sum", "$amount") },
            { "entries", new BsonDocument("$sum", 1) }
        });

    /// <summary>Часы сотрудника по дням — для лимита 24 часа и отметки переработки.</summary>
    public static BsonDocument GroupDayHours() =>
        new BsonDocument("$group", new BsonDocument
        {
            {
                "_id", new BsonDocument
                {
                    { "employeeId", "$employeeId" },
                    { "date", "$date" }
                }
            },
            { "hours", new BsonDocument("$sum", "$hours") }
        });

    public static BsonDocument GroupByProject() =>
        new BsonDocument("$group", new BsonDocument
        {
            { "_id", "$projectId" },
            { "hours", new BsonDocument("$sum", "$hours") },
            { "amount", new BsonDocument("$sum", "$amount") },
            { "entries", new BsonDocument("$sum", 1) }
        });

    /// <summary>Процент освоения бюджета; при нулевом или отсутствующем бюджете — null, а не деление на ноль.</summary>
    public static BsonDocument AddBudgetPercent() =>
        new BsonDocument("$addFields", new BsonDocument("percent", new BsonDocument("$cond", new BsonArray
        {
            new BsonDocument("$gt", new BsonArray { new BsonDocument("$ifNull", new BsonArray { "$project.budget", 0 }), 0 }),
            new BsonDocument("$round", new BsonArray
            {
                new BsonDocument("$multiply", new BsonArray
                {
                    new BsonDocument("$divide", new BsonArray { "$amount", "$project.budget" }),
                    100
                }),
                Domain.Money.Scale
            }),
            BsonNull.Value
        })));

    public static BsonDocument AddBudgetFlags() =>
        new BsonDocument("$addFields", new BsonDocument
        {
            {
                "overspent", new BsonDocument("$and", new BsonArray
                {
                    new BsonDocument("$ne", new BsonArray { "$percent", BsonNull.Value }),
                    new BsonDocument("$gt", new BsonArray { "$percent", 100 })
                })
            },
            {
                "atRisk", new BsonDocument("$and", new BsonArray
                {
                    new BsonDocument("$ne", new BsonArray { "$percent", BsonNull.Value }),
                    new BsonDocument("$gt", new BsonArray { "$percent", 80 })
                })
            }
        });

    public static BsonDocument ProjectReportShape() =>
        new BsonDocument("$project", new BsonDocument
        {
            { "_id", 0 },
            { "projectId", "$_id" },
            { "projectCode", new BsonDocument("$ifNull", new BsonArray { "$project.code", "—" }) },
            { "projectName", new BsonDocument("$ifNull", new BsonArray { "$project.name", "Проект удалён" }) },
            { "hours", 1 },
            { "amount", 1 },
            { "budget", new BsonDocument("$ifNull", new BsonArray { "$project.budget", 0 }) },
            { "percent", 1 },
            { "overspent", 1 },
            { "atRisk", 1 },
            { "entries", 1 }
        });

    public static BsonDocument GroupReportTotals() =>
        new BsonDocument("$group", new BsonDocument
        {
            { "_id", BsonNull.Value },
            { "hours", new BsonDocument("$sum", "$hours") },
            { "amount", new BsonDocument("$sum", "$amount") },
            { "entries", new BsonDocument("$sum", "$entries") },
            { "projects", new BsonDocument("$sum", 1) }
        });

    /// <summary>
    /// Строки отчёта и итоговая строка — за один проход по уже сгруппированным данным.
    /// Внутри $facet намеренно только простые стадии: справочник проектов подтягивается
    /// до него (по одному документу на проект), и конвейер не зависит от того,
    /// какие стадии конкретная версия MongoDB разрешает внутри $facet.
    /// </summary>
    public static BsonDocument ReportFacet() =>
        new BsonDocument("$facet", new BsonDocument
        {
            {
                "rows", new BsonArray
                {
                    ProjectReportShape(),
                    Sort(new BsonDocument("projectCode", 1))
                }
            },
            {
                "totals", new BsonArray { GroupReportTotals() }
            }
        });
}
