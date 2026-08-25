// Исправленная версия TimesheetReportHandler.cs.
//
// Что изменилось по сравнению с оригиналом (подробности и ранжирование — в REVIEW.md):
//   FIX 1  вся коллекция больше не выгружается в память — отбор месяца делает $match по индексу;
//   FIX 2  ставка берётся действовавшая на дату записи ($reduce по истории), а не первая из массива;
//   FIX 3  N+1 запросов на сотрудника и проект заменён на $lookup;
//   FIX 4  убран .Result — блокирующего ожидания в async-методе нет;
//   FIX 5  деньги и часы переведены на decimal (Decimal128 в Mongo);
//   FIX 6  деления на нулевой бюджет больше нет: percent = null, вместо Infinity/NaN;
//   FIX 7  удалённый сотрудник или проект больше не роняют отчёт в NullReferenceException;
//   FIX 8  CancellationToken доходит до драйвера;
//   FIX 9  границы месяца считаются в UTC — отчёт не зависит от часового пояса сервера;
//   FIX 10 добавлена итоговая строка и признак риска (>80 %) — они и так требовались по ТЗ;
//   FIX 11 год и месяц валидируются, а не приходят в запрос «как есть».
//
// Что осталось за пределами файла и сделано бы в проекте (см. раздел «Структура» в REVIEW.md):
// доступ к коллекциям — за MongoContext, сущности — в отдельном слое домена,
// выбор ставки — общая доменная функция, которую переиспользует запись табеля.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Demo.Api.Queries.Reports
{
    public class ProjectReportRow
    {
        public string ProjectId { get; set; }
        public string ProjectCode { get; set; }
        public string ProjectName { get; set; }
        public decimal Hours { get; set; }
        public decimal Amount { get; set; }
        public decimal Budget { get; set; }

        /// <summary>null — бюджет не задан, процент освоения не определён.</summary>
        public decimal? Percent { get; set; }

        public bool Overspent { get; set; }
        public bool AtRisk { get; set; }
    }

    public class ProjectReportTotal
    {
        public decimal Hours { get; set; }
        public decimal Amount { get; set; }
        public int Projects { get; set; }
    }

    public class ProjectReport
    {
        public List<ProjectReportRow> Rows { get; set; } = new List<ProjectReportRow>();
        public ProjectReportTotal Total { get; set; } = new ProjectReportTotal();
    }

    public class GetProjectReportQuery : IRequest<ProjectReport>
    {
        public int Year { get; set; }
        public int Month { get; set; }
    }

    public class TimesheetReportHandler : IRequestHandler<GetProjectReportQuery, ProjectReport>
    {
        private readonly IMongoDatabase _db;

        public TimesheetReportHandler(IMongoDatabase db)
        {
            _db = db;
        }

        public async Task<ProjectReport> Handle(GetProjectReportQuery request, CancellationToken token)
        {
            // FIX 11: невалидный месяц — это 400 от валидатора, а не пустой отчёт и не исключение из БД.
            if (request.Month < 1 || request.Month > 12)
            {
                throw new ArgumentOutOfRangeException(nameof(request.Month), "Месяц должен быть числом от 1 до 12");
            }

            // FIX 9: границы месяца — полуинтервал [from, to) в UTC.
            // Сравнение по e.Date.Year/Month, как было раньше, не ложится на индекс
            // и зависит от Kind у DateTime.
            var from = new DateTime(request.Year, request.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var to = from.AddMonths(1);

            var pipeline = BuildPipeline(from, to);

            using (var cursor = await _db.GetCollection<TimeEntry>("time_entries")
                       .AggregateAsync<BsonDocument>(pipeline, cancellationToken: token)   // FIX 8
                       .ConfigureAwait(false))
            {
                var document = await cursor.FirstOrDefaultAsync(token).ConfigureAwait(false);
                return Map(document);
            }
        }

        private static PipelineDefinition<TimeEntry, BsonDocument> BuildPipeline(DateTime from, DateTime to)
        {
            // FIX 1: отбор месяца делает база по индексу { date: 1 }.
            var match = new BsonDocument("$match", new BsonDocument
            {
                { "date", new BsonDocument { { "$gte", from }, { "$lt", to } } }
            });

            // FIX 3: сотрудник подтягивается один раз конвейером, а не отдельным запросом на запись.
            var lookupEmployee = BsonDocument.Parse(@"{ ""$lookup"": {
                ""from"": ""employees"",
                ""localField"": ""employeeId"",
                ""foreignField"": ""_id"",
                ""as"": ""employee""
            } }");

            // FIX 7: preserveNullAndEmptyArrays — запись удалённого сотрудника не должна
            // ни исчезать из отчёта, ни ронять его.
            var unwindEmployee = BsonDocument.Parse(@"{ ""$unwind"": {
                ""path"": ""$employee"",
                ""preserveNullAndEmptyArrays"": true
            } }");

            // FIX 2: ставка, действовавшая на дату записи, — та, у которой from <= date
            // и from максимальна. Именно это правило, а не «первая ставка из массива».
            var addRate = BsonDocument.Parse(@"{ ""$addFields"": {
                ""rate"": { ""$let"": {
                    ""vars"": { ""effective"": { ""$reduce"": {
                        ""input"": { ""$ifNull"": [""$employee.rates"", []] },
                        ""initialValue"": null,
                        ""in"": { ""$cond"": [
                            { ""$and"": [
                                { ""$lte"": [""$$this.from"", ""$date""] },
                                { ""$or"": [
                                    { ""$eq"": [""$$value"", null] },
                                    { ""$gt"": [""$$this.from"", ""$$value.from""] }
                                ] }
                            ] },
                            ""$$this"",
                            ""$$value""
                        ] }
                    } } },
                    ""in"": ""$$effective.value""
                } }
            } }");

            // FIX 5: hours и rate хранятся как Decimal128, поэтому и произведение остаётся decimal.
            var addAmount = BsonDocument.Parse(@"{ ""$addFields"": {
                ""amount"": { ""$round"": [
                    { ""$multiply"": [""$hours"", { ""$ifNull"": [""$rate"", 0] }] },
                    2
                ] }
            } }");

            var groupByProject = BsonDocument.Parse(@"{ ""$group"": {
                ""_id"": ""$projectId"",
                ""hours"": { ""$sum"": ""$hours"" },
                ""amount"": { ""$sum"": ""$amount"" }
            } }");

            // Проект подтягивается уже после группировки — по одному документу на проект,
            // а не на каждую запись табеля.
            var lookupProject = BsonDocument.Parse(@"{ ""$lookup"": {
                ""from"": ""projects"",
                ""localField"": ""_id"",
                ""foreignField"": ""_id"",
                ""as"": ""project""
            } }");

            var unwindProject = BsonDocument.Parse(
                @"{ ""$unwind"": { ""path"": ""$project"", ""preserveNullAndEmptyArrays"": true } }");

            // FIX 6: при нулевом или отсутствующем бюджете процент освоения — null,
            // а не Infinity/NaN, на которых падает сериализация ответа.
            var addBudget = BsonDocument.Parse(@"{ ""$addFields"": {
                ""budget"": { ""$ifNull"": [""$project.budget"", 0] }
            } }");

            var addPercent = BsonDocument.Parse(@"{ ""$addFields"": {
                ""percent"": { ""$cond"": [
                    { ""$gt"": [""$budget"", 0] },
                    { ""$round"": [{ ""$multiply"": [{ ""$divide"": [""$amount"", ""$budget""] }, 100] }, 2] },
                    null
                ] }
            } }");

            var shape = BsonDocument.Parse(@"{ ""$project"": {
                ""_id"": 0,
                ""projectId"": ""$_id"",
                ""projectCode"": { ""$ifNull"": [""$project.code"", ""—""] },
                ""projectName"": { ""$ifNull"": [""$project.name"", ""Проект удалён""] },
                ""hours"": 1,
                ""amount"": 1,
                ""budget"": 1,
                ""percent"": 1,
                ""overspent"": { ""$and"": [
                    { ""$ne"": [""$percent"", null] },
                    { ""$gt"": [""$percent"", 100] }
                ] },
                ""atRisk"": { ""$and"": [
                    { ""$ne"": [""$percent"", null] },
                    { ""$gt"": [""$percent"", 80] }
                ] }
            } }");

            // FIX 10: строки и итог считаются за один проход по уже сгруппированным данным.
            // Внутри $facet — только простые стадии, чтобы не зависеть от того, какие стадии
            // конкретная версия MongoDB разрешает в его подконвейерах.
            var facet = BsonDocument.Parse(@"{ ""$facet"": {
                ""rows"": [ { ""$sort"": { ""projectCode"": 1 } } ],
                ""totals"": [
                    { ""$group"": {
                        ""_id"": null,
                        ""hours"": { ""$sum"": ""$hours"" },
                        ""amount"": { ""$sum"": ""$amount"" },
                        ""projects"": { ""$sum"": 1 }
                    } }
                ]
            } }");

            return new BsonDocumentStagePipelineDefinition<TimeEntry, BsonDocument>(new[]
            {
                match,
                lookupEmployee,
                unwindEmployee,
                addRate,
                addAmount,
                groupByProject,
                lookupProject,
                unwindProject,
                addBudget,
                addPercent,
                shape,
                facet
            });
        }

        private static ProjectReport Map(BsonDocument document)
        {
            var report = new ProjectReport();

            if (document == null)
            {
                return report;
            }

            foreach (var value in document["rows"].AsBsonArray)
            {
                var row = value.AsBsonDocument;

                report.Rows.Add(new ProjectReportRow
                {
                    // ToString(), а не AsObjectId: идентификатор может храниться и строкой,
                    // и ObjectId — маппинг не должен падать на этом.
                    ProjectId = row["projectId"].ToString(),
                    ProjectCode = row["projectCode"].AsString,
                    ProjectName = row["projectName"].AsString,
                    Hours = row["hours"].ToDecimal(),
                    Amount = row["amount"].ToDecimal(),
                    Budget = row["budget"].ToDecimal(),
                    Percent = row["percent"].IsBsonNull ? (decimal?)null : row["percent"].ToDecimal(),
                    Overspent = row["overspent"].AsBoolean,
                    AtRisk = row["atRisk"].AsBoolean
                });
            }

            var totals = document["totals"].AsBsonArray.FirstOrDefault();

            if (totals != null)
            {
                var total = totals.AsBsonDocument;

                report.Total = new ProjectReportTotal
                {
                    Hours = total["hours"].ToDecimal(),
                    Amount = total["amount"].ToDecimal(),
                    Projects = total["projects"].ToInt32()
                };
            }

            return report;
        }
    }

    // --- сущности (в проекте им место в отдельном слое, здесь оставлены рядом для читаемости диффа) ---

    public class TimeEntry
    {
        public string Id { get; set; }
        public string EmployeeId { get; set; }
        public string ProjectId { get; set; }
        public DateTime Date { get; set; }
        public decimal Hours { get; set; }   // FIX 5
        public string Comment { get; set; }
    }

    public class Employee
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public List<Rate> Rates { get; set; }
    }

    public class Rate
    {
        public DateTime From { get; set; }
        public decimal Value { get; set; }   // FIX 5
    }

    public class Project
    {
        public string Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public decimal Budget { get; set; }  // FIX 5
    }
}
