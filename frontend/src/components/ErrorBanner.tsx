import { ApiError, ErrorCode } from "../api/client";

interface Props {
  error: unknown;
  onDismiss?: () => void;
}

/**
 * Ошибку с сервера должен видеть пользователь, а не только консоль.
 * Показываем текст, который прислал бэкенд, плюс подсказку для типовых кодов.
 */
export function ErrorBanner({ error, onDismiss }: Props) {
  if (!error) {
    return null;
  }

  const apiError = error instanceof ApiError ? error : null;
  const message = apiError?.message ?? (error instanceof Error ? error.message : String(error));

  return (
    <div className="banner banner-error" role="alert">
      <div>
        <strong>{message}</strong>
        {apiError && apiError.errors.length > 0 && (
          <ul className="banner-list">
            {apiError.errors.map((item) => (
              <li key={`${item.field}-${item.message}`}>
                {item.field}: {item.message}
              </li>
            ))}
          </ul>
        )}
        {apiError?.code === ErrorCode.Concurrent && (
          <p className="banner-hint">
            Обновите страницу, посмотрите чужие изменения и повторите правку.
          </p>
        )}
        {apiError?.code === ErrorCode.PeriodClosed && (
          <p className="banner-hint">
            Чтобы редактировать месяц, его нужно открыть заново — кнопка «Открыть период».
          </p>
        )}
      </div>

      {onDismiss && (
        <button type="button" className="icon-button" onClick={onDismiss} aria-label="Скрыть">
          ×
        </button>
      )}
    </div>
  );
}
