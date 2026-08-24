import { useState } from "react";
import { TimeEntriesPage } from "./pages/TimeEntriesPage";
import { ProjectReportPage } from "./pages/ProjectReportPage";

type Tab = "entries" | "report";

export function App() {
  const [tab, setTab] = useState<Tab>("entries");

  return (
    <div className="app">
      <header className="app-header">
        <h1>Учёт трудозатрат</h1>
        <nav className="tabs">
          <button
            type="button"
            className={tab === "entries" ? "tab tab-active" : "tab"}
            onClick={() => setTab("entries")}
          >
            Табель
          </button>
          <button
            type="button"
            className={tab === "report" ? "tab tab-active" : "tab"}
            onClick={() => setTab("report")}
          >
            Отчёт по проектам
          </button>
        </nav>
      </header>

      <main>{tab === "entries" ? <TimeEntriesPage /> : <ProjectReportPage />}</main>
    </div>
  );
}
