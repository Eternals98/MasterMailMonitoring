interface EmptyStateProps {
  title: string;
  description?: string;
}

export function EmptyState({ title, description }: EmptyStateProps): JSX.Element {
  return (
    <div className="card empty-state">
      <h3>{title}</h3>
      {description ? <p>{description}</p> : null}
    </div>
  );
}