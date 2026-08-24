const money = new Intl.NumberFormat("ru-RU", {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

const hoursFormat = new Intl.NumberFormat("ru-RU", {
  minimumFractionDigits: 0,
  maximumFractionDigits: 2,
});

export function formatMoney(value: number | null | undefined): string {
  if (value === null || value === undefined) {
    return "—";
  }

  return `${money.format(value)} ₽`;
}

export function formatHours(value: number | null | undefined): string {
  if (value === null || value === undefined) {
    return "—";
  }

  return hoursFormat.format(value);
}

export function formatPercent(value: number | null | undefined): string {
  if (value === null || value === undefined) {
    return "—";
  }

  return `${hoursFormat.format(value)} %`;
}

/** 2026-03-05 → 05.03.2026. Без Date, чтобы не поймать сдвиг часового пояса. */
export function formatDate(isoDate: string): string {
  const [year, month, day] = isoDate.split("-");
  return year && month && day ? `${day}.${month}.${year}` : isoDate;
}

/** 2026-03 → { year: 2026, month: 3 } */
export function parseMonth(value: string): { year: number; month: number } {
  const [year, month] = value.split("-");
  return { year: Number(year), month: Number(month) };
}

export function toMonthValue(year: number, month: number): string {
  return `${year}-${String(month).padStart(2, "0")}`;
}

export function monthTitle(year: number, month: number): string {
  const names = [
    "январь", "февраль", "март", "апрель", "май", "июнь",
    "июль", "август", "сентябрь", "октябрь", "ноябрь", "декабрь",
  ];

  return `${names[month - 1] ?? month} ${year}`;
}
