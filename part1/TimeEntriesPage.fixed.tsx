// Исправленная версия TimeEntriesPage.tsx.
//
// Файл намеренно оставлен на голых хуках, без react-query и стора: это правка исходного
// экрана, а не его замена. Как тот же экран выглядит в нормальной архитектуре —
// в part 2, frontend/src/pages/TimeEntriesPage.tsx.
//
// Что изменилось (ранжирование и подробности — в REVIEW.md):
//   FIX 1  useEffect без массива зависимостей → бесконечный цикл запросов. Добавлены зависимости;
//   FIX 2  мутация стейта (entries.push) заменена на перезагрузку списка с сервера;
//   FIX 3  добавлена обработка ошибок: код и текст с сервера показываются пользователю;
//   FIX 4  "Сохранено" больше не показывается при неудачном сохранении;
//   FIX 5  дата уходит в ISO (yyyy-MM-dd), а не в локализованном формате toLocaleDateString();
//   FIX 6  часы уходят числом, а не строкой;
//   FIX 7  any заменён на типы ответов API;
//   FIX 8  key={index} заменён на key={entry.id};
//   FIX 9  фильтр по сотруднику ушёл на сервер, добавлена пагинация;
//   FIX 10 итоги берутся с сервера (деньги считает бэкенд), а не складываются parseFloat'ом;
//   FIX 11 гонка ответов лечится AbortController: ответ отменённого запроса не перезаписывает свежий;
//   FIX 12 loading сбрасывается в finally — «Загрузка…» больше не залипает после ошибки;
//   FIX 13 == заменено на ===;
//   FIX 14 entry.amount.toFixed(2) больше не роняет экран, если стоимость не пришла;
//   FIX 15 у таблицы появился <thead>, у удаления — подтверждение.

import React, { useCallback, useEffect, useState } from "react";

interface Props {
    year: number;
    month: number;
}

interface TimeEntry {
    id: string;
    employeeId: string;
    employeeName: string;
    projectId: string;
    projectCode: string;
    date: string;
    hours: number;
    rate: number | null;
    amount: number;
    version: number;
    isOvertimeDay: boolean;
}

interface TimeEntryList {
    items: TimeEntry[];
    page: number;
    totalPages: number;
    total: number;
    totals: { hours: number; amount: number };
}

interface Employee {
    id: string;
    fullName: string;
}

interface ApiError {
    code: string;
    message: string;
}

const PAGE_SIZE = 20;

async function request<T>(url: string, init?: RequestInit): Promise<T> {
    const response = await fetch(url, {
        ...init,
        headers: { "Content-Type": "application/json", ...(init?.headers ?? {}) },
    });

    if (response.status === 204) {
        return undefined as unknown as T;
    }

    const text = await response.text();
    const payload = text ? JSON.parse(text) : null;

    // FIX 3: fetch не бросает на 4xx — статус надо проверять руками.
    if (!response.ok) {
        const error = payload as ApiError | null;
        throw new Error(error?.message ?? `Ошибка ${response.status}`);
    }

    return payload as T;
}

const formatMoney = (value: number | null): string =>
    value === null || value === undefined ? "—" : value.toFixed(2); // FIX 14

export const TimeEntriesPage = (props: Props) => {
    const [data, setData] = useState<TimeEntryList | null>(null);
    const [employees, setEmployees] = useState<Employee[]>([]);
    const [employeeId, setEmployeeId] = useState("");
    const [page, setPage] = useState(1);
    const [hours, setHours] = useState("");
    const [date, setDate] = useState("");
    const [projectId, setProjectId] = useState("");
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    // FIX 9, FIX 11: фильтр и страница — часть запроса; отменяем предыдущий,
    // чтобы поздний ответ не перезаписал ранний.
    const load = useCallback(
        async (signal?: AbortSignal) => {
            setLoading(true);
            setError(null);

            try {
                const query = new URLSearchParams({
                    year: String(props.year),
                    month: String(props.month),
                    page: String(page),
                    pageSize: String(PAGE_SIZE),
                });

                if (employeeId) {
                    query.set("employeeId", employeeId);
                }

                const result = await request<TimeEntryList>(`/api/time-entries?${query}`, { signal });
                setData(result);
            } catch (cause) {
                if ((cause as Error).name === "AbortError") {
                    return;
                }

                setError((cause as Error).message);
            } finally {
                // FIX 12
                setLoading(false);
            }
        },
        [props.year, props.month, employeeId, page],
    );

    // FIX 1: зависимости объявлены, цикла нет.
    useEffect(() => {
        const controller = new AbortController();
        void load(controller.signal);
        return () => controller.abort();
    }, [load]);

    useEffect(() => {
        let cancelled = false;

        request<Employee[]>("/api/employees")
            .then((result) => {
                if (!cancelled) {
                    setEmployees(result);
                }
            })
            .catch((cause: Error) => setError(cause.message));

        return () => {
            cancelled = true;
        };
    }, []);

    const save = async () => {
        setError(null);

        const parsedHours = Number(hours.replace(",", "."));

        if (!date || !projectId || !employeeId) {
            setError("Заполните сотрудника, проект и дату");
            return;
        }

        if (Number.isNaN(parsedHours) || parsedHours <= 0 || parsedHours % 0.5 !== 0) {
            setError("Часы должны быть положительными и кратными 0,5");
            return;
        }

        try {
            await request<TimeEntry>("/api/time-entries", {
                method: "PUT",
                body: JSON.stringify({
                    employeeId,
                    projectId,
                    date, // FIX 5: input[type=date] уже отдаёт yyyy-MM-dd
                    hours: parsedHours, // FIX 6
                }),
            });

            setHours("");
            setDate("");
            setProjectId("");

            // FIX 2: список перечитываем с сервера — стоимость и переработку считает он.
            await load();
        } catch (cause) {
            // FIX 4: сообщение об успехе только после успеха.
            setError((cause as Error).message);
        }
    };

    const remove = async (entry: TimeEntry) => {
        if (!window.confirm(`Удалить запись от ${entry.date}?`)) {
            return;
        }

        try {
            await request<void>(`/api/time-entries/${entry.id}?version=${entry.version}`, {
                method: "DELETE",
            });
            await load();
        } catch (cause) {
            setError((cause as Error).message);
        }
    };

    return (
        <div style={{ padding: 20 }}>
            <h2>
                Табель за {props.month}.{props.year}
            </h2>

            {error && (
                <div role="alert" style={{ background: "#fdecea", padding: 10, marginBottom: 12 }}>
                    {error}
                </div>
            )}

            <select
                value={employeeId}
                onChange={(e) => {
                    setEmployeeId(e.target.value);
                    setPage(1);
                }}
            >
                <option value="">Все сотрудники</option>
                {employees.map((employee) => (
                    // FIX 8
                    <option key={employee.id} value={employee.id}>
                        {employee.fullName}
                    </option>
                ))}
            </select>

            <div style={{ marginTop: 20 }}>
                <input type="date" value={date} onChange={(e) => setDate(e.target.value)} />
                <input placeholder="Проект" value={projectId} onChange={(e) => setProjectId(e.target.value)} />
                <input placeholder="Часы" value={hours} onChange={(e) => setHours(e.target.value)} />
                <button onClick={save}>Добавить</button>
            </div>

            {loading && <div>Загрузка...</div>}

            <table style={{ marginTop: 20, width: "100%" }}>
                {/* FIX 15 */}
                <thead>
                    <tr>
                        <th>Дата</th>
                        <th>Сотрудник</th>
                        <th>Проект</th>
                        <th>Часы</th>
                        <th>Ставка</th>
                        <th>Стоимость</th>
                        <th />
                    </tr>
                </thead>
                <tbody>
                    {(data?.items ?? []).map((entry) => (
                        <tr key={entry.id}>
                            <td>{entry.date}</td>
                            <td>{entry.employeeName}</td>
                            <td>{entry.projectCode}</td>
                            <td>
                                {entry.hours}
                                {entry.isOvertimeDay && " (переработка)"}
                            </td>
                            <td>{formatMoney(entry.rate)}</td>
                            <td>{formatMoney(entry.amount)}</td>
                            <td>
                                <button onClick={() => remove(entry)}>Удалить</button>
                            </td>
                        </tr>
                    ))}
                </tbody>
            </table>

            {/* FIX 10: итог по всему фильтру приходит с сервера, а не считается по странице */}
            <div style={{ marginTop: 10 }}>
                Итого: {data?.totals.hours ?? 0} ч, {formatMoney(data?.totals.amount ?? 0)} руб.
            </div>

            {(data?.totalPages ?? 0) > 1 && (
                <div style={{ marginTop: 10 }}>
                    <button onClick={() => setPage((current) => Math.max(1, current - 1))} disabled={page <= 1}>
                        Назад
                    </button>
                    <span style={{ margin: "0 10px" }}>
                        Страница {data?.page} из {data?.totalPages}
                    </span>
                    <button
                        onClick={() => setPage((current) => current + 1)}
                        disabled={page >= (data?.totalPages ?? 1)}
                    >
                        Вперёд
                    </button>
                </div>
            )}
        </div>
    );
};
