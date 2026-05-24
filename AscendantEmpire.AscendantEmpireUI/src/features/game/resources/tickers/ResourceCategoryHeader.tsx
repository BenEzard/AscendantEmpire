import "./ResourceTickerCollection.css";

export interface ResourceCategoryHeaderProps {
  label: string;
}

export default function ResourceCategoryHeader({ label }: ResourceCategoryHeaderProps) {
  return <div className="resource-category__header">{label}</div>;
}
