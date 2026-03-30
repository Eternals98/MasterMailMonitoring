import { BrowserRouter } from "react-router-dom";
import { AppRouter } from "./AppRouter";
import { AuthProvider } from "../auth/AuthContext";

export function App(): JSX.Element {
  return (
    <AuthProvider>
      <BrowserRouter>
        <AppRouter />
      </BrowserRouter>
    </AuthProvider>
  );
}
