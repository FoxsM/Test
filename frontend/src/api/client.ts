import type { ApiErrorBody, ApiValidationError } from "./types";

/**
 * Ошибка API в типизированном виде. Пользователю показывается message,
 * форма раскладывает errors по полям, код нужен для особых случаев
 * (например, устаревшая версия записи).
 */
export class ApiError extends Error {
  readonly code: string;
  readonly status: number;
  readonly errors: ApiValidationError[];
  readonly details: Record<string, unknown>;

  constructor(status: number, body: ApiErrorBody) {
    super(body.message);
    this.name = "ApiError";
    this.status = status;
    this.code = body.code;
    this.errors = body.errors ?? [];
    this.details = body.details ?? {};
  }

  /** Сообщение для конкретного поля формы, если сервер его вернул. */
  fieldError(field: string): string | undefined {
    const match = this.errors.find(
      (error) => error.field.toLowerCase() === field.toLowerCase(),
    );
    return match?.message;
  }
}

export const ErrorCode = {
  Validation: "VALIDATION_FAILED",
  RateNotDefined: "RATE_NOT_DEFINED",
  DailyLimit: "DAILY_HOURS_LIMIT_EXCEEDED",
  PeriodClosed: "PERIOD_CLOSED",
  OutOfProjectPeriod: "DATE_OUT_OF_PROJECT_PERIOD",
  Concurrent: "CONCURRENT_MODIFICATION",
  NotFound: "NOT_FOUND",
} as const;

interface RequestOptions {
  method?: string;
  body?: string;
}

async function request<T>(path: string, init?: RequestOptions): Promise<T> {
  let response: Response;

  try {
    response = await fetch(path, {
      method: init?.method ?? "GET",
      body: init?.body,
      headers: {
        "Content-Type": "application/json",
        // Аутентификации нет, но служебные поля «кто изменил» должны быть заполнены.
        "X-User": "demo",
      },
    });
  } catch {
    // Сеть отвалилась — до сервера мы не дошли, и текста ошибки от него не будет.
    throw new ApiError(0, {
      code: "NETWORK_ERROR",
      message: "Сервер недоступен. Проверьте, что API запущен.",
    });
  }

  if (response.status === 204) {
    return undefined as T;
  }

  const text = await response.text();
  const payload: unknown = text.length > 0 ? safeParse(text) : null;

  if (!response.ok) {
    const body = isErrorBody(payload)
      ? payload
      : {
          code: "UNEXPECTED_ERROR",
          message: `Ошибка ${response.status}. ${text.slice(0, 200)}`.trim(),
        };

    throw new ApiError(response.status, body);
  }

  return payload as T;
}

function safeParse(text: string): unknown {
  try {
    return JSON.parse(text);
  } catch {
    return null;
  }
}

function isErrorBody(payload: unknown): payload is ApiErrorBody {
  return (
    typeof payload === "object" &&
    payload !== null &&
    typeof (payload as ApiErrorBody).code === "string" &&
    typeof (payload as ApiErrorBody).message === "string"
  );
}

export const api = {
  get: <T>(path: string) => request<T>(path),
  put: <T>(path: string, body: unknown) =>
    request<T>(path, { method: "PUT", body: JSON.stringify(body) }),
  post: <T>(path: string, body: unknown) =>
    request<T>(path, { method: "POST", body: JSON.stringify(body) }),
  delete: <T>(path: string) => request<T>(path, { method: "DELETE" }),
};
