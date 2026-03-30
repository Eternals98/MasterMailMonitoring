import { Link } from "react-router-dom";

const shortcuts = [
  {
    to: "/companies",
    title: "Companies",
    description: "Alta, edición y baja de compañías con filtros y validaciones."
  },
  {
    to: "/settings",
    title: "Settings",
    description: "Configuración general de rutas base y referencia operativa."
  },
  {
    to: "/graph-settings",
    title: "Graph Settings",
    description: "Configuración segura de acceso a Microsoft Graph y scopes."
  },
  {
    to: "/monitoring",
    title: "Monitoring",
    description: "Consulta de estadísticas, KPI y exportación a Excel."
  }
];

export function HomePage(): JSX.Element {
  return (
    <div className="grid two-columns">
      {shortcuts.map((shortcut) => (
        <article className="card" key={shortcut.to}>
          <h3>{shortcut.title}</h3>
          <p>{shortcut.description}</p>
          <Link to={shortcut.to} className="btn primary inline">
            Abrir
          </Link>
        </article>
      ))}
    </div>
  );
}