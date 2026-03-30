import { BrowserRouter } from "react-router-dom";
import { AppRouter } from "./AppRouter";

export function App(): JSX.Element {
  return (
    <BrowserRouter>
      <AppRouter />
    </BrowserRouter>
  );
}