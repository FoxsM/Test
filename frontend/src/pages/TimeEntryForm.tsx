import { useState, type FormEvent } from "react";
import { ApiError } from "../api/client";
import { useCreateTimeEntry, useUpdateTimeEntry } from "../api/hooks";
import type { Employee, Project, SaveTimeEntry, TimeEntry } from "../api/types";
import { ErrorBanner } from "../components/ErrorBanner";
import { Modal } from "../components/Modal";

interface Props {
  entry: TimeEntry | null;
  employees: Employee[];
  projects: Project[];
  defaultDate: string;
  onClose: () => void;
}

type FieldErrors = Partial<Record<"employeeId" | "projectId" | "date" | "hours", string>>;

const HOURS_STEP = 0.5;
const MAX_HOURS = 24;

/** Проверки формы — те же, что на сервере: пользователь не должен ждать ответа, чтобы узнать про 3,7 часа. */
function validate(values: { employeeId: string; projectId: string; date: string; hours: string }): FieldErrors {
  const errors: FieldErrors = {};

  if (!values.employeeId) {
    errors.employeeId = "Выберите сотрудника";
  }

  if (!values.projectId) {
    errors.projectId = "Выберите проект";
  }

  if (!values.date) {
    errors.date = "Укажите дату";
  }

  const hours = Number(values.hours.replace(",", "."));

  if (!values.hours.trim()) {
    errors.hours = "Укажите количество часов";
  } else if (Number.isNaN(hours)) {
    errors.hours = "Часы — это число";
  } else if (hours <= 0) {
    errors.hours = "Часы должны быть больше нуля";
  } else if (hours > MAX_HOURS) {
    errors.hours = `В одной записи не может быть больше ${MAX_HOURS} ч`;
  } else if (Math.abs(hours / HOURS_STEP - Math.round(hours / HOURS_STEP)) > 1e-9) {
    errors.hours = "Часы задаются с шагом 0,5 ч: например 0,5 или 7,5";
  }

  return errors;
}

export function TimeEntryForm({ entry, employees, projects, defaultDate, onClose }: Props) {
  const [employeeId, setEmployeeId] = useState(entry?.employeeId ?? "");
  const [projectId, setProjectId] = useState(entry?.projectId ?? "");
  const [date, setDate] = useState(entry?.date ?? defaultDate);
  const [hours, setHours] = useState(entry ? String(entry.hours) : "");
  const [comment, setComment] = useState(entry?.comment ?? "");
  const [errors, setErrors] = useState<FieldErrors>({});

  const create = useCreateTimeEntry();
  const update = useUpdateTimeEntry();
  const mutation = entry ? update : create;
  const serverError = mutation.error instanceof ApiError ? mutation.error : null;

  const errorFor = (field: keyof FieldErrors): string | undefined =>
    errors[field] ?? serverError?.fieldError(field);

  const submit = (event: FormEvent) => {
    event.preventDefault();

    const values = { employeeId, projectId, date, hours };
    const found = validate(values);
    setErrors(found);

    if (Object.keys(found).length > 0) {
      return;
    }

    const payload: SaveTimeEntry = {
      employeeId,
      projectId,
      date,
      hours: Number(hours.replace(",", ".")),
      comment: comment.trim() === "" ? null : comment.trim(),
    };

    if (entry) {
      // Версию отправляем ту, с которой начали редактировать: если запись
      // успели изменить, сервер ответит 409, а не затрёт чужую правку.
      update.mutate(
        { id: entry.id, payload: { ...payload, version: entry.version } },
        { onSuccess: onClose },
      );
    } else {
      create.mutate(payload, { onSuccess: onClose });
    }
  };

  return (
    <Modal title={entry ? "Изменить запись" : "Новая запись"} onClose={onClose}>
      <form onSubmit={submit} className="form">
        {serverError && serverError.errors.length === 0 && <ErrorBanner error={serverError} />}

        <label className="field">
          <span>Сотрудник</span>
          <select value={employeeId} onChange={(event) => setEmployeeId(event.target.value)}>
            <option value="">— выберите —</option>
            {employees.map((employee) => (
              <option key={employee.id} value={employee.id}>
                {employee.fullName}
              </option>
            ))}
          </select>
          {errorFor("employeeId") && <em className="field-error">{errorFor("employeeId")}</em>}
        </label>

        <label className="field">
          <span>Проект</span>
          <select value={projectId} onChange={(event) => setProjectId(event.target.value)}>
            <option value="">— выберите —</option>
            {projects.map((project) => (
              <option key={project.id} value={project.id}>
                {project.code} — {project.name}
              </option>
            ))}
          </select>
          {errorFor("projectId") && <em className="field-error">{errorFor("projectId")}</em>}
        </label>

        <label className="field">
          <span>Дата</span>
          <input type="date" value={date} onChange={(event) => setDate(event.target.value)} />
          {errorFor("date") && <em className="field-error">{errorFor("date")}</em>}
        </label>

        <label className="field">
          <span>Часы</span>
          <input
            type="text"
            inputMode="decimal"
            placeholder="8"
            value={hours}
            onChange={(event) => setHours(event.target.value)}
          />
          {errorFor("hours") && <em className="field-error">{errorFor("hours")}</em>}
        </label>

        <label className="field">
          <span>Комментарий</span>
          <textarea
            rows={2}
            value={comment}
            onChange={(event) => setComment(event.target.value)}
          />
        </label>

        <div className="form-actions">
          <button type="button" className="button" onClick={onClose}>
            Отмена
          </button>
          <button type="submit" className="button button-primary" disabled={mutation.isPending}>
            {mutation.isPending ? "Сохраняем…" : "Сохранить"}
          </button>
        </div>
      </form>
    </Modal>
  );
}
