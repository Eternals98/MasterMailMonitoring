import { Navigate, Route, Routes } from "react-router-dom";
import { AppLayout } from "./AppLayout";
import { CompaniesPage } from "../pages/CompaniesPage";
import { GraphSettingsPage } from "../pages/GraphSettingsPage";
import { HomePage } from "../pages/HomePage";
import { LoginPage } from "../pages/LoginPage";
import { MonitoringPage } from "../pages/MonitoringPage";
import { SettingsPage } from "../pages/SettingsPage";
import { RequireAuth } from "../auth/RequireAuth";

export function AppRouter(): JSX.Element {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<RequireAuth />}>
        <Route path="/" element={<AppLayout />}>
          <Route index element={<HomePage />} />
          <Route path="settings" element={<SettingsPage />} />
          <Route path="companies" element={<CompaniesPage />} />
          <Route path="graph-settings" element={<GraphSettingsPage />} />
          <Route path="monitoring" element={<MonitoringPage />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Route>
      </Route>
    </Routes>
  );
}
