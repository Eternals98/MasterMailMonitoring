export function Loading({ text = "Cargando..." }: { text?: string }): JSX.Element {
  return <div className="card loading">{text}</div>;
}