import { NavLink, Outlet, useLocation } from "react-router-dom";

const pageTitles: Record<string, string> = {
  "/": "Inicio",
  "/settings": "Settings",
  "/companies": "Companies",
  "/graph-settings": "Graph Settings",
  "/monitoring": "Monitoring"
};

const links = [
  { to: "/", label: "Home" },
  { to: "/settings", label: "Settings" },
  { to: "/companies", label: "Companies" },
  { to: "/graph-settings", label: "Graph Settings" },
  { to: "/monitoring", label: "Monitoring" }
];

export function AppLayout(): JSX.Element {
  const location = useLocation();
  const title = pageTitles[location.pathname] ?? "MailMonitor";

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <h1 className="brand">MailMonitor</h1>
        <nav className="menu">
          {links.map((link) => (
            <NavLink
              className={({ isActive }) => (isActive ? "menu-link active" : "menu-link")}
              key={link.to}
              to={link.to}
              end={link.to === "/"}
            >
              {link.label}
            </NavLink>
          ))}
        </nav>
      </aside>

      <main className="main-content">
        <header className="page-header">
          <h2>{title}</h2>
        </header>
        <section className="page-body">
          <Outlet />
        </section>
      </main>
    </div>
  );
}