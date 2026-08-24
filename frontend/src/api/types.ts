// Типы ответов API. any здесь не встречается намеренно: строки таблицы
// приходят из агрегации, и любое расхождение с бэкендом должно ловиться компилятором.

export interface TimeEntry {
  id: string;
  employeeId: string;
  employeeName: string;
  projectId: string;
  projectCode: string;
  projectName: string;
  /** ISO-дата без времени: 2026-03-05 */
  date: string;
  hours: number;
  /** Ставка, действовавшая на дату записи. null — ставку удалили задним числом. */
  rate: number | null;
  amount: number;
  comment: string | null;
  version: number;
  dayHours: number;
  isOvertimeDay: boolean;
  isPeriodClosed: boolean;
}

export interface TimeEntryList {
  items: TimeEntry[];
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
  totals: { hours: number; amount: number };
  isPeriodClosed: boolean;
}

export interface Employee {
  id: string;
  fullName: string;
  department: string;
  rates: { from: string; value: number }[];
}

export interface Project {
  id: string;
  code: string;
  name: string;
  budget: number;
  startDate: string;
  endDate: string | null;
}

export interface ProjectReportRow {
  projectId: string;
  projectCode: string;
  projectName: string;
  hours: number;
  amount: number;
  budget: number;
  percent: number | null;
  overspent: boolean;
  atRisk: boolean;
  entries: number;
}

export interface ProjectReport {
  year: number;
  month: number;
  rows: ProjectReportRow[];
  total: { hours: number; amount: number; projects: number; entries: number };
}

export interface ClosedPeriod {
  year: number;
  month: number;
  closedAt: string;
  closedBy: string;
}

export interface SaveTimeEntry {
  employeeId: string;
  projectId: string;
  date: string;
  hours: number;
  comment: string | null;
  version?: number;
}

export interface ApiValidationError {
  field: string;
  message: string;
}

export interface ApiErrorBody {
  code: string;
  message: string;
  errors?: ApiValidationError[];
  details?: Record<string, unknown>;
  traceId?: string;
}
