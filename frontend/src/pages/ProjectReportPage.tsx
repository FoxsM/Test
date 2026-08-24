import { useState } from "react";
import { useProjectReport } from "../api/hooks";
import { ErrorBanner } from "../components/ErrorBanner";
import { formatHours, formatMoney, formatPercent, monthTitle, parseMonth, toMonthValue } from "../format";

const DEFAULT_MONTH = toMonthValue(2026, 3);

export function ProjectReportPage() {
  const [month, setMonth] = useState(DEFAULT_MONTH);
  const { year, month: monthNumber } = parseMonth(month);
  const report = useProjectReport(year, monthNumber);

  const rows = report.data?.rows ?? [];
  const total = report.data?.total;

  return (
    <section className="page">
      <div className="toolbar">
        <label className="filter">
          <span>Месяц</span>
          <input
            type="month"
            value={month}
            onChange={(event) => setMonth(event.target.value || DEFAULT_MONTH)}
          />
        </label>
      </div>

      <ErrorBanner error={report.error} />

      <div className="table-wrap">
        <table className="grid">
          <thead>
            <tr>
              <th>Проект</th>
              <th>Название</th>
              <th className="num">Часы</th>
              <th className="num">Стоимость</th>
              <th className="num">Бюджет</th>
              <th className="num">Освоено</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <tr
                key={row.projectId}
                className={row.overspent ? "row-overspent" : row.atRisk ? "row-at-risk" : undefined}
              >
                <td>{row.projectCode}</td>
                <td>{row.projectName}</td>
                <td className="num">{formatHours(row.hours)}</td>
                <td className="num">{formatMoney(row.amount)}</td>
                <td className="num">{formatMoney(row.budget)}</td>
                <td className="num">
                  {formatPercent(row.percent)}
                  {row.overspent && <span className="badge badge-danger">перерасход</span>}
                  {!row.overspent && row.atRisk && <span className="badge badge-warning">риск</span>}
                </td>
              </tr>
            ))}

            {!report.isLoading && rows.length === 0 && (
              <tr>
                <td colSpan={6} className="empty">
                  За {monthTitle(year, monthNumber)} трудозатрат нет
                </td>
              </tr>
            )}
          </tbody>
          <tfoot>
            <tr>
              <td colSpan={2}>Итого{total ? ` по ${total.projects} проектам` : ""}</td>
              <td className="num">{formatHours(total?.hours ?? 0)}</td>
              <td className="num">{formatMoney(total?.amount ?? 0)}</td>
              <td colSpan={2} />
            </tr>
          </tfoot>
        </table>
      </div>

      {report.isFetching && <div className="hint">Обновляем…</div>}

      <p className="hint">
        Проценты и признак перерасхода считает MongoDB в той же агрегации, что и суммы, —
        строки отчёта приходят готовыми.
      </p>
    </section>
  );
}
