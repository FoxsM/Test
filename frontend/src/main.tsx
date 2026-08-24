import React from "react";
import ReactDOM from "react-dom/client";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { App } from "./App";
import "./styles.css";

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      // Автообновление при возврате фокуса выключено намеренно: иначе приёмочный
      // сценарий 7 («две вкладки») не воспроизвести — вторая вкладка тихо получала бы
      // свежую версию записи вместо конфликта.
      refetchOnWindowFocus: false,
      retry: 1,
    },
  },
});

const container = document.getElementById("root");

if (!container) {
  throw new Error("Не найден корневой элемент #root");
}

ReactDOM.createRoot(container).render(
  <React.StrictMode>
    <QueryClientProvider client={queryClient}>
      <App />
    </QueryClientProvider>
  </React.StrictMode>,
);
