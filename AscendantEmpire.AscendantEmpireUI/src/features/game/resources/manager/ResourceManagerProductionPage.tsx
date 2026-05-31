import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer } from "recharts";
const tickFormatter = (v: number) => (v >= 1000 ? `${(v / 1000).toFixed(1)}k` : String(v));
import type { ResourceTickerData } from "../tickers/ResourceTickerCollection";
import { formatResourceAmount } from "@/util/number";
import { DeltaIndicator } from "@/components/ui";
import ResourceManagerPageHeader from "./ResourceManagerPageHeader";
interface ResourceManagerProductionPageProps {
  selectedTicker: ResourceTickerData | null;
  totalThisTurn: number;
  totalLastTurn: number;
  currentConsumption: number;
  maxConsumption: number;
}

export default function ResourceManagerProductionPage({
  selectedTicker,
  totalThisTurn,
  totalLastTurn,
  currentConsumption,
  maxConsumption,
}: ResourceManagerProductionPageProps) {
  const productionConfig = selectedTicker?.productionConfig;

  return (
    <div className="flex flex-col gap-6 p-2">
      <ResourceManagerPageHeader
        title={selectedTicker?.name}
        resourcesPerTurn={totalThisTurn}
        amountLastTurn={totalLastTurn}
        currentConsumption={currentConsumption}
        maxConsumption={maxConsumption}
      />
      {!productionConfig ? (
        <p className="text-sm text-slate-500 p-2">No production data available.</p>
      ) : (
        <>
          {/* Top 5 producing regions */}
          <section>
            <h3 className="text-sm font-semibold text-slate-800 mb-2">Top Producing Regions</h3>
            <div className="overflow-x-auto rounded-md border border-slate-300">
              <table className="min-w-full text-sm text-left">
                <thead className="bg-slate-100 text-slate-700 text-xs tracking-widest">
                  <tr>
                    <th className="px-3 py-2 font-semibold">Region</th>
                    <th className="px-3 py-2 text-right font-semibold">This Turn</th>
                    <th className="px-3 py-2 text-right font-semibold">Last Turn</th>
                  </tr>
                </thead>
                <tbody>
                  {productionConfig.topRegions.map((row) => {
                    const delta = row.thisTurn - row.lastTurn;
                    return (
                      <tr key={row.name}>
                        <td className="px-3 py-2 text-slate-600">{row.name}</td>
                        <td className="px-3 py-2 text-right text-slate-600">{formatResourceAmount(row.thisTurn)}</td>
                        <td className="px-3 py-2 text-right">
                          <DeltaIndicator delta={delta} />
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
                <tfoot className="border-t-2 border-slate-300 bg-slate-50 font-semibold text-sm">
                  <tr>
                    <td className="px-3 py-2 text-slate-800">Total</td>
                    <td className="px-3 py-2 text-right text-slate-600">{formatResourceAmount(totalThisTurn)}</td>
                    <td className="px-3 py-2 text-right">
                      <DeltaIndicator delta={totalThisTurn - totalLastTurn} />
                    </td>
                  </tr>
                </tfoot>
              </table>
            </div>
          </section>

          {/* Production vs Demand over time */}
          <section>
            <h3 className="text-sm font-semibold text-slate-800 mb-2">Production vs Demand Over Time</h3>
            <div className="h-56">
              <ResponsiveContainer width="100%" height="100%">
                <LineChart data={productionConfig.history} margin={{ top: 4, right: 56, left: 0, bottom: 0 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke="#1e293b" />
                  <XAxis
                    dataKey="turn"
                    tick={{ fill: "#94a3b8", fontSize: 11 }}
                    label={{ value: "Turn", position: "insideBottomRight", offset: -4, fill: "#64748b", fontSize: 11 }}
                  />
                  <YAxis yAxisId="production" orientation="left" tick={{ fill: "#34d399", fontSize: 11 }} width={48} tickFormatter={tickFormatter} />
                  <YAxis yAxisId="demand" orientation="right" tick={{ fill: "#f87171", fontSize: 11 }} width={48} tickFormatter={tickFormatter} />
                  <Tooltip
                    contentStyle={{ backgroundColor: "#0f172a", border: "1px solid #334155", borderRadius: 6, fontSize: 12 }}
                    labelStyle={{ color: "#94a3b8" }}
                    itemStyle={{ color: "#e2e8f0" }}
                    formatter={(value, name) => [Number(value).toLocaleString(), name]}
                    labelFormatter={(label) => `Turn ${label}`}
                  />
                  <Legend wrapperStyle={{ fontSize: 12, color: "#94a3b8", paddingTop: 6 }} />
                  <Line
                    type="monotone"
                    dataKey="production"
                    name="Production"
                    yAxisId="production"
                    stroke="#34d399"
                    strokeWidth={2}
                    dot={{ r: 3, fill: "#34d399" }}
                    activeDot={{ r: 5 }}
                  />
                  <Line
                    type="monotone"
                    dataKey="demand"
                    name="Demand"
                    yAxisId="demand"
                    stroke="#f87171"
                    strokeWidth={2}
                    strokeDasharray="5 3"
                    dot={{ r: 3, fill: "#f87171" }}
                    activeDot={{ r: 5 }}
                  />
                </LineChart>
              </ResponsiveContainer>
            </div>
          </section>
        </>
      )}
    </div>
  );
}
