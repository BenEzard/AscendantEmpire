import "./AssetTickerCollection.css";

export interface AssetCategoryHeaderProps {
  label: string;
}

export default function AssetCategoryHeader({ label }: AssetCategoryHeaderProps) {
  return <div className="asset-category__header">{label}</div>;
}
