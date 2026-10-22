# Учёт трудозатрат и стоимость работ по проектам

.NET 8 + MongoDB + React 18/TypeScript.

* **Часть 1** — разбор чужого кода: [`REVIEW.md`](REVIEW.md), исправленные файлы в [`part1/`](part1).
* **Часть 2** — мини-фича end-to-end: бэкенд в [`backend/`](backend), фронтенд в [`frontend/`](frontend).
* Принятые решения, допущения и обоснование индексов — [`NOTES.md`](NOTES.md).

---

## Запуск: 4 шага

Нужен только Docker.

```bash
# 1. Собрать и поднять всё: MongoDB, API, фронтенд
docker compose up --build

# 2. Дождаться строки "Now listening on: http://[::]:8080" от контейнера timesheet-api
#    Контейнер timesheet-seed к этому моменту уже наполнил базу приёмочными данными и вышел.

# 3. Открыть интерфейс
#    http://localhost:5173

# 4. При необходимости перезалить тестовые данные (сид пересоздаёт их с нуля)
docker compose run --rm seed
```

| Что | Адрес |
|-----|-------|
| Интерфейс | http://localhost:5173 |
| API | http://localhost:5080/api |
| Swagger | http://localhost:5080/swagger |
| MongoDB | mongodb://localhost:27017/timesheet |

Экраны открываются на **марте 2026** — месяце, за который заданы приёмочные данные.

## Запуск без Docker

```bash
# 1. MongoDB на localhost:27017 (docker run -d -p 27017:27017 mongo:7)
# 2. Бэкенд + наполнение базы
cd backend
dotnet run --project src/Timesheet.Api -- --seed   # наполнить базу и выйти
dotnet run --project src/Timesheet.Api             # поднять API на http://localhost:5080

# 3. Фронтенд (в отдельном терминале), dev-сервер проксирует /api на 5080
cd frontend
npm install
npm run dev                                        # http://localhost:5173
```

Строка подключения переопределяется переменной окружения:
`Mongo__ConnectionString=mongodb://host:27017`, база — `Mongo__Database`.

## Тесты

```bash
cd backend
dotnet test
```

50 тестовых случаев в 7 классах — на бизнес-правила: выбор ставки по дате, лимит 24 часа
за день, отметка переработки, закрытый период, границы периода проекта, кратность 0,5 часа,
округление денег и контрольный расчёт всех цифр из раздела «Приёмочные проверки».

---

## Приёмочные проверки

Сид создаёт ровно те данные, что описаны в задании. Идентификаторы фиксированы, поэтому
проверки можно прогонять и через curl.

| Объект | Id |
|--------|-----|
| Иванов И. И. | `111111111111111111111111` |
| Петрова А. С. | `222222222222222222222222` |
| П-001 | `aaaaaaaaaaaaaaaaaaaaaaaa` |
| П-002 | `bbbbbbbbbbbbbbbbbbbbbbbb` |
| Записи табеля | `cccccccccccccccccccccc01` … `cccccccccccccccccccccc04` |

### Отчёты

```bash
curl "http://localhost:5080/api/reports/projects?year=2026&month=3"
# П-001 — 12 ч, 7 600 ₽, бюджет 20 000 ₽, освоено 38 %
# П-002 — 10 ч, 7 000 ₽, бюджет 5 000 ₽, освоено 140 %, overspent: true
# Итого  — 22 ч, 14 600 ₽

curl "http://localhost:5080/api/reports/projects?year=2026&month=2"
# П-001 — 8 ч, 4 000 ₽, освоено 20 %
```

### Сценарии с ошибками

Каждый даёт 400 или 409 с машиночитаемым кодом и русским текстом; в интерфейсе текст
показывается пользователю, а не только в консоли.

**1. Нет ставки на дату** → `409 RATE_NOT_DEFINED`

```bash
curl -i -X PUT http://localhost:5080/api/time-entries -H "Content-Type: application/json" -d '{
  "employeeId":"222222222222222222222222","projectId":"aaaaaaaaaaaaaaaaaaaaaaaa",
  "date":"2026-01-15","hours":8}'
```

**2. 20 часов за день — сохраняется и помечается переработкой** → `201`, в списке `isOvertimeDay: true`

```bash
curl -i -X PUT http://localhost:5080/api/time-entries -H "Content-Type: application/json" -d '{
  "employeeId":"111111111111111111111111","projectId":"aaaaaaaaaaaaaaaaaaaaaaaa",
  "date":"2026-03-06","hours":20}'
```

**3. Ещё 6 часов тому же сотруднику в тот же день** → `409 DAILY_HOURS_LIMIT_EXCEEDED`

```bash
curl -i -X PUT http://localhost:5080/api/time-entries -H "Content-Type: application/json" -d '{
  "employeeId":"111111111111111111111111","projectId":"aaaaaaaaaaaaaaaaaaaaaaaa",
  "date":"2026-03-06","hours":6}'
```

**4. Дата раньше начала проекта** → `409 DATE_OUT_OF_PROJECT_PERIOD`

```bash
curl -i -X PUT http://localhost:5080/api/time-entries -H "Content-Type: application/json" -d '{
  "employeeId":"222222222222222222222222","projectId":"bbbbbbbbbbbbbbbbbbbbbbbb",
  "date":"2026-02-20","hours":8}'
```

**5. Закрытый период** → `409 PERIOD_CLOSED`

```bash
curl -X POST http://localhost:5080/api/periods/close -H "Content-Type: application/json" -d '{"year":2026,"month":2}'

curl -i -X POST http://localhost:5080/api/time-entries/cccccccccccccccccccccc01 -H "Content-Type: application/json" -d '{
  "employeeId":"111111111111111111111111","projectId":"aaaaaaaaaaaaaaaaaaaaaaaa",
  "date":"2026-02-20","hours":4,"version":1}'

# вернуть месяц в работу
curl -X POST http://localhost:5080/api/periods/open -H "Content-Type: application/json" -d '{"year":2026,"month":2}'
```

**6. Часы 0 или 3,7** → `400 VALIDATION_FAILED` (в интерфейсе — прямо в форме, до отправки)

```bash
curl -i -X PUT http://localhost:5080/api/time-entries -H "Content-Type: application/json" -d '{
  "employeeId":"111111111111111111111111","projectId":"aaaaaaaaaaaaaaaaaaaaaaaa",
  "date":"2026-03-10","hours":3.7}'
```

**7. Конкурентное редактирование** → второй запрос получает `409 CONCURRENT_MODIFICATION`

```bash
# оба «пользователя» открыли запись версии 1
curl -X POST http://localhost:5080/api/time-entries/cccccccccccccccccccccc02 -H "Content-Type: application/json" -d '{
  "employeeId":"111111111111111111111111","projectId":"aaaaaaaaaaaaaaaaaaaaaaaa",
  "date":"2026-03-05","hours":7,"version":1}'

curl -i -X POST http://localhost:5080/api/time-entries/cccccccccccccccccccccc02 -H "Content-Type: application/json" -d '{
  "employeeId":"111111111111111111111111","projectId":"aaaaaaaaaaaaaaaaaaaaaaaa",
  "date":"2026-03-05","hours":6,"version":1}'
```

В интерфейсе: открыть одну запись в двух вкладках, сохранить в первой, затем во второй.
Автообновление данных при возврате фокуса намеренно выключено — иначе вторая вкладка
тихо получала бы свежую версию и конфликт было бы не воспроизвести.

**8. Изменение ставки задним числом** → отчёт за март пересчитывается сразу

```bash
curl -X PUT http://localhost:5080/api/employees/111111111111111111111111/rates \
  -H "Content-Type: application/json" -d '{"rates":[
    {"from":"2026-01-01","value":500},
    {"from":"2026-03-01","value":650}]}'

curl "http://localhost:5080/api/reports/projects?year=2026&month=3"
# запись от 05.03.2026 теперь стоит 5 200 ₽, П-001 — 8 000 ₽ вместо 7 600 ₽
```

> Метода правки ставок в задании не было — он добавлен, чтобы этот сценарий проверялся
> из API, а не руками в mongosh. Стоимость нигде не денормализована, поэтому пересчитывать
> записи не требуется: отчёт берёт ставку на дату записи в момент чтения.

---

## API

| Метод | Путь | Назначение |
|-------|------|------------|
| `GET` | `/api/time-entries` | список за месяц: `year`, `month`, `employeeId`, `projectId`, `page`, `pageSize` |
| `GET` | `/api/time-entries/{id}` | одна запись |
| `PUT` | `/api/time-entries` | создать |
| `POST` | `/api/time-entries/{id}` | изменить (требует `version`) |
| `DELETE` | `/api/time-entries/{id}?version=` | удалить |
| `GET` | `/api/reports/projects` | отчёт по проектам за месяц |
| `GET` | `/api/employees`, `/api/projects` | справочники |
| `PUT` | `/api/employees/{id}/rates` | заменить историю ставок |
| `GET` | `/api/periods` | закрытые периоды |
| `POST` | `/api/periods/close`, `/api/periods/open` | закрыть и открыть месяц |

Формат ошибки одинаков для всех эндпоинтов:

```json
{
  "code": "DAILY_HOURS_LIMIT_EXCEEDED",
  "message": "У сотрудника «Иванов И. И.» за 06.03.2026 уже учтено 20 ч по всем проектам…",
  "details": { "alreadyLoggedHours": 20, "requestedHours": 6, "limitHours": 24 },
  "traceId": "0HN7…"
}
```

---

## Структура репозитория

```
backend/
  src/Timesheet.Api/
    Domain/          сущности и бизнес-правила без единого обращения к БД
    Validation/      проверка формата запроса (400), отдельно от бизнес-правил (409)
    Application/     сервисы: приносят данные, применяют правила домена
    Infrastructure/  MongoContext, индексы, конвейеры агрегаций, сидер
    Contracts/       DTO API
    Controllers/     тонкие контроллеры
    Middleware/      единственное место, где исключение становится HTTP-ответом
  tests/Timesheet.Tests/   юнит-тесты бизнес-правил
frontend/
  src/api/          типизированный клиент и хуки react-query
  src/pages/        экран «Табель» и экран «Отчёт по проектам»
part1/              разбор чужого кода: оригиналы и исправленные версии
```
