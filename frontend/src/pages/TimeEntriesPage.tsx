import { useState } from "react";
import {
  useDeleteTimeEntry,
  useEmployees,
  useProjects,
  useTimeEntries,
  useTogglePeriod,
} from "../api/hooks";
import type { TimeEntry } from "../api/types";
import { ErrorBanner } from "../components/ErrorBanner";
import { formatDate, formatHours, formatMoney, monthTitle, parseMonth, toMonthValue } from "../format";
import { TimeEntryForm } from "./TimeEntryForm";

const PAGE_SIZE = 20;

// Данные сида — за март 2026, поэтому экран открывается сразу на них.
const DEFAULT_MONTH = toMonthValue(2026, 3);

export function TimeEntriesPage() {
  const [month, setMonth] = useState(DEFAULT_MONTH);
  const [employeeId, setEmployeeId] = useState("");
  const [projectId, setProjectId] = useState("");
  const [page, setPage] = useState(1);
  const [editing, setEditing] = useState<TimeEntry | null>(null);
  const [creating, setCreating] = useState(false);

  const { year, month: monthNumber } = parseMonth(month);
  const filter = { year, month: monthNumber, employeeId, projectId, page, pageSize: PAGE_SIZE };

  const entries = useTimeEntries(filter);
  const employees = useEmployees();
  const projects = useProjects();
  const remove = useDeleteTimeEntry();
  const togglePeriod = useTogglePeriod();

  const data = entries.data;
  const isClosed = data?.isPeriodClosed ?? false;

  const changeFilter = (apply: () => void) => {
    apply();
    setPage(1);
  };

  const onDelete = (entry: TimeEntry) => {
    if (!window.confirm(`Удалить запись от ${formatDate(entry.date)} на ${formatHours(entry.hours)} ч?`)) {
      return;
    }

    remove.mutate({ id: entry.id, version: entry.version });
  };

  return (
    <section className="page">
      <div className="toolbar">
        <label className="filter">
          <span>Месяц</span>
          <input
            type="month"
            value={month}
            onChange={(event) => changeFilter(() => setMonth(event.target.value || DEFAULT_MONTH))}
          />
        </label>

        <label className="filter">
          <span>Сотрудник</span>
          <select
            value={employeeId}
            onChange={(event) => changeFilter(() => setEmployeeId(event.target.value))}
          >
            <option value="">Все</option>
            {(employees.data ?? []).map((employee) => (
              <option key={employee.id} value={employee.id}>
                {employee.fullName}
              </option>
            ))}
          </select>
        </label>

        <label className="filter">
          <span>Проект</span>
          <select
            value={projectId}
            onChange={(event) => changeFilter(() => setProjectId(event.target.value))}
          >
            <option value="">Все</option>
            {(projects.data ?? []).map((project) => (
              <option key={project.id} value={project.id}>
                {project.code}
              </option>
            ))}
          </select>
        </label>

        <div className="toolbar-spacer" />

        <button
          type="button"
          className="button"
          onClick={() => togglePeriod.mutate({ year, month: monthNumber, close: !isClosed })}
          disabled={togglePeriod.isPending}
        >
          {isClosed ? "Открыть период" : "Закрыть период"}
        </button>

        <button
          type="button"
          className="button button-primary"
          onClick={() => setCreating(true)}
          disabled={isClosed}
          title={isClosed ? "Период закрыт бухгалтерией" : undefined}
        >
          Добавить запись
        </button>
      </div>

      {isClosed && (
        <div className="banner banner-warning">
          Период {monthTitle(year, monthNumber)} закрыт бухгалтерией — записи только для чтения.
        </div>
      )}

      <ErrorBanner error={entries.error} />
      <ErrorBanner error={remove.error} onDismiss={() => remove.reset()} />
      <ErrorBanner error={togglePeriod.error} onDismiss={() => togglePeriod.reset()} />

      <div className="table-wrap">
        <table className="grid">
          <thead>
            <tr>
              <th>Дата</th>
              <th>Сотрудник</th>
              <th>Проект</th>
              <th className="num">Часы</th>
              <th className="num">Ставка</th>
              <th className="num">Стоимость</th>
              <th>Комментарий</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {(data?.items ?? []).map((entry) => (
              <tr key={entry.id} className={entry.isOvertimeDay ? "row-overtime" : undefined}>
                <td>{formatDate(entry.date)}</td>
                <td>{entry.employeeName}</td>
                <td title={entry.projectName}>{entry.projectCode}</td>
                <td className="num">
                  {formatHours(entry.hours)}
                  {entry.isOvertimeDay && (
                    <span
                      className="badge badge-overtime"
                      title={`За ${formatDate(entry.date)} у сотрудника ${formatHours(entry.dayHours)} ч — переработка`}
                    >
                      переработка
                    </span>
                  )}
                </td>
                <td className="num">{formatMoney(entry.rate)}</td>
                <td className="num">{formatMoney(entry.amount)}</td>
                <td className="comment">{entry.comment ?? ""}</td>
                <td className="actions">
                  <button
                    type="button"
                    className="link"
                    onClick={() => setEditing(entry)}
                    disabled={isClosed}
                  >
                    Изменить
                  </button>
                  <button
                    type="button"
                    className="link link-danger"
                    onClick={() => onDelete(entry)}
                    disabled={isClosed || remove.isPending}
                  >
                    Удалить
                  </button>
                </td>
              </tr>
            ))}

            {!entries.isLoading && (data?.items.length ?? 0) === 0 && (
              <tr>
                <td colSpan={8} className="empty">
                  За {monthTitle(year, monthNumber)} записей нет
                </td>
              </tr>
            )}
          </tbody>
          <tfoot>
            <tr>
              <td colSpan={3}>Итого по фильтру ({data?.total ?? 0} записей)</td>
              <td className="num">{formatHours(data?.totals.hours ?? 0)}</td>
              <td />
              <td className="num">{formatMoney(data?.totals.amount ?? 0)}</td>
              <td colSpan={2} />
            </tr>
          </tfoot>
        </table>
      </div>

      {entries.isFetching && <div className="hint">Обновляем…</div>}

      {(data?.totalPages ?? 0) > 1 && (
        <div className="pager">
          <button
            type="button"
            className="button"
            onClick={() => setPage((current) => Math.max(1, current - 1))}
            disabled={page <= 1}
          >
            Назад
          </button>
          <span>
            Страница {data?.page ?? page} из {data?.totalPages ?? 1}
          </span>
          <button
            type="button"
            className="button"
            onClick={() => setPage((current) => current + 1)}
            disabled={page >= (data?.totalPages ?? 1)}
          >
            Вперёд
          </button>
        </div>
      )}

      {(creating || editing) && (
        <TimeEntryForm
          entry={editing}
          employees={employees.data ?? []}
          projects={projects.data ?? []}
          defaultDate={`${month}-01`}
          onClose={() => {
            setCreating(false);
            setEditing(null);
          }}
        />
      )}
    </section>
  );
}
