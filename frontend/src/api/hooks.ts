import {
  keepPreviousData,
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import { api } from "./client";
import type {
  ClosedPeriod,
  Employee,
  Project,
  ProjectReport,
  SaveTimeEntry,
  TimeEntry,
  TimeEntryList,
} from "./types";

export interface EntriesFilter {
  year: number;
  month: number;
  employeeId: string;
  projectId: string;
  page: number;
  pageSize: number;
}

const keys = {
  employees: ["employees"] as const,
  projects: ["projects"] as const,
  entries: (filter: EntriesFilter) => ["time-entries", filter] as const,
  report: (year: number, month: number) => ["project-report", year, month] as const,
  periods: ["periods"] as const,
};

function query(params: Record<string, string | number | undefined>): string {
  const search = new URLSearchParams();

  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== "") {
      search.set(key, String(value));
    }
  }

  return search.toString();
}

// Справочники меняются редко — держим их в кэше и не дёргаем на каждый рендер.
export function useEmployees() {
  return useQuery({
    queryKey: keys.employees,
    queryFn: () => api.get<Employee[]>("/api/employees"),
    staleTime: 5 * 60 * 1000,
  });
}

export function useProjects() {
  return useQuery({
    queryKey: keys.projects,
    queryFn: () => api.get<Project[]>("/api/projects"),
    staleTime: 5 * 60 * 1000,
  });
}

export function useTimeEntries(filter: EntriesFilter) {
  return useQuery({
    queryKey: keys.entries(filter),
    queryFn: () =>
      api.get<TimeEntryList>(
        `/api/time-entries?${query({
          year: filter.year,
          month: filter.month,
          employeeId: filter.employeeId,
          projectId: filter.projectId,
          page: filter.page,
          pageSize: filter.pageSize,
        })}`,
      ),
    // При смене страницы и фильтров показываем предыдущие данные, чтобы таблица не мигала.
    placeholderData: keepPreviousData,
  });
}

export function useProjectReport(year: number, month: number) {
  return useQuery({
    queryKey: keys.report(year, month),
    queryFn: () => api.get<ProjectReport>(`/api/reports/projects?${query({ year, month })}`),
  });
}

export function useClosedPeriods() {
  return useQuery({
    queryKey: keys.periods,
    queryFn: () => api.get<ClosedPeriod[]>("/api/periods"),
  });
}

/**
 * После любой записи инвалидируем всё, что могло измениться:
 * список, отчёт и закрытые периоды. Ставка и стоимость считаются на сервере,
 * поэтому локально ничего не пересчитываем — только перечитываем.
 */
function useInvalidateAll() {
  const client = useQueryClient();

  return () => {
    void client.invalidateQueries({ queryKey: ["time-entries"] });
    void client.invalidateQueries({ queryKey: ["project-report"] });
    void client.invalidateQueries({ queryKey: keys.periods });
  };
}

export function useCreateTimeEntry() {
  const invalidate = useInvalidateAll();

  return useMutation({
    mutationFn: (payload: SaveTimeEntry) => api.put<TimeEntry>("/api/time-entries", payload),
    onSuccess: invalidate,
  });
}

export function useUpdateTimeEntry() {
  const invalidate = useInvalidateAll();

  return useMutation({
    mutationFn: (input: { id: string; payload: SaveTimeEntry }) =>
      api.post<TimeEntry>(`/api/time-entries/${input.id}`, input.payload),
    onSuccess: invalidate,
  });
}

export function useDeleteTimeEntry() {
  const invalidate = useInvalidateAll();

  return useMutation({
    mutationFn: (input: { id: string; version: number }) =>
      api.delete<void>(`/api/time-entries/${input.id}?version=${input.version}`),
    onSuccess: invalidate,
  });
}

export function useTogglePeriod() {
  const invalidate = useInvalidateAll();

  return useMutation({
    mutationFn: (input: { year: number; month: number; close: boolean }) =>
      api.post<unknown>(`/api/periods/${input.close ? "close" : "open"}`, {
        year: input.year,
        month: input.month,
      }),
    onSuccess: invalidate,
  });
}
